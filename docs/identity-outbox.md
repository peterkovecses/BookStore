# Identity outbox operations

The Identity outbox is a module-owned SQL Server table. Producers must add the
outbox row using the same Identity context and transaction as their state
change; no registration event is emitted by this story. Other modules must
implement their own `IOutboxStore` over their own context and table and register
it with `AddOutbox<TStore>`. Never use the Identity table for another module.

## Event registration and delivery

Persist a non-secret stable ID, correlation ID, versioned event identifier and
JSON payload. Register each identifier explicitly with
`OutboxRegistry<TStore>.Register<TEvent>` at composition before workers start, and register the concrete
Requests handlers and `IEventPublisher<TEvent>` in DI. Never persist CLR
assembly-qualified type names. Changes to an event's schema require a new
version and a compatible registration for old rows. Requests runs its handlers
sequentially by default. Every registered handler must finish successfully;
missing handlers, missing publisher, unknown identifiers, invalid JSON and
handler failures are failures, not successes. A successful publish does not
prove that a particular intended handler was registered: integration stories
must test the required subscribers.

Event-specific `IOutboxExpiry<TEvent>` and `IOutboxPreflight<TEvent>` are
optional scoped policies. At host composition, the SMTP preflight adapter
`SmtpOutboxPreflight<TEvent>` can be registered only for an event that actually
needs email. It consults the EmailDispatcher contract, not its implementation.
An open circuit before publishing defers the event without consuming an attempt;
after publishing starts, any exception, including a circuit opening, consumes
the attempt. The preflight is advisory, not a reservation. Monitor long-running
deferrals. Expiry is terminal and requires a fresh business request, not
technical replay.

Delivery is **at least once**, not exactly once. A crash, pause, expired lease or
failed completion after an external side effect can repeat the entire event,
including earlier successful handlers. SMTP acceptance is not final delivery.
Consumers must provide idempotency where duplicate effects matter; ordering
between events is not guaranteed. Handlers should observe cancellation.
Scoped handlers can inject `OutboxDispatchContext` to obtain the stable
`EventId` and `CorrelationId`; those values are set before resolving the
publisher and are preserved across attempts and replays. A context resolved
outside an outbox dispatch has an empty event ID and must not be used as an
idempotency key.

## Processing and configuration

Configure `Outbox` using .NET configuration. Defaults are batch limit 20,
concurrency 4, polling 5 seconds, maximum 8 attempts, 30-second exponential
retry delay with multiplier 2 and ±20% jitter capped at 30 minutes, 5-minute
lease, 60-second renewal, 3-minute handler timeout, and 30-second shutdown
drain. Cleanup runs every 24 hours with a 7-day completion-based retention and
100-row batches; diagnostics run every minute. Settings are validated at
startup; lease and retry durations must also fit SQL Server's millisecond
`DATEADD` integer range. The timeout must be shorter than the lease, and the
renewal interval must be shorter than the lease. Size the lease, timeout and
renewal for database and application latency. Synchronize host and database
clocks because preflight recovery times and cleanup cutoffs use host time,
whereas claims and lease fencing use database time.

The worker claims only available execution capacity. SQL Server uses one
`UPDATE` over a `TOP` CTE with `UPDLOCK`, `READPAST` and
`READCOMMITTEDLOCK`, including when read-committed snapshot is enabled. The
claim increments the attempt and cumulative count, generates a fresh token and
sets a lease using database time. A separate bounded operation marks expired
claims at their attempt limit failed. No database transaction is held while a
handler runs. Renewal, completion, failure and preflight deferral require the
current token and an unexpired lease. A stopped process cannot strand an item
permanently: another worker recovers it after lease expiry, unless its attempts
are exhausted, in which case it becomes failed.

Cancellation is cooperative. On timeout or shutdown, the worker cancels and
tracks work for the configured shutdown period, never records interrupted work
as complete, and does not release a claim while a handler is still running.
Once a timed-out handler has stopped, the current owner records a bounded
failure and delayed retry; shutdown leaves the claim for lease recovery.
Lease expiry can nevertheless permit overlapping execution. Fencing prevents
stale database updates, not already-started external effects.

## Monitoring, replay and privacy

The monitor logs bounded aggregate pending, processing, failed and expired
counts and oldest pending age. Alert on failed or expired counts, growing queue
age, recurring worker/cleanup errors, and sustained preflight deferrals.
Attempt and replay logs contain event and correlation IDs; choose opaque,
non-secret IDs and actor names. Persisted `LastError` and worker errors contain
only an exception type, never exception messages, payloads, tokens or SMTP
credentials. Handler logging must independently observe the same precautions.
Do not enable EF sensitive-data logging in production.

After correcting a technical failure, an authenticated and authorized
operational caller may invoke `IIdentityOutboxOperations.ReplayAsync` with an
opaque actor identity and event ID. This interface itself performs **no**
authentication or authorization and is not exposed as an HTTP endpoint. Only
failed and completed rows can be replayed; the atomic operation preserves the
event ID, resets the cycle attempt count and increments processing cycle and
replay count while recording the last actor and timestamp. Replay of a completed
event may repeat effects. Expired events require a fresh request instead.
Review logs for attempt and replay records. Stored metadata is only the latest
attempt/replay summary, not a complete or financial audit trail; logs may be
rotated or lost.

Cleanup atomically deletes only completed records older than the configured
retention, in bounded batches. Multiple cleaners and replay can race safely:
the delete and replay predicates are state-conditional. Deletion also removes
the event's replay metadata, limiting subsequent audit and replay. Pending,
processing, failed and expired records are never automatically deleted.
Operators must monitor their volume and privacy exposure and explicitly
authorize investigation, export or deletion according to policy. Payloads may
contain protected sensitive data: restrict database and backup access, encrypt
payloads where appropriate, and retain decryption keys until all dependent
records and backups are removed. Establish retention and key-rotation procedures
before adding production sensitive events.
