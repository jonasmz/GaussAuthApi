# Security baseline

The API returns security headers (`X-Content-Type-Options`, referrer policy, correlation id), uses a safe generic error body, and enforces request and upload limits. CORS is denied by default; browser access requires a future explicit allowlist feature.

Rate-limit groups isolate credential, recovery, admin, audit, consumer authorization, session, signing-key, and profile-image traffic (see configuration). Consumer secrets are random symmetric credentials stored as hashes and rotated through the administrative API. They are unrelated to the asymmetric ECDSA P-256/ES256 signing key.

Never log access tokens, consumer secrets, passwords, reset credentials, Authorization headers, sensitive request bodies, file contents, connection strings, or PEM/key material. EF sensitive-data logging remains disabled.
