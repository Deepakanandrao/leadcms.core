// <copyright file="LiquidTemplateService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;
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

    /// <inheritdoc/>
    public async Task<string> RenderAsync(string template, Dictionary<string, object>? variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var normalised = NormalisePlaceholders(template);

        if (!Parser.TryParse(normalised, out var fluidTemplate, out var parseError))
        {
            Log.Warning("Failed to parse Liquid template: {Error}. Returning template as-is.", parseError);
            return normalised;
        }

        var options = new TemplateOptions();
        options.MemberAccessStrategy = new UnsafeMemberAccessStrategy();

        var context = new TemplateContext(options);

        if (variables != null)
        {
            foreach (var kv in variables)
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

    private static string NormalisePlaceholders(string template)
    {
        var result = AngleBracketPattern.Replace(template, "{{ $1 }}");
        result = AngleBracketHtmlEncodedPattern.Replace(result, "{{ $1 }}");
        result = DollarBracePattern.Replace(result, "{{ $1 }}");
        return result;
    }
}
