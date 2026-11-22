# ADR 0005 — Schema owned by versioned SQL migrations; EF Core maps onto it

- Status: Accepted
- Date: 2026-11-22

## Context

The spec asks for committed database migrations. EF Core's migration tooling generates C# migrations
from the model. Plain SQL migrations are explicit, reviewable, and can use PostgreSQL features directly:
check constraints, triggers, partial indexes, `xmin` concurrency.

## Decision

- The schema lives in `db/migrations/NNNN_description.sql`, applied in order by
  `src/EventContent.Migrator` (a small console tool, also shipped as a container image). The migrator:
  - takes a PostgreSQL **advisory lock**, so concurrent deploys cannot race;
  - runs each migration in **its own transaction** and records it in `schema_migrations`;
  - stores a **SHA-256 checksum** of each migration and refuses to run if an applied migration was edited;
  - refuses to run if the database has migrations this build doesn't know (protects against rollback accidents).
- EF Core is used for queries and change tracking only. Its model maps onto the SQL schema using snake_case.
- An integration test queries every mapped entity against the migrated database, so model/schema drift
  fails CI.
- Migrations are forward-only. A fix is a new migration; applied files are never edited.

## Consequences

Schema changes are written by hand in SQL, a little more work than `dotnet ef migrations add`.
In exchange, the database enforces invariants itself (enum checks, slug format, an append-only audit log)
even for writes that bypass the application.
