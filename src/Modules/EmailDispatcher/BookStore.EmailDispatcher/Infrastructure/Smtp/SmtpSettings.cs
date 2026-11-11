namespace BookStore.EmailDispatcher.Infrastructure.Smtp;

internal sealed class SmtpSettings
{
    public string Host { get; init; } = "";
    public string FromAddress { get; init; } = "";
    public int Port { get; init; } = 587;
    public SecureSocketOptions Security { get; init; } = SecureSocketOptions.StartTls;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int OperationTimeoutSeconds { get; init; } = 15;
    public int IdleTimeoutSeconds { get; init; } = 60;
    public int MaxAttempts { get; init; } = 2;
    public int RetryDelaySeconds { get; init; } = 2;
    public bool CircuitEnabled { get; init; } = true;
    public int CircuitFailureThreshold { get; init; } = 5;
    public int CircuitBreakSeconds { get; init; } = 30;
}
