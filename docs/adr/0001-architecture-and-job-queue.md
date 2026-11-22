# ADR 0001 — Modular monolith and a PostgreSQL-backed job queue

- Status: Accepted
- Date: 2026-11-22

## Context

The spec asks for an API, background workers (media import, image analysis, AI generation), an outbox,
and idempotent consumers, and it says to choose between MassTransit + RabbitMQ and a durable
PostgreSQL-backed queue. It also says to prefer simple, maintainable solutions and not to require
Kubernetes. The likely hosting target is Cloudflare Containers plus a managed PostgreSQL.

## Decision

1. **Modular monolith** in one solution: `Domain` → `Application` → `Infrastructure` → `Api`/`Worker`,
   with feature folders (Organizations, Events, Media, Imports, AI, Campaigns, Publishing, Audit).
   `Domain` and `Application` reference no infrastructure packages, so business rules are unit-testable
   without a database.
2. **PostgreSQL-backed job queue with a transactional outbox** (implemented in Phase 1, when the first
   background work appears):
   - A `jobs` table, claimed with `SELECT … FOR UPDATE SKIP LOCKED`, with attempt counts, bounded
     exponential back-off, a `run_after` timestamp, and dead-lettering after the maximum number of attempts.
   - Jobs are inserted **in the same transaction** as the state change that causes them (this is the outbox),
     so a job is never lost or emitted for a rolled-back change.
   - Each job has an idempotency key, and handlers are written to be safely re-runnable.
   - Concurrency is bounded per job type (e.g. AI calls), configurable per environment.

## Consequences

- One fewer service to run, secure, back up and monitor. Local development needs only PostgreSQL.
- Throughput is sufficient for the MVP (hundreds of photos per event). PostgreSQL queues comfortably handle
  thousands of jobs per minute; this is far below that.
- Newer MassTransit versions are commercially licensed, so avoiding it also avoids a licensing decision.
- If volume grows beyond what PostgreSQL handles comfortably, the job abstraction (`IJobQueue`) lets us move
  to a broker (RabbitMQ, Cloudflare Queues, SQS) without touching handlers.
