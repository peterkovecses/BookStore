# Identity authentication

The API exposes `POST /api/identity/login`, `/refresh`, and `/logout`. Login accepts
`{"email":"...","password":"..."}`. A successful login or refresh returns
`{"accessToken":"...","expiresAt":"..."}` and sets the host-only
`__Secure-bookstore-refresh` cookie. Keep the access token in memory only. Do not
store it or the refresh cookie in local storage, and never log credentials or
tokens. Refresh requires only the cookie, so it works after a page reload.
Logout expires the cookie and revokes its session family; it does not affect
other sessions. An already-issued stateless access token remains usable until
its expiry.

All three endpoints require HTTPS, an `Origin` exactly matching a configured UI
origin, and `X-BookStore-Request: 1`. Browser clients must send credentials
(`credentials: "include"`) and use JSON for login. Credentialed CORS permits
only the configured origins, POST, `Content-Type`, and the custom header.
Missing or untrusted origins and missing headers are rejected even if the cookie
is present. SameSite is defense in depth, not the CSRF defense. Only configure
origins you control; a compromised allowed origin can make authenticated
requests. API clients without a browser must also supply the origin and header.

The cookie is Secure, HttpOnly, host-only, and scoped to `/api/identity`.
`CookieSameSite` defaults to `Strict`; choose `Lax` or `None` only for a
deployment that requires it. Cross-site deployments require `None` and may
still be blocked by browser third-party cookie restrictions.

Refresh tokens are random and only their SHA-256 hashes are stored. Each
successful refresh rotates the token in a SQL transaction. Reuse of a rotated
token revokes the entire affected family, including its active descendant.
Concurrent refreshes using the same cookie are treated as reuse: one can
succeed, but the other revokes the successor, requiring a new login. The UI
must coordinate refresh using single-flight across tabs as well as within a
tab. Replay handling does not revoke already-issued access tokens.

Failed logins for a known account use ASP.NET Identity's SQL-backed lockout,
serialized per normalized email across API instances. Lockout expires
automatically; the response does not distinguish a locked account from invalid
credentials. An additional per-process network limit helps absorb local bursts
but is not a distributed limiter. Production must also enforce a shared,
network-aware rate limit at its gateway or WAF across all instances; protect
the application from direct access that bypasses it. Configure trusted client
IP handling at that gateway, not from arbitrary forwarded headers. Monitor
lockouts and gateway throttling, and provide an operator-assisted account
recovery process. A successful password for an unconfirmed account reports
that confirmation is required.

## Configuration

Set these under `Identity:Authentication` using environment variables, a
secret manager, or other protected configuration. The API fails startup if
the signing key, issuer, audience, origins, lifetimes, or limits are unsafe.
All instances must share the same signing key, issuer, audience, and SQL
database. Key rotation invalidates outstanding access tokens unless consumers
are configured to accept the previous key during the transition.

| Setting | Default | Requirement |
| --- | --- | --- |
| `SigningKey` | none | Base64-encoded cryptographically random key of at least 32 bytes; keep secret. |
| `Issuer`, `Audience` | none | Nonempty values; token consumers must validate both, the signature and expiry. |
| `AllowedOrigins:0`, etc. | none | Exact HTTPS origins, no paths, credentials, query, wildcard, or trailing slash. |
| `AccessTokenLifetime` | `00:10:00` | From one through 15 minutes. |
| `RefreshTokenLifetime` | `14.00:00:00` | At least the access lifetime and at most 30 days; reset on rotation. |
| `CookieSameSite` | `Strict` | `Strict`, `Lax`, or `None`. |
| `LoginPermitLimit`, `LoginWindowSeconds` | `20`, `60` | Positive per-instance network burst limit and window. |
| `LockoutAttempts`, `LockoutDuration` | `5`, `00:15:00` | Attempts 1–100; duration from one minute through one day. |

For local development, create a key with `openssl rand -base64 32` and set
`Identity__Authentication__SigningKey`, `Identity__Authentication__Issuer`,
`Identity__Authentication__Audience`, and
`Identity__Authentication__AllowedOrigins__0` in the API process environment.
Use the actual HTTPS UI origin, for example `https://localhost:7001`, and keep
the key out of source control. Configure the existing Data Protection,
verification, database, and SMTP settings as described in their respective
documentation.

In production terminate HTTPS at the application or use TLS all the way from
the trusted proxy to the application, so the API observes an HTTPS request.
The application does not trust forwarded headers by default. Apply the
Identity migration through the normal deployment process before accepting
traffic. Restrict SQL permissions and back up the Identity database. Token
history must be retained for replay detection while a family can still have an
active descendant; do not delete rotated rows merely because their own
expiration has passed. Monitor storage growth and handle retention explicitly
before deploying a cleanup policy.

The original VocaBuddy login uses Identity password validation and a confirmed
email gate; BookStore retains those semantics. Unlike VocaBuddy, refresh does
not require an expired access token, raw refresh tokens are not stored or
returned in JSON, rotation and replay revocation are transactional, and logout
is server-side. No roles or authorization policies are introduced here.
