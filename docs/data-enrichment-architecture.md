# Data Enrichment – Architecture & Implementation Plan (LeadCMS)

## Problem recap
You want to support **data enrichment** for CRM entities (e.g., `Contact`, `Account`) that:

- Trigger when entities are inserted/updated
- Can update the original entity and/or create related entities
- Can be extended by third-party provider plugins (subscription model)
- Are resilient to provider errors and expose them to users for fixing/retrying
- Enforce request limits/quotas per provider/API key (daily/monthly, etc.)

The goal is **best-practice architecture first**, but with an explicit mapping onto the existing LeadCMS patterns.

---

## What LeadCMS already has that we can leverage
LeadCMS already contains several building blocks that are *very close* to a robust enrichment system:

- **Plugins**: `IPlugin.ConfigureServices(...)` + `PluginManager` that loads plugin assemblies and `pluginsettings.json`.
- **Durable “change stream”**: entities annotated with `[SupportsChangeLog]` generate entries in `change_log` during `PgDbContext.SaveChangesAsync`.
- **Batch processing**: `ChangeLogTask` provides a reliable, resumable way to consume `change_log` in batches.
- **Background work framework**: Quartz-driven `TaskRunner` executes registered `ITask` implementations with distributed locking.
- **Batch processing + retry**: `ChangeLogTask` processes changes per entity type, stores execution state in `change_log_task_log`, and retries failed batches.

These pieces form a solid foundation for **event-driven enrichment** in V1.

---

## Recommended high-level design (best practices)
### 1) Make enrichment **asynchronous** and **event-driven**
Do not call external providers inside the API request path that creates/updates entities.

Instead:

1. Request writes entity
2. `SaveChangesAsync` writes a `ChangeLog` row (durable event)
3. Enrichment workers consume that event stream and execute provider enrichments

This gives you:

- predictable latency for users
- retry + backoff for flaky providers
- controlled concurrency/rate limiting
- clearer auditing and failure visibility

### 2) Prefer **sequential enrichment** (provider-driven, dependency-aware)
Instead of designing explicit “pipelines” and “steps”, treat enrichment as a set of **independent provider enrichments** that may become applicable after other enrichments change the entity state.

Example for `Contact` (conceptually sequential, without hardcoded step chains):

- Provider A enriches the contact from email (e.g., name candidates, disposable checks).
- Provider B enriches company/account details once a usable domain/company identifier exists.
- Provider C enriches additional details once company identifiers become available.

The sequencing emerges naturally because each provider decides whether it should run based on the current persisted state.

Requirement: each provider run must be **idempotent**, so retries won’t corrupt data.

### 3) Add a durable “enrichment work item” layer (recommended)
While you *can* run directly from `ChangeLog` batches, best practice is to persist explicit work items:

- decouple “entity changed” from “enrichment to perform”
- allow scheduling decisions (delay, cooldown, quotas)
- allow cancellation and manual retries

This is essentially an internal queue.

### 3.1) Triggering is **event + provider predicate**
Enrichment should be triggered by:

- **Action**: `Created`, `Updated`, `Manual`, `ProviderEnabled` (administrative)
- **Condition**: a provider-owned predicate that checks the *current* entity state and decides whether enrichment is needed

This keeps the orchestration generic while allowing providers to encode “is enrichment still required?” logic.

Key consequences:

- The system can safely re-evaluate enrichment needs without hardcoding field logic in core.
- A provider can decide to enrich partially (only some fields) and later continue, without causing loops.
- The predicate must be deterministic and based on the current persisted record (not transient request DTOs).

### 4) Implement provider resilience patterns
Each provider call should be wrapped with:

- timeouts
- retries with exponential backoff for transient failures
- circuit breaker for repeated failures
- concurrency limiting (per provider)

In .NET, this is typically done via `IHttpClientFactory` + Polly policies.

### 5) Track data audit/lineage and conflicts
When enrichment updates a field, store:

- *which provider* wrote it
- *when* it was written
- *confidence* (optional)
- *raw response reference* (or hash)

Also define merge strategy:

- never overwrite user-entered values by default
- prefer “missing-only” updates unless explicitly enabled
- require explicit rules for conflict resolution

### 6) Rate limits/quotas are first-class
Model quotas explicitly:

- per provider
- per tenant/account (if multi-tenant is added later)
- per API key/credential
- per time window (day/month/hour)

Quotas should be enforced both:

- before making an external call
- and recorded after (including failed attempts, depending on provider billing)

### 7) Make failures visible and actionable
Users need to see:

- which records failed enrichment
- why (categorized errors)
- which provider failed
- retry options
- “blocked by quota” vs “auth invalid” vs “bad data”

This should be stored in DB (not only logs), and surfaced through API/UI.

---

## Provider configuration & runtime management (following LeadCMS Settings pattern)

### Design-time (code): provider registration
- **Core-owned contracts**: `IDataEnrichmentProvider`, orchestration services, and base abstractions live in core and are published for plugins to consume.
- **Plugin-provided implementations**: providers (shipped in plugins) implement `IDataEnrichmentProvider` and register via DI in `IPlugin.ConfigureServices`.
- Each provider exposes: `ProviderKey`, `DisplayName`, `SupportedEntityTypes`, `SupportedTriggers`, `GetConfigurationSchema()`, `ShouldEnrichAsync(...)`, `EnrichAsync(...)`.
- Installing a plugin auto-registers its providers; they become discoverable via the admin surface (API shape TBD).

### Runtime (data): database-driven configuration
- Use a Settings-like model: code declares providers; DB stores enabled state and config JSON.
- Admin surface: reuse the Settings-style controller pattern; exact API surface is TBD until the entity model is finalized.

### Proposed data model (V1)
Core DB tables (owned by core enrichment infrastructure; plugins may add provider-specific tables if required):
- `enrichment_provider_config`
  - `provider_key` (PK, text, unique)
  - `enabled` (bool)
  - `configuration` (jsonb) // provider-specific (API keys, endpoints, options)
  - `daily_quota`, `monthly_quota`, `hourly_quota` (int, nullable)
  - `min_call_interval_ms` (int, nullable) // enforced delay between calls
  - `max_concurrency` (int, nullable) // caps parallel executions per provider
  - `allow_parallel_calls` (bool) // whether provider permits concurrent calls
  - `last_config_change_at` (timestamp)

- `enrichment_work_item`
  - `id` (PK)
  - `provider_key` (FK to enrichment_provider_config.provider_key)
  - `entity_type` (text), `entity_id` (int)
  - `trigger` (text: Created/Updated/Manual/ProviderEnabled)
  - `status` (text: Pending/InProgress/Completed/Failed/Blocked/Cancelled)
  - `retry_count` (int)
  - `scheduled_at`, `executed_at` (timestamp)
  - Unique constraint: (`provider_key`, `entity_type`, `entity_id`, `status` in Pending/InProgress) to avoid duplicates

- `enrichment_provider_attempt`
  - `id` (PK)
  - `work_item_id` (FK to enrichment_work_item.id)
  - `provider_key`, `entity_type`, `entity_id`
  - `success` (bool), `error_category` (text), `error_message` (text), `duration_ms` (int)
  - `response_payload` (jsonb), `request_payload` (jsonb optional)
  - Index: (`provider_key`, `entity_type`, `entity_id`, `created_at` desc)

- `enrichment_audit`
  - `id` (PK)
  - `entity_type`, `entity_id`, `field_name`
  - `provider_key`
  - `old_value` (text), `new_value` (text), `confidence` (float, nullable)
  - `enriched_at` (timestamp)
  - Index: (`entity_type`, `entity_id`, `field_name`)

- `enrichment_quota_usage`
  - `id` (PK)
  - `provider_key`
  - `window_type` (text: Daily/Monthly), `window_start` (timestamp)
  - `usage_count` (int)
  - Unique: (`provider_key`, `window_type`, `window_start`)

Catch-up/backfill: intentionally no dedicated table in V1; a reconciliation/backfill mechanism can be added later without altering the primary model.

### Execution-time rules
- Scheduler reads `enrichment_provider_config` (enabled providers) and `change_log`; writes `enrichment_work_item`.
- Executor reads `enrichment_work_item`, re-checks `enabled`, enforces quotas (`enrichment_quota_usage`), logs `enrichment_provider_attempt`, writes `enrichment_audit` on successful field updates.
- Both tasks rely on DI-registered providers; if a provider is removed from code but rows exist, executor skips with a clear error.

---

## Execution strategy inside LeadCMS (mapping to existing code)
### Use `ChangeLog` as the “outbox”
LeadCMS already writes change events for `[SupportsChangeLog]` entities and persists them durably.

Recommended approach (V1):

- Implement **two separate tasks**:
  1) a **scheduler task** that turns `ChangeLog` into enrichment work items
  2) an **executor task** that runs provider enrichments for queued work items

Why this is strong:

- you get batch processing + retries via `change_log_task_log`
- you keep external calls off the request path
- the persisted work items become the durable execution backlog

### Two-task architecture (recommended)
**Task 1: Enrichment Scheduler (ChangeLog → work items)**

- Reads `ChangeLog` batches for supported entity types.
- For each change, evaluates provider subscriptions:
  - provider is enabled?
  - trigger matches (created/updated/manual)?
  - provider predicate says “enrichment required” *for the current persisted record*?
- Creates/updates a work item per (ProviderKey, TargetType, TargetId).

**Task 2: Enrichment Executor (work items → provider calls)**

- Pulls pending work items.
- Re-checks provider enabled state (handles provider being disabled after scheduling).
- Enforces quotas/concurrency.
- Executes provider enrichments and applies updates using safe merge policies.
- Records provider attempt outcomes and user-visible errors.

### Prevent infinite loops / repeated re-enrichment
Because enrichment updates entities (which produces more `ChangeLog` entries), you must add loop controls:

- idempotency keys per (ProviderKey, TargetType, TargetId, fingerprint)
- cooldown windows (don’t rerun same provider enrichment for the same record within N hours)
- only trigger when relevant fields changed (diff-based predicates)

Additionally, because providers own the “is enrichment required?” predicate, core should enforce:

- **No-op detection**: if a provider run produces no entity changes, record that and avoid immediately re-queuing.
- **Progress tracking**: providers should persist minimal state (e.g., what sub-operations were completed, timestamps) so the predicate can distinguish:
  - “needs enrichment”
  - “in progress / waiting for cooldown”
  - “done (until new relevant user edit occurs)”

This is what prevents circular calls when enrichment is partial.

### Provider enable/disable must include “catch up” behavior
Enabling/disabling providers at runtime introduces an extra requirement: the system must both:

1) **Catch up** existing records when a provider becomes enabled (or its config changes)
2) **Keep up** with new updates via the normal ChangeLog-driven scheduling

Recommended V1 mechanism (without adding a reconciliation table yet):

- When a provider is enabled (or config changes), trigger a one-time backfill via the scheduler that scans existing records in batches and evaluates the provider predicate.
- For each record where enrichment is required, create work items (same as if it came from ChangeLog).

Important details:

- Scans must be incremental/batched and resumable (store last processed ID cursor alongside task metadata or provider state).
- Scans must obey quotas (don’t enqueue more than can be executed reasonably).
- Disabling a provider should prevent new scheduling and cause executor to skip pending work items for that provider.

### Recommended “field policy”
Default safe policy:

- only fill missing values
- never overwrite user-entered fields unless the user opts-in
- store audit lineage for every update

---

## Error handling: making failures user-fixable
Design error categories (examples):

- `BadInput` (e.g., invalid TIN / invalid email)
- `AuthInvalid` (API key revoked)
- `RateLimited` (quota exhausted)
- `ProviderUnavailable` (5xx / timeout)
- `DataConflict` (would overwrite user value)

### Prevent retries when user action is required
Some errors should immediately transition the provider/run into a **blocked** state rather than being retried.

Recommended V1 rules:

- `AuthInvalid` / `AuthMissing` → **BlockedUntilConfigChange** (do not retry until admin updates credentials/config)
- `BadInput` → **BlockedUntilDataChange** (do not retry until record changes)
- `RateLimited` → **DelayedUntilWindowReset** (retry after next allowed time)
- `ProviderUnavailable` → normal retry/backoff (transient)

Implementation implication:

- Persist the last terminal failure reason per (ProviderKey, TargetType, TargetId).
- The scheduler predicate must treat blocked states as “not schedulable” until an unblocking event occurs (config changed, record changed, or time window reached).

Store these in persisted provider attempt/outcome records, then expose via API:

- list jobs by status (Failed/Blocked)
- show per-provider errors
- allow retry with “retry now” action
- allow “ignore error” / “disable provider” actions

---

## Rate limiting & quotas
Implement a `QuotaService` that supports:

- fixed window (hourly/daily/monthly)
- sliding window (optional)
- token bucket for smoother control (optional)

Enforcement must happen before any external call.

Also consider:

- concurrency limit per provider (avoid spikes)
- cooldown between calls per provider (respect `min_call_interval_ms`)
- caching by input fingerprint (avoid repeated calls for same domain/email)

---

## What else to consider (often missed)
- **PII & compliance**: consent, retention policy, data minimization, encryption at rest for raw provider payloads.
- **Secrets management**: store provider credentials in config/secret store; avoid storing raw keys in DB.
- **Observability**: structured logs + metrics (per provider: success rate, latency, quota usage, error codes).
- **Idempotency**: must be enforced at DB level (unique constraint on provider idempotency key).
- **Backfills**: ability to enrich existing contacts/accounts with a bulk job.
- **Explainability**: show “why this value changed” (audit table + UI).
- **Testing**: provider contract tests, fake provider for local/dev, replay of recorded responses.

---

## Quick Wins & Migration Strategy

### Immediate candidates for enrichment providers
LeadCMS already has external enrichment services that are perfect candidates for migration to the new architecture:

**1. EmailValidation → EmailEnrichmentProvider**
- Current: `IEmailValidationExternalService` / `EmailValidationExternalService`
- Enriches: `Contact` and `Domain` entities
- External dependency: Email verification API (configured via `EmailVerificationApiConfig`)
- Fields updated: `Domain.CatchAll`, disposable check, free email check
- Current flow: synchronous in `EmailVerifyService.Verify()` → called from `EmailController`
- Migration benefit: move off request path, add retry logic, track failures

**2. AccountDetails → AccountEnrichmentProvider**
- Current: `IAccountExternalService` / `AccountExternalService`
- Enriches: `Account` entities from domain information
- External dependency: Account details API (configured via `AccountDetailsApiConfig`)
- Fields updated: company name, employee range, revenue, social media, tags, location
- Current flow: used in `ContactService.EnrichWithAccountId()` and related enrichment methods
- Migration benefit: async enrichment, quota management, better error visibility

**3. Additional enrichment candidates**
- `IpDetailsService` → `IpEnrichmentProvider` (geolocation from IP)
- `DomainService.Verify()` → `DomainVerificationProvider` (DNS/MX record verification)
- `MxVerifyService` → `MailServerVerificationProvider` (SMTP verification)

### Plugin structure recommendation

**Core (LeadCMS core)** owns the enrichment infrastructure so all providers share one pipeline:
- Contracts and orchestration: `IDataEnrichmentProvider`, `IEnrichmentScheduler`, `IEnrichmentExecutor`, base provider classes.
- Tasks and services: `EnrichmentSchedulerTask` (extends `ChangeLogTask`), `EnrichmentExecutorTask` (extends `BaseTask`), `QuotaService`, audit + attempt logging, cooldown/idempotency enforcement.
- Data model: `EnrichmentProviderConfig`, `EnrichmentWorkItem`, `EnrichmentProviderAttempt`, `EnrichmentQuotaUsage`, `EnrichmentAudit` (core schema). Core exposes APIs/UI surfaces for provider enablement, status, and retries.
- Non-external enrichment that is synchronous/simple (e.g., calculated fields) and `SettingsEnrichmentService` stay in core.

**Plugin (e.g., `LeadCMS.Plugin.DataEnrichment`)** delivers concrete provider implementations only:
- `EmailValidationProvider` (migrated from core)
- `AccountDetailsProvider` (migrated from core)
- `IpDetailsProvider` (migrated from core)
- `DomainVerificationProvider` (migrated from core)
- `MailServerVerificationProvider` (migrated from core)

Plugins may optionally add provider-specific tables/config if a provider needs its own state, but they do **not** duplicate the core work-item/attempt/quota schema. Third parties follow the same pattern: implement `IDataEnrichmentProvider`, register via `IPlugin.ConfigureServices`, and rely on core orchestration.

**Why this split works:**
- Core guarantees a single, consistent queue/executor/observability path for all providers.
- Providers ship independently and can be enabled/disabled without altering core binaries.
- Deployment stays optional (install the plugin to get providers; core infra remains available for any plugin to use).

---

## Concrete LeadCMS integration points (where to implement)
- Trigger source: `PgDbContext.SaveChangesAsync` → `ChangeLog`.
- Worker scheduling: Quartz `TaskRunner` + `ITask` registration.
- Batch processing: extend `ChangeLogTask` pattern.
- Plugin extensibility: use `IPlugin.ConfigureServices` to register enrichment providers via the new plugin.
