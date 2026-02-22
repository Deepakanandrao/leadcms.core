# Email Marketing Platform — Implementation Plan

Date: 2026-02-21

---

## 1. Vision & Scope

Build a marketing email platform within LeadCMS that handles the three fundamental email marketing patterns every SaaS needs:

| Pattern              | Industry Term                | Example                                                   |
| -------------------- | ---------------------------- | --------------------------------------------------------- |
| **Sequences**        | Drip / Automation / Journey  | Onboarding series, trial nurture, post-purchase follow-up |
| **One-time sends**   | Campaign / Broadcast / Blast | Product launch announcement, holiday sale, feature update |
| **Subscriber sends** | Newsletter / Mailing list    | Weekly digest, monthly product news, changelog updates    |

The plan is designed to be **incremental** — each phase produces a working, testable, shippable feature that builds toward the full vision without requiring throw-away work.

---

## 2. Architectural Decisions & Rationale

### 2.1 What stays and why

| Current Asset                      | Verdict                                   | Reason                                                                                                                                             |
| ---------------------------------- | ----------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| **EmailTemplate**                  | Keep as-is                                | Strong reusable content primitive. MJML + Liquid rendering pipeline is solid. Templates are content — they should remain decoupled from execution. |
| **EmailGroup**                     | Keep, but **redefine its role** (see 2.2) | Currently blurs the line between "content folder" and "sequence definition". Needs clarity.                                                        |
| **Segment**                        | Keep as-is                                | Dynamic and static segments with rule engine — fully sufficient as the audience system. No changes needed.                                         |
| **Contact**                        | Keep as-is                                | Good CRM contact model with language, timezone, tags, unsubscribe link.                                                                            |
| **Unsubscribe**                    | Keep + extend later                       | Works for global opt-out. Will need list-level unsubscribe in Phase 4.                                                                             |
| **EmailLog**                       | Keep as-is                                | Solid audit trail. All future sending continues logging here.                                                                                      |
| **IEmailFromTemplateService**      | Keep as-is                                | Template resolution, MJML rendering, Liquid processing, send + log — reused by all new sending paths.                                              |
| **Background task infrastructure** | Keep as-is                                | BaseTask, cron-based scheduling, TaskExecutionLog — solid foundation for new tasks.                                                                |

### 2.2 What changes and why

**EmailGroup needs a role clarification.** Today it serves two purposes simultaneously:

1. A **content organiser** — grouping related templates by topic (e.g., "Onboarding Emails" folder).
2. A **sequence definition** — the ordered list of templates that ContactScheduledEmailTask walks through.

This dual role creates problems:

- Ordering is implicit (by template Id), making reordering impossible.
- The schedule belongs to the group, not to individual steps, so you cannot say "wait 3 days after the previous email".
- Adding a template to the group silently changes the sequence for in-progress contacts.
- There is no way to use the same template in multiple sequences.

**Decision: Separate content organisation from execution orchestration.**

- **EmailGroup** remains a **content library organiser**. Templates still belong to groups by topic. This relationship is purely taxonomic — it helps humans find and manage templates. No execution logic lives here.
- A new **Sequence** concept owns the execution orchestration: step order, per-step delays, audience binding, enrolment tracking.
- A new **Campaign** concept handles one-time sends.

This is the standard industry separation (Mailchimp: "Templates" vs "Journeys" vs "Campaigns"; HubSpot: "Templates" vs "Sequences" vs "Marketing Emails"; ActiveCampaign: "Templates" vs "Automations" vs "Campaigns").

### 2.3 What gets created and why

| New Concept            | Purpose                                                               | Why it can't be avoided                                                                                                                                    |
| ---------------------- | --------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Campaign**           | One-time email send to audience                                       | Current system has no concept of a broadcast. Forcing a campaign through the sequence pipeline adds unnecessary complexity and confusing semantics.        |
| **Campaign Recipient** | Per-contact delivery record for a campaign                            | Required for progress tracking, retry, and idempotency. Without it, you cannot show "45,000 sent / 3 failed / 200 skipped".                                |
| **Sequence**           | Ordered, event-triggered series of email steps                        | The current EmailGroup+EmailSchedule conflation cannot support per-step delays, explicit ordering, or retroactive policies. A dedicated concept is needed. |
| **Sequence Step**      | Individual email within a sequence + its timing and eligibility rules | Required for per-step delay control, reordering, and retroactive send policy.                                                                              |
| **Sequence Enrolment** | Per-contact membership in a running sequence                          | Replaces ContactEmailSchedule with richer state tracking (current step, exit reason, etc.).                                                                |
| **Sequence Delivery**  | Per-contact per-step send record                                      | Idempotency ("never send the same step to the same contact twice") and granular tracking.                                                                  |
| **Mailing List**       | Named subscription channel for newsletters                            | Global unsubscribe is not sufficient for newsletters — contacts must be able to subscribe to Topic A but not Topic B.                                      |
| **Subscription**       | Per-contact membership in a mailing list                              | Tracks opt-in state, confirmation, and list-level unsubscribe.                                                                                             |

### 2.4 Backward compatibility strategy

- **No existing entities are removed or renamed.** EmailGroup, EmailTemplate, EmailSchedule, and ContactEmailSchedule remain in the schema.
- **Existing drip functionality continues to work** during and after migration. ContactScheduledEmailTask keeps running. Once Sequences are built, existing drips can be migrated to the new model as a data migration — but there is no urgency; both can coexist.
- **EmailTemplate.EmailGroupId** continues to work as the content taxonomy relationship. New sequence steps link to templates independently.
- All existing API endpoints remain stable. New endpoints are additive.

### 2.5 Key architectural principles (industry standard)

1. **Content and orchestration are separate.** A template is a reusable asset. A sequence step or campaign references a template but owns the scheduling, targeting, and eligibility rules.

2. **Idempotent delivery.** No contact should ever receive the same email twice from the same campaign or sequence step. Enforced by unique constraints at the database level.

3. **Audience snapshot at execution time.** When a campaign launches, the segment is evaluated and contacts are materialised as recipient records. This ensures auditability ("who was in the audience?") and prevents mid-send audience drift.

4. **Compliance gates before every send.** Every send path must check: global unsubscribe → list-level unsubscribe → suppression (bounce/complaint) → template validity. In that order.

5. **Observability.** Every campaign and sequence produces counters: queued, sent, failed, skipped (with reason). These feed dashboards and diagnostics.

---

## 3. Business Concepts & Behaviour Specifications

### 3.1 Sequences (Drip Campaigns / Automations)

A **Sequence** is an ordered set of email steps that a contact progresses through over time after an enrolment trigger.

#### Core behaviours

**Creating a sequence:**

- Admin names the sequence, optionally adds a description.
- Admin adds steps, each step consists of: which template to send, when to send (delay from previous step or specific weekday/time constraints), and an eligibility policy (explained below).
- Steps have an explicit sort order. Admin can drag/drop to reorder.

**Enrolment triggers:**

- Segment-based: "Enrol all contacts in Segment X" — evaluated periodically, new contacts entering the segment are auto-enrolled.
- Event-based: API call enrols a specific contact (e.g., after signup, after purchase).
- Manual: Admin bulk-enrols contacts from a segment or selection.

**Step timing:**

- Each step defines a delay relative to the previous step (e.g., "3 days after Step 1", "immediately", "7 days").
- Optional weekday/time constraints (e.g., "only send on Tue/Thu at 10:00 in the contact's timezone").
- First step can fire immediately upon enrolment or with a delay.

**Progression:**

- The background task evaluates each enrolled contact, finds the next unsent step, checks if the timing condition is met, and sends if so.
- If a contact unsubscribes mid-sequence, their enrolment is marked as exited (reason: unsubscribed) and no further steps are sent.
- If sending fails and retries are exhausted, the step is marked as failed. The sequence can continue to the next step or halt — this should be a configurable sequence-level setting ("continue on failure" vs "halt on failure").

**Adding steps to an active sequence — the retroactive eligibility policy:**

This is the critical feature. When a step is added to a sequence that already has enrolled contacts, the system needs to know what to do. Each step carries an **eligibility policy**, chosen at the time of adding:

| Policy                  | Behaviour                                                                                                                                                                                                                                   |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **New enrolments only** | Only contacts enrolled after this step was added will receive it. Contacts who are mid-sequence and have already passed this position or completed the sequence are not affected. This is the safest default.                               |
| **Include in-progress** | Contacts who are currently progressing through the sequence and have not yet reached or passed this step's position will receive it as part of their normal progression. Contacts who have already completed the sequence are not affected. |
| **Include all**         | All contacts enrolled in the sequence — including those who have already completed it — will receive this email. Completed enrolments are reopened. This is for important announcements or corrections.                                     |

When a step is inserted in the middle (not appended at the end), the same policies apply relative to each contact's current position in the sequence.

**Removing steps from an active sequence:**

- The step is removed from the sequence definition. Contacts who have already received it are not affected. Contacts who have not yet reached it simply skip over it. Pending deliveries for this step are cancelled.

**Reordering steps in an active sequence:**

- For contacts who have not yet reached the affected steps, the new order takes effect.
- For contacts who have already passed the affected region, no change occurs.
- A safety warning should be shown to the admin explaining the impact.

#### Example scenario

> **Onboarding Sequence** (triggered by: contact enters "Trial Users" segment)
>
> 1. Welcome email — immediately
> 2. Getting started guide — 2 days later
> 3. Feature highlight — 5 days later
> 4. Case study — 10 days later
> 5. Trial ending reminder — 12 days later
>
> 500 contacts are enrolled. 200 have completed the sequence.
>
> Admin adds Step 3.5: "New feature announcement" between steps 3 and 4,
> with policy "Include in-progress".
>
> Result: The ~100 contacts currently between steps 1-3 will receive the new
> email as part of their normal progression. The 200 completed contacts and
> ~200 contacts already past step 3 are not affected.

### 3.2 Campaigns (One-Time Sends / Broadcasts)

A **Campaign** is a one-time email send to a defined audience.

#### Core behaviours

**Creating a campaign:**

- Admin creates a campaign with a name, selects a template, and selects one or more segments as the audience.
- Optionally selects exclusion segments (contacts who match the audience but should be excluded).
- Chooses send timing: send immediately, or schedule for a specific date/time (with timezone).

**Audience resolution:**

- When the campaign transitions from Draft to Sending (either immediately or at the scheduled time), the system **snapshots** the audience by evaluating all included segments, subtracting excluded segments, subtracting globally unsubscribed contacts, and deduplicating.
- Each resolved contact becomes a recipient record with a status: Pending → Sent / Failed / Skipped.
- The snapshot is immutable — if the segment changes after the campaign starts, it does not affect in-progress delivery.

**Send distribution (throttling):**

- For large audiences, sending should be distributed over time to avoid overwhelming the mail server and to maintain deliverability reputation.
- Configurable sending rate (e.g., "500 emails per 5 minutes").
- Industry standard: stagger sends in batches rather than all-at-once.

**Tracking:**

- Total recipients, sent count, failed count, skipped count (with breakdown of skip reasons: unsubscribed, duplicate, suppressed).
- When integrated with a provider like SendGrid: opens, clicks, bounces, complaints as event webhooks update the delivery records.

**Campaign statuses:**

- Draft → Scheduled → Sending → Sent
- Draft → Sending → Sent (for immediate sends)
- Scheduled → Cancelled (admin cancellation before send time)
- Sending → Paused → Sending → Sent (admin can pause mid-send for large campaigns)

**Preventing duplicate campaigns:**

- It should not be possible to send the exact same template to the exact same segment twice without explicit confirmation. This is a guardrail, not a hard block — sometimes you do want to resend.

#### Example scenario

> **Product Launch Campaign**
>
> Template: "Introducing Feature X"
> Audience: Segment "Active Users" (12,000 contacts) + Segment "Trial Users" (3,000)
> Exclude: Segment "Churned Users" (500 overlap)
>
> Resolved audience: 14,500 unique contacts (after dedup + exclusion)
> Send rate: 500/minute → full send completes in ~29 minutes
>
> Results: 14,320 sent, 80 skipped (unsubscribed), 100 failed (invalid email)

### 3.3 Newsletters (Mailing Lists & Subscriptions)

A **Newsletter** (or Mailing List) is a subscription-based channel where contacts opt in to receive ongoing content.

#### Core behaviours

**Mailing lists:**

- Admin creates named mailing lists (e.g., "Product Updates", "Engineering Blog", "Weekly Digest").
- Each list has its own subscriber base, independent of CRM segments.
- A contact can be subscribed to multiple lists.

**Subscription management:**

- Contacts subscribe via: public signup form (website), admin manual enrolment, bulk import, or API call.
- Industry standard: **double opt-in** — contact receives a confirmation email with a link; subscription is only active after confirmation. This is legally recommended (GDPR) and required in some jurisdictions.
- Subscription states: Pending (awaiting confirmation) → Active → Unsubscribed.
- Contacts can unsubscribe from a specific list without affecting other lists or their CRM contact status.

**Sending a newsletter issue:**

- Admin selects a mailing list and a template, then sends. This is conceptually similar to a campaign, but the audience is derived from the subscription list rather than a segment.
- Alternatively, for recurring newsletters, the system can detect new templates added to a designated group and automatically dispatch to all subscribers.
- Deduplication and unsubscribe checks apply as with campaigns.

**Unsubscribe hierarchy:**

This is critical for compliance and user experience. The system must support:

| Level                      | Effect                                                                                                                                                                 |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Global unsubscribe**     | Contact receives no marketing emails at all — no campaigns, no sequences, no newsletters. Transactional emails (password reset, order confirmation) can still be sent. |
| **List-level unsubscribe** | Contact stops receiving emails from one specific mailing list. All other lists, campaigns, and sequences continue.                                                     |
| **Sequence exit**          | Contact exits one specific sequence. All other activity continues.                                                                                                     |

Every marketing email must include a one-click unsubscribe link and a link to a preference centre where the contact can manage their list subscriptions and global opt-out.

**Relationship between mailing lists and segments:**

- They are complementary, not competing. Segments are CRM constructs based on contact properties. Mailing lists are explicit opt-in lists based on contact preference.
- You can use a segment to bulk-subscribe contacts to a mailing list (e.g., "subscribe all contacts tagged 'interested-in-blog' to the Blog list").
- A campaign can target a segment. A newsletter issue targets a mailing list.
- Segments CAN be built that reference subscription state (e.g., a dynamic segment "all contacts who are subscribed to the Product Updates list") — this requires subscription data to be queryable.

---

## 4. Suppression & Compliance Model

This section applies across all three patterns.

### 4.1 Suppression checks before every send

Every send path — sequence step, campaign recipient, newsletter issue — must pass through these gates before dispatching:

1. **Global unsubscribe?** → Skip (reason: globally unsubscribed)
2. **List-level unsubscribe?** → Skip if sending from that list (reason: list unsubscribed)
3. **Suppressed email?** → Skip if contact's email has hard-bounced or filed a complaint (reason: suppressed)
4. **Already sent this exact step/campaign?** → Skip (reason: duplicate, idempotency guard)
5. **Contact email valid?** → Skip if email is empty/invalid (reason: invalid recipient)

### 4.2 Bounce and complaint handling

- **Hard bounce** (permanent delivery failure): Immediately suppress the email address from all future sends. Mark the contact. Decrement counts.
- **Soft bounce** (temporary failure): Retry according to retry policy. After N consecutive soft bounces, promote to hard bounce.
- **Spam complaint**: Immediately suppress and globally unsubscribe the contact. This is a legal requirement under CAN-SPAM.

These events typically arrive via webhooks from the email provider (e.g., SendGrid event webhooks, which already have a plugin in your system).

### 4.3 Preference centre

A public-facing page where contacts can:

- See which mailing lists they are subscribed to, and toggle each.
- Set a global opt-out.
- View why they are receiving emails (e.g., "You are receiving this because you signed up on our website on Jan 15, 2026").

This page is accessed via a unique, signed URL included in every email footer.

---

## 5. Incremental Implementation Plan

Each phase is a shippable increment with clear acceptance criteria.

### Phase 1: Campaign Foundation

**Goal:** Enable one-time email sends to segments with progress tracking.

**Deliverables:**

1. Campaign entity with lifecycle states (Draft, Scheduled, Sending, Sent, Cancelled, Paused).
2. Campaign recipient entity tracking per-contact delivery status (Pending, Sent, Failed, Skipped).
3. Campaign CRUD API — create, update, list, get details with statistics.
4. Campaign launch action — "send now" or "schedule for datetime".
5. Background task that: picks up Scheduled campaigns at their send time, resolves the segment audience (snapshot), creates recipient records, and sends in batches through existing IEmailFromTemplateService.
6. Deduplication across segments (no contact receives the same campaign twice).
7. Global unsubscribe check during send (skip unsubscribed contacts).
8. Campaign statistics endpoint: total, sent, failed, skipped counts.

**Acceptance criteria:**

- Admin can create a campaign, pick a template and segment, and send immediately. The system sends to all contacts in the segment, logs each send, and reports completion with counts.
- Admin can schedule a campaign for a future time. The system sends at that time.
- Unsubscribed contacts are skipped.
- Two overlapping segments do not cause duplicate sends.
- Campaign can be cancelled before send time.

**What to verify:**

- Create campaign with a test segment of 10 contacts → all 10 receive the email.
- Create campaign with 2 overlapping segments → contacts in both segments receive only 1 email.
- Create campaign, unsubscribe 2 contacts → those 2 are skipped, count shows correctly.
- Schedule campaign for 5 minutes from now → campaign sends at the right time.
- Cancel a scheduled campaign → nothing is sent.

---

### Phase 2: Sequence Engine (Replaces Current Drip System)

**Goal:** Build the new sequence engine that supports explicit ordering, per-step delays, and segment-based enrolment.

> **Note:** This phase runs **alongside** the existing ContactScheduledEmailTask. Both systems coexist. Existing drip sequences continue to operate. New sequences use the new engine.

**Deliverables:**

1. Sequence entity (name, description, status: Draft/Active/Paused/Archived, settings like "halt on failure" vs "continue on failure").
2. Sequence step entity (template reference, sort order, delay configuration — offset minutes from previous step, optional weekday/time constraints).
3. Sequence enrolment entity (contact reference, enrolment source, current state: Active/Completed/Exited, exit reason if applicable, reference to last completed step).
4. Sequence delivery entity (contact + step reference, status: Pending/Scheduled/Sent/Failed/Skipped, scheduled timestamp, unique constraint on contact+step for idempotency).
5. Sequence CRUD API — create, update steps (add/remove/reorder), activate, pause, archive.
6. Enrolment API — enrol single contact, bulk-enrol from segment.
7. Segment-based auto-enrolment: link a segment to a sequence, periodically evaluate the segment and auto-enrol new contacts.
8. Background task that: for each active sequence, evaluates pending deliveries, checks timing conditions, and sends due emails via IEmailFromTemplateService.
9. Unsubscribe and suppression checks.
10. Sequence and step statistics: enrolments, active/completed/exited counts, per-step sent/pending/failed.

**Acceptance criteria:**

- Admin creates a 3-step sequence with delays of 0, 2 days, and 5 days. Enrols a contact. Contact receives step 1 immediately, step 2 two days later, step 3 five days later. Enrolment status is Completed.
- Admin links a segment to a sequence. A new contact enters the segment. System auto-enrols them.
- Contact unsubscribes mid-sequence. No further steps are sent. Enrolment shows as exited.
- Admin pauses a sequence. No delivery processing occurs. Admin resumes, processing continues.

**What to verify:**

- Enrol 5 contacts into a 3-step sequence → each receives all 3 emails on the correct schedule.
- Unsubscribe 1 contact after step 1 → that contact receives no further emails, others continue.
- Pause sequence after step 1 is sent → no step 2 sends until resumed.
- Two contacts, same sequence step — only 1 delivery each (idempotency).

---

### Phase 3: Retroactive Step Management

**Goal:** Enable adding, removing, and reordering steps in active sequences with control over what happens to existing enrolments.

**Deliverables:**

1. Eligibility policy field on each sequence step: "New enrolments only", "Include in-progress", "Include all".
2. When a step is added to an active sequence with "Include in-progress" or "Include all" policy, the system generates appropriate delivery records for affected enrolments.
3. When a step is removed, pending deliveries for that step are cancelled.
4. When steps are reordered, the system recalculates next-step for in-progress contacts who have not yet reached the affected region.
5. Admin UI warning when modifying an active sequence with enrolled contacts, showing impact preview: "This change will affect X of Y enrolled contacts."

**Acceptance criteria:**

- Active sequence with 3 steps, 100 enrolled contacts (50 completed, 30 on step 2, 20 on step 1). Admin adds step 4 with "Include all" policy → all 100 contacts will eventually receive step 4, including the 50 who had completed.
- Same scenario, "New enrolments only" policy → 50 completed stay completed, 30 and 20 continue normally and receive step 4 as part of their progression.
- Admin removes step 2 from an active sequence → contacts on step 1 skip directly to step 3 (now step 2). Contacts who already sent step 2 are unaffected.

**What to verify:**

- Add step with "Include all" to sequence where 10 contacts have completed → those 10 receive the new email.
- Add step with "New enrolments only" → existing contacts (any state) do not receive it, new enrolment does.
- Add step to middle of sequence with "Include in-progress" → contact that hasn't reached that position yet receives it in order, contact that has already passed it does not.
- Remove a step → pending deliveries cancelled, no orphaned data.

---

### Phase 4: Newsletter / Mailing List System

**Goal:** Implement subscription-based mailing lists and newsletter dispatch.

**Deliverables:**

1. Mailing list entity (name, description, default from email/name, active status, subscriber count).
2. Subscription entity (contact + list reference, status: Pending/Active/Unsubscribed, confirmation token, confirmed at, source — "website form", "admin import", "API", etc.).
3. Mailing list CRUD API.
4. Subscription management API — subscribe, confirm (double opt-in), unsubscribe, bulk-subscribe from segment.
5. Public subscription endpoints (no auth, token-based): subscribe form submission, confirmation link, one-click unsubscribe.
6. Double opt-in flow: subscribe request → confirmation email → click to confirm → subscription active.
7. Newsletter send action: select a list and a template → system sends to all active subscribers. This reuses the campaign sending engine (audience = list subscribers instead of segment contacts).
8. Extend unsubscribe model: support list-level unsubscribe separate from global unsubscribe.
9. List subscriber count maintenance (increment on subscribe, decrement on unsubscribe).
10. Newsletter history: list of past issues sent to a mailing list, with per-issue delivery statistics.

**Acceptance criteria:**

- Admin creates a mailing list. 50 contacts subscribe. Admin sends a newsletter issue → all 50 receive it.
- A contact subscribes via public endpoint with double opt-in → confirmation email is sent, contact clicks, subscription becomes Active.
- A contact unsubscribes from one list → they stop receiving that list's newsletters, but continue receiving campaigns and other lists.
- A globally unsubscribed contact is skipped even if they are subscribed to a list.
- Subscriber count reflects current active subscriptions.

**What to verify:**

- Subscribe 10 contacts to a list, send newsletter → 10 receive it.
- Unsubscribe 2 → send another newsletter → 8 receive it.
- Subscribe via public endpoint without confirming → contact does NOT receive newsletter (pending status).
- Subscribe via public endpoint, confirm, then send → contact receives it.
- Global unsubscribe trumps list subscription.

---

### Phase 5: Preference Centre & Enhanced Compliance

**Goal:** Public-facing subscription management and full compliance infrastructure.

**Deliverables:**

1. Preference centre page (public, token-authenticated): contact sees all mailing lists, can toggle subscriptions, can opt out globally.
2. One-click unsubscribe header (RFC 8058 List-Unsubscribe-Post) — automatically included in every marketing email.
3. Unsubscribe link in every email footer pointing to preference centre.
4. Bounce and complaint webhook handler: process bounce/complaint events from email provider (e.g., SendGrid), update suppression status, globally unsubscribe on complaint.
5. Suppression list management API: view suppressed contacts, reason, date. Manual re-enable with confirmation.
6. Audit trail: every subscription change, unsubscribe, and suppression event is logged with timestamp and reason.

**Acceptance criteria:**

- Contact clicks unsubscribe link → preference centre opens showing their subscriptions. They can unsubscribe from one list or opt out globally.
- Gmail/Yahoo one-click unsubscribe button works (RFC 8058 header present).
- Hard bounce event from SendGrid → contact is suppressed from all future sends automatically.
- Spam complaint event → contact is globally unsubscribed automatically.
- Admin can view suppression list and see reason/date for each entry.

**What to verify:**

- Send campaign email, inspect headers → List-Unsubscribe and List-Unsubscribe-Post headers present.
- Click unsubscribe link → preference centre loads with correct subscription state.
- Simulate hard bounce webhook → contact marked as suppressed, subsequent campaigns skip them.
- Simulate complaint webhook → contact globally unsubscribed.

---

### Phase 6: Sending Optimisation & Analytics

**Goal:** Smart sending, deliverability management, and engagement analytics.

**Deliverables:**

1. **Send throttling**: configurable rate limiting per campaign/sequence (e.g., max 500 emails per minute). Distribute large sends over time windows.
2. **Timezone-aware sending**: for sequences and campaigns, optionally send at a specific local time for each contact (e.g., "send at 10:00 AM in each contact's timezone").
3. **Quiet hours**: configurable time windows during which no marketing emails are sent (e.g., no sends between 10 PM and 8 AM in the contact's timezone). Emails due during quiet hours are deferred to the next send window.
4. **Engagement tracking**: open tracking (tracking pixel), click tracking (link rewriting through tracking URLs). Store events on per-email-log level.
5. **Campaign analytics**: open rate, click rate, unsubscribe rate, bounce rate. Per-campaign and trend-over-time.
6. **Sequence analytics**: per-step open/click rates, drop-off between steps, completion rate, average time to completion.

**Acceptance criteria:**

- A campaign to 50,000 contacts with 500/min throttle takes ~100 minutes to complete.
- A sequence step set to "10:00 AM contact local time" sends to a UTC+2 contact at 08:00 UTC and a UTC-5 contact at 15:00 UTC.
- Email sent during quiet hours is held and sent at next allowed window.
- Clicking a tracked link records a click event associated with the email log and contact.

---

### Phase 7: Migration of Existing Drip System

**Goal:** Migrate existing EmailGroup/EmailSchedule/ContactEmailSchedule-based drips to the new Sequence engine, then deprecate the old system.

> **This phase is intentionally last.** The old system continues to work throughout Phases 1-6. This phase is about consolidation, not urgency.

**Deliverables:**

1. Data migration tool: converts each EmailGroup (that is being used as a sequence) into a Sequence + Steps, maps ContactEmailSchedule records to Sequence Enrolments, and maps historical EmailLog data to Sequence Deliveries.
2. Validation report: before migration, generate a report showing what will be migrated and any edge cases.
3. Side-by-side verification: run both old and new systems, compare behaviour on test data.
4. Deprecation: once migrated, mark old ContactScheduledEmailTask, EmailSchedule, and ContactEmailSchedule as deprecated. Keep entities in schema for historical queries.

**Acceptance criteria:**

- All active drip sequences are migrated to the new Sequence model.
- Contacts who were mid-sequence continue from where they left off.
- Historical email logs remain accessible.
- Old task can be disabled in configuration without data loss.

---

## 6. Features You May Want to Consider

Based on industry standards and common SaaS marketing needs, here are additional capabilities that are not in your three original requirements but are worth discussing. Let me know which of these, if any, you would like factored into the plan:

1. **A/B testing (split testing)** — Send variant A to 20% of the audience, variant B to 20%, then send the winner to the remaining 60%. Common for campaigns and newsletter subject line optimisation.

2. **Smart resend to non-openers** — Automatically resend a campaign to contacts who did not open the first send, optionally with a different subject line, after a configurable delay (e.g., 3 days).

3. **Send time optimisation (STO)** — Instead of one send time, let the system learn the best time for each contact based on their historical open patterns. This is an advanced feature that platforms like Mailchimp and HubSpot offer.

4. **Dynamic content blocks** — Within a single template, show different content sections to different contacts based on segment membership or contact properties (e.g., show different hero image to "Enterprise" vs "Startup" contacts in the same campaign).

5. **Conditional sequence branching** — Instead of purely linear sequences, allow branching: "If contact opened Step 2, send Step 3A; otherwise, send Step 3B." This turns sequences into automations/workflows. Significantly more complex but very powerful.

6. **Contact scoring / lead scoring** — Assign points for opens, clicks, page visits, etc. Use score thresholds to trigger sequences or segment contacts into "hot lead" / "cold lead" segments. Often a companion feature to email marketing.

7. **Sender domain management and warming** — When using a new sending domain, gradually increase volume to build reputation. The system would manage warming schedules and monitor deliverability metrics.

8. **Multi-channel sequences** — Extend sequences beyond email to include SMS steps, in-app messages, or webhook triggers. Your existing SMS plugin suggests this might be a future direction.

---

## 7. Summary of Entities (All Phases)

| Entity                         | Phase | New/Modified                           |
| ------------------------------ | ----- | -------------------------------------- |
| Campaign                       | 1     | New                                    |
| CampaignRecipient              | 1     | New                                    |
| Sequence                       | 2     | New                                    |
| SequenceStep                   | 2     | New                                    |
| SequenceEnrolment              | 2     | New                                    |
| SequenceDelivery               | 2     | New                                    |
| SequenceStep.EligibilityPolicy | 3     | Modified (add field)                   |
| MailingList                    | 4     | New                                    |
| Subscription                   | 4     | New                                    |
| Unsubscribe (add list-level)   | 4     | Modified (add nullable list reference) |
| Contact suppression state      | 5     | Modified or new                        |
| EmailLog engagement events     | 6     | Modified (add open/click tracking)     |

---

## 8. Risk Considerations

| Risk                                                | Mitigation                                                                                                                                                                          |
| --------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Large send volumes overwhelming SMTP                | Phase 6 introduces throttling. In the interim, use reasonable batch sizes in campaigns.                                                                                             |
| Timezone calculation errors                         | Invest in thorough testing with contacts in diverse timezones. Use UTC internally, convert only at send-time evaluation.                                                            |
| Retroactive step policies corrupting sequence state | Phase 3 includes preview/impact analysis before applying. Eligibility policy is immutable once set on a step.                                                                       |
| Dual system (old drip + new sequence) confusion     | Clear naming in admin UI. "Legacy Sequences" label for old system. Phase 7 provides migration path.                                                                                 |
| GDPR/CAN-SPAM non-compliance                        | Phase 5 addresses compliance comprehensively. Until then, global unsubscribe (already working) provides the minimum legal baseline.                                                 |
| Newsletter without double opt-in                    | Implement double opt-in from day one in Phase 4. Do not allow Active subscription without confirmation for public signups. Admin imports can bypass (with explicit acknowledgment). |
