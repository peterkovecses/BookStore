using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                active.RemoveWhere(task => task.IsCompleted);
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
                }
                else
                {
                    await Task.Delay(_options.PollInterval, clock, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            var draining = Task.WhenAll(active);
            await Task.WhenAny(draining, Task.Delay(_options.ShutdownTimeout, clock));
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
            bool updated;
            if (dispatch.Status is OutboxDispatchStatus.Completed)
            {
                updated = await store.CompleteAsync(claim, timeout.Token);
            }
            else if (dispatch.Status is OutboxDispatchStatus.Expired)
            {
                updated = await store.ExpireAsync(claim, timeout.Token);
            }
            else
            {
                var now = clock.GetUtcNow();
                var retry = dispatch.RetryAfter is { } recovery && recovery > now
                    ? recovery : now + _options.PreflightFallbackDelay;
                retry += TimeSpan.FromSeconds(Random.Shared.NextDouble() * _options.PreflightFallbackDelay.TotalSeconds * 0.2);
                updated = await store.DeferAsync(claim, retry, timeout.Token);
                logger.Deferred(claim.Id, claim.CorrelationId);
            }

            if (!updated) logger.StaleClaim(claim.Id, claim.CorrelationId);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.Interrupted(claim.Id, claim.CorrelationId);
            if (deadline.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
                using var persistenceDeadline = new CancellationTokenSource(_options.ShutdownTimeout, clock);
                using var persistenceCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, persistenceDeadline.Token);
                await RecordFailureAsync(claim, nameof(TimeoutException), persistenceCancellation.Token);
            }
        }
        catch (Exception exception)
        {
            logger.DispatchFailed(claim.Id, claim.CorrelationId, exception.GetType().Name);
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

    private async Task RecordFailureAsync(OutboxClaim claim, string error, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<TStore>();
            var exponent = Math.Min(claim.AttemptCount - 1, 30);
            var seconds = Math.Min(_options.RetryMaximumDelay.TotalSeconds,
                _options.RetryInitialDelay.TotalSeconds * Math.Pow(2, exponent) * (0.8 + Random.Shared.NextDouble() * 0.4));
            if (!await store.FailAsync(claim, error, TimeSpan.FromSeconds(seconds), _options.MaxAttempts, cancellationToken))
            {
                logger.StaleClaim(claim.Id, claim.CorrelationId);
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
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox attempt {Attempt} for {EventId}, correlation {CorrelationId}")]
    internal static partial void Attempt(this ILogger logger, Guid eventId, string correlationId, int attempt);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox preflight deferred {EventId}, correlation {CorrelationId}")]
    internal static partial void Deferred(this ILogger logger, Guid eventId, string correlationId);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox stale claim for {EventId}, correlation {CorrelationId}")]
    internal static partial void StaleClaim(this ILogger logger, Guid eventId, string correlationId);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox interrupted {EventId}, correlation {CorrelationId}")]
    internal static partial void Interrupted(this ILogger logger, Guid eventId, string correlationId);
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox failed {EventId}, correlation {CorrelationId}; error {Error}")]
    internal static partial void DispatchFailed(this ILogger logger, Guid eventId, string correlationId, string error);
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox worker error {Error}")]
    internal static partial void WorkerError(this ILogger logger, string error);
}
