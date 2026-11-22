# ADR 0006 — Multi-tenancy and authorization model

- Status: Accepted
- Date: 2026-11-22

## Decision

- **Shared database, shared schema, `organization_id` on every tenant-owned row.**
- The tenant for a request is resolved server-side, from the `organizationId` route value or the
  `X-Organization-Id` header, **and checked against membership** before any handler runs
  (`TenantEndpointFilter`).
- Defense in depth: every entity implementing `ITenantScoped` gets an EF Core global query filter on
  the resolved tenant.
  - The filter **fails closed**: with no resolved tenant, it returns nothing.
  - Model building throws if a tenant-scoped entity lacks the filter.
  - Cross-tenant reads (such as "my organizations") opt out explicitly with `IgnoreQueryFilters()`, and are
    reviewed as such.
- Roles map to permissions in one place (`RolePermissions`). Endpoints require permissions, never roles:

  | Role | Permissions |
  |---|---|
  | Owner | everything |
  | Admin | read, edit, approve, manage members, organization settings, integrations |
  | Editor | read, edit |
  | Reviewer | read, approve |
  | Viewer | read |

- Membership invariants live in the `Organization` aggregate:
  - Only owners grant, change or remove the Owner role.
  - The last owner cannot be removed or demoted.
  - Every membership change bumps `membership_revision`, so concurrent changes conflict on the
    organization row (optimistic concurrency via `xmin`). This prevents races such as two admins removing
    the last two owners at once.
- Every membership and settings change writes an audit entry in the same transaction. The `audit_logs`
  table rejects updates at the database level.

## Later

PostgreSQL row-level security (RLS) can be added as a third layer, with a per-transaction `SET app.tenant_id`,
if we ever run untrusted SQL or reporting queries against the primary database.
