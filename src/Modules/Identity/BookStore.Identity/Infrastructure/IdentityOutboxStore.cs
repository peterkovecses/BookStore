using BookStore.Common.Outbox;
using Microsoft.Extensions.Logging;

namespace BookStore.Identity.Infrastructure;

internal sealed class IdentityOutboxStore(IdentityDbContext db, ILogger<IdentityOutboxStore> logger) : IOutboxStore, IIdentityOutboxOperations
{
    public async Task<IReadOnlyList<OutboxClaim>> ClaimAsync(int count, OutboxOptions options, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE TOP ({count}) [identity].[OutboxMessages]
            SET [Status] = {(int)OutboxStatus.Failed}, [LastError] = {"AttemptLimitExceeded"},
                [ClaimToken] = NULL, [LeaseExpiresAt] = NULL
            WHERE ([Status] = {(int)OutboxStatus.Processing} AND [LeaseExpiresAt] <= SYSDATETIMEOFFSET()
                   OR [Status] = {(int)OutboxStatus.Pending})
                AND [AttemptCount] >= {options.MaxAttempts}
            """, cancellationToken);

        var claims = new List<OutboxClaim>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                ;WITH candidates AS (
                    SELECT TOP (@count) * FROM [identity].[OutboxMessages] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                    WHERE ([Status] = @pending AND ([NextAttemptAt] IS NULL OR [NextAttemptAt] <= SYSDATETIMEOFFSET())
                           OR [Status] = @processing AND [LeaseExpiresAt] <= SYSDATETIMEOFFSET())
                          AND [AttemptCount] < @maxAttempts
                    ORDER BY [CreatedAt], [Id]
                )
                UPDATE candidates
                SET [Status] = @processing, [ClaimToken] = NEWID(),
                    [LeaseExpiresAt] = DATEADD(millisecond, @leaseMilliseconds, SYSDATETIMEOFFSET()),
                    [AttemptCount] = [AttemptCount] + 1, [CumulativeAttemptCount] = [CumulativeAttemptCount] + 1,
                    [LastAttemptAt] = SYSDATETIMEOFFSET(), [NextAttemptAt] = NULL
                OUTPUT inserted.[Id], inserted.[EventType], inserted.[Payload], inserted.[CorrelationId],
                       inserted.[ClaimToken], inserted.[AttemptCount];
                """;
            AddParameter(command, "@count", count);
            AddParameter(command, "@pending", (int)OutboxStatus.Pending);
            AddParameter(command, "@processing", (int)OutboxStatus.Processing);
            AddParameter(command, "@maxAttempts", options.MaxAttempts);
            AddParameter(command, "@leaseMilliseconds", checked((int)options.LeaseDuration.TotalMilliseconds));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                claims.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetGuid(4), reader.GetInt32(5)));
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        return claims;
    }

    public async Task<bool> RenewAsync(OutboxClaim claim, OutboxOptions options, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [identity].[OutboxMessages]
            SET [LeaseExpiresAt] = DATEADD(millisecond, {checked((int)options.LeaseDuration.TotalMilliseconds)}, SYSDATETIMEOFFSET())
            WHERE [Id] = {claim.Id} AND [Status] = {(int)OutboxStatus.Processing}
              AND [ClaimToken] = {claim.Token} AND [LeaseExpiresAt] > SYSDATETIMEOFFSET()
            """, cancellationToken) == 1;

    public async Task<bool> CompleteAsync(OutboxClaim claim, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [identity].[OutboxMessages]
            SET [Status] = {(int)OutboxStatus.Completed}, [CompletedAt] = SYSDATETIMEOFFSET(),
                [ClaimToken] = NULL, [LeaseExpiresAt] = NULL, [LastError] = NULL
            WHERE [Id] = {claim.Id} AND [Status] = {(int)OutboxStatus.Processing}
              AND [ClaimToken] = {claim.Token} AND [LeaseExpiresAt] > SYSDATETIMEOFFSET()
            """, cancellationToken) == 1;

    public async Task<bool> FailAsync(OutboxClaim claim, string error, TimeSpan delay, int maxAttempts, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [identity].[OutboxMessages]
            SET [Status] = CASE WHEN [AttemptCount] >= {maxAttempts} THEN {(int)OutboxStatus.Failed} ELSE {(int)OutboxStatus.Pending} END,
                [LastError] = {error}, [NextAttemptAt] = DATEADD(millisecond, {checked((int)delay.TotalMilliseconds)}, SYSDATETIMEOFFSET()),
                [ClaimToken] = NULL, [LeaseExpiresAt] = NULL
            WHERE [Id] = {claim.Id} AND [Status] = {(int)OutboxStatus.Processing}
              AND [ClaimToken] = {claim.Token} AND [LeaseExpiresAt] > SYSDATETIMEOFFSET()
            """, cancellationToken) == 1;

    public async Task<bool> DeferAsync(OutboxClaim claim, DateTimeOffset nextAttempt, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [identity].[OutboxMessages]
            SET [Status] = {(int)OutboxStatus.Pending}, [NextAttemptAt] = {nextAttempt}, [AttemptCount] = [AttemptCount] - 1,
                [CumulativeAttemptCount] = [CumulativeAttemptCount] - 1, [ClaimToken] = NULL, [LeaseExpiresAt] = NULL
            WHERE [Id] = {claim.Id} AND [Status] = {(int)OutboxStatus.Processing}
              AND [ClaimToken] = {claim.Token} AND [LeaseExpiresAt] > SYSDATETIMEOFFSET()
            """, cancellationToken) == 1;

    public async Task<bool> ExpireAsync(OutboxClaim claim, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [identity].[OutboxMessages]
            SET [Status] = {(int)OutboxStatus.Expired}, [ClaimToken] = NULL, [LeaseExpiresAt] = NULL
            WHERE [Id] = {claim.Id} AND [Status] = {(int)OutboxStatus.Processing}
              AND [ClaimToken] = {claim.Token} AND [LeaseExpiresAt] > SYSDATETIMEOFFSET()
            """, cancellationToken) == 1;

    public Task<int> CleanupAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE TOP ({batchSize}) FROM [identity].[OutboxMessages]
            WHERE [Status] = {(int)OutboxStatus.Completed} AND [CompletedAt] < {cutoff}
            """, cancellationToken);

    public async Task<OutboxDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var groups = await db.OutboxMessages.AsNoTracking()
            .Where(item => item.Status != OutboxStatus.Completed)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount(), Oldest = group.Min(item => item.CreatedAt) })
            .ToListAsync(cancellationToken);
        var pending = groups.SingleOrDefault(group => group.Status == OutboxStatus.Pending);

        return new(pending?.Count ?? 0, groups.SingleOrDefault(group => group.Status == OutboxStatus.Processing)?.Count ?? 0,
            groups.SingleOrDefault(group => group.Status == OutboxStatus.Failed)?.Count ?? 0,
            groups.SingleOrDefault(group => group.Status == OutboxStatus.Expired)?.Count ?? 0, pending?.Oldest);
    }

    public async Task<bool> ReplayAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (actor.Length > 256) throw new ArgumentOutOfRangeException(nameof(actor));
        await db.Database.OpenConnectionAsync(cancellationToken);
        string? correlationId;
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
            UPDATE [identity].[OutboxMessages]
            SET [Status] = @pending, [AttemptCount] = 0, [NextAttemptAt] = NULL,
                [ProcessingCycle] = [ProcessingCycle] + 1, [ReplayCount] = [ReplayCount] + 1,
                [LastReplayedAt] = SYSDATETIMEOFFSET(), [LastReplayedBy] = @actor,
                [CompletedAt] = NULL, [LastError] = NULL
            OUTPUT inserted.[CorrelationId]
            WHERE [Id] = @id AND [Status] IN (@failed, @completed);
            """;
            AddParameter(command, "@pending", (int)OutboxStatus.Pending);
            AddParameter(command, "@failed", (int)OutboxStatus.Failed);
            AddParameter(command, "@completed", (int)OutboxStatus.Completed);
            AddParameter(command, "@actor", actor);
            AddParameter(command, "@id", id);
            correlationId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        if (correlationId is not null)
        {
            logger.Replayed(id, correlationId, actor);
        }

        return correlationId is not null;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

internal static partial class IdentityOutboxStoreLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox replay {EventId}, correlation {CorrelationId}, actor {Actor}")]
    internal static partial void Replayed(this ILogger<IdentityOutboxStore> logger, Guid eventId, string correlationId, string actor);
}
