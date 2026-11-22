# Event Content Studio

AI-assisted planning of social media content from authorized event photos. Organizations create events,
import photos, and generate reviewed, platform-specific posts and campaigns for Instagram and Facebook.
The full specification is in [`docs/spec.md`](docs/spec.md).

> **Status: Phase 0, Foundation.** This phase covers organizations (tenants), memberships with roles, the
> audit log, the API and web foundations, CI and the architecture decisions. Sign-in is a
> **development-only** stand-in for now ([ADR 0002](docs/adr/0002-authentication.md)).

## Stack

| Area | Choice |
|---|---|
| API / worker | ASP.NET Core 10 (LTS), Minimal APIs, EF Core 10 + Npgsql |
| Database | PostgreSQL 17, schema owned by SQL migrations ([ADR 0005](docs/adr/0005-sql-migrations.md)) |
| Jobs | PostgreSQL queue + transactional outbox ([ADR 0001](docs/adr/0001-architecture-and-job-queue.md)) |
| Storage | Cloudflare R2 (private, presigned URLs); MinIO locally ([ADR 0003](docs/adr/0003-storage.md)) |
| AI | Provider-neutral: Anthropic, OpenAI, Gemini, Azure OpenAI ([ADR 0004](docs/adr/0004-ai-provider.md)) |
| Web | Next.js 16 (App Router), TypeScript, Tailwind, TanStack Query, React Hook Form + Zod |
| Observability | OpenTelemetry traces and metrics (OTLP), structured JSON logs, health checks |

```
src/
  EventContent.Domain          Entities and business rules; no infrastructure dependencies
  EventContent.Application     Use cases (handlers) and ports
  EventContent.Contracts       Request/response DTOs (the public API shape)
  EventContent.Infrastructure  EF Core persistence, repositories, health checks
  EventContent.Api             HTTP endpoints, auth, tenancy filters, ProblemDetails
  EventContent.Migrator        Forward-only SQL migration runner
db/migrations                  NNNN_description.sql (never edit an applied file)
web/                           Next.js app with a backend-for-frontend proxy (/api/backend/*)
tests/Unit, tests/Integration  xUnit; integration tests use Testcontainers PostgreSQL
docs/adr                       Architecture decision records
```

## Prerequisites

- .NET SDK 10 (`brew install --cask dotnet-sdk`, or from dotnet.microsoft.com). `global.json` pins major version 10.
- Node.js 22+
- Docker Desktop

## Run locally

```bash
cp .env.example .env                      # local-only values; .env is git-ignored
docker compose up -d postgres             # PostgreSQL on 127.0.0.1:5432

# Apply migrations
ConnectionStrings__Database="Host=localhost;Database=event_content;Username=event_content;Password=event_content_dev" \
  dotnet run --project src/EventContent.Migrator

dotnet run --project src/EventContent.Api # http://localhost:5080 (OpenAPI at /openapi/v1.json)

cd web && cp .env.example .env.local && npm install && npm run dev   # http://localhost:3000
```

Or run everything in containers: `docker compose --profile app up --build`, then open http://localhost:3000.

**Trying roles:** the **DEV** box in the header switches the signed-in user. Have each person sign in once,
add them as members with different roles, then switch to them and see what they can and can't do.

## Tests

```bash
dotnet test tests/Unit/EventContent.UnitTests
dotnet test tests/Integration/EventContent.IntegrationTests   # needs Docker running
cd web && npm run lint && npm run typecheck && npm test
```

CI (`.github/workflows/ci.yml`) runs all of these, plus:
- a formatting check;
- vulnerable-package checks (NuGet and npm);
- container builds;
- gitleaks secret scanning;
- CodeQL (`codeql.yml`).

Third-party actions are pinned to commit SHAs. Dependabot keeps them current.

## API conventions

- Base path `/api/v1`. Errors are RFC 9457 ProblemDetails with a stable `code` and a `traceId`.
- Status codes:

  | Code | Meaning |
  |---|---|
  | `201` | created |
  | `202` | asynchronous job accepted |
  | `404` | not found, or not a member (ids are not confirmable) |
  | `403` | member without the needed permission |
  | `409` | version or uniqueness conflict |
  | `422` | validation failed (with per-field `errors`) |
  | `429` | rate limited |

- Updates carry the `version` received from the server (optimistic concurrency).

## Security notes

- Tenant isolation:
  - membership is checked server-side on every tenant route;
  - EF global query filters fail closed;
  - non-members get 404.
- The development sign-in cannot run outside `ASPNETCORE_ENVIRONMENT=Development`, and the API fails
  startup on any unknown auth mode.
- The browser never calls the API directly. The BFF proxy reaches only `/api/v1/*`, forwards an allow-list
  of headers, drops cookies in both directions, and rejects cross-site writes.
- Secrets come from the environment or user-secrets, never from committed files. Postgres is bound to localhost in Compose.
- Containers run as non-root (chiseled .NET images, `node` user).

## Roadmap

| Phase | Scope |
|---|---|
| 0 ✅ | Foundation: tenancy, RBAC, audit, CI, ADRs |
| 1 | Events, direct upload to R2, photographer invitations, media gallery, dedup, job queue |
| 2 | AI image analysis (provider-neutral), quality flags, grouping |
| 3 | Brand profile, creative brief, campaign generation and validation |
| 4 | Editorial UI: ordering, captions, refinements with diffs, approvals, calendar, export |
| 5 | Google Photos Picker, shared-album reference UX and fallbacks |
| 6 | Hardening: OIDC, CSP nonces, rate limits, observability, E2E, Cloudflare deployment |
