# Password recovery

## Configuration and trust

Set `Identity:PasswordRecovery:LinkBaseUrl` to the trusted frontend reset page,
not an origin derived from a request. It must be an absolute HTTPS URL without
userinfo, query or fragment. Set the positive `TokenLifetime` consistently on
every instance; its default is one hour. For example:

```text
Identity__PasswordRecovery__LinkBaseUrl=https://accounts.example.com/reset-password
Identity__PasswordRecovery__TokenLifetime=01:00:00
```

The link contains an encoded user ID and a base64url-encoded Identity token.
Both are bearer-sensitive; configure proxies, analytics, tracing and frontend
error reporting not to record query strings, tokens, links or passwords. The
reset page should remove the query string from browser history after capturing
it, avoid third-party resources and use a restrictive referrer policy.

The token provider and protected outbox payloads use the shared persistent
Data Protection key ring and application discriminator. Keys must be encrypted
at rest, shared by every API/worker instance and preserved across restarts.
Follow [Data Protection operations](data-protection.md) for certificates,
rotation, backups and recovery: retain old keys and the certificates needed to
decrypt them until pending messages and retained backups no longer need them.
Protect database and backup access even though the reset link is encrypted.
Never put decrypted event content into operational diagnostics.

## API and session behavior

`POST /api/identity/password/forgot` accepts `email` (valid, at most 256
characters). Every accepted request persists a protected request event and
returns `204`, regardless of account existence; processing later creates an
email event only for an existing account. A `204` is not a delivery guarantee.

`POST /api/identity/password/reset` accepts `userId`, `token` and
`newPassword`. The existing Identity password policy applies. A successful
request returns `204`, confirms an unconfirmed email address, changes the
password and revokes all refresh sessions in one SQL transaction. A new login
is required. Previously issued stateless access tokens remain usable until
their configured expiration; see [authentication](identity-authentication.md).
Invalid, expired, tampered or already used tokens and rejected passwords
produce a generic validation error without changing password or sessions.
Concurrent uses of a token are serialized; at most one succeeds. Login and
refresh issuance also participate in the per-user transaction lock.

Each endpoint has its own per-process, per-remote-IP fixed-window limit with no
queue. Defaults are 3 forgot and 10 reset requests per 60 seconds. Configure
positive values using `Identity:PasswordRecovery:RateLimiting:Forgot` and
`:Reset`, each with `PermitLimit` and `WindowSeconds`. Over-limit requests
return `429`. Use trusted proxy forwarding configuration and a distributed
gateway limit where appropriate; these limits are not shared across instances.

## Delivery and recovery

The worker publishes events through the Identity-owned transactional outbox
and local Requests publisher, not RabbitMQ. SMTP happens asynchronously. New
reset events protect the link with a Data Protection purpose bound to the
user ID, without including the recipient address in that purpose. The email
dispatcher retains compatibility with previously queued links that used the
legacy user-ID-and-address purpose; Data Protection informational logs are
filtered to avoid exposing legacy purpose values.
The intermediate recovery request and its generated email event are saved in
one SQL transaction. A durable receipt keyed by the intermediate event ID
prevents a retry or replay of that event from generating a second token or
email event. The receipt is retained for as long as its parent outbox row can
be replayed, and is deleted with that row after completed-event retention.
Requests for unknown accounts are also recorded as processed; if an account is
created later, a new forgot request is required.

SMTP retries may produce duplicate delivery of the same email and reset link;
SMTP acceptance is not final delivery. A child email retry reuses its
persisted protected link. Replaying a completed parent does not mint a new
token; the user must submit a new forgot request for a fresh token.
See [Identity outbox operations](identity-outbox.md) for monitoring, bounded
retries, failure recovery and retention. Failed and expired records require
operator attention under that policy; completed records are cleaned up under
the configured retention. Never replay an expired event or mint a new token
during replay.

Before token creation, the intermediate request expires at the earlier of its
recorded expiration and its outbox creation time plus the currently configured
token lifetime. Existing payloads without an expiration remain readable and
use their outbox `CreatedAt` with the current lifetime; stale legacy requests
expire before generating a token. Shortening the lifetime expires pending
messages earlier, while extending it does not extend an existing recorded
expiration. The reset-email event independently expires at the earlier of its
recorded expiration and issue time plus the current lifetime. Expired events
enter the terminal `expired` state and are not sent. A token can expire after
SMTP acceptance; the frontend must safely handle that error and offer a fresh
request. Synchronize clocks across instances and workers. A circuit-open SMTP
preflight defers reset-email events without consuming attempts or blocking
unrelated event types.

The Identity token and email template concepts adapt VocaBuddy's recovery
flow. BookStore uses its own protected outbox and awaited local publisher,
never VocaBuddy's broker worker, token logging or plaintext refresh tokens.
