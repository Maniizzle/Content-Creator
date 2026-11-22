# ADR 0003 — Media storage on Cloudflare R2 (S3-compatible), MinIO locally

- Status: Accepted (implemented in Phase 1)
- Date: 2026-11-22

## Decision

- Store originals and derivatives in **private** R2 buckets, accessed through an S3-compatible client behind
  an `IObjectStorage` port. Use MinIO in Docker Compose for local development.
- Browsers upload directly with **short-lived presigned PUT URLs** that are scoped to one object key, with a
  content-length limit. The server then verifies each object:
  - its size and SHA-256;
  - its file signature (magic bytes, not the browser-declared MIME type);
  - that it decodes safely, within pixel limits.
- Object keys are generated server-side: `{orgId}/{eventId}/{assetId}/{variant}`. Clients never see raw
  storage keys. Downloads use short-lived presigned GET URLs.
- Presigned URLs, provider temporary URLs and credentials never appear in logs, audit diffs or error responses.
- GPS and other sensitive EXIF data are stripped from derivatives. Originals are retained only under an explicit policy.

## Consequences

Large uploads bypass the API (its request body limit is 1 MB). Storage stays private, and access is
always mediated by a tenant-checked API call.
