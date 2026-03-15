# Email Sequence Core Design

Date: 2026-03-12

---

## 1. Goal

This document defines the Phase 1 internal structure for email sequences in Core.

The Phase 1 design aims to:

- keep the sequence definition self-contained,
- store the workflow itself in JSONB,
- keep sequence-wide execution behaviour explicit on the entity,
- fit the current Core patterns already used by campaigns, email logs, contacts, segments, and the legacy scheduled-email model,
- stay extensible for later automation and workflow growth.

---

## 2. Phase 1 Design Rules

Phase 1 sequences follow these rules.

### 2.1 The sequence entity carries explicit top-level execution attributes

Sequence-wide behaviour is represented by normal top-level entity attributes.

This keeps the entity easy to query, list, filter, inspect, and operate.

### 2.2 The workflow is stored directly on the sequence in JSONB

The current active workflow definition is stored in a required `Flow` JSONB field on the sequence.

This keeps the sequence self-contained and avoids splitting the step graph across multiple definition tables.

### 2.3 The sequence is language-agnostic

Language is resolved through:

- who gets enrolled,
- which templates each step references,
- the existing template language support already present in Core.

The sequence itself does not define a language field.

### 2.4 Phase 1 optimizes for linear email journeys

The Phase 1 workflow shape represents ordered multi-step email sequences.

This gives us a clean and practical first implementation while leaving room for richer logic later.

### 2.5 Runtime execution state remains relational

The sequence stores the workflow definition.

Contact execution state, delivery state, and audit history are stored in runtime tables so they can be indexed, queried, and updated efficiently.

---

## 3. Recommended High-Level Model

The Phase 1 model contains:

- one main `Sequence` entity,
- one required `Flow` JSONB field,
- one optional `Enrollment` JSONB field,
- one optional `UtmParameters` JSONB field,
- separate runtime entities for enrollments and deliveries.

This gives us a clean split:

- `Sequence` stores what the sequence is and how it is defined,
- runtime tables store what happened to contacts inside it.

---

## 4. Phase 1 Sequence Entity Shape

The `Sequence` entity should contain the following groups of attributes.

## 4.1 Identity and lifecycle

- `Id`
- `Name`
- `Description`
- `Status`
- `LastActivatedAt`
- `LastPausedAt`
- `ArchivedAt`

`Status` should support at least:

- `Draft`
- `Active`
- `Paused`
- `Archived`

## 4.2 Sequence-wide execution attributes

These top-level attributes define execution behaviour across the whole sequence:

- `ContinueOnStepFailure`
- `StopOnReply`
- `StopOnGlobalUnsubscribe`
- `UseContactTimeZoneByDefault`
- `TimeZone`

### Attribute semantics

`ContinueOnStepFailure`

- controls whether a failed step blocks further progression.

`StopOnReply`

- supports the business requirement for personal outreach workflows.

`StopOnGlobalUnsubscribe`

- expresses the global unsubscribe rule explicitly on the sequence.

`UseContactTimeZoneByDefault`

- allows steps to inherit contact-local-time behaviour without repeating the same flag everywhere.

`TimeZone`

- stores the fallback timezone offset in minutes when contact timezone is unavailable, following the same general pattern already used by campaigns.

## 4.3 Summary counters

Recommended top-level counters:

- `ActiveEnrollmentCount`
- `CompletedEnrollmentCount`
- `ExitedEnrollmentCount`
- `SentCount`
- `FailedCount`

These are operational summaries rather than source of truth.

## 4.4 JSONB attributes

Phase 1 uses these JSONB attributes:

- `Flow` required
- `Enrollment` optional
- `UtmParameters` optional

This is the intended JSONB surface for the first implementation.

---

## 5. `Flow` JSONB Structure

`Flow` stores the actual sequence workflow.

This is the main JSONB field and the primary self-contained definition of the journey.

## 5.1 Responsibilities of `Flow`

`Flow` contains:

- ordered step definitions,
- stable step identifiers,
- step-level template references,
- step-level timing rules,
- step-level live-edit behaviour.

## 5.2 Top-level `Flow` shape

Phase 1 `Flow` shape:

- `mode`
- `steps`

Phase 1 value for `mode`:

- `linear`

The `Flow` document stores the current workflow shape directly.

## 5.3 Step shape inside `Flow`

Each step contains:

- `id`
- `type`
- `title`
- `templateId`
- `timing`
- `liveEdit`
- optional `metadata`

### Step field semantics

`id`

- stable string identifier inside the sequence,
- independent from array index,
- used by runtime state and delivery tracking to refer to a specific step reliably.

`type`

- Phase 1 supports `email`.

`title`

- internal business/admin label for the step.

`templateId`

- references an existing email template.

`timing`

- defines when the step becomes eligible to send.

`liveEdit`

- defines how the step behaves when it is inserted or changed on an active sequence.

`metadata`

- optional editor-facing data that does not drive execution.

---

## 6. Step Timing Structure

Timing is step-specific and belongs inside each step.

Recommended structure:

- `anchor`
- `delay`
- optional `sendAt`
- optional `useContactTimeZone`
- optional `allowedWeekDays`

## 6.1 Timing semantics

`anchor`

- `enrollment`
- `previous_step`

`delay`

- object with `value` and `unit`
- units should initially support `minutes`, `hours`, `days`

`sendAt`

- optional local time such as `10:00`

`useContactTimeZone`

- optional step-level override
- if absent, the step inherits `UseContactTimeZoneByDefault` from the sequence

`allowedWeekDays`

- optional array for cases where a step should only send on specific weekdays

---

## 7. Step Live-Edit Structure

The implementation plan already identifies retroactive step behaviour as a key requirement.

Each step therefore contains a small `liveEdit` structure.

Recommended fields:

- `insertPolicy`

Recommended values:

- `new_enrollments_only`
- `include_in_progress`
- `include_all`

This keeps the critical retroactive rule attached directly to the step definition.

---

## 8. `Enrollment` JSONB Structure

`Enrollment` is an optional JSONB field for simple built-in sequence entry rules.

Its role in Phase 1 is to make the sequence reasonably self-contained for common entry scenarios without turning the sequence itself into the full automation engine.

## 8.1 `Enrollment` shape

Recommended fields:

- `mode`
- `includeSegmentIds`
- `excludeSegmentIds`
- `reentryPolicy`

Recommended values for `mode`:

- `manual`
- `api`
- `segment`

Recommended values for `reentryPolicy`:

- `once_ever`
- `allow_after_completion`
- `always`

## 8.2 Scope of `Enrollment` in Phase 1

`Enrollment` covers these built-in entry patterns:

- manual enrollment,
- API-driven enrollment,
- segment-based enrollment.

Richer automation triggers can target a sequence later as a separate concept.

---

## 9. `UtmParameters` JSONB Structure

Following the existing campaign design, a sequence may define optional UTM overrides.

This remains a dedicated JSONB field and follows the same conceptual model already used by campaigns.

Its purpose is to:

- provide default UTM context for sequence sends,
- avoid repeating the same UTM values on every step,
- allow send logic to derive step-specific details while still inheriting sequence-level campaign context.

---

## 10. JSONB Fields Used in Phase 1

Phase 1 uses these JSONB fields:

### 10.1 `Flow` required

Stores the ordered step workflow.

### 10.2 `Enrollment` optional

Stores simple built-in entry configuration.

### 10.3 `UtmParameters` optional

Stores structured UTM overrides.

---

## 11. Real Sample Structures

## 11.1 Example `Flow`

Example for a simple onboarding sequence:

```json
{
  "mode": "linear",
  "steps": [
    {
      "id": "welcome",
      "type": "email",
      "title": "Welcome Email",
      "templateId": 101,
      "timing": {
        "anchor": "enrollment",
        "delay": {
          "value": 0,
          "unit": "minutes"
        },
        "useContactTimeZone": true
      },
      "liveEdit": {
        "insertPolicy": "new_enrollments_only"
      }
    },
    {
      "id": "getting-started",
      "type": "email",
      "title": "Getting Started Guide",
      "templateId": 102,
      "timing": {
        "anchor": "previous_step",
        "delay": {
          "value": 2,
          "unit": "days"
        },
        "sendAt": "10:00",
        "useContactTimeZone": true
      },
      "liveEdit": {
        "insertPolicy": "include_in_progress"
      }
    },
    {
      "id": "case-study",
      "type": "email",
      "title": "Case Study",
      "templateId": 103,
      "timing": {
        "anchor": "previous_step",
        "delay": {
          "value": 5,
          "unit": "days"
        },
        "sendAt": "10:00"
      },
      "liveEdit": {
        "insertPolicy": "new_enrollments_only"
      }
    }
  ]
}
```

## 11.2 Example `Enrollment`

Example for a sequence that automatically enrolls contacts from a segment:

```json
{
  "mode": "segment",
  "includeSegmentIds": [12, 18],
  "excludeSegmentIds": [25],
  "reentryPolicy": "once_ever"
}
```

Example for a sequence that is only used by manual or API enrollment:

```json
{
  "mode": "api",
  "reentryPolicy": "allow_after_completion"
}
```

## 11.3 Example `UtmParameters`

Example for sequence-level UTM defaults:

```json
{
  "source": "leadcms",
  "medium": "email",
  "campaign": "trial_onboarding",
  "content": "sequence_default"
}
```

## 11.4 Example top-level sequence shape

Illustrative example of how the full entity would look conceptually:

```json
{
  "id": 44,
  "name": "Trial Onboarding",
  "description": "Core onboarding sequence for new trial users.",
  "status": "Active",
  "continueOnStepFailure": false,
  "stopOnReply": true,
  "stopOnGlobalUnsubscribe": true,
  "useContactTimeZoneByDefault": true,
  "timeZone": 0,
  "flow": {
    "mode": "linear",
    "steps": [
      {
        "id": "welcome",
        "type": "email",
        "title": "Welcome Email",
        "templateId": 101,
        "timing": {
          "anchor": "enrollment",
          "delay": {
            "value": 0,
            "unit": "minutes"
          }
        },
        "liveEdit": {
          "insertPolicy": "new_enrollments_only"
        }
      }
    ]
  },
  "enrollment": {
    "mode": "segment",
    "includeSegmentIds": [12],
    "reentryPolicy": "once_ever"
  },
  "utmParameters": {
    "source": "leadcms",
    "medium": "email",
    "campaign": "trial_onboarding"
  }
}
```

---

## 12. Runtime State Boundary

The JSONB fields carry the sequence definition and lightweight enrollment configuration.

Contact execution state lives in runtime tables so it can be updated, indexed, filtered, and counted efficiently.

That runtime state includes:

- current contact step,
- last sent timestamp per contact,
- retry counters per contact,
- contact exit reason,
- reply state per contact,
- recipient snapshots,
- delivery history.

---

## 13. Runtime Entities Needed in Phase 1

Even with a self-contained workflow, Phase 1 runtime state is relational.

## 13.1 `SequenceEnrollment`

Recommended purpose:

- one row per contact in a sequence,
- current progression state,
- exit state,
- entry source.

Recommended fields:

- `SequenceId`
- `ContactId`
- `Status`
- `CurrentStepId` or `LastCompletedStepId`
- `EnteredAt`
- `CompletedAt`
- `ExitedAt`
- `ExitReason`
- `EnrollmentSource`

## 13.2 `SequenceDelivery`

Recommended purpose:

- one row per contact and step delivery,
- idempotency,
- scheduling,
- auditability,
- future analytics.

Recommended fields:

- `SequenceId`
- `ContactId`
- `StepId`
- `Status`
- `ScheduledAt`
- `SentAt`
- `SkipReason`
- `ErrorMessage`
- optional `EmailLogId`

## 13.3 Why these remain relational

These records:

- change frequently,
- need indexes,
- need uniqueness rules,
- need efficient counting and filtering,
- fit the existing Core operational patterns much better than JSONB.

---

## 14. Concrete Phase 1 Shape

### On `Sequence`

- top-level scalar metadata,
- top-level execution attributes,
- top-level counters,
- `Flow` JSONB,
- optional `Enrollment` JSONB,
- optional `UtmParameters` JSONB.

### In `Flow`

- linear ordered `steps`,
- stable `id` per step,
- `templateId`,
- `timing`,
- `liveEdit.insertPolicy`.

### Outside `Sequence`

- `SequenceEnrollment` runtime rows,
- `SequenceDelivery` runtime rows.

This is the concrete Phase 1 shape.

---

## 15. Summary

Phase 1 sequences are defined by:

- explicit top-level execution attributes,
- a self-contained `Flow` JSONB workflow,
- optional `Enrollment` and `UtmParameters` JSONB fields,
- relational runtime entities for enrollment progress and delivery state.

This gives us a concrete first implementation that stays aligned with the current Core design and remains extensible later.## 2. Phase 1 Design Rules

Phase 1 sequences should follow these concrete rules.
### 2.1 The sequence entity carries explicit top-level execution attributes

Sequence-wide execution behaviour is represented by normal top-level entity attributes.
### 2.2 The sequence definition is represented by three JSONB fields at most

Phase 1 uses the following JSONB fields:
- `Flow` required
- `Enrollment` optional
- `UtmParameters` optional
### 2.3 Language is resolved through enrollment and template selection

The sequence itself remains language-agnostic.
### 2.4 Phase 1 stores the current active workflow shape directly

The Phase 1 model stores the current workflow definition directly on the sequence.
### 2.5 Phase 1 optimizes for linear email journeys

The workflow structure is designed for ordered multi-step email sequences.
## 4.2 Sequence-wide execution attributes

Phase 1 sequence-wide execution behaviour is defined by these top-level attributes:
### Notes

`StopOnGlobalUnsubscribe`
- is an explicit global rule for all sequence sends,
- communicates sequence behaviour clearly at the entity level.
## 4.4 JSONB fields

Phase 1 JSONB fields:
These fields are sufficient for the first implementation and leave room for future extension without making the entity vague.
For Phase 1, `Flow` represents a linear journey.
Phase 1 top-level shape:
Phase 1 value for `mode`:
- `linear`

The `Flow` document stores the current active shape of the sequence.
`id`

- stable string identifier inside the sequence,
- independent from array index,
- used by runtime state and delivery tracking to refer to a specific step reliably.
`metadata`

- optional editor-facing data that does not drive execution.
## 8. What Goes Into `Enrollment`

`Enrollment` is an optional JSONB field for simple built-in entry rules.
Its role in Phase 1 is to make the sequence reasonably self-contained for common entry scenarios without turning the sequence entity into the full automation system.
## 8.2 Phase 1 scope of `Enrollment`

`Enrollment` covers these built-in entry patterns in Phase 1:
- manual enrollment,
- API-driven enrollment,
- segment-based enrollment.
Phase 1 uses these JSONB fields:
This is the intended JSONB surface for the first implementation.
## 12. Runtime State Boundary

The JSONB fields carry the sequence definition and lightweight enrollment configuration.
Contact execution state lives in runtime tables so it can be updated, indexed, filtered, and counted efficiently.
### In `Flow`

- `liveEdit.insertPolicy`.
This is the concrete Phase 1 shape.
Phase 1 sequences are defined by:

- explicit top-level execution attributes,
- a self-contained `Flow` JSONB workflow,
- optional `Enrollment` and `UtmParameters` JSONB fields,
- relational runtime entities for enrollment progress and delivery state.

This gives us a concrete first implementation that stays aligned with the current Core design and remains extensible later.
# Email Sequence Core Design

Date: 2026-03-12

---

## 1. Goal

This document defines the recommended internal structure for email sequences in Core.

The main goals are:

- keep the sequence definition as self-contained as possible,
- store the actual workflow in JSONB,
- avoid over-engineering the first version,
- fit the current Core patterns already used by campaigns, email logs, contacts, segments, and the legacy scheduled-email model.

---

## 2. Decisions for the First Version

The design should follow these rules.

### 2.1 Sequence-wide settings should be top-level attributes

We should not introduce a generic `Settings` JSONB field for sequence-wide behaviour.

If a setting is important enough to affect execution globally, filtering, list views, or operator understanding, it should be a normal top-level sequence attribute.

### 2.2 No explicit sequence versioning for now

We should not introduce a `FlowVersion`, `SequenceVersion`, or similar first-version concept.

If editing active sequences later requires clever upgrade behaviour for existing enrollments, that should be handled by explicit runtime logic, not by exposing a version model as part of the first design.

### 2.3 No explicit sequence versioning for now

Language should not be defined at the sequence level.

Language is already handled by:

- who gets enrolled,
- which templates a step references,
- existing template language support.

The sequence itself should not try to override that.

### 2.4 Keep JSONB only where it brings real value

The JSONB fields should be used for:

- the ordered workflow definition,
- optional self-contained enrollment configuration,
- structured UTM overrides if we want sequence-level UTM defaults like campaigns already support.

Everything else should stay explicit on the entity.

---

## 3. Recommended High-Level Model

The recommended first-version model is:

- one main `Sequence` entity,
- one required JSONB field for the workflow definition,
- one optional JSONB field for enrollment definition,
- one optional JSONB field for UTM overrides,
- separate runtime entities for enrollments and deliveries.

This gives us a good balance:

- the workflow stays self-contained,
- the sequence entity remains understandable and queryable,
- runtime state does not pollute the main JSON document.

---

## 4. Proposed Top-Level Sequence Attributes

The `Sequence` entity should contain the fields below.

## 4.1 Identity and lifecycle

- `Id`
- `Name`
- `Description`
- `Status`
- `LastActivatedAt`
- `LastPausedAt`
- `ArchivedAt`

`Status` should support at least:

- `Draft`
- `Active`
- `Paused`
- `Archived`

## 4.2 Sequence-wide execution attributes

These are the attributes that should be flattened to the top level instead of going into `Settings` JSONB:

- `ContinueOnStepFailure`
- `StopOnReply`
- `StopOnGlobalUnsubscribe`
- `UseContactTimeZoneByDefault`
- `TimeZone`

### Notes

`ContinueOnStepFailure`

- controls whether a failed step blocks further progression.

`StopOnReply`

- supports the business requirement for personal outreach workflows.

`StopOnGlobalUnsubscribe`

- should almost certainly default to `true`, but it is still useful as an explicit attribute because it expresses global sequence behaviour clearly.

`UseContactTimeZoneByDefault`

- allows a step to inherit contact-local-time behaviour without requiring every step to repeat the same default.

`TimeZone`

- fallback timezone offset in minutes when contact timezone is not available, mirroring the campaign pattern.

## 4.3 Summary counters

Recommended top-level counters:

- `ActiveEnrollmentCount`
- `CompletedEnrollmentCount`
- `ExitedEnrollmentCount`
- `SentCount`
- `FailedCount`

These are operational summaries, not source of truth.

## 4.4 JSONB fields

Recommended JSONB fields:

- `Flow` required
- `Enrollment` optional
- `UtmParameters` optional

That should be enough for the first version.

---

## 5. What Should Go Into `Flow`

`Flow` should store the actual sequence structure.

This is the most important JSONB field and the reason the sequence can remain self-contained.

## 5.1 Responsibilities of `Flow`

`Flow` should contain:

- ordered step definitions,
- stable step identifiers,
- step-level template references,
- step-level timing rules,
- step-level live-edit behaviour.

For the first version, `Flow` should represent a linear journey only.

## 5.2 Recommended structure of `Flow`

Recommended top-level shape:

- `mode`
- `steps`

Recommended first-version value for `mode`:

- `linear`

No explicit `schemaVersion` is recommended right now.

If we later need to evolve the JSON shape, that can be handled through application-level migration logic rather than persisting a version concept from day one.

## 5.3 What each step in `Flow` should contain

Each step should contain:

- `id`
- `type`
- `title`
- `templateId`
- `timing`
- `liveEdit`
- optional `metadata`

### Step field notes

`id`

- stable string identifier inside the sequence,
- should not depend on array index,
- becomes especially important because we are not introducing sequence versions right now.

`type`

- should initially support only `email`.

`title`

- internal business label for the step.

`templateId`

- references an existing email template.

`timing`

- defines when this step becomes eligible.

`liveEdit`

- defines how a step behaves when inserted or changed in an active sequence.

`metadata`

- optional editor-facing data that should not drive execution.

---

## 6. What Should Go Into Step Timing

Timing belongs inside each step because it is a property of the step itself.

Recommended structure:

- `anchor`
- `delay`
- optional `sendAt`
- optional `useContactTimeZone`
- optional `allowedWeekDays`

## 6.1 Recommended timing semantics

`anchor`

- `enrollment`
- `previous_step`

`delay`

- object with `value` and `unit`
- units should initially support `minutes`, `hours`, `days`

`sendAt`

- optional local time such as `10:00`

`useContactTimeZone`

- optional step-level override
- if absent, the sequence should fall back to `UseContactTimeZoneByDefault`

`allowedWeekDays`

- optional array for cases like only sending on working days
- not required to be used immediately, but safe to support in the structure if wanted

---

## 7. What Should Go Into Step Live-Edit Behaviour

The implementation plan already identifies retroactive step behaviour as a key requirement.

That means the step definition should include a small structure for live-edit behaviour.

Recommended fields:

- `insertPolicy`

Recommended values:

- `new_enrollments_only`
- `include_in_progress`
- `include_all`

This keeps the critical rule on the step itself, which is cleaner than handling it only at UI time.

---

## 8. What Should Go Into `Enrollment`

`Enrollment` should be optional.

It should exist only to describe simple built-in sequence entry rules when we want the sequence to be somewhat self-contained.

It should not try to become a full automation engine.

## 8.1 Recommended structure of `Enrollment`

Recommended fields:

- `mode`
- `includeSegmentIds`
- `excludeSegmentIds`
- `reentryPolicy`

Recommended values for `mode`:

- `manual`
- `api`
- `segment`

Recommended values for `reentryPolicy`:

- `once_ever`
- `allow_after_completion`
- `always`

## 8.2 What should not go into `Enrollment`

Do not put full event-trigger automation logic here yet.

Examples of what should stay out for now:

- website event triggers,
- CRM event triggers,
- complex conditional automation rules,
- multi-condition trigger trees.

Those should become a separate automation concept later that can target a sequence.

---

## 9. What Should Go Into `UtmParameters`

Following the existing campaign design, it is reasonable for a sequence to have optional UTM overrides.

This should remain a dedicated JSONB field, reusing the same conceptual model already used by campaigns.

Recommended purpose:

- provide default UTM context for sequence sends,
- avoid repeating the same UTM values on every step,
- allow the sending logic to derive step-specific detail while still inheriting sequence-level campaign context.

---

## 10. JSONB Fields We Actually Need

For the first version, the recommended JSONB fields are only these:

### 10.1 `Flow` required

Stores the ordered step workflow.

### 10.2 `Enrollment` optional

Stores simple built-in entry configuration.

### 10.3 `UtmParameters` optional

Stores structured UTM overrides, following the current campaign pattern.

That should be the full JSONB surface unless a real business need appears.

---

## 11. Real Sample Structures

## 11.1 Example `Flow`

Example for a simple onboarding sequence:

```json
{
  "mode": "linear",
  "steps": [
    {
      "id": "welcome",
      "type": "email",
      "title": "Welcome Email",
      "templateId": 101,
      "timing": {
        "anchor": "enrollment",
        "delay": {
          "value": 0,
          "unit": "minutes"
        },
        "useContactTimeZone": true
      },
      "liveEdit": {
        "insertPolicy": "new_enrollments_only"
      }
    },
    {
      "id": "getting-started",
      "type": "email",
      "title": "Getting Started Guide",
      "templateId": 102,
      "timing": {
        "anchor": "previous_step",
        "delay": {
          "value": 2,
          "unit": "days"
        },
        "sendAt": "10:00",
        "useContactTimeZone": true
      },
      "liveEdit": {
        "insertPolicy": "include_in_progress"
      }
    },
    {
      "id": "case-study",
      "type": "email",
      "title": "Case Study",
      "templateId": 103,
      "timing": {
        "anchor": "previous_step",
        "delay": {
          "value": 5,
          "unit": "days"
        },
        "sendAt": "10:00"
      },
      "liveEdit": {
        "insertPolicy": "new_enrollments_only"
      }
    }
  ]
}
```

## 11.2 Example `Enrollment`

Example for a sequence that automatically enrolls contacts from a segment:

```json
{
  "mode": "segment",
  "includeSegmentIds": [12, 18],
  "excludeSegmentIds": [25],
  "reentryPolicy": "once_ever"
}
```

Example for a sequence that is only used by manual or API enrollment:

```json
{
  "mode": "api",
  "reentryPolicy": "allow_after_completion"
}
```

## 11.3 Example `UtmParameters`

Example for sequence-level UTM defaults:

```json
{
  "source": "leadcms",
  "medium": "email",
  "campaign": "trial_onboarding",
  "content": "sequence_default"
}
```

## 11.4 Example top-level sequence shape

Illustrative example of how the full entity would feel conceptually:

```json
{
  "id": 44,
  "name": "Trial Onboarding",
  "description": "Core onboarding sequence for new trial users.",
  "status": "Active",
  "continueOnStepFailure": false,
  "stopOnReply": true,
  "stopOnGlobalUnsubscribe": true,
  "useContactTimeZoneByDefault": true,
  "timeZone": 0,
  "flow": {
    "mode": "linear",
    "steps": [
      {
        "id": "welcome",
        "type": "email",
        "title": "Welcome Email",
        "templateId": 101,
        "timing": {
          "anchor": "enrollment",
          "delay": {
            "value": 0,
            "unit": "minutes"
          }
        },
        "liveEdit": {
          "insertPolicy": "new_enrollments_only"
        }
      }
    ]
  },
  "enrollment": {
    "mode": "segment",
    "includeSegmentIds": [12],
    "reentryPolicy": "once_ever"
  },
  "utmParameters": {
    "source": "leadcms",
    "medium": "email",
    "campaign": "trial_onboarding"
  }
}
```

---

## 12. What Should Not Go Into the Sequence JSONB Fields

Do not store contact-specific mutable state in `Flow`, `Enrollment`, or `UtmParameters`.

Specifically, do not embed:

- current contact step,
- last sent timestamp per contact,
- retry counters per contact,
- contact exit reason,
- reply state per contact,
- recipient snapshots,
- delivery history.

That data changes too frequently and needs relational indexing and querying.

---

## 13. Runtime Entities Still Needed

Even with a self-contained workflow, runtime state should stay relational.

## 13.1 `SequenceEnrollment`

Recommended purpose:

- one row per contact in a sequence,
- current progression state,
- exit state,
- entry source.

Recommended fields:

- `SequenceId`
- `ContactId`
- `Status`
- `CurrentStepId` or `LastCompletedStepId`
- `EnteredAt`
- `CompletedAt`
- `ExitedAt`
- `ExitReason`
- `EnrollmentSource`

## 13.2 `SequenceDelivery`

Recommended purpose:

- one row per contact and step delivery,
- idempotency,
- scheduling,
- auditability,
- future analytics.

Recommended fields:

- `SequenceId`
- `ContactId`
- `StepId`
- `Status`
- `ScheduledAt`
- `SentAt`
- `SkipReason`
- `ErrorMessage`
- optional `EmailLogId`

## 13.3 Why these should remain relational

These records:

- change frequently,
- need indexes,
- need uniqueness rules,
- need efficient counting and filtering,
- fit the existing Core operational patterns much better than JSONB.

---

## 14. Recommended First-Version Shape

### On `Sequence`

- top-level scalar metadata,
- top-level execution attributes,
- top-level counters,
- `Flow` JSONB,
- optional `Enrollment` JSONB,
- optional `UtmParameters` JSONB.

### In `Flow`

- linear ordered `steps`,
- stable `id` per step,
- `templateId`,
- `timing`,
- `liveEdit.insertPolicy`.

### Outside `Sequence`

- `SequenceEnrollment` runtime rows,
- `SequenceDelivery` runtime rows.

This is the cleanest first implementation.

---

## 15. Key Recommendation

The strongest recommendation is:

- keep the workflow self-contained in JSONB,
- keep sequence-wide execution controls explicit on the entity,
- avoid introducing versioning now,
- keep runtime execution state relational.

This gives us a practical model that stays aligned with the current Core design and avoids unnecessary complexity in the first implementation.