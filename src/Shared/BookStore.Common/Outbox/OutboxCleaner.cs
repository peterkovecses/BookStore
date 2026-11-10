namespace BookStore.Common.Outbox;

public sealed class OutboxCleaner<TStore>(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<OutboxCleaner<TStore>> logger) : BackgroundService where TStore : class, IOutboxStore
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(settings.CleanupInterval, clock, stoppingToken);
                int deleted;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    deleted = await scope.ServiceProvider.GetRequiredService<TStore>()
                        .CleanupAsync(clock.GetUtcNow() - settings.Retention, settings.CleanupBatchSize, stoppingToken);
                    logger.Deleted(deleted);
                } while (deleted == settings.CleanupBatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.CleanupError(exception.GetType().Name);
            }
        }
    }
}

internal static partial class OutboxCleanerLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox cleanup deleted {Count} records")]
    internal static partial void Deleted(this ILogger logger, int count);
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox cleanup error {Error}")]
    internal static partial void CleanupError(this ILogger logger, string error);
}
