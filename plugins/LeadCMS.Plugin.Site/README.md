# Site Plugin Documentation

## Overview

The Site Plugin provides essential website functionality for LeadCMS, including contact form processing, newsletter subscription management, and lead capture capabilities. This plugin serves as the foundation for website-based lead generation and customer interaction.

## Purpose

- **Contact Form Processing**: Handle contact form submissions from website visitors
- **Lead Capture**: Automatically create lead records from website interactions
- **Newsletter Management**: Process newsletter subscription requests
- **Customer Communication**: Send automated acknowledgment and follow-up emails
- **Website Integration**: Provide seamless integration between website and CRM

## Key Features

### Contact Form Management

- Process contact form submissions with file attachments
- Automatic lead creation and enhancement from form data
- Multi-language support for international websites
- Spam protection and form validation
- Automated email notifications to sales/support teams

### Lead Generation

- **Automatic Lead Creation**: Convert website visitors into lead records
- **Data Enrichment**: Populate lead fields from form submissions
- **Source Attribution**: Track leads from "Contact Us" source
- **Time Zone Handling**: Capture visitor time zone information
- **Lead Scoring**: Initial lead scoring based on form interactions

### Email Integration

- **Acknowledgment Emails**: Send confirmation emails to form submitters
- **Internal Notifications**: Alert sales/support teams of new inquiries
- **Template-Based Emails**: Use customizable email templates
- **Multi-language Support**: Send emails in visitor's preferred language
- **Attachment Handling**: Process and forward file attachments

### Newsletter Subscription

- Newsletter signup form processing
- Subscription management and preferences
- Double opt-in confirmation workflows
- Unsubscribe handling and compliance
- Subscriber segmentation and tagging

## Configuration

### Environment Variables

```bash
# Site Configuration
SITE__SITEURL=https://your-domain.com
SITE__SUPPORTEMAIL=support@your-domain.com

# Contact Form Recipients
CONTACTUS__TO__0=sales@your-domain.com
CONTACTUS__TO__1=support@your-domain.com
CONTACTUS__TO__2=info@your-domain.com
```

### Plugin Settings

The plugin configuration includes website settings and contact form configuration:

```json
{
  "SiteUrl": "https://leadcms.ai",
  "ContactUs": {
    "To": ["$CONTACTUS__TO__0"]
  },
  "SupportEmail": "support@leadcms.ai",
  "RecaptchaSecretKey": "$RECAPTCHA_SECRET_KEY"
}
```

Set the `RecaptchaSecretKey` to your Google reCAPTCHA secret to enable automated spam protection. If this value is left as the placeholder or blank, reCAPTCHA is not enforced.

### Lead Capture Settings (Database)

Lead Capture settings are stored in the database and can be configured via the Settings API. These settings allow you to configure notifications when a new lead is captured through the contact form.

| Setting Key                    | Type       | Default | Description                                                                          |
| ------------------------------ | ---------- | ------- | ------------------------------------------------------------------------------------ |
| `LeadCapture.Email.Enabled`    | boolean    | `true`  | Enable/disable email notifications                                                   |
| `LeadCapture.Email.Recipients` | JSON array | `[]`    | Email addresses to receive lead notifications. Falls back to `ContactUs.To` if empty |
| `LeadCapture.Telegram.Enabled` | boolean    | `false` | Enable/disable Telegram notifications                                                |
| `LeadCapture.Telegram.BotId`   | string     | `""`    | Your Telegram bot ID (create via [@BotFather](https://t.me/botfather))               |
| `LeadCapture.Telegram.ChatId`  | string     | `""`    | The Telegram chat ID to send notifications to                                        |
| `LeadCapture.Slack.Enabled`    | boolean    | `false` | Enable/disable Slack notifications                                                   |
| `LeadCapture.Slack.WebhookUrl` | string     | `""`    | Your Slack incoming webhook URL                                                      |

#### Configuring Lead Capture via API

Use the Settings API to configure lead capture settings:

```http
POST /api/settings
Content-Type: application/json

{
  "key": "LeadCapture.Email.Enabled",
  "value": "true"
}
```

For email recipients, use a JSON array:

```http
POST /api/settings
Content-Type: application/json

{
  "key": "LeadCapture.Email.Recipients",
  "value": "[\"leads@example.com\", \"sales@example.com\"]"
}
```

#### Notification Message Format

When a lead is captured, the notification message includes all available lead information:

```
🥹 New demo request
✔️ Name: Emily Lauren,
✔️ Phone: 15154271358,
✔️ Company: Acme Corp,
✔️ Email: emily@example.com,
✔️ Subject: Product inquiry,
✔️ Message: I'm interested in learning more...
```

## Capabilities

This plugin provides the `Site` capability, which can be queried via the capabilities API. When this capability is present, clients can display and configure the Lead Capture settings.

## Reuse in Custom Plugins

The Site plugin now exposes reusable APIs so your custom plugin can reference it as a package and pick only required pieces.

### Reuse DTOs

Use DTOs from `LeadCMS.Plugin.Site.DTOs` directly (for example `LeadNotificationInfo`, `ContactUsDto`, `SubscribeDto`).

### Reuse and Customize Lead Notifications

`LeadNotificationService` now depends on `ILeadNotificationMessageBuilder`.

- Default implementation: `DefaultLeadNotificationMessageBuilder`
- Override message format (Telegram/Slack/email args): register your own `ILeadNotificationMessageBuilder`

Example registration:

```csharp
services.AddSiteLeadNotificationServices();
services.AddScoped<ILeadNotificationMessageBuilder, MyCustomLeadNotificationMessageBuilder>();
```

### Reuse Only Selected Setting Definitions

Use `LeadCaptureSettingDefinitions`:

- `LeadCaptureSettingDefinitions.All`
- `LeadCaptureSettingDefinitions.ForKeys(...)`
- `LeadCaptureSettingDefinitions.TryGet(...)`

This allows custom `ISettingsProvider` implementations to register only a subset of Site lead-capture settings.

### Reuse Service Registrations Selectively

Service registration helpers:

- `AddSitePluginSettingsAccessor(configuration)`
- `AddSiteLeadNotificationServices()`
- `AddSiteSubscriptionTokenService()`
- `AddSiteCoreServices(configuration)`

### Reuse / Inherit Controllers

`ContactUsController` and `SubscribesController` actions are virtual and key hooks are protected virtual, so custom plugins can inherit and override behavior while reusing base flow.

`SubscribesController` no longer depends on static `SitePlugin.Settings`; it uses `ISitePluginSettingsAccessor`, which can be replaced in custom plugins.

## API Endpoints

### Contact Form Processing

- **POST `/api/contact-us`** - Process contact form submission
  - **Input**: Form data including name, email, message, and optional attachment
  - **Output**: Confirmation of form submission and lead creation
  - **Features**:
    - Automatic lead creation/enhancement
    - File attachment processing
    - Email notifications to configured recipients
    - Acknowledgment email to form submitter

### Newsletter Subscription

- **POST `/api/subscribe`** - Process newsletter subscription
  - **Input**: Email address, preferences, and opt-in confirmation
  - **Output**: Subscription confirmation and preferences
  - **Features**:
    - Subscription preference management
    - Double opt-in workflow support
    - Automatic unsubscribe link generation
    - Subscriber segmentation

## Contact Form Processing

### Form Data Handling

The contact form endpoint processes the following data:

**Required Fields:**

- **Email**: Contact email address (used for lead creation)
- **Message**: Contact message content
- **Language**: Preferred language for responses

**Optional Fields:**

- **FirstName**: Contact's first name
- **LastName**: Contact's last name
- **Company**: Company or organization name
- **Subject**: Message subject line
- **TimeZoneOffset**: Visitor's time zone offset
- **Attachment**: File attachment (documents, images, etc.)
- **RecaptchaToken**: reCAPTCHA token (included if spam protection is enabled)

## Use Cases

### Lead Generation

- **Website Inquiries**: Capture leads from contact forms
- **Product Interest**: Track interest in specific products/services
- **Demo Requests**: Handle requests for product demonstrations
- **Quote Requests**: Process requests for pricing and quotes
- **Support Requests**: Channel support inquiries to appropriate teams

### Customer Service

- **General Inquiries**: Handle general customer questions
- **Technical Support**: Process technical support requests
- **Billing Questions**: Route billing and payment inquiries
- **Feedback Collection**: Gather customer feedback and suggestions
- **Complaint Handling**: Process and track customer complaints

### Marketing and Communication

- **Newsletter Signups**: Build email marketing lists
- **Event Registration**: Collect event registration information
- **Content Downloads**: Gate content downloads with lead capture
- **Webinar Registration**: Process webinar and event signups
- **Survey Participation**: Collect survey responses and feedback

### Sales Support

- **Lead Qualification**: Initial lead qualification through form data
- **Sales Inquiries**: Route sales inquiries to appropriate team members
- **Proposal Requests**: Handle requests for proposals and quotes
- **Partnership Inquiries**: Process partnership and collaboration requests
- **Vendor Applications**: Handle vendor and supplier applications

## Email Template Integration

### Template Requirements

The plugin relies on email templates configured in LeadCMS:

**Contact_Us Template:**

- Used for internal notifications to sales/support teams
- Should include all form data variables
- Support for file attachments
- Multi-language support

**Acknowledgment Template:**

- Used for confirmation emails to form submitters
- Should include personalization (first name)
- Professional and welcoming tone
- Clear next steps or expectations

### Template Variables

**Available Variables for Contact_Us Template:**

- `{fromEmail}` - Submitter's email address
- `{firstName}` - Submitter's first name
- `{lastName}` - Submitter's last name
- `{company}` - Company or organization
- `{subject}` - Message subject
- `{message}` - Message content

**Available Variables for Acknowledgment Template:**

- `{firstName}` - Submitter's first name
- Additional variables as configured in template

---

This plugin provides the foundation for website-based lead generation and customer interaction, seamlessly integrating website visitors into the LeadCMS lead management system while maintaining professional communication standards and regulatory compliance.
