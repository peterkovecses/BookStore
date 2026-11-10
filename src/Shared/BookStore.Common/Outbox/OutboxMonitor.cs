namespace BookStore.Common.Outbox;

public sealed class OutboxMonitor<TStore>(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<OutboxMonitor<TStore>> logger) : BackgroundService where TStore : class, IOutboxStore
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var data = await scope.ServiceProvider.GetRequiredService<TStore>().GetDiagnosticsAsync(stoppingToken);
                var ageSeconds = data.OldestPending is { } oldest
                    ? Math.Max(0, (clock.GetUtcNow() - oldest).TotalSeconds) : 0;
                logger.Queue(data.Pending, data.Processing, data.Failed, data.Expired, ageSeconds);
                await Task.Delay(options.Value.DiagnosticsInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.MonitorError(exception.GetType().Name);
                try
                {
                    await Task.Delay(options.Value.DiagnosticsInterval, clock, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}

internal static partial class OutboxMonitorLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox backlog pending {Pending}, processing {Processing}, failed {Failed}, expired {Expired}, oldest pending seconds {OldestSeconds}")]
    internal static partial void Queue(this ILogger logger, long pending, long processing, long failed, long expired, double oldestSeconds);
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox monitor error {Error}")]
    internal static partial void MonitorError(this ILogger logger, string error);
}
