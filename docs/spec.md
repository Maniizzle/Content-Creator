# Event Content Studio — Technical Product & Implementation Specification

**Version:** 1.0  
**Status:** Ready for implementation planning  
**Audience:** Autonomous developer agent, backend/frontend engineers, QA  
**Target:** Responsive web SaaS, multi-tenant  

## 1. Mission and scope

Build an AI-assisted event content planning platform that transforms authorized event photos and verified event context into editable, platform-specific social media posts and campaigns. A user creates an event, imports/selects photos, provides an optional creative brief, generates a single post or scheduled campaign, reviews photo selections/captions, and approves/export-plans content. Instagram and Facebook are the first target platforms.

**MVP includes:** organization/workspace, authentication and membership, event CRUD, event context, direct photo upload, photographer upload invitation, Google Photos Picker integration, Google Photos shared-link reference with assisted selection/fallback, private media library, asynchronous image analysis, photo deduplication, one-time post and campaign modes, creative brief, AI clarification prompts, platform-specific drafts, editing/reordering, AI refinements with previews, approvals, calendar, manual publishing/export, audit trail.

**Post-MVP:** Google Drive Picker/folder import, direct Instagram/Facebook API publishing subject to platform permissions, analytics, recurring campaigns, performance optimization, additional social networks. The domain and provider abstractions must accommodate these without requiring their implementation now.

**Non-goals:** Scraping Google Photos pages; importing arbitrary shared Google Photos links without user authorization; autonomous publication without explicit approval; inventing speakers, quotes, attendance or outcomes; training models on customer photos; guaranteeing optimal posting times.

## 2. Personas and user journeys

**Communications manager:** Creates an event and 14-day awareness campaign with three posts/week for Instagram and Facebook. Wants photo selection, narrative variation, platform-specific captions, editable drafts, and a publishing plan.

**Photographer:** Receives time-limited event upload invitation, submits event photos without creating an account (or with a lightweight authenticated flow), sees upload progress, cannot access other event content.

**Reviewer/editor:** Can replace/reorder photos, edit text, request AI refinements, approve/reject posts and view change history.

### Journey A: user-owned Google Photos
1. Create event with title, date, summary, optional verified agenda/speaker names/outcomes.
2. Choose Google Photos; authorize Picker scope; backend creates Picker session; frontend opens Google-provided picker URL.
3. User selects items; backend polls according to API guidance and fetches authorized selected media within URL/session lifetime.
4. Background jobs normalize, validate, store, analyze, and deduplicate media.
5. User chooses single-post or campaign, platform(s), objective, cadence, duration, tone, focus, avoidances and custom creative brief.
6. AI asks only material clarification questions; user can answer or skip.
7. AI produces structured content plan, ordered media IDs, captions per platform, selection reasons and posting instructions.
8. User previews, edits, regenerates individual posts, approves and exports or manually schedules.

### Journey B: photographer-shared Google Photos album
1. User pastes a shared album URL; app validates the URL format and stores it as **reference only**.
2. UI explicitly states that a pasted shared album URL cannot be bulk-downloaded using the supported Google Photos APIs.
3. Offer: open album and select accessible items via Google Photos Picker, ask photographer to share through Drive, or generate a photographer upload invitation.
4. Never label a URL reference as a successful media import. Continue only after actual authorized media arrives.

### Journey C: photographer portal
1. Event owner creates restricted, expiring upload invitation.
2. Photographer opens tokenized page and uploads images directly to private storage via short-lived presigned URLs.
3. Backend validates uploaded objects and attaches them to event; owner reviews and starts generation.

## 3. Product UX and screens

Primary navigation: Dashboard, Events, Campaigns, Calendar, Media Library, Organization Settings.

### Event creation wizard
1. **Event details:** title, date/time zone, description, organizer, event type, location optional, target audience, verified speakers/partners, agenda, notes, measurable outcomes and supporting documents optional.
2. **Media import:** tabs for Upload, Google Photos (My Library), Photographer Shared Album, Photographer Upload Link; Drive provider reserved for later. Show truthful source-specific status and fallback instructions.
3. **Media review:** progress, counts, gallery thumbnails, similarity groups, quality flags, replace/exclude, selection count.
4. **Campaign setup:** one-time or campaign; target platforms; objective; campaign start date, duration (default 14 days), posts/week (default 3), timezone; preferred formats.
5. **Creative direction:** organization brand profile (read-only reference with edit permission), optional freeform creative brief, tone, focus chips, avoidance chips, custom instructions, optional reusable template. Allow user to override compatible brand preferences for this campaign.
6. **Clarifications:** AI-generated targeted questions only when missing facts materially affect the output; allow skip.
7. **Generated result:** campaign calendar and list of posts; each post shows selected photos, cover, selection rationale, platform-specific editable captions, hashtags, CTA, instructions, review status.

### Campaign editor
Desktop: left post list/calendar, center photo carousel and caption editor, right AI assistant/instructions. Mobile: stacked tabs. Support drag/drop image order, replacement from event library, cover designation, regenerate post/caption, compare proposed edits, undo, version history, explicit approval, and per-platform publishing status. Preserve manual edits unless user explicitly confirms overwriting them.

### Calendar
Month/week/list views; scheduled date/time with timezone; drag/drop rescheduling; conflict warnings; statuses Draft, NeedsReview, Approved, Planned, PublishedManually, Failed. MVP must not imply automated publication. Allow export of images and copyable captions; downloadable CSV/JSON schedule and ZIP media package optional if storage/bandwidth permits.

## 4. Recommended stack and repository

- Frontend: Next.js (App Router), TypeScript, Tailwind, accessible UI primitives, TanStack Query, React Hook Form + Zod, sortable drag/drop library.
- Backend: ASP.NET Core on supported LTS .NET, C#, Minimal APIs, vertical slices/modules, EF Core + Npgsql, FluentValidation or equivalent, ProblemDetails.
- Database: PostgreSQL; `pgvector` optional for semantic similarity; first version may use perceptual hash and tags without vector DB.
- Storage: Cloudflare R2, private bucket(s), S3-compatible client, presigned upload/download URLs.
- Jobs: MassTransit + RabbitMQ or durable PostgreSQL-backed queue; use outbox and idempotent consumers. Choose one and document tradeoff. Do not require Kubernetes for MVP.
- AI: provider-neutral vision/text interface with structured JSON output, schema validation, retry/backoff, bounded concurrency, per-job cost tracking. Do not require LangGraph initially.
- Observability: OpenTelemetry traces/metrics, structured logs, health checks, job progress, error reporting.
- Auth: OIDC/OAuth for application login; separate provider authorization for Google Photos. Tenant-scoped RBAC.

Suggested structure:
```
/src
  /EventContent.Api
  /EventContent.Application
  /EventContent.Domain
  /EventContent.Infrastructure
  /EventContent.Worker
  /EventContent.Contracts
/web
  /app
  /components
  /features
/tests
  /Unit
  /Integration
  /E2E
/docs
  /adr
```
Modules: Organizations, Events, Media, Imports, AI, Campaigns, Publishing, Audit.

## 5. Core domain model

Use UUID primary keys, UTC timestamps, tenant-scoped queries, optimistic concurrency tokens, soft deletion where required, and separate media provenance from canonical media assets.

| Entity | Essential fields |
|---|---|
| Organization | Id, Name, Slug, DefaultTimeZone, CreatedAt |
| OrganizationMember | Id, OrganizationId, UserId, Role (Owner/Admin/Editor/Reviewer/Viewer) |
| BrandProfile | Id, OrganizationId, Voice, GuidelinesJson, HashtagsJson, Version |
| Event | Id, OrganizationId, Title, Description, EventAt, TimeZone, Status, ContextJson, CreatedBy |
| EventFact | Id, EventId, Type, Value, Source, VerifiedBy, VerifiedAt; optional supporting evidence |
| MediaSource | Id, EventId, Provider, SourceReference, SourceStatus, CreatedBy |
| ImportSession | Id, MediaSourceId, ProviderSessionId, Status, ExpiresAt, CountsJson, ErrorCode, CreatedAt |
| ImportItem | Id, ImportSessionId, ProviderItemId, Status, MediaAssetId, ErrorCode, AttemptCount |
| MediaAsset | Id, OrganizationId, StorageKey, MimeType, Bytes, Width, Height, Sha256, PerceptualHash, CreatedAt |
| EventMedia | Id, EventId, MediaAssetId, SourceId, ExternalMediaId, DisplayName, Visibility, Excluded, AddedAt |
| MediaAnalysis | Id, MediaAssetId, ModelVersion, Description, TagsJson, QualityScoresJson, FlagsJson, Embedding optional |
| Campaign | Id, EventId, Mode, Objective, StartDate, DurationDays, PostsPerWeek, TimeZone, Status, CreativeBriefJson, Version |
| CampaignPlatform | CampaignId, Platform |
| CampaignPost | Id, CampaignId, Sequence, Theme, NarrativePurpose, ScheduledAt, Status, Version |
| PostMedia | Id, CampaignPostId, EventMediaId, Position, IsCover, SelectionReason |
| PostPlatformContent | Id, CampaignPostId, Platform, Caption, HashtagsJson, Format, InstructionsJson, Status, ApprovedAt |
| GenerationJob | Id, CampaignId, Type, Status, InputSnapshotJson, ModelVersion, CostEstimate, ErrorCode, StartedAt, FinishedAt |
| RefinementRequest | Id, CampaignId, CampaignPostId nullable, UserPrompt, ProposedPatchJson, Status, CreatedBy |
| Approval | Id, CampaignPostId, Platform nullable, ReviewerId, Decision, Note, CreatedAt |
| Publication | Id, CampaignPostId, Platform, Method (Manual/Api), ScheduledAt, PublishedAt, ExternalPostId, Status |
| AuditLog | Id, OrganizationId, ActorId, EntityType, EntityId, Action, DiffJson, OccurredAt |

Constraints: unique `(ImportSessionId, ProviderItemId)`; unique `(CampaignPostId, EventMediaId)`; unique `(CampaignPostId, Position)`; unique `(CampaignPostId, Platform)`; no cross-tenant media references. Preserve the relationship between uploaded files, source reference and selected campaign media. Use a content hash for exact duplicate detection within tenant boundary and perceptual similarity for review suggestions.

## 6. Google Photos and external media requirements

### Official Google Photos Picker
- Use OAuth 2.0 user authorization and the Picker API's appropriate read-only scope; do not use service accounts.
- Backend creates picker session and provides picker URL; frontend opens authorized selection UI.
- Poll for completion using documented cadence, retrieve selected media metadata, and promptly transfer media while access is valid.
- Treat temporary URLs as ephemeral; never persist them as permanent asset locations or expose them in logs.
- Check current official Google API documentation at implementation time for session lifecycle, quotas, media formats, and allowed retention/use.
- Google Photos Library API must not be used to enumerate a user's entire personal library. Its broad library-read and shared-album capabilities were restricted in March 2025.

### Shared album links
- Accept only recognized Google Photos share-link hosts and HTTPS, normalize and store reference; **do not scrape** HTML or bypass authentication.
- Show `AwaitingSelection` rather than `Imported` until selected media actually arrives.
- If the recipient cannot select the shared photos in Picker, explain and offer direct photographer upload or authorized Drive transfer.

### Google Drive (phase 2)
- Use Drive Picker/OAuth and least-privilege scopes, handle resource keys, pagination, permissions, quotas and shortcut/folder edge cases.
- Never assume an anonymous folder link grants backend access; do not fetch arbitrary URLs.

### Direct upload and photographer portal
- Create upload sessions with max file count, max bytes, allowed MIME types, expiration, per-event scope and abuse protection.
- Issue short-lived presigned R2 PUT URLs. Confirm completion server-side by verifying actual object metadata/content; do not trust browser-provided MIME alone.
- Validate file signatures, decodeability, dimensions, pixel limits and EXIF handling. Strip GPS metadata from derivatives; preserve originals only under explicit policy.
- Photographer invitations use high-entropy tokens stored hashed, short TTL, rate limits and revocation. Scope to upload-only permissions.

## 7. Media processing pipeline

1. Import item discovered or upload finalized.
2. Verify authorization, type, size, checksum, decoder safety and tenant/event mapping.
3. Stream original to private R2 storage; store canonical MediaAsset and EventMedia references.
4. Generate display thumbnails/derivatives, normalize orientation, strip sensitive metadata from derivatives.
5. Calculate SHA-256 and perceptual hash; group near-duplicates for review.
6. Run vision analysis with structured outputs: scene description, event-moment category, composition, clarity, subject prominence, potential quality/privacy flags. Do not identify unknown people by name or infer sensitive traits.
7. Store analysis version and model metadata. Support reanalysis after model updates.
8. Mark event media batch ready when all items are terminal (completed/failed), with partial-success notice.

Example events: `MediaImportRequested`, `MediaImported`, `MediaAnalysisRequested`, `MediaAnalysisCompleted`, `ImportBatchCompleted`, `CampaignGenerationRequested`, `CampaignGenerated`. Consumers must be idempotent; transient failures retried with bounded backoff; permanent errors stored per item. Apply queue and AI concurrency limits.

## 8. AI creative brief and rule hierarchy

User controls:
- Objective: awareness, engagement, impact, recognition.
- Mode: single or campaign.
- Platforms: Instagram, Facebook.
- Duration, posts/week, timezone and start date.
- Tone: storytelling, professional, friendly, energetic, etc.
- Focus tags: impact, participants, speakers, partners, networking, activities, takeaways.
- Avoid tags: repetitive captions, duplicate photos, generic phrases, excessive emojis, long captions, unverified claims.
- Freeform creative brief and additional instructions.
- Reusable templates and organization-level brand guidelines.

Precedence: system safety/platform restrictions > verified event facts > campaign-specific direction > organization brand preferences > per-post refinements for that post where compatible. Post-level edits may override stylistic campaign settings but not facts or platform constraints. Treat event notes, uploaded documents and OCR/vision output as untrusted data, never executable instructions.

Ask up to 1–3 clarifying questions only when important facts are missing; allow Skip. Do not invent quotations, sponsors, attendance numbers, participant outcomes or speaker identities. A claimed impact should be framed as aspiration/opportunity unless supported by verified evidence. Label unverified draft statements for review.

## 9. AI generation stages and schemas

**Stage A — Analyze:** Batch image understanding, metadata, quality, duplicates, moment clustering. Store per-media analysis.

**Stage B — Plan:** Produce structured themes, narrative progression, proposed posting slots and media allocations. For 14 days at 3/week, create 6 posts, spread across the two weeks. Sample themes: Event Recap, Speaker Insights, Participant Connections, Learning in Action, Value/Impact, Closing Reflection.

**Stage C — Select media:** Score relevance, quality, storytelling and variety. Enforce photo uniqueness by default across campaign, with explicit exception policy; verify media IDs exist in the event and belong to tenant. Suggest covers and selection reasons. If insufficient distinct photos, reduce photo counts or request approval for reuse rather than fabricate media.

**Stage D — Write:** Generate distinct Instagram and Facebook captions, hashtags, CTAs and posting instructions per post; include factual provenance markers where meaningful. Avoid repetitive hooks and unsupported claims.

**Stage E — Validate:** JSON schema, tenant/media IDs, count/cadence, date/time zone, duplicate media assignments, character/platform constraints (check live documentation), forbidden claims, style consistency, narrative diversity and safe content. Flag unresolved issues and require review.

**Stage F — Refinement:** Accept natural-language requests at campaign/post/platform scope; generate a typed patch/diff with affected entities, preserve existing approved/manual edits by default; user previews and applies/rejects. Maintain version history and invalidate affected approvals.

Example response contract:
```json
{
  "campaignId": "uuid",
  "status": "NeedsReview",
  "summary": "Six-post awareness campaign over 14 days",
  "warnings": ["Confirm speaker names and outcomes"],
  "posts": [{
    "id": "uuid",
    "sequence": 1,
    "dayOffset": 0,
    "theme": "Event Recap",
    "narrativePurpose": "Introduce the event",
    "media": [{"eventMediaId":"uuid","position":1,"isCover":true,"reason":"Strong event atmosphere"}],
    "platformContent": {
      "instagram": {"caption":"...","hashtags":["#Leadership"],"format":"carousel","instructions":["Use hero image first"]},
      "facebook": {"caption":"...","hashtags":[],"format":"multi_photo","instructions":["Add event context"]}
    },
    "scheduledAt": "2026-12-01T18:00:00+01:00",
    "status": "Draft"
  }]
}
```
Use typed DTOs and strict JSON-schema validation. Reject invalid media IDs and repair model outputs using bounded retry; never trust generated IDs or claims. Generate using structured inputs, not freeform concatenated prompts alone. Track model/provider, token/cost and retries per job.

## 10. API contracts (v1)

Auth and tenant context on all protected endpoints. Use ProblemDetails with stable error codes; pagination, idempotency keys on job creation, optimistic concurrency for editing.

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/organizations` | Create organization |
| GET/PUT | `/api/v1/organizations/{id}/brand-profile` | Read/update brand profile |
| POST/GET | `/api/v1/events` | Create/list events |
| GET/PATCH | `/api/v1/events/{id}` | Read/update event |
| POST | `/api/v1/events/{id}/media-sources` | Add provider/source reference |
| POST | `/api/v1/events/{id}/upload-sessions` | Start direct upload |
| POST | `/api/v1/upload-sessions/{id}/complete` | Verify uploaded objects and queue processing |
| POST | `/api/v1/events/{id}/photographer-invitations` | Generate scoped invitation |
| POST | `/api/v1/events/{id}/imports/google-photos/picker-sessions` | Create Picker session |
| GET | `/api/v1/imports/{id}` | Progress, counts, errors |
| POST | `/api/v1/imports/{id}/sync-selection` | Trigger selection retrieval after Picker completion |
| GET | `/api/v1/events/{id}/media` | Filter/paginate event gallery |
| PATCH | `/api/v1/events/{id}/media/{mediaId}` | Exclude, tags, manual flags |
| POST | `/api/v1/events/{id}/campaigns` | Create campaign configuration |
| POST | `/api/v1/campaigns/{id}/clarifications` | Answer/skip questions |
| POST | `/api/v1/campaigns/{id}/generate` | Enqueue AI generation |
| GET | `/api/v1/generation-jobs/{id}` | Progress/results/errors |
| GET | `/api/v1/campaigns/{id}` | Campaign with posts and media |
| PATCH | `/api/v1/campaign-posts/{id}` | Edit theme/schedule/status |
| PUT | `/api/v1/campaign-posts/{id}/media` | Replace/reorder media |
| PATCH | `/api/v1/campaign-posts/{id}/platforms/{platform}` | Edit caption/instructions |
| POST | `/api/v1/campaigns/{id}/refinements` | Propose AI edit |
| POST | `/api/v1/refinements/{id}/apply` | Apply reviewed diff |
| POST | `/api/v1/campaign-posts/{id}/approvals` | Approve/reject |
| GET | `/api/v1/calendar` | Calendar feed |
| POST | `/api/v1/campaigns/{id}/export` | Create manual-publishing package |
| POST | `/api/v1/publications/{id}/mark-published` | Record manually published post |

Response statuses: 201 for created resources, 202 for asynchronous jobs, 409 for version conflicts, 422 for validation/unsupported source, 429 for quota/rate limits. Do not expose OAuth tokens or raw storage keys to clients. Frontend polls or subscribes to job updates (SSE optional).

## 11. Publishing model

`CampaignPost` represents one editorial idea. `PostPlatformContent` stores platform-specific captions, media format and instructions. `Publication` records the actual destination and status. Approval may be per platform. MVP: plan and export only; optionally let user mark published manually. Phase 2: connect Instagram/Facebook accounts via official Meta APIs, check eligibility/permissions, validate per-platform formats and limits, queue publishing, retry safely, store external IDs, handle token expiry and API errors. Never claim a post was published based only on local schedule.

## 12. Security, privacy and tenancy

- Tenant isolation at every API, query, job and storage lookup; authorization must be server-side.
- RBAC: Owner/Admin manage organization and integrations; Editor creates/edits; Reviewer approves; Viewer reads.
- OAuth tokens encrypted at rest, scoped minimally, rotated/revoked; no tokens, temporary provider URLs or presigned links in logs.
- Private R2, signed access URLs, rate limits, file validation, decompression/pixel safeguards, malware scanning as appropriate.
- Photographer upload tokens high entropy, hashed, expiring, revocable, event-scoped; no read access by default.
- Retention policies, delete/export controls, remove media derivatives/analysis when deleting source media per policy; account for backups and legal obligations.
- Consent/rights acknowledgement before import and publishing; user review for photos of children or other sensitive contexts.
- Prompt-injection defenses: treat captions in images, metadata and uploaded notes as data; enforce structured output and fact verification.
- Audit content changes, approvals, source connections and publication actions.
- Comply with current Google Photos API and user-data policies, including limits on storage/use; review documentation before launch.

## 13. Nonfunctional requirements

- Responsive desktop/mobile UI, keyboard-accessible interactions, meaningful empty/error/loading states.
- Handle at least 200 photos per event in MVP; process incrementally with progress and partial failures. Configurable quotas and per-tenant usage caps.
- API requests for long-running work return 202 quickly; work runs asynchronously. No fixed latency promises for AI processing.
- Idempotent uploads/imports/generation requests; transactional outbox for reliable event publication.
- Structured tracing across API, worker and AI calls; dashboards for queue depth, import success, job latency, AI cost and provider errors.
- Cost guardrails: downsample for analysis where permitted, reuse analysis, batch requests, cap generation retries, estimate cost before generation.
- Timezone-aware scheduling and daylight-saving-safe storage. Display local times explicitly.

## 14. Testing and acceptance criteria

### Unit
- Campaign post count/cadence for 14 days and 3/week = 6.
- Media selection never references media from another event/tenant.
- Unique media allocation across posts by default; no near-duplicate covers without override.
- Creative brief and brand preferences applied with correct precedence.
- Caption edits are not silently overwritten by regeneration.
- Applying a refinement invalidates affected approvals and preserves unaffected posts.
- Schedule conversion handles timezone and DST.

### Integration
- OAuth/Picker mocked contract tests, session expiry, quota errors, selection cancellation and partial imports.
- Shared Google Photos link creates reference only; no scraping/download attempts.
- Direct upload verifies object exists, MIME/content signature, size and checksum.
- Import retries do not duplicate assets or job effects.
- Authorization tests for cross-tenant IDs, reviewer/editor permissions and photographer token scope.
- AI invalid JSON/unknown media IDs/unverified fact output handled safely.

### E2E
1. Create event → upload 60 photos → analyze → configure 14-day campaign, Instagram + Facebook, awareness, 3/week → generate six posts.
2. Confirm distinct post themes, selected photo order/reasons, platform-specific captions and instructions.
3. Edit Day 3 Instagram caption, replace cover, approve; request campaign refinement; confirm existing edits remain unless user accepts replacement.
4. Move post in calendar; confirm saved schedule and local timezone.
5. Export approved content and mark manually published; never show false auto-publication.
6. Paste photographer shared Google Photos URL; see accurate assisted-import UX and fallback upload invitation.
7. Simulate failed/expired Picker session; user sees recoverable error and restart option.

### Definition of done
- All MVP journeys operational with real storage, persistence, job processing and an AI provider (no fake completion states).
- Automated tests run in CI; database migrations committed; local Docker Compose setup works.
- Environment sample and secrets setup documented; OAuth configuration and provider limitations documented.
- API OpenAPI spec and frontend type client generated/maintained.
- Logs/metrics, health checks, audit history and error states implemented.
- Developer walkthrough demonstrating two-week campaign with media IDs, edits, approvals and export.

## 15. Implementation phases and agent execution order

**Phase 0 — Foundation:** Bootstrap monorepo, Docker Compose PostgreSQL/RabbitMQ, auth, tenant RBAC, CI, migrations, logging, environment configuration. Document architecture decisions.

**Phase 1 — Events and media:** Event CRUD, direct upload, private R2 integration, media gallery, thumbnails, validation, photographer invitations, import progress and deduplication.

**Phase 2 — AI analysis:** Background processing, provider-neutral vision interface, analysis persistence, grouping and quality flags. Test with fixture galleries.

**Phase 3 — Campaign creation:** Brand profile, creative brief, campaign config, clarification flow, AI planning/media selection/caption generation, validation, six-post campaign example.

**Phase 4 — Editorial UI:** Photo ordering/replacement, platform-specific caption editing, AI refinements with diffs, approvals, calendar, manual export, history.

**Phase 5 — Google Photos:** OAuth, official Picker integration, selection ingestion, shared-link reference UX, recovery and fallback. May be implemented in parallel with Phase 3 if a separate developer works on providers.

**Phase 6 — Hardening:** Security review, tenant isolation, rate limits, provider failure tests, AI cost limits, observability, accessibility, performance, end-to-end testing and deployment docs.

## 16. Agent operating instructions

1. Inspect repository before coding; reuse existing conventions and packages where appropriate. If empty, scaffold the architecture above.
2. Write a short implementation plan and ADRs for queue choice, auth, storage and AI provider.
3. Implement vertical slices end-to-end; do not create empty placeholder routes or fake success states.
4. For unavailable external credentials, build provider interfaces, local mocks and explicit `NotConfigured` states; document the exact setup required.
5. Use real migrations, typed DTOs, validation, idempotency, tests and accessible UI for each slice.
6. After each phase, run tests, fix failures, and summarize completed functionality, outstanding blockers and next steps.
7. Do not add unsupported Google Photos scraping or arbitrary-link downloading.
8. Do not implement automatic social publishing in MVP unless explicitly requested.
9. Never manufacture event facts; use confirmed metadata and flag missing information.
10. Prefer simple maintainable solutions over unnecessary distributed services or agent orchestration frameworks.

## 17. References to verify during implementation

- Google Photos Picker API: https://developers.google.com/photos/picker/guides/get-started
- Google Photos API updates: https://developers.google.com/photos/support/updates
- Google Drive API: https://developers.google.com/workspace/drive/api/guides/about-sdk
- Cloudflare R2: https://developers.cloudflare.com/r2/
- Meta developer platform: https://developers.facebook.com/docs/

**Important:** External API permissions, quotas, formats and developer policies change. Re-check official docs at implementation time rather than relying on hardcoded assumptions in this specification.
