# Email Sequence Visual Editor — Component Spec

Date: 2026-03-12

---

## 1. Purpose

This document describes only the visual component used to edit an actual email sequence.

It is not a full sequence-management spec. It only defines the main capabilities the sequence editor component itself must provide.

---

## 2. What the Component Is

The sequence editor is a visual canvas or ordered flow view that lets a user see and edit a sequence of email steps.

It should make the sequence understandable at a glance:

- what steps exist,
- in what order they run,
- what email template each step uses,
- when each step is sent.

The component should feel like a journey editor, not like a raw form or table.

---

## 3. Recommended Visual Model

The component should use a vertical flow of step cards connected in sequence order.

Each step card should show:

- step number,
- step name or label,
- selected email template,
- timing summary,
- quick actions.

Between steps, the component should show the delay or timing relationship in a human-readable way, for example:

- Immediately
- After 2 days
- After 5 days at 10:00 local time

---

## 4. Main Features the Editor Must Have

### 4.1 Clear ordered sequence view

The user must always be able to understand the exact order of steps.

The component must make order obvious without requiring the user to open each step.

### 4.2 Add step

The component must allow adding a new step:

- at the end of the sequence,
- between existing steps.

The add action should be visible and easy to discover.

### 4.3 Edit step

The component must allow editing the selected step.

At minimum, a step must expose:

- step label,
- linked email template,
- timing or delay settings.

Editing can happen in a side panel, drawer, or inline detail area.

### 4.4 Reorder steps

The component must support drag-and-drop reordering of steps.

When a step is moved:

- the new order must be visually clear immediately,
- the user must understand that the journey changed.

### 4.5 Delete step

The component must allow step removal with confirmation.

Deletion should be easy, but not too easy to trigger accidentally.

### 4.6 Duplicate step

The component should allow duplicating a step so a user can quickly create similar follow-ups.

### 4.7 Timing visibility

The component must show timing directly in the flow, not hide it inside forms only.

Timing is one of the most important things users need to understand when looking at a sequence.

### 4.8 Template visibility

The component must clearly show which email template is attached to each step.

If a step has no template yet, that should be visually obvious.

### 4.9 Validation state

The component must show whether a step is complete or incomplete.

Examples:

- missing template,
- invalid timing,
- unresolved warning.

The user should be able to identify broken steps without opening every card.

### 4.10 Safe editing for live sequences

If the sequence is already active, the component must warn the user when a change is likely to affect contacts already in progress.

This is especially important for:

- inserting a new step,
- deleting a step,
- reordering steps,
- changing timing.

The warning does not need to be complex inside the component itself, but the component must clearly surface that the edit is impactful.

---

## 5. Step Card Requirements

Each step card should support these visible elements:

- step index,
- step title,
- template name,
- timing summary,
- status marker,
- action menu.

The step card should support these actions:

- select,
- edit,
- duplicate,
- delete,
- drag to reorder,
- insert after.

---

## 6. UX Requirements

The component should optimize for fast comprehension.

That means:

- sequence order must be visible at all times,
- timing must be readable without extra clicks,
- incomplete steps must stand out,
- adding and moving steps must feel simple,
- the component should remain usable even for longer sequences.

The component should be desktop-first. Mobile support can focus on viewing and light editing rather than full sequence construction.

---

## 7. Minimal Acceptance Criteria

The component is good enough for the first version if a user can:

- see the full sequence in order,
- add a new step,
- edit a step's template and timing,
- reorder steps visually,
- remove or duplicate a step,
- immediately see which steps are incomplete,
- understand when an edit to a live sequence is risky.

---

## 8. Summary

The sequence editor component should be a simple visual journey builder for linear email sequences.

Its main job is to make sequence structure obvious and editing safe. The essential capabilities are: visible ordered steps, template assignment, timing display and editing, drag-and-drop reordering, and clear warnings for impactful changes.