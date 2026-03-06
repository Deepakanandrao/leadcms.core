// <copyright file="LiquidTemplateService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;
using LeadCMS.Configuration;
using LeadCMS.Constants;
using LeadCMS.Interfaces;

namespace LeadCMS.Services;

/// <summary>
/// Renders Liquid templates (Fluid dialect) with runtime variable substitution.
/// Before rendering, legacy placeholder formats (<c>&lt;%var%&gt;</c>, <c>${var}</c>)
/// are normalised to standard <c>{{ var }}</c> Liquid syntax for backwards compatibility.
/// </summary>
public class LiquidTemplateService : ILiquidTemplateService
{
    private static readonly FluidParser Parser = new();

    // Matches <%varName%>
    private static readonly Regex AngleBracketPattern =
        new(@"<%([^%]+)%>", RegexOptions.Compiled);

    // Matches &lt;%varName%&gt; (HTML-encoded angle-bracket tokens)
    private static readonly Regex AngleBracketHtmlEncodedPattern =
        new(@"&lt;%([^%]+)%&gt;", RegexOptions.Compiled);

    // Matches ${varName}
    private static readonly Regex DollarBracePattern =
        new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    private readonly ISettingService settingService;
    private readonly IConfiguration configuration;

    public LiquidTemplateService(ISettingService settingService, IConfiguration configuration)
    {
        this.settingService = settingService;
        this.configuration = configuration;
    }

    /// <inheritdoc/>
    public async Task<string> RenderAsync(string template, Dictionary<string, object>? variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var effectiveVariables = await EnsureSiteLinksAsync(variables);

        var normalised = NormalisePlaceholders(template);

        if (!Parser.TryParse(normalised, out var fluidTemplate, out var parseError))
        {
            Log.Warning("Failed to parse Liquid template: {Error}. Returning template as-is.", parseError);
            return normalised;
        }

        var options = new TemplateOptions();
        options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();

        var context = new TemplateContext(options);

        if (effectiveVariables != null)
        {
            foreach (var kv in effectiveVariables)
            {
                if (kv.Value is string strValue)
                {
                    if (string.IsNullOrEmpty(strValue))
                    {
                        continue;
                    }

                    var htmlSafeValue = strValue.Replace("\n", "<br />");
                    context.SetValue(kv.Key, new StringValue(htmlSafeValue));
                }
                else
                {
                    context.SetValue(kv.Key, FluidValue.Create(kv.Value, options));
                }
            }
        }

        try
        {
            return await fluidTemplate.RenderAsync(context);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to render Liquid template. Returning template as-is.");
            return normalised;
        }
    }

    private async Task<Dictionary<string, object>?> EnsureSiteLinksAsync(Dictionary<string, object>? variables)
    {
        var effectiveVariables = variables ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var siteLinks = await BuildSiteLinksConfigAsync();

        SetIfMissing(effectiveVariables, "site_url", siteLinks.SiteUrl);
        SetIfMissing(effectiveVariables, "unsubscribe_url", siteLinks.UnsubscribeUrl);
        SetIfMissing(effectiveVariables, "privacy_url", siteLinks.PrivacyUrl);

        return effectiveVariables;
    }

    private void SetIfMissing(Dictionary<string, object> variables, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var existingKey = GetKeyIgnoreCase(variables, key);
        if (existingKey != null)
        {
            if (!IsNullOrWhiteSpaceValue(variables[existingKey]))
            {
                return;
            }

            variables[existingKey] = value;
            return;
        }

        variables[key] = value;
    }

    private string? GetKeyIgnoreCase(Dictionary<string, object> variables, string key)
    {
        return variables.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsNullOrWhiteSpaceValue(object? value)
    {
        if (value == null)
        {
            return true;
        }

        if (value is string stringValue)
        {
            return string.IsNullOrWhiteSpace(stringValue);
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => true,
                JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()),
                _ => false,
            };
        }

        return false;
    }

    private async Task<SiteLinksConfig> BuildSiteLinksConfigAsync()
    {
        var siteUrl = await settingService.GetSystemSettingAsync(SettingKeys.GeneralSiteUrl);
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            siteUrl = configuration["General:SiteUrl"];
        }

        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            siteUrl = configuration["SiteUrl"];
        }

        var unsubscribeUrl = await settingService.GetSystemSettingAsync(SettingKeys.GeneralUnsubscribeUrl);
        if (string.IsNullOrWhiteSpace(unsubscribeUrl))
        {
            unsubscribeUrl = configuration["General:UnsubscribeUrl"];
        }

        var privacyUrl = await settingService.GetSystemSettingAsync(SettingKeys.GeneralPrivacyUrl);
        if (string.IsNullOrWhiteSpace(privacyUrl))
        {
            privacyUrl = configuration["General:PrivacyUrl"];
        }

        var normalizedSiteUrl = string.IsNullOrWhiteSpace(siteUrl)
            ? string.Empty
            : siteUrl.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(unsubscribeUrl) && !string.IsNullOrWhiteSpace(normalizedSiteUrl))
        {
            unsubscribeUrl = $"{normalizedSiteUrl}/unsubscribe";
        }

        if (string.IsNullOrWhiteSpace(privacyUrl) && !string.IsNullOrWhiteSpace(normalizedSiteUrl))
        {
            privacyUrl = $"{normalizedSiteUrl}/privacy";
        }

        return new SiteLinksConfig
        {
            SiteUrl = normalizedSiteUrl,
            UnsubscribeUrl = string.IsNullOrWhiteSpace(unsubscribeUrl) ? string.Empty : unsubscribeUrl.TrimEnd('/'),
            PrivacyUrl = string.IsNullOrWhiteSpace(privacyUrl) ? string.Empty : privacyUrl.TrimEnd('/'),
        };
    }

    private string NormalisePlaceholders(string template)
    {
        var result = AngleBracketPattern.Replace(template, "{{ $1 }}");
        result = AngleBracketHtmlEncodedPattern.Replace(result, "{{ $1 }}");
        result = DollarBracePattern.Replace(result, "{{ $1 }}");
        return result;
    }
}
