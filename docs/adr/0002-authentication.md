# ADR 0002 — Authentication: development sign-in now, OIDC later

- Status: Accepted (interim)
- Date: 2026-11-22

## Context

The product owner asked to defer real login for the first iteration. Tenancy and role-based access
control (RBAC) are still required from day one, because every later feature depends on them.

## Decision

- Identity is modelled as an external `(issuer, subject)` pair. Users are provisioned just-in-time on
  their first authenticated request. E-mail is profile data, not an identity key.
- **Interim: a development authentication scheme** (`Auth:Mode = Development`) signs every API request in as
  a configured local user. The `X-Dev-User` header switches the user, so roles and tenant isolation can be
  exercised locally.
  - The API **refuses to start** with this mode unless `ASPNETCORE_ENVIRONMENT=Development`.
  - Any other `Auth:Mode` value fails startup with a clear message, so there is no silent unauthenticated mode.
- **Backend-for-frontend (BFF).** The browser only talks to the Next.js app. A server-side route handler
  (`/api/backend/*`) attaches the identity and proxies to the API:
  - It reaches only `/api/v1/*`.
  - It forwards an allow-list of headers, and never forwards browser cookies.
  - It rejects cross-site writes by checking the `Origin` header.
- Authorization is enforced **server-side** per endpoint by permission (`ContentEdit`, `MembersManage`, …),
  derived from the member's role in the organization resolved for the request.
  - A non-member gets `404`, so organization ids cannot be probed.
  - A member without the needed permission gets `403`.

## Later (before any shared deployment)

- Add an OIDC scheme (JWT bearer on the API; authorization-code flow with PKCE in the BFF). Tokens are
  kept server-side in an encrypted, HTTP-only session cookie, so they never reach browser JavaScript.
- The provider is chosen by config. Keycloak works locally; Google, Microsoft Entra ID, Auth0 and Clerk all fit.
- Replace "member must have signed in once" with e-mail invitations.
- Add a nonce-based Content Security Policy for scripts.
