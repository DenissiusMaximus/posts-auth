# Security review

Дата: 2026-10-07

Повний аудит із доказами та рекомендаціями наведено в [AUDIT-REPORT.md](AUDIT-REPORT.md).

## Findings

| # | Severity | File | Lines | Vulnerability | Confidence |
|---|----------|------|-------|---------------|------------|
| 1 | 🔴 CRITICAL | src/Api/Controllers/ApiKeysController.cs | 13-30 | API-key management endpoints lack authentication and ownership checks; arbitrary users can create, list, or revoke keys. | 10/10 |
| 2 | 🟠 HIGH | docker-compose.yml | 17-24 | PostgreSQL is published on host port 5432 with static `app`/`app` credentials. | 9/10 |
| 3 | 🟠 HIGH | Dockerfile | 17-18 | Docker API listens on HTTP 8080 without TLS; credentials and tokens require an HTTPS reverse proxy in production. | 9/10 |
| 4 | 🟠 HIGH | src/Api/Program.cs | 11-31 | Authentication/authorization middleware is not configured and Swagger is exposed without a production restriction. | 9/10 |
| 5 | 🟡 MEDIUM | src/Application/Handlers/Auth/RefreshHandler.cs | 24-53 | Refresh-token rotation is not atomic, allowing concurrent replay of the same refresh token. | 8/10 |
| 6 | 🟡 MEDIUM | src/Application/Handlers/ApiKeys/CreateApiKeyHandler.cs | 22-54 | Duplicate API-key names can escape as an unhandled database exception instead of a conflict response. | 9/10 |
| 7 | 🟡 MEDIUM | src/Application/Handlers/Auth/RegisterHandler.cs | 23-49 | Concurrent registration can violate the unique login constraint and escape as an unhandled database exception. | 9/10 |
| 8 | 🟡 MEDIUM | src/Application/Handlers/ApiKeys/ListApiKeysHandler.cs | 12-21 | API-key listing has no pagination or maximum result limit. | 8/10 |

## Recommended remediation order

1. Enforce JWT authentication/authorization and derive API-key ownership from authenticated claims.
2. Remove the public PostgreSQL binding and enforce TLS at the external boundary.
3. Make refresh-token rotation atomic and single-use under concurrency.
4. Translate unique-constraint violations to stable `409 Conflict` responses.
5. Add bounded pagination to API-key listing.

This review was read-only. No source code or database data was changed.
