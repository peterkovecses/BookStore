# Identity email verification operations

## Configuration and trust

`Identity:Verification:LinkBaseUrl` is the trusted frontend destination, not a
value derived from an incoming host or forwarded header. It must be an absolute
HTTPS URL without user information, query string or fragment. Supply the actual
frontend address from trusted deployment configuration. For example:

```text
Identity__Verification__LinkBaseUrl=https://accounts.example.com/verify-email
Identity__Verification__TokenLifetime=1.00:00:00
```

`TokenLifetime` is a positive .NET `TimeSpan` and is used by Identity's Data
Protection token provider as well as the verification event's expiry decision.
The development default is one day; deployment owners should explicitly choose
a value consistent with delivery delay and security policy. Configure every
replica consistently. See [Data Protection operations](data-protection.md) for
the mandatory shared, encrypted ring, certificate rotation and recovery.
Synchronize clocks across API instances and workers; excessive clock skew can
make token validation and outbox expiry disagree.

Verification links and tokens are bearer credentials. Do not log request query
strings containing them, raw links, decrypted event payloads, or passwords.
The protected link is bound to the event's user ID and recipient address, so
changing either in a persisted event prevents the subscriber from decrypting it.
Configure reverse proxies, tracing, error reporting and application logging
accordingly. Limit database and backup access even though the persisted link is
protected. Protect and retain keys for pending messages and retained backups.

## Verification API

- `POST /api/identity/verification/confirmation` accepts JSON with `userId`
  and the base64url-encoded `token` supplied in the verification link. A valid
  token confirms the user's email and returns `204 No Content`. An already
  confirmed user also returns `204`, making repeat confirmation idempotent.
  Unknown users and invalid, expired or tampered tokens return a generic
  validation error. Both fields are required; `userId` is limited to 256
  characters and `token` to 2048.
- `POST /api/identity/verification/resend` accepts JSON with `email`. For an
  accepted request it always persists a protected resend request without
  looking up the account. The outbox worker subsequently creates a fresh
  verification email event for an unconfirmed account; unknown and already
  confirmed accounts receive no email. All three return `204 No Content`
  without disclosing account status or promising delivery. Email must be
  nonempty, valid and at most 256 characters; malformed input returns a
  validation error.

Both endpoints use independent per-client-IP fixed-window rate limits. The
defaults are 10 confirmation requests and 3 resend requests per 60 seconds,
with no queue; rejected requests return `429 Too Many Requests`. Configure
positive integer values, for example:

```text
Identity__Verification__RateLimiting__Confirmation__PermitLimit=10
Identity__Verification__RateLimiting__Confirmation__WindowSeconds=60
Identity__Verification__RateLimiting__Resend__PermitLimit=3
Identity__Verification__RateLimiting__Resend__WindowSeconds=60
```

Limits are per process, not shared across replicas. The partition uses the
connection's remote IP, or a shared `unknown` partition when unavailable.
Behind a proxy, configure trusted forwarded-header handling appropriately; do
not trust arbitrary client-supplied forwarding headers. Use a distributed
gateway limit where a deployment-wide limit is required.

## Delivery, expiry and recovery

Registration queues a durable verification request; it does not synchronously
send email. SMTP acceptance does not guarantee delivery. Outbox processing is
at least once: crashes or retry after SMTP acceptance can produce duplicate
emails. Resend requests are also retried, and a crash after a verification email
event is saved may create another event on retry. Do not promise delivery merely
because a request was queued. Resend processing checks the account's status at
processing time. Monitor and recover failed resend events as described below;
the protected address remains in the outbox until its normal retention cleanup.

Before delivery, the verification expiry policy uses the earlier of the event's
recorded expiry and its issue time plus the currently configured token lifetime.
Shortening the configured lifetime therefore expires pending events sooner;
lengthening it does not extend their recorded expiry. An expired event is
terminal and must not be sent or replayed; it does not mint a replacement token
during replay. A user can request a new event and fresh token using the
non-enumerating resend endpoint. A token can also expire after dispatch;
the frontend must handle this safely and offer a fresh request. The expiry
policy cannot recall an email already accepted by SMTP.

For technical failures, investigate the outbox using opaque event IDs, not
token-bearing content. Replay eligible failed events only after correcting the
cause; replay may duplicate a previously accepted email and expiry must still
be enforced. Follow [Identity outbox operations](identity-outbox.md) for
retention, authorization, retries and terminal states. If old Data Protection
keys or certificate material are unavailable, restore them before retrying;
otherwise issue a new verification request rather than trying to recover tokens.

## Design

The flow adapts VocaBuddy's Identity token and outbox-message-factory ideas to
the local Identity-owned outbox and `Kovecses.Requests` publisher; it does not
use a broker or RabbitMQ worker. Registration's user creation and outbox insert
must share the same scoped Identity context and actual database transaction,
including Identity's internal saves; a separately constructed context does not
provide that guarantee. No SMTP operation belongs in that transaction.
