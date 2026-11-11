namespace BookStore.EmailDispatcher;

internal sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public int Port { get; set; } = 587;
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int OperationTimeoutSeconds { get; set; } = 15;
    public int IdleTimeoutSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 2;
    public int RetryDelaySeconds { get; set; } = 2;
    public bool CircuitEnabled { get; set; } = true;
    public int CircuitFailureThreshold { get; set; } = 5;
    public int CircuitBreakSeconds { get; set; } = 30;
}

internal sealed class SmtpSettingsValidator(IHostEnvironment environment) : IValidateOptions<SmtpSettings>
{
    public ValidateOptionsResult Validate(string? name, SmtpSettings options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host)) errors.Add("SMTP host is required.");
        if (options.Port is < 1 or > 65535) errors.Add("SMTP port must be between 1 and 65535.");
        if (options.Security is not (SecureSocketOptions.None or SecureSocketOptions.StartTls or SecureSocketOptions.SslOnConnect))
            errors.Add("SMTP security must be None, StartTls, or SslOnConnect.");
        if (!environment.IsDevelopment() && options.Security is not (SecureSocketOptions.StartTls or SecureSocketOptions.SslOnConnect))
            errors.Add("Production SMTP requires TLS.");
        if (string.IsNullOrWhiteSpace(options.Username) != string.IsNullOrWhiteSpace(options.Password))
            errors.Add("SMTP username and password must both be supplied.");
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.Username))
            errors.Add("Production SMTP authentication is required.");
        if (options.OperationTimeoutSeconds is < 1 or > 300 || options.IdleTimeoutSeconds is < 1 or > 86400 ||
            options.MaxAttempts is < 1 or > 5 || options.RetryDelaySeconds is < 1 or > 60 ||
            options.CircuitFailureThreshold is < 1 or > 100 || options.CircuitBreakSeconds is < 1 or > 3600)
            errors.Add("SMTP timeout, retry, or circuit settings are outside their supported bounds.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
