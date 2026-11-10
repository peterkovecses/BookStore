namespace BookStore.EmailDispatcher;

internal sealed class EmailSender : IEmailSender, IAsyncDisposable
{
    private readonly SmtpSettings _settings;
    private readonly SmtpCircuitBreaker _breaker;
    private readonly TimeProvider _clock;
    private readonly ILogger<EmailSender> _logger;
    private readonly Func<ISmtpClient> _createClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _idleTask;
    private ISmtpClient? _client;
    private DateTimeOffset _lastUse;
    private int _disposed;

    public EmailSender(IOptions<SmtpSettings> options, SmtpCircuitBreaker breaker, TimeProvider clock, ILogger<EmailSender> logger)
        : this(options, breaker, clock, logger, () => new SmtpClient()) { }

    internal EmailSender(IOptions<SmtpSettings> options, SmtpCircuitBreaker breaker, TimeProvider clock,
        ILogger<EmailSender> logger, Func<ISmtpClient> createClient)
    {
        _settings = options.Value;
        _breaker = breaker;
        _clock = clock;
        _logger = logger;
        _createClient = createClient;
        _idleTask = DisconnectWhenIdleAsync();
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(message);
        var mail = CreateMimeMessage(message);

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await SendAttemptAsync(mail, lifetime.Token);

                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < _settings.MaxAttempts && !lifetime.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(_settings.RetryDelaySeconds * Math.Pow(2, attempt - 1) * (0.8 + Random.Shared.NextDouble() * 0.4));
                await Task.Delay(delay, _clock, lifetime.Token);
            }
        }
    }

    private static MimeMessage CreateMimeMessage(EmailMessage message)
    {
        var mail = new MimeMessage();
        mail.From.Add(MailboxAddress.Parse(message.From));
        mail.To.Add(MailboxAddress.Parse(message.To));
        mail.Subject = message.Subject;
        mail.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        return mail;
    }

    private async Task SendAttemptAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var availability = _breaker.GetAvailability();
        if (!availability.IsAvailable) throw new EmailCircuitOpenException(availability.RetryAfter);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var probe = _breaker.Enter();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(_settings.OperationTimeoutSeconds));
                try
                {
                    _client ??= _createClient();
                    if (!_client.IsConnected)
                    {
                        await _client.ConnectAsync(_settings.Host, _settings.Port, _settings.Security, timeout.Token);
                        if (!string.IsNullOrWhiteSpace(_settings.Username))
                        {
                            try
                            {
                                await _client.AuthenticateAsync(_settings.Username, _settings.Password!, timeout.Token);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                throw new SmtpAuthenticationFailedException(ex);
                            }
                        }
                    }
                    await _client.SendAsync(message, timeout.Token);
                    _lastUse = _clock.GetUtcNow();
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("The SMTP operation timed out.", ex);
                }
                _breaker.Complete(probe, false, false);
            }
            catch (Exception ex)
            {
                _client?.Dispose();
                _client = null;
                _breaker.Complete(probe, IsTransient(ex), ex is OperationCanceledException);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsTransient(Exception exception) => exception switch
    {
        SmtpCommandException command => (int)command.StatusCode is >= 400 and < 500,
        SmtpProtocolException => true,
        ServiceNotConnectedException => true,
        IOException => true,
        SocketException socket => socket.SocketErrorCode is SocketError.ConnectionAborted or SocketError.ConnectionRefused
            or SocketError.ConnectionReset or SocketError.HostDown or SocketError.HostUnreachable or SocketError.NetworkDown
            or SocketError.NetworkReset or SocketError.NetworkUnreachable or SocketError.NotConnected or SocketError.Shutdown
            or SocketError.TimedOut or SocketError.TryAgain,
        TimeoutException => true,
        _ => false
    };

    private async Task DisconnectWhenIdleAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.IdleTimeoutSeconds), _clock, _shutdown.Token);
                await _gate.WaitAsync(_shutdown.Token);
                try
                {
                    if (_client is not null && _clock.GetUtcNow() - _lastUse >= TimeSpan.FromSeconds(_settings.IdleTimeoutSeconds))
                    {
                        _client.Dispose();
                        _client = null;
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogIdleCleanupFailed(ex);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _shutdown.CancelAsync();
        try
        {
            await _idleTask;
        }
        finally
        {
            await _gate.WaitAsync();
            try
            {
                _client?.Dispose();
                _client = null;
            }
            finally
            {
                _gate.Release();
                _shutdown.Dispose();
            }
        }
    }
}

internal sealed class SmtpAuthenticationFailedException(Exception innerException)
    : Exception("SMTP authentication failed.", innerException);

internal static partial class EmailSenderLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "SMTP idle connection cleanup failed.")]
    internal static partial void LogIdleCleanupFailed(this ILogger<EmailSender> logger, Exception exception);
}
