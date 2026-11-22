# ADR 0004 — Provider-neutral AI layer (Anthropic, OpenAI, Gemini, Azure OpenAI)

- Status: Accepted (implemented in Phases 2–3)
- Date: 2026-11-22

## Decision

- Define `IVisionAnalyzer` and `IContentGenerator` ports in `Application`. Each takes **typed inputs** and
  returns **typed outputs** that are validated against a JSON schema.
- Ship adapters for **Anthropic Claude, OpenAI, Google Gemini and Azure OpenAI**, chosen per environment by
  configuration (`Ai:Provider`, plus model names per task). Adapters translate to each provider's
  structured-output or tool-calling mechanism.
- Cross-cutting behaviour lives in a decorator, not in each adapter:
  - retries with bounded exponential back-off for transient errors;
  - bounded concurrency;
  - timeouts;
  - per-job token and cost tracking;
  - schema validation with a bounded "repair" retry.
- A missing API key surfaces an explicit `NotConfigured` state in the UI and API. It never fakes a success.
- Generated IDs and claims are never trusted:
  - Media IDs must exist in the event and belong to the tenant.
  - Facts must trace back to verified `EventFact`s, or be flagged for review.
- Event notes, documents, image text and metadata are passed as clearly delimited **data**, never as
  instructions (prompt-injection defense).

## Consequences

Switching providers is a configuration change. Prompt templates and schemas live in one place. Each
provider needs its own contract tests (with recorded fixtures) to catch API drift.
