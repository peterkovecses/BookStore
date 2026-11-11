# Email dispatcher

The in-process EmailDispatcher supplies SMTP acceptance only. It does not queue mail,
guarantee delivery, or implement registration or verification. It exposes
`IEmailSender` and `IEmailDispatchAvailability` through its Contracts project.

## Development

Run the Aspire AppHost in the Development environment. It starts Mailpit and passes
its SMTP host and port to the API through `EmailDispatcher__Smtp__Host` and
`EmailDispatcher__Smtp__Port`, with `Security=None`; no credentials are needed.
Open the Mailpit HTTP endpoint in the Aspire dashboard to inspect captured messages.
Mailpit is a development sink, not a production sender.

## Configuration

Configure `EmailDispatcher:Smtp` using the standard .NET configuration providers.
Outside Development, a host, username, password and required TLS are mandatory.
Use `Security=StartTls` (default) or `SslOnConnect`; automatic or opportunistic
TLS is rejected. Store credentials in user secrets during local testing or in
the deployment's secret provider, never in source control. The default port is 587.
Set `EmailDispatcher:Smtp:FromAddress` to a valid sender mailbox for verification
emails. Startup fails if it is missing or invalid.
The subscriber also requires the same configured Data Protection provider and key
ring used to protect the verification link; it does not configure a key ring.

| Setting | Default | Allowed range / meaning |
| --- | --- | --- |
| `FromAddress` | none | Required valid sender mailbox for verification emails |
| `OperationTimeoutSeconds` | 15 | 1–300; bounds connect, authentication and send together per attempt |
| `IdleTimeoutSeconds` | 60 | 1–86400; close an idle connection |
| `MaxAttempts` | 2 | 1–5 total SMTP attempts per invocation |
| `RetryDelaySeconds` | 2 | 1–60; exponential backoff with ±20% jitter |
| `CircuitEnabled` | true | Enable transient-failure circuit breaker |
| `CircuitFailureThreshold` | 5 | 1–100 consecutive transient SMTP failures |
| `CircuitBreakSeconds` | 30 | 1–3600 seconds before one recovery probe |

Transient network failures, timeouts and SMTP 4xx responses can be retried;
permanent SMTP responses, authentication errors and caller cancellation are not.
An unknown SMTP host is treated as a configuration failure, not a transient outage.
Failed connections are discarded. An open circuit raises
`EmailCircuitOpenException`, with a `RetryAfter` timestamp when known, before
an SMTP attempt. Once the break duration expires, one half-open probe is allowed;
success or a permanent response closes the circuit, while a transient failure
reopens it. Cancellation does not count as an outage.

## Coordination with the future outbox

`IEmailDispatchAvailability.GetAvailability()` is an advisory preflight, not a
reservation: the circuit can open between the check and sending. A future outbox
can defer an event without consuming an attempt when preflight says the circuit
is open. A circuit-open result after publishing starts consumes that event attempt.
When the half-open probe is busy, no reliable retry-after timestamp is available.

Each whole-event outbox attempt can perform up to `MaxAttempts` SMTP attempts;
choose the outbox retry budget and delay with this multiplier in mind. The
sender releases its client lock during retry delays, but actual SMTP sends share
one client and are serialized even for concurrently processed events. A timeout
after the server accepted a message may result in duplicates on retry. SMTP
acceptance is not proof of final delivery; neither this sender nor a future
outbox alone can guarantee exactly-once email delivery.
