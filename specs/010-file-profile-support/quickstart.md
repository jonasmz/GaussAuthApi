# Quickstart Validation: Secure Profile Images

Configure a private local `ProfileImages:StorageRoot` outside source and executable paths. Set maximum input/output bytes to 5 MB and dimensions to 4096×4096, and ensure the development container has deliberate project-scoped persisted storage.

1. Authenticate as a user and upload a valid JPEG, PNG, and WebP through the self-service endpoint; confirm a new opaque avatar reference/URL.
2. Retrieve the opaque URL without authentication; confirm trusted content type, inline disposition, safe headers, and cache behavior.
3. Upload a spoofed extension/MIME, invalid image bytes, SVG, oversized input, and oversized dimensions; confirm safe rejection and no current-avatar change.
4. Replace an avatar; confirm the reference changes, new image is current, and previous file is retired. Simulate persistence and cleanup failures to confirm compensation/observability.
5. Remove twice; confirm idempotent success, cleared reference, and no unrelated file deletion.
6. Run `dotnet test GaussAuth.slnx` in the SDK container.
