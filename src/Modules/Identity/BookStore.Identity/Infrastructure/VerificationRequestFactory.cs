using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace BookStore.Identity.Infrastructure;

internal sealed class VerificationOptions
{
    public string LinkBaseUrl { get; set; } = string.Empty;
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(24);

    public bool IsValid() =>
        TokenLifetime > TimeSpan.Zero &&
        Uri.TryCreate(LinkBaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment);
}

internal interface IVerificationRequestFactory
{
    Task<OutboxMessage> CreateAsync(ApplicationUser user, CancellationToken cancellationToken);
}

internal sealed class VerificationRequestFactory(
    UserManager<ApplicationUser> users,
    IDataProtectionProvider protection,
    IOptions<VerificationOptions> options,
    TimeProvider clock) : IVerificationRequestFactory
{
    internal const string EventType = "identity.verification-email-requested.v1";
    internal const string ProtectionPurpose = "BookStore.Identity.VerificationLink.v1";

    public async Task<OutboxMessage> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = clock.GetUtcNow();
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = QueryHelpers.AddQueryString(options.Value.LinkBaseUrl, new Dictionary<string, string?>
        {
            ["userId"] = user.Id,
            ["token"] = encodedToken
        });
        var value = new VerificationEmailRequested(user.Id, user.Email!, user.FirstName,
            protection.CreateProtector(ProtectionPurpose).CreateProtector(user.Id).CreateProtector(user.Email!).Protect(link),
            now + options.Value.TokenLifetime)
        {
            IssuedAt = now
        };
        var id = Guid.NewGuid();

        return new OutboxMessage
        {
            Id = id,
            EventType = EventType,
            Payload = JsonSerializer.Serialize(value),
            CorrelationId = id.ToString(),
            CreatedAt = now
        };
    }
}

internal sealed class VerificationRequestExpiry(IOptions<VerificationOptions> options) : IOutboxExpiry<VerificationEmailRequested>
{
    public bool IsExpired(VerificationEmailRequested value, DateTimeOffset now) =>
        now >= value.ExpiresAt || (now >= value.IssuedAt && now - value.IssuedAt >= options.Value.TokenLifetime);
}
