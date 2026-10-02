# Deployment

Build the image with `docker build -t gaussauth .`; provide the required configuration through the deployment platform or a compose env file, never by baking secrets into the image. Apply a rollout in this order: run `dotnet GaussAuth.Api.dll migrate` once, start the API, then wait for `GET /health/ready` to return `204` before sending traffic. `GET /health/live` only confirms that the process is alive.

Do not publish health endpoints to the internet unless that is intentional. Keep them behind the deployment network, reverse proxy, firewall, or ingress policy appropriate for the orchestrator.

TLS terminates upstream. Forward `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`; configure each proxy IP/CIDR in `ForwardedHeaders:KnownProxies` or `KnownNetworks`. With no trusted proxy list, forwarded headers are ignored and Production-class startup warns that client IP and scheme may be inaccurate. Do not set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: it trusts arbitrary proxies and is refused without explicit trust lists. HSTS is effective only when HTTPS is correctly forwarded.

Production logs use JSON and include scopes. Correlate an incident using response `X-Correlation-Id` (the trace id). On termination the host accepts the configured graceful-shutdown window; remove it from routing first and allow requests to complete.
