# API Data Protection operations

The API uses ASP.NET Core Data Protection for features such as Identity tokens
and protected outbox payloads. Every replica in the same deployment must use
the **same** application name and the **same durable, shared, writable key-ring
directory**, including across restarts and deployments. Different environments
must use separate directories, names, and certificates. A local container
filesystem or node-local volume is not a shared persistent store.

## Production and other non-development environments

Provision a shared filesystem with reliable atomic file creation and visibility
between all replicas, and mount it at the same absolute path on every instance.
The directory must already exist: the API will not create it outside Development.
Restrict read/write access to the API service identity and trusted operators.
An existing but incorrectly mounted empty directory cannot be detected by the
application; deployment health checks must verify the mount and sharing.

Provision an RSA certificate with a private key in a password-protected PKCS#12
file. Mount the certificate read-only and inject the password from the deployment
secret manager, not source control, an image, logs, or command-line arguments.
The service identity must be able to read it. Configure (example values only):

```text
DataProtection__ApplicationName=BookStore.Api.Production
DataProtection__KeyRingPath=/mnt/bookstore/data-protection
DataProtection__Certificate__Path=/run/secrets/bookstore-dp-current.pfx
DataProtection__Certificate__Password=<injected secret>
```

Previous certificate paths and passwords are supplied as indexed configuration,
for example `DataProtection__PreviousCertificates__0__Path` and
`DataProtection__PreviousCertificates__0__Password`. Do not put actual values
in a deployment manifest. Protect both the ring and certificate backups against
unauthorized access and modification; certificate encryption does not protect
against a writer replacing or deleting keys. Certificate files, passwords, and
the key ring should be stored separately. On macOS, the runtime uses its
platform key-storage behavior rather than ephemeral PKCS#12 import.

The API rejects missing settings, nonexistent directories, relative production
paths and missing/unusable certificates in every environment.
Startup performs a protect operation to detect key initialization errors; it
does not prove that the configured directory is really shared or that every
historical key can be decrypted. Monitor key-ring errors and filesystem capacity,
permissions and availability; do not log tokens, protected payloads, private keys,
passwords or verification links.

Start with a ring whose existing key files are already certificate-encrypted.
Configuring a certificate encrypts newly written keys; it does not retroactively
encrypt old plaintext key files. The API rejects plaintext key files found at
startup. Migrate or replace an old plaintext ring only
with an approved procedure that preserves any still-needed protected data.

## Development and tests

Development explicitly uses `.data-protection-keys` under the API content root
and a development-only discriminator. This directory is gitignored and is
**not** suitable for shared or production workloads. Restrict access to the
developer account; deleting it invalidates tokens and makes protected messages
unreadable.

Create a dedicated RSA certificate and password-protected PKCS#12 bundle outside
source control. For example, run `openssl req -x509 -newkey rsa:3072 -keyout
dev-key.pem -out dev-cert.pem -days 365` and then `openssl pkcs12 -export -inkey
dev-key.pem -in dev-cert.pem -out dev-dataprotection.pfx`; OpenSSL prompts for
passwords rather than placing them in shell history. Run these commands in a
private directory, restrict permissions, and securely remove the intermediate
PEM files after export. Configure `DataProtection:Certificate:Path` with the
absolute PFX path and inject `DataProtection:Certificate:Password` via user
secrets or a secret manager. Do not commit the bundle or password. Certificate
loss has the same recovery implications as production.

Integration test factories generate a test-only certificate and isolated ring,
then remove them on disposal. The `IntegrationTesting` environment is not
exempt from production's certificate requirement.

## Rotation, retries and recovery

Data Protection rotates keys automatically (the framework default is 90 days).
Do not delete expired keys: old keys remain necessary for valid verification
tokens, protected pending outbox messages, delayed retries and backups.

For certificate rotation, first deploy the new certificate to **every** replica
as a previous/decryption certificate while retaining the old current certificate.
Once all replicas can decrypt both, switch the current certificate to the new one
and list the old one among previous certificates on every replica. Roll out and
verify that all instances can read old and new messages before removing anything.
New key files use the then-current certificate; existing key files are not
automatically re-encrypted. Retain all old private certificates and passwords
until every key and every restorable backup encrypted by them is no longer needed.

Back up the complete ring and all required certificates/passwords independently,
and regularly test restoration to an isolated environment with the same
application name and a copy of representative protected data. Restore a
consistent ring and corresponding certificates before resuming retries. Do not
replace a ring with a new empty one as a recovery action: this invalidates prior
tokens and makes pending encrypted payloads unreadable. If material is lost,
unreadable messages cannot be safely replayed; follow the outbox incident
process and offer users a fresh verification request rather than exposing or
guessing tokens. Coordinate key retention with the longest token lifetime,
outbox retry/retention window and backup retention period.

## Identity integration

Identity registers the default token providers and applies the validated
`Identity:Verification:TokenLifetime` to its Data Protection token provider.
The verification outbox expiry policy uses the event's recorded expiry and the
current configured lifetime. The verification link in the outbox event is
protected using the versioned purpose `BookStore.Identity.VerificationLink.v1`;
EmailDispatcher uses the same purpose to decrypt it before sending.

All API instances must use the same ring, application name, certificates and
verification settings for tokens and pending events to work across instances.
Keep tokens and links out of logs. See
[Identity verification operations](identity-verification.md) for endpoint,
expiry and delivery behavior.
