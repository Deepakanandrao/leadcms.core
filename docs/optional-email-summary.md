# Optional Contact Email — Implementation Summary & Plugin Developer Guide

## Overview

`Contact.Email` is now **nullable** across the entire LeadCMS core and all first-party plugins. Contacts can exist with only a phone number, only an email, or neither. This document summarizes all changes made and provides guidance for plugin developers.

---

## Summary of Changes

### Phase 1 — Data Model & Database

| Area                     | Change                                                                                                                                      |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `Contact.Email`          | `string` → `string?` (nullable)                                                                                                             |
| `Contact.DomainId`       | `int` → `int?` (nullable)                                                                                                                   |
| `Contact.PhoneRaw`       | New field — stores original phone input when E.164 normalization fails                                                                      |
| `Contact.PendingUpdates` | New `List<PendingContactUpdate>?` JSONB column for admin-reviewable field conflicts                                                         |
| `PendingContactUpdate`   | New value object (Field, OldValue, ProposedValue, Source, Ip, UserAgent, CreatedAt)                                                         |
| Unique indexes           | Replaced `[Index(IsUnique)]` with **partial unique indexes**: `WHERE email IS NOT NULL` and `WHERE phone IS NOT NULL`                       |
| Import identity          | Added `[SurrogateIdentity(nameof(Email))]` to `Contact` for upsert-by-email during CSV/JSON import                                          |
| DTOs                     | `ContactCreateDto.Email`, `ContactImportDto.Email` — already nullable; `ContactDetailsDto` exposes `PendingUpdates`                         |
| Migration                | `MakeContactEmailAndDomainOptional_AddPhoneRawAndPendingUpdates` — includes `UPDATE contact SET email = NULL WHERE email = ''` data cleanup |

### Phase 2 — Phone Normalization

| Area                                      | Change                                                                                                                    |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `IPhoneNormalizationService`              | New interface — `Normalize(rawPhone, countryCode?, language?)` returns E.164 or `null`                                    |
| `PhoneNormalizationService`               | 4-strategy cascade: as-is → country → language → default region from `SupportedLanguages[0]` config; hard fallback `"US"` |
| `ContactService.SaveAsync/SaveRangeAsync` | Auto-normalizes phone on every save; failed normalizations move value to `PhoneRaw`                                       |
| `ContactService.FindOrCreateByPhone`      | New method — finds by normalized phone or raw phone fallback; creates phone-only contact if not found                     |
| Registration                              | Registered as singleton in `Program.cs`                                                                                   |
| Dependency                                | `libphonenumber-csharp 9.0.25` added to core; SMS plugin upgraded from `8.13.52`                                          |

### Phase 3 — Email Behavior Hardening

| Area                                    | Change                                                                                          |
| --------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `ContactScheduledEmailTask`             | Skips contacts without email (logs warning, marks `Failed`); null-safe `Timezone ?? 0`          |
| `SendgridController`                    | Deduplication changed from `e.Contact!.Email` to `e.ContactId` (works for null-email contacts)  |
| `DefaultLeadNotificationMessageBuilder` | `leadInfo.Email ?? string.Empty` in template args; conditional email line in text notifications |
| `SubscribeController`                   | Defensive null-email guards before confirmation/unsubscribe email sends                         |

### Phase 4 — Anti-Abuse Merge Policy

| Area                  | Change                                                                                                           |
| --------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `ContactMergeHelper`  | New static helper — `ApplyPublicUpdate(contact, fieldName, currentValue, proposedValue, setter, source, ip, ua)` |
| Policy                | **Fill-only-if-null**: empty field → apply directly; same value → skip; conflict → append to `PendingUpdates`    |
| `ContactUsController` | `FirstName`, `LastName`, `CompanyName`, `Source` use merge policy instead of direct assignment                   |
| `SubscribeController` | `Source = "Subscribed"` uses merge policy                                                                        |

### Phase 5 — Validation, Code Quality & Tests

| Area                                      | Change                                                                                    |
| ----------------------------------------- | ----------------------------------------------------------------------------------------- |
| `CampaignService.ProcessRecipientsAsync`  | Defensive null-email guard before send; prevents NRE race condition                       |
| `CommentService.EnrichWithContactIdAsync` | Guarded `Email!` dictionary key against null                                              |
| `GravatarHelper`                          | Replaced `MD5.Create().ComputeHash()` with `MD5.HashData()` (no undisposed `IDisposable`) |
| `ContactService.FindOrCreate`             | Added `ArgumentException.ThrowIfNullOrWhiteSpace(email)` guard                            |
| `LeadNotificationInfo.Email`              | Changed from `string` to `string?` to match nullable-email design                         |
| `ContactImportDto` email setter           | Unified to `value?.ToLower()` for consistency                                             |
| `ContactUsDto` / `SubscribeDto`           | Null-safe email setters: `value?.ToLower() ?? string.Empty`                               |
| New integration tests                     | Phone-only contact creation, phone→email lifecycle, mixed import, bare-minimum contact    |
| New test data                             | `contacts_mixed.json` — phone-only + email+phone entries                                  |
| Test count                                | 821 total (819 passed, 2 skipped) — all green                                             |

---

## Plugin Developer Guide

### Key Principle

**`Contact.Email` can be `null`.** Always check before using it.

### Rules for Plugin Code

#### 1. Never assume `Contact.Email` is non-null

```csharp
// ❌ BAD — will throw NullReferenceException
await emailService.SendAsync(template, language, new[] { contact.Email! }, args, null);

// ✅ GOOD — guard first
if (!string.IsNullOrWhiteSpace(contact.Email))
{
    await emailService.SendAsync(template, language, new[] { contact.Email }, args, null);
}
```

#### 2. Use `ContactId` for deduplication, not `Email`

```csharp
// ❌ BAD — fails for null-email contacts
var unique = records.GroupBy(r => r.Contact!.Email);

// ✅ GOOD
var unique = records.GroupBy(r => r.ContactId);
```

#### 3. LINQ queries must filter null emails explicitly

```csharp
// ❌ BAD — emails.Contains(null) produces unexpected SQL
var contacts = dbContext.Contacts!.Where(c => emails.Contains(c.Email));

// ✅ GOOD
var contacts = dbContext.Contacts!.Where(c => c.Email != null && emails.Contains(c.Email));
```

#### 4. Dictionary keys from `Email` need guards

```csharp
// ❌ BAD — null key crash
existingContacts[contact.Email!] = contact;

// ✅ GOOD
if (!string.IsNullOrWhiteSpace(contact.Email))
{
    existingContacts[contact.Email] = contact;
}
```

#### 5. Use `ContactMergeHelper` for public/untrusted form submissions

When updating contact fields from public endpoints (contact forms, subscribe widgets, webhooks), use the anti-abuse merge policy instead of direct assignment:

```csharp
using LeadCMS.Helpers;

// ❌ BAD — overwrites existing data silently
contact.FirstName = formDto.FirstName;
contact.Source = "MyPlugin";

// ✅ GOOD — fills if empty, queues conflict for admin review
ContactMergeHelper.ApplyPublicUpdate(
    contact,
    nameof(contact.FirstName),
    contact.FirstName,
    formDto.FirstName,
    v => contact.FirstName = v,
    "MyPlugin",        // source identifier
    ipAddress,         // nullable
    userAgent);        // nullable
```

#### 6. Phone normalization is automatic

When saving contacts via `IContactService.SaveAsync()`, phone numbers are automatically normalized to E.164 format. If normalization fails, the original value is stored in `PhoneRaw` and `Phone` is set to `null`. No plugin action is needed.

#### 7. Use `FindOrCreateByPhone` for phone-only leads

```csharp
// For leads captured by phone (e.g., call tracking, SMS):
var contact = await contactService.FindOrCreateByPhone(phoneNumber, language, timezone);
```

This method:

- Normalizes the phone number to E.164
- Searches by normalized phone, then falls back to raw phone
- Creates a new contact without email if no match is found
- Domain enrichment is automatically skipped for email-less contacts

#### 8. `LeadNotificationInfo.Email` is nullable

If your plugin builds `LeadNotificationInfo` objects, `Email` is now `string?`. The `DefaultLeadNotificationMessageBuilder` already handles null email gracefully in both email templates and text notifications.

#### 9. Never use try/catch in controllers

All exceptions are handled centrally by `ErrorsController`. If you need a new HTTP status mapping for a custom exception, add a `case` in `ErrorsController.HandleError()`.

### Migration Checklist for Existing Plugins

- [ ] Search your plugin code for `.Email` property access on `Contact` — add null checks
- [ ] Search for `.Email!` null-forgiving operators — verify each is safe or add a guard
- [ ] Replace direct field assignments on contacts from public endpoints with `ContactMergeHelper.ApplyPublicUpdate`
- [ ] If you create contacts, decide whether to use `FindOrCreate` (requires email) or `FindOrCreateByPhone` (phone-only)
- [ ] If you send emails, always check `!string.IsNullOrWhiteSpace(contact.Email)` before sending
- [ ] If you use `ToDictionary` or `GroupBy` on `Email`, add `!= null` filter
- [ ] Update any email DTO setters to use `value?.ToLower()` for null safety
