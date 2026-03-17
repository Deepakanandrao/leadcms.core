# Email Sequence Core Design

Date: 2026-03-12
Revised: 2026-03-17

---

## 1. Goal

This document defines the Phase 1 internal structure for email sequences in Core.

The Phase 1 design aims to:

- keep the sequence definition understandable and queryable,
- use relational tables for steps so template references are database-enforced,
- use JSONB only for bounded structured payloads within entities,
- keep sequence-wide execution behaviour explicit on the entity,
- fit the current Core patterns already used by campaigns, email logs, contacts, segments, and the legacy scheduled-email model,
- stay extensible for later automation and workflow growth.

---

## 2. Design Rules

### 2.1 The sequence entity carries explicit top-level execution attributes

Sequence-wide behaviour is represented by normal top-level entity attributes.

This keeps the entity easy to query, list, filter, inspect, and operate.

### 2.2 Steps are relational with JSONB for flexible sub-structures

Each step is a row in the `SequenceStep` table with a foreign key to `Sequence` and a foreign key to `EmailTemplate`.

Step-level configuration that is inherently structured and not independently queried (`Timing`) is stored as a JSONB field on the step row.

This gives us:

- database-enforced template references,
- easy "where is this template used?" queries,
- proper delete behaviour,
- straightforward data migration,
- retained flexibility for timing rules and future step-type-specific configuration.

### 2.3 The sequence is language-agnostic

Language is resolved through:

- who gets enrolled,
- which templates each step references,
- the existing template language support already present in Core.

The sequence itself does not define a language field.

### 2.4 Phase 1 optimizes for linear email journeys

Steps carry an explicit `Position` attribute that defines execution order within the sequence.

This gives us a clean and practical first implementation while leaving room for richer logic later (transitions, branching).

### 2.5 Runtime execution state remains relational

The sequence and its steps store the workflow definition.

Contact execution state, delivery state, and audit history are stored in runtime tables so they can be indexed, queried, and updated efficiently.

### 2.6 No explicit sequence versioning for now

If editing active sequences later requires upgrade behaviour for existing enrollments, that should be handled by explicit runtime logic, not by exposing a version model from day one.

### 2.7 Default behaviours are not configurable

Some behaviours are always-on defaults and do not need per-sequence toggles:

- **Stop on global unsubscribe** — if a contact is globally unsubscribed, the sequence always stops. This is not a setting.
- **Fail and exit on repeated step failure** — if a step fails and retries are exhausted, the enrollment exits with a failure reason. This is not a setting.

---

## 3. High-Level Model

The Phase 1 model contains:

### Definition entities

- `Sequence` — the main entity with lifecycle, execution flags, counters, and JSONB for enrollment and UTM configuration.
- `SequenceStep` — one row per step, ordered by `Position`, with FK to `Sequence` and FK to `EmailTemplate`. Step-specific timing is a JSONB field on this entity.

### Runtime entities

- `SequenceEnrollment` — one row per contact in a sequence.
- `SequenceDelivery` — one row per contact per step delivery.

This gives us a clean split:

- definition entities store what the sequence is and how it is structured,
- runtime tables store what happened to contacts inside it.

---

## 4. Sequence Entity

### 4.1 Identity and lifecycle

- `Id`
- `Name`
- `Description`
- `Status`
- `LastActivatedAt`
- `LastPausedAt`
- `ArchivedAt`

`Status` values:

- `Draft`
- `Active`
- `Paused`
- `Archived`

### 4.2 Execution attributes

These top-level attributes define execution behaviour across the whole sequence:

- `StopOnReply`
- `UseContactTimeZone`
- `TimeZone`

#### Attribute semantics

`StopOnReply` — supports the business requirement for personal outreach workflows. When enabled, a reply from the contact stops further sequence progression for that contact.

`UseContactTimeZone` — when `true`, step timing is resolved in each contact's local timezone. When `false`, the sequence-level `TimeZone` is used for all contacts.

`TimeZone` — fallback timezone offset in minutes when `UseContactTimeZone` is `false` or when a contact has no timezone set, following the same pattern already used by campaigns.

### 4.3 Summary counters

- `ActiveEnrollmentCount`
- `CompletedEnrollmentCount`
- `ExitedEnrollmentCount`
- `SentCount`
- `FailedCount`

These are operational summaries, not source of truth.

### 4.4 JSONB fields on Sequence

- `Enrollment` — optional
- `UtmParameters` — optional

These are the only JSONB fields on the sequence entity itself.

---

## 5. SequenceStep Entity

Each step is a separate row in the `SequenceStep` table.

### 5.1 Relational fields

- `Id` — primary key.
- `SequenceId` — FK to `Sequence`.
- `EmailTemplateId` — FK to `EmailTemplate`. Database-enforced reference.
- `Position` — integer that defines execution order within the sequence. Unique per sequence.
- `StepKey` — stable string identifier (e.g. `"welcome"`, `"getting-started"`), independent from position. Used by runtime state and delivery tracking to refer to a specific step reliably. Unique per sequence.
- `Type` — step type. Phase 1 supports `Email` only.
- `Title` — internal business/admin label for the step.

### 5.2 JSONB fields on SequenceStep

#### `Timing` — required

Defines when the step becomes eligible to send.

Fields:

- `delay` — object with `value` (int) and `unit` (`minutes`, `hours`, `days`).
- `sendAt` — optional local time such as `"10:00"`. When present, the send is aligned to the next occurrence of that local time after the delay elapses.
- `allowedWeekDays` — optional array for cases where a step should only send on specific weekdays.

### 5.3 Position-based timing rules

Timing interpretation depends on the step's position in the sequence:

- **Position 0 (first step):** the delay is relative to when the contact was enrolled. A delay of `0 minutes` means send immediately upon enrollment.
- **Position > 0 (subsequent steps):** the delay is relative to when the previous step was sent to this contact.

This makes timing unambiguous without requiring an explicit anchor field. The position already determines the reference point.

### 5.4 Immediate execution

A step with `delay: { "value": 0, "unit": "minutes" }` executes as soon as it becomes eligible:

- For position 0, this means immediately upon enrollment.
- For subsequent steps, this means immediately after the previous step is sent.

This is the standard way to model steps that should fire without any waiting period— for example, a welcome email that should go out the moment a contact is enrolled.

### 5.5 Why this split

Relational fields (`SequenceId`, `EmailTemplateId`, `Position`, `StepKey`, `Type`, `Title`):

- need FK enforcement,
- need uniqueness constraints,
- are queried, filtered, and joined frequently,
- benefit from standard EF Core tooling.

JSONB field (`Timing`):

- is structured but not independently queried or joined,
- may vary by step type in the future,
- benefits from schema flexibility without requiring migrations for every new option.

---

## 6. Timing Semantics

### 6.1 Delay

Object with `value` and `unit`. Units: `minutes`, `hours`, `days`.

The reference point for the delay is determined by the step's position (see section 5.3).

### 6.2 Send-at alignment

When `sendAt` is present (e.g. `"16:00"`), the send is aligned to the next occurrence of that local time after the delay elapses.

Example: for a first step (position 0) with `delay = 0 minutes`, `sendAt = 16:00`:

- if the contact is enrolled before 16:00 local time, send at 16:00 on the same day,
- if the contact is enrolled at or after 16:00 local time, send at 16:00 on the next day.

### 6.3 Timezone resolution

The sequence-level `UseContactTimeZone` flag determines how local times are resolved:

- When `true`, the contact's timezone is used. If the contact has no timezone, `TimeZone` on the sequence is used as fallback.
- When `false`, the sequence-level `TimeZone` is used for all contacts.

There is no per-step timezone override.

---

## 7. Enrollment JSONB Structure

`Enrollment` is an optional JSONB field on the `Sequence` entity for simple built-in entry rules.

Its role is to make the sequence reasonably self-contained for common entry scenarios without turning the sequence itself into the full automation engine.

### 7.1 Shape

- `modes` — array of enabled enrollment modes. Supported values: `"manual"`, `"api"`, `"segment"`. A sequence can support multiple modes simultaneously, e.g. `["manual", "api"]` or `["manual", "api", "segment"]`.
- `includeSegmentIds` — array of segment IDs. Required when `"segment"` is in `modes`.
- `excludeSegmentIds` — array of segment IDs to exclude. Used with `"segment"` mode.
- `reentryPolicy` — `once_ever`, `allow_after_completion`, or `always`.

### 7.2 Mode combinations

A sequence can enable any combination of enrollment modes:

- `["manual"]` — contacts can only be enrolled manually by an operator.
- `["api"]` — contacts can only be enrolled via the API.
- `["manual", "api"]` — both manual and API enrollment are allowed.
- `["segment"]` — contacts are automatically enrolled from matching segments.
- `["manual", "api", "segment"]` — all three modes are active.

The `modes` array defines which enrollment paths are open. The `reentryPolicy` applies uniformly regardless of how the contact was enrolled.

### 7.3 Scope

`Enrollment` covers these built-in entry patterns:

- manual enrollment,
- API-driven enrollment,
- segment-based enrollment.

Richer automation triggers (website events, CRM events, conditional rules) should become a separate automation concept later that can target a sequence.

---

## 8. UtmParameters JSONB Structure

Following the existing campaign design, a sequence may define optional UTM overrides.

This remains a dedicated JSONB field and follows the same conceptual model already used by campaigns.

Purpose:

- provide default UTM context for sequence sends,
- avoid repeating the same UTM values on every step,
- allow send logic to derive step-specific details while still inheriting sequence-level campaign context.

---

## 9. Runtime Entities

### 9.1 SequenceEnrollment

One row per contact in a sequence.

Fields:

- `Id`
- `SequenceId` — FK to `Sequence`.
- `ContactId` — FK to `Contact`.
- `Status`
- `LastCompletedStepId` — references `SequenceStep.StepKey`.
- `EnteredAt`
- `CompletedAt`
- `ExitedAt`
- `ExitReason` — why the enrollment ended, e.g. `Completed`, `Failed`, `Unsubscribed`, `ReplyStopped`, `ManuallyRemoved`.
- `EnrollmentSource` — how the contact was enrolled: `Manual`, `Api`, `Segment`, `Migration`.
- `EnrollmentReason` — free-text or structured description of why the contact was enrolled. For manual enrollments this could be an operator note. For API enrollments this could be a trigger description. For segment enrollments this is populated automatically with the segment name or ID.

### 9.2 SequenceDelivery

One row per contact per step delivery.

Fields:

- `Id`
- `SequenceId` — FK to `Sequence`.
- `SequenceStepId` — FK to `SequenceStep`.
- `ContactId` — FK to `Contact`.
- `Status`
- `ScheduledAt`
- `SentAt`
- `SkipReason`
- `ErrorMessage`
- `EmailLogId` — optional FK to `EmailLog`.

### 9.3 Why runtime state stays relational

These records:

- change frequently,
- need indexes,
- need uniqueness rules,
- need efficient counting and filtering,
- fit the existing Core operational patterns much better than JSONB.

---

## 10. Examples

### 10.1 Example SequenceStep rows

For a simple onboarding sequence (id = 44):

| Id  | SequenceId | StepKey         | Position | Type  | Title                 | EmailTemplateId | Timing                                                    |
| --- | ---------- | --------------- | -------- | ----- | --------------------- | --------------- | --------------------------------------------------------- |
| 1   | 44         | welcome         | 0        | Email | Welcome Email         | 101             | `{"delay":{"value":0,"unit":"minutes"},"sendAt":"16:00"}` |
| 2   | 44         | getting-started | 1        | Email | Getting Started Guide | 102             | `{"delay":{"value":2,"unit":"days"},"sendAt":"10:00"}`    |
| 3   | 44         | case-study      | 2        | Email | Case Study            | 103             | `{"delay":{"value":5,"unit":"days"},"sendAt":"10:00"}`    |

Step 1 (welcome): sends at 16:00 on the day of enrollment (or next day if enrolled after 16:00). Delay is 0 from enrollment.

Step 2 (getting-started): sends 2 days after the welcome email was sent, aligned to 10:00.

Step 3 (case-study): sends 5 days after the getting-started email was sent, aligned to 10:00.

### 10.2 Example Enrollment

All three modes enabled with segment filtering:

```json
{
  "modes": ["manual", "api", "segment"],
  "includeSegmentIds": [12, 18],
  "excludeSegmentIds": [25],
  "reentryPolicy": "once_ever"
}
```

Manual and API only:

```json
{
  "modes": ["manual", "api"],
  "reentryPolicy": "allow_after_completion"
}
```

### 10.3 Example UtmParameters

```json
{
  "source": "leadcms",
  "medium": "email",
  "campaign": "trial_onboarding",
  "content": "sequence_default"
}
```

### 10.4 Example conceptual sequence shape

```json
{
  "id": 44,
  "name": "Trial Onboarding",
  "description": "Core onboarding sequence for new trial users.",
  "status": "Active",
  "stopOnReply": true,
  "useContactTimeZone": true,
  "timeZone": 0,
  "enrollment": {
    "modes": ["manual", "api", "segment"],
    "includeSegmentIds": [12],
    "reentryPolicy": "once_ever"
  },
  "utmParameters": {
    "source": "leadcms",
    "medium": "email",
    "campaign": "trial_onboarding"
  },
  "steps": [
    {
      "stepKey": "welcome",
      "position": 0,
      "type": "Email",
      "title": "Welcome Email",
      "emailTemplateId": 101,
      "timing": {
        "delay": { "value": 0, "unit": "minutes" }
      }
    },
    {
      "stepKey": "getting-started",
      "position": 1,
      "type": "Email",
      "title": "Getting Started Guide",
      "emailTemplateId": 102,
      "timing": {
        "delay": { "value": 2, "unit": "days" },
        "sendAt": "10:00"
      }
    }
  ]
}
```

---

## 11. Runtime State Boundary

JSONB fields carry bounded configuration only (`Enrollment`, `UtmParameters` on the sequence; `Timing` on each step).

Contact execution state lives in runtime tables so it can be updated, indexed, filtered, and counted efficiently.

Runtime state includes:

- current contact step,
- last sent timestamp per contact,
- retry counters per contact,
- contact exit reason,
- reply state per contact,
- recipient snapshots,
- delivery history.

None of that belongs on definition entities.

---

## 12. Phase 1 Shape Summary

### Definition

**Sequence** — top-level scalar metadata, execution attributes (`StopOnReply`, `UseContactTimeZone`, `TimeZone`), counters, optional `Enrollment` JSONB, optional `UtmParameters` JSONB.

**SequenceStep** — relational row per step with FK to `Sequence`, FK to `EmailTemplate`, `Position`, `StepKey`, `Type`, `Title`, plus JSONB for `Timing`.

### Runtime

**SequenceEnrollment** — one row per contact enrollment, with `EnrollmentSource`, `EnrollmentReason`, and `ExitReason`.

**SequenceDelivery** — one row per contact per step delivery, with FK to `SequenceStep` and optional FK to `EmailLog`.

---

## 13. Migration from Legacy Model

The current legacy drip-email implementation uses `EmailGroup`, `EmailSchedule`, and `ContactEmailSchedule`. This must be migrated to the new sequence model via a database-level migration script.

### 13.1 Legacy model mapping

| Legacy entity                                | Legacy role                                                                  | New entity                                                    |
| -------------------------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------------------- |
| `EmailGroup`                                 | Groups related email templates in send order                                 | `Sequence`                                                    |
| `EmailTemplate` (ordered by ID within group) | Individual emails in the drip                                                | `SequenceStep` (one per template, ordered by position)        |
| `EmailSchedule`                              | Defines the send cadence for a group (JSON: cron, day/time, immediate+delay) | `SequenceStep.Timing` (converted from legacy schedule format) |
| `ContactEmailSchedule`                       | Tracks per-contact progress through the group                                | `SequenceEnrollment`                                          |
| `EmailLog` (with `ScheduleId`)               | Delivery history                                                             | `SequenceDelivery` + existing `EmailLog`                      |

### 13.2 Migration script responsibilities

The migration must:

1. **Create `Sequence` rows** — one per `EmailGroup` that has an associated `EmailSchedule`. Copy `Name`, `Language` (as description context). Set `Status = Active` for groups that have pending `ContactEmailSchedule` rows, `Status = Paused` otherwise.

2. **Create `SequenceStep` rows** — for each `EmailGroup`, query its `EmailTemplate` rows ordered by `Id`. Create one `SequenceStep` per template with:
   - `Position` = 0-based index in the ordered template list,
   - `StepKey` = slugified template name or auto-generated stable key,
   - `EmailTemplateId` = existing template ID (FK preserved),
   - `Timing` = converted from the `EmailSchedule.Schedule` JSON. Map legacy `{"Day": "5,14", "Time": "14.00"}` to appropriate delays between steps. Map `{"Immediately": "true", "Delay": "15"}` to `{"delay": {"value": 15, "unit": "minutes"}}`. Map cron expressions to equivalent day/time delays.

3. **Create `SequenceEnrollment` rows** — one per `ContactEmailSchedule`:
   - `Status` mapped from `ScheduleStatus` (`Pending` → `Active`, `Completed` → `Completed`, `Failed` → `Exited`, `Unsubscribed` → `Exited`),
   - `ExitReason` = `Failed` or `Unsubscribed` as appropriate,
   - `EnrollmentSource` = `Migration`,
   - `EnrollmentReason` = `"Migrated from legacy EmailGroup: {GroupName}"`,
   - `LastCompletedStepId` = determined by counting sent `EmailLog` entries for this contact and schedule.

4. **Create `SequenceDelivery` rows** — one per `EmailLog` entry that references a `ScheduleId`, linking to the appropriate `SequenceStep` based on template ID matching.

5. **Preserve legacy tables** — do not drop `EmailGroup`, `EmailSchedule`, or `ContactEmailSchedule` in the migration. Mark them as deprecated. They can be removed in a later release after verification.

### 13.3 Migration considerations

- The legacy schedule format is flexible (cron, day-based, immediate). The migration script must handle each format and produce the closest equivalent `Timing` structure.
- Legacy groups that have no `EmailSchedule` (just template grouping) should not be migrated to sequences.
- The migration should be idempotent — running it twice must not create duplicate sequences.

---

## 14. API Design

The sequence API follows the same patterns established by `CampaignsController` and other Core controllers.

### 14.1 Sequences Controller

`SequencesController : BaseController<Sequence, SequenceCreateDto, SequenceUpdateDto, SequenceDetailsDto>`

Route: `api/sequences`

Inherits standard CRUD from `BaseController`: `GET`, `GET {id}`, `POST`, `PATCH {id}`, `DELETE {id}`, `DELETE` (batch), `GET export`, `GET sync`.

Additional action endpoints:

| Method | Route             | Purpose                                                                                                                   |
| ------ | ----------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `POST` | `{id}/activate`   | Transition from `Draft` or `Paused` to `Active`. Validates that the sequence has at least one step with a valid template. |
| `POST` | `{id}/pause`      | Transition from `Active` to `Paused`. Stops scheduling new deliveries. In-flight deliveries are allowed to complete.      |
| `POST` | `{id}/archive`    | Transition to `Archived`. Exits all active enrollments with reason `Archived`.                                            |
| `GET`  | `{id}/statistics` | Returns summary statistics (enrollment counts, delivery counts, failure rates).                                           |

### 14.2 Sequence Steps Controller

`SequenceStepsController`

Route: `api/sequences/{sequenceId}/steps`

Steps are managed as a sub-resource of a sequence.

| Method   | Route      | Purpose                                                                             |
| -------- | ---------- | ----------------------------------------------------------------------------------- |
| `GET`    |            | List all steps for the sequence, ordered by position.                               |
| `GET`    | `{stepId}` | Get a single step.                                                                  |
| `POST`   |            | Add a new step. Automatically assigns the next position unless explicitly provided. |
| `PATCH`  | `{stepId}` | Update step fields (title, template, timing).                                       |
| `DELETE` | `{stepId}` | Remove a step. Reorders remaining step positions.                                   |
| `POST`   | `reorder`  | Accepts an ordered array of step IDs to redefine positions.                         |

Editing steps on an `Active` sequence is allowed but restricted: the API must validate that changes do not break in-progress enrollments (e.g. removing a step that contacts are currently waiting on).

### 14.3 Sequence Enrollments Controller

`SequenceEnrollmentsController`

Route: `api/sequences/{sequenceId}/enrollments`

| Method   | Route            | Purpose                                                                                                                                                                                        |
| -------- | ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET`    |                  | List enrollments for the sequence with filtering by status.                                                                                                                                    |
| `GET`    | `{enrollmentId}` | Get a single enrollment with its delivery history.                                                                                                                                             |
| `POST`   |                  | Manually enroll a contact or batch of contacts. Requires `"manual"` in the sequence's `enrollment.modes`. Body includes `contactId` (or array of contact IDs) and optional `enrollmentReason`. |
| `DELETE` | `{enrollmentId}` | Remove a contact from the sequence. Sets `ExitReason = ManuallyRemoved`.                                                                                                                       |

API-driven enrollment (`"api"` mode) uses the same `POST` endpoint but is distinguished by the caller context (API key vs. admin session). The `EnrollmentSource` is set accordingly.

### 14.4 Editing active sequences

When a sequence is `Active`:

- Adding a new step: allowed. New step only applies to future enrollments and contacts who have not yet passed that position.
- Removing a step: allowed only if no contacts are currently waiting on that step. The API returns an error otherwise.
- Reordering steps: not allowed while the sequence is active. The sequence must be paused first.
- Changing a step's template or timing: allowed. Changes apply to future deliveries only; already-scheduled deliveries are not affected.

---

## 15. Runtime Background Execution

Sequence execution is driven by a background task, following the same `BaseTask` pattern used by `CampaignSendTask` and `ContactScheduledEmailTask`.

### 15.1 SequenceSendTask

A new `SequenceSendTask` registered as an `ITask` implementation, configured in `appsettings.json` with its own polling interval.

Each execution cycle performs these steps:

#### Step 1: Process segment-based enrollments

For each active sequence that has `"segment"` in its `enrollment.modes`:

- Resolve the audience from `includeSegmentIds` minus `excludeSegmentIds`.
- For each contact in the resolved audience that is not already enrolled (respecting `reentryPolicy`), create a new `SequenceEnrollment` with `EnrollmentSource = Segment`.

#### Step 2: Schedule next deliveries

For each active `SequenceEnrollment`:

- Determine the next step based on `LastCompletedStepId` and the step sequence (by `Position`).
- If no `SequenceDelivery` exists for that step and contact, calculate `ScheduledAt` using the step's `Timing`:
  - For position 0: base time = `EnteredAt`.
  - For position > 0: base time = `SentAt` of the previous step's delivery.
  - Add the delay.
  - If `sendAt` is specified, align to the next occurrence of that local time.
  - If `allowedWeekDays` is specified, advance to the next allowed day.
  - Resolve timezone using the sequence's `UseContactTimeZone` and `TimeZone` settings.
- Create a `SequenceDelivery` row with `Status = Scheduled`.

#### Step 3: Send eligible deliveries

For each `SequenceDelivery` with `Status = Scheduled` and `ScheduledAt <= DateTime.UtcNow`:

- Check global unsubscribe — if the contact is unsubscribed, skip the delivery and exit the enrollment with `ExitReason = Unsubscribed`.
- Check `StopOnReply` — if enabled on the sequence and the contact has replied (tracked via `EmailLog`), skip and exit with `ExitReason = ReplyStopped`.
- Send the email using `IEmailFromTemplateService`, passing the step's `EmailTemplateId`.
- On success: update `SequenceDelivery.Status = Sent`, `SentAt = DateTime.UtcNow`, link `EmailLogId`. Update `SequenceEnrollment.LastCompletedStepId`. Increment sequence counters.
- On failure: update `SequenceDelivery.Status = Failed`, record `ErrorMessage`. The task will retry on the next cycle up to a configured retry limit. If retries are exhausted, exit the enrollment with `ExitReason = Failed`.

#### Step 4: Complete enrollments

For each active enrollment where all steps have been delivered (`LastCompletedStepId` equals the last step's `StepKey`):

- Set `SequenceEnrollment.Status = Completed`, `CompletedAt = DateTime.UtcNow`.
- Increment `Sequence.CompletedEnrollmentCount`.

#### Step 5: Update sequence counters

Update the summary counters on the `Sequence` entity (`ActiveEnrollmentCount`, `CompletedEnrollmentCount`, `ExitedEnrollmentCount`, `SentCount`, `FailedCount`).

### 15.2 Task configuration

```json
{
  "Tasks": {
    "SequenceSendTask": {
      "IntervalSeconds": 60,
      "Enabled": true
    }
  }
}
```

### 15.3 Concurrency and idempotency

- The task must be safe to run concurrently across multiple instances. Use the existing `ILockService` to acquire a lock before processing.
- Delivery scheduling is idempotent: if a `SequenceDelivery` already exists for a step and contact, the task does not create a duplicate.
- Enrollment from segments is idempotent: if a contact is already enrolled, the task respects the `reentryPolicy`.

---

## 16. Key Decisions

- Steps are relational rows, not embedded in JSONB. This gives us FK enforcement on template references, standard migration support, and easy reporting queries.
- JSONB is used within `SequenceStep` for timing configuration only, keeping that structure flexible for future step types without requiring schema migrations for every new option.
- Sequence-wide execution controls (`StopOnReply`, `UseContactTimeZone`, `TimeZone`) are explicit top-level attributes.
- Default behaviours (stop on unsubscribe, fail-and-exit on repeated failure) are always-on and not configurable per sequence.
- Timing reference point is implicit from step position (position 0 = from enrollment, position > 0 = from previous step). No explicit anchor field.
- Timezone is resolved at the sequence level only, not per step.
- Enrollment supports multiple modes simultaneously via a `modes` array.
- No explicit sequence versioning. Editing behaviour for active sequences is handled by API-level restrictions.
- Runtime state is fully relational.
- Legacy drip-email data (`EmailGroup`, `EmailSchedule`, `ContactEmailSchedule`) is migrated via a database-level script.

This gives us a practical first implementation that stays aligned with the current Core design and remains extensible later.
