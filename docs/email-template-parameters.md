# Email Template Parameters

This document describes all email template parameters currently supported by LeadCMS.

## How variables are injected

At render time, variables come from several layers:

1. Contact-based template arguments (default core set)
2. Custom template variables passed by caller/API (override existing keys)
3. UTM parameters (`utm_*`, `utm_query`) when present
4. Site link variables (`site_url`, `unsubscribe_url`, `privacy_url`) injected centrally by Liquid rendering

Variable dictionary keys are case-insensitive.

## 1) Default contact-based parameters (top-level)

These are the core top-level variables added by `TemplateArgumentsBuilder.FromContact(...)`.

### Contact scalar/string parameters

- `{{ Email }}`
- `{{ FirstName }}`
- `{{ LastName }}`
- `{{ FullName }}`
- `{{ MiddleName }}`
- `{{ Prefix }}`
- `{{ Phone }}`
- `{{ JobTitle }}`
- `{{ CompanyName }}`
- `{{ Department }}`
- `{{ CityName }}`
- `{{ State }}`
- `{{ Zip }}`
- `{{ Address1 }}`
- `{{ Address2 }}`
- `{{ Language }}`
- `{{ CountryCode }}`
- `{{ ContinentCode }}`
- `{{ Birthday }}` (format: `yyyy-MM-dd`)
- `{{ Timezone }}`
- `{{ TimezoneFormatted }}`
- `{{ IpAddress }}` (prefers `UpdatedByIp`; falls back to `CreatedByIp`)
- `{{ LastOrderDate }}` (format: `yyyy-MM-dd`)

### Contact numeric/list/map parameters

- `{{ DealsCount }}`
- `{{ OrdersCount }}`
- `{{ TotalRevenue }}`
- `{{ Tags }}` (list of strings)
- `{{ SocialMedia }}` (map/dictionary)

### Flattened account/domain helpers

- `{{ AccountName }}`
- `{{ AccountSiteUrl }}`
- `{{ DomainName }}`

## 2) Nested objects and collections

When nested objects are enabled (default behavior in most sending paths), these variables are also available:

- `{{ Account.* }}`
- `{{ Domain.* }}`
- `{% for order in Orders %} ... {% endfor %}`
- `{% for deal in Deals %} ... {% endfor %}`

`Orders` and `Deals` are sorted newest-first by `UpdatedAt ?? CreatedAt`.

Examples:

- `{{ Account.Name }}`
- `{{ Domain.Url }}`
- `{% for order in Orders limit:1 %}{{ order.RefNo }}{% endfor %}`
- `{% for item in order.OrderItems %}{{ item.ProductName }} × {{ item.Quantity }}{% endfor %}`

## 3) UTM parameters

When UTM data is provided for the sending context, these are available:

- `{{ utm_source }}`
- `{{ utm_medium }}`
- `{{ utm_campaign }}`
- `{{ utm_content }}`
- `{{ utm_term }}`
- `{{ utm_id }}`
- `{{ utm_query }}` (pre-built query string)

Typical usage:

- `https://example.com/page?{{ utm_query }}`
- `https://example.com/page?existing=1&{{ utm_query }}`

## 4) Site link parameters (centrally injected)

These are injected in the central Liquid renderer (`LiquidTemplateService`) for every template render:

- `{{ site_url }}`
- `{{ unsubscribe_url }}`
- `{{ privacy_url }}`

### Resolution order and fallbacks

`site_url`:

1. DB setting: `General.SiteUrl`
2. Appsettings: `General.SiteUrl`
3. Appsettings top-level: `SiteUrl`

`unsubscribe_url`:

1. DB setting: `General.UnsubscribeUrl`
2. Appsettings: `General.UnsubscribeUrl`
3. If still empty and `site_url` exists: `${site_url}/unsubscribe`

`privacy_url`:

1. DB setting: `General.PrivacyUrl`
2. Appsettings: `General.PrivacyUrl`
3. If still empty and `site_url` exists: `${site_url}/privacy`

## 5) Custom template variables

Callers can pass custom variables (for example via API request `TemplateVariables` / custom template args).
These are merged into the same dictionary and override existing keys with the same name.

## 6) Liquid syntax and legacy placeholders

Supported Liquid patterns:

- `{{ variable }}`
- `{% if condition %} ... {% endif %}`
- `{% unless condition %} ... {% endunless %}`
- `{% for item in list %} ... {% endfor %}`

Legacy placeholders are normalized automatically before render:

- `<%VarName%>` -> `{{ VarName }}`
- `&lt;%VarName%&gt;` -> `{{ VarName }}`
- `${VarName}` -> `{{ VarName }}`

---

If you need, this doc can be extended with a generated exhaustive list of common nested fields (`Account`, `Domain`, `Order`, `OrderItem`, `Deal`) used in templates.
