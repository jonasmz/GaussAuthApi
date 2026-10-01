# Operational API Contract: 001-foundation

## Purpose

This is the only public HTTP route introduced by the foundation.
It confirms that the API process can serve a request. It is a liveness
check, not a database-readiness or authentication check.

## GET /health/live

| Condition | Response |
|---|---|
| API process is serving requests | HTTP 204 No Content; empty body. |
| API process is unavailable | No HTTP response; caller observes connection failure or timeout. |

- Anonymous access is allowed.
- No request body, query parameters, or application data are accepted.
- No database credentials, connection state, stack trace, host path, or
  implementation detail is returned.
- The route performs no database query and does not create or change data.
- PostgreSQL connectivity is validated by the migration and integration
  workflow in [quickstart.md](../quickstart.md).

## General error shape

Unexpected API errors in production use the built-in ASP.NET Core Problem
Details JSON contract with HTTP 500. The response may contain a generic
title and status, but MUST NOT contain exception messages, stack traces,
SQL, connection strings, physical paths, secrets, or authentication data.
The implementation must provide a safe fallback if the request does not
accept the default Problem Details media type.

The foundation exposes no test-only error route in production and no
identity, role, permission, session, or business operation endpoint.
