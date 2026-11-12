namespace BookStore.Common.Outbox;

public sealed class OutboxWorker<TStore>(
    IServiceScopeFactory scopes,
    OutboxRegistry<TStore> registry,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<OutboxWorker<TStore>> logger) : BackgroundService where TStore : class, IOutboxStore
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var active = new HashSet<Task>();
        logger.WorkerStarted(_options.BatchSize, _options.Concurrency);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ObserveCompletedTasksAsync(active);
                var capacity = Math.Min(_options.BatchSize, _options.Concurrency - active.Count);
                if (capacity > 0)
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        var store = scope.ServiceProvider.GetRequiredService<TStore>();
                        var claims = await store.ClaimAsync(capacity, _options, stoppingToken);
                        foreach (var claim in claims)
                        {
                            active.Add(ProcessAsync(claim, stoppingToken));
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        logger.WorkerError(exception.GetType().Name);
                    }
                }

                if (active.Count > 0)
                {
                    await Task.WhenAny(Task.WhenAny(active), Task.Delay(_options.PollInterval, clock, stoppingToken));
                    continue;
                }

                await Task.Delay(_options.PollInterval, clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.WorkerError(exception.GetType().Name);

            throw;
        }
        finally
        {
            logger.WorkerStopping(active.Count);
            var draining = Task.WhenAll(active);
            if (await Task.WhenAny(draining, Task.Delay(_options.ShutdownTimeout, clock)) == draining)
            {
                try
                {
                    await draining;
                    logger.DrainCompleted();
                }
                catch (Exception exception)
                {
                    logger.BackgroundTaskFailed(exception.GetType().Name);
                }
            }
            else
            {
                logger.DrainTimedOut(active.Count);
                _ = draining.ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        logger.BackgroundTaskFailed(task.Exception!.GetBaseException().GetType().Name);
                    }
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    private async Task ObserveCompletedTasksAsync(HashSet<Task> active)
    {
        foreach (var task in active.Where(task => task.IsCompleted).ToArray())
        {
            active.Remove(task);
            try
            {
                await task;
            }
            catch (Exception exception)
            {
                logger.BackgroundTaskFailed(exception.GetType().Name);
            }
        }
    }

    private async Task ProcessAsync(OutboxClaim claim, CancellationToken stoppingToken)
    {
        using var deadline = new CancellationTokenSource(_options.HandlerTimeout, clock);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, deadline.Token);
        using var renewalStop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var renewal = RenewAsync(claim, renewalStop.Token);
        try
        {
            logger.Attempt(claim.Id, claim.CorrelationId, claim.AttemptCount);
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<OutboxDispatchContext>().Set(claim);
            var store = scope.ServiceProvider.GetRequiredService<TStore>();
            var dispatch = await registry.Get(claim.EventType)
                .DispatchAsync(scope.ServiceProvider, claim.Payload, clock.GetUtcNow(), timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            var updated = await UpdateStatusAsync(store, claim, dispatch, timeout.Token);
            if (!updated) logger.StaleClaim(claim.Id, claim.CorrelationId);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (deadline.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
                logger.HandlerTimedOut(claim.Id, claim.CorrelationId, claim.AttemptCount);
            }
            else
            {
                logger.Interrupted(claim.Id, claim.CorrelationId, claim.AttemptCount);
            }
            if (deadline.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
                using var persistenceDeadline = new CancellationTokenSource(_options.ShutdownTimeout, clock);
                using var persistenceCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, persistenceDeadline.Token);
                await RecordFailureAsync(claim, nameof(TimeoutException), persistenceCancellation.Token);
            }
        }
        catch (Exception exception)
        {
            if (!timeout.IsCancellationRequested)
            {
                await RecordFailureAsync(claim, exception.GetType().Name, timeout.Token);
            }
        }
        finally
        {
            await renewalStop.CancelAsync();
            await renewal;
        }
    }

    private async Task<bool> UpdateStatusAsync(TStore store, OutboxClaim claim, OutboxDispatchResult dispatch,
        CancellationToken cancellationToken)
    {
        if (dispatch.Status is OutboxDispatchStatus.Completed)
        {
            var completed = await store.CompleteAsync(claim, cancellationToken);
            if (completed) logger.Completed(claim.Id, claim.CorrelationId, claim.AttemptCount);

            return completed;
        }

        if (dispatch.Status is OutboxDispatchStatus.Expired)
        {
            var expired = await store.ExpireAsync(claim, cancellationToken);
            if (expired) logger.Expired(claim.Id, claim.CorrelationId, claim.AttemptCount);

            return expired;
        }

        var now = clock.GetUtcNow();
        var retry = dispatch.RetryAfter is { } recovery && recovery > now
            ? recovery : now + _options.PreflightFallbackDelay;
        retry += TimeSpan.FromSeconds(Random.Shared.NextDouble() * _options.PreflightFallbackDelay.TotalSeconds * 0.2);
        var updated = await store.DeferAsync(claim, retry, cancellationToken);
        if (updated) logger.Deferred(claim.Id, claim.CorrelationId, claim.AttemptCount, retry);

        return updated;
    }

    private async Task RecordFailureAsync(OutboxClaim claim, string error, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<TStore>();
            var exponent = Math.Min(claim.AttemptCount - 1, 30);
            var seconds = Math.Min(_options.RetryMaximumDelay.TotalSeconds,
                _options.RetryInitialDelay.TotalSeconds * Math.Pow(2, exponent) * (0.8 + Random.Shared.NextDouble() * 0.4));
            var delay = TimeSpan.FromSeconds(seconds);
            if (!await store.FailAsync(claim, error, delay, _options.MaxAttempts, cancellationToken))
            {
                logger.StaleClaim(claim.Id, claim.CorrelationId);
            }
            else if (claim.AttemptCount >= _options.MaxAttempts)
            {
                logger.TerminalFailure(claim.Id, claim.CorrelationId, claim.AttemptCount, error);
            }
            else
            {
                logger.RetryScheduled(claim.Id, claim.CorrelationId, claim.AttemptCount, error, clock.GetUtcNow() + delay);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.WorkerError(exception.GetType().Name);
        }
    }

    private async Task RenewAsync(OutboxClaim claim, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_options.RenewalInterval, clock, cancellationToken);
                await using var scope = scopes.CreateAsyncScope();
                if (!await scope.ServiceProvider.GetRequiredService<TStore>().RenewAsync(claim, _options, cancellationToken))
                {
                    logger.StaleClaim(claim.Id, claim.CorrelationId);

                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.WorkerError(exception.GetType().Name);
        }
    }
}

internal static partial class OutboxWorkerLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox worker started with batch size {BatchSize} and concurrency {Concurrency}")]
    internal static partial void WorkerStarted(this ILogger logger, int batchSize, int concurrency);
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox worker stopping with {ActiveCount} active handlers")]
    internal static partial void WorkerStopping(this ILogger logger, int activeCount);
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox worker drained all handlers")]
    internal static partial void DrainCompleted(this ILogger logger);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox worker drain timed out with {ActiveCount} handlers still active; their claims remain leased")]
    internal static partial void DrainTimedOut(this ILogger logger, int activeCount);
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox attempt {Attempt} for {EventId}, correlation {CorrelationId}")]
    internal static partial void Attempt(this ILogger logger, Guid eventId, string correlationId, int attempt);
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox completed {EventId}, correlation {CorrelationId}, attempt {Attempt}")]
    internal static partial void Completed(this ILogger logger, Guid eventId, string correlationId, int attempt);
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox expired {EventId}, correlation {CorrelationId}, attempt {Attempt}")]
    internal static partial void Expired(this ILogger logger, Guid eventId, string correlationId, int attempt);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox preflight deferred {EventId}, correlation {CorrelationId}, attempt {Attempt} until {NextAttemptAt}")]
    internal static partial void Deferred(this ILogger logger, Guid eventId, string correlationId, int attempt, DateTimeOffset nextAttemptAt);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox stale claim for {EventId}, correlation {CorrelationId}")]
    internal static partial void StaleClaim(this ILogger logger, Guid eventId, string correlationId);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox handler interrupted by shutdown for {EventId}, correlation {CorrelationId}, attempt {Attempt}")]
    internal static partial void Interrupted(this ILogger logger, Guid eventId, string correlationId, int attempt);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox handler timed out for {EventId}, correlation {CorrelationId}, attempt {Attempt}")]
    internal static partial void HandlerTimedOut(this ILogger logger, Guid eventId, string correlationId, int attempt);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox retry scheduled for {EventId}, correlation {CorrelationId}, attempt {Attempt}, error {ErrorType}, next attempt {NextAttemptAt}")]
    internal static partial void RetryScheduled(this ILogger logger, Guid eventId, string correlationId, int attempt, string errorType, DateTimeOffset nextAttemptAt);
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox reached terminal failure for {EventId}, correlation {CorrelationId}, attempt {Attempt}, error {ErrorType}")]
    internal static partial void TerminalFailure(this ILogger logger, Guid eventId, string correlationId, int attempt, string errorType);
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox background task failed with {ErrorType}")]
    internal static partial void BackgroundTaskFailed(this ILogger logger, string errorType);
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox worker error {Error}")]
    internal static partial void WorkerError(this ILogger logger, string error);
}
