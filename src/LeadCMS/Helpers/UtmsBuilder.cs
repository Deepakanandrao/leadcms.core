// <copyright file="UtmsBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Models;

namespace LeadCMS.Helpers;

/// <summary>
/// Fluent builder that assembles <see cref="Utms"/> using a three-layer override model:
///
/// <list type="number">
///   <item><description><b>Defaults</b> — baseline values applied to every outgoing email
///   (e.g. <c>utm_source=leadcms</c>, <c>utm_medium=email</c>).</description></item>
///   <item><description><b>Context</b> — values supplied by the calling business-logic layer
///   (campaign service, scheduled-send task, notification service) that describe the current
///   sending context (e.g. <c>utm_campaign=spring_sale_2026</c>).</description></item>
///   <item><description><b>Overrides</b> — explicit values set by the end-user, an API caller,
///   or plugin configuration that always trump lower layers.</description></item>
/// </list>
///
/// <para>
/// Each layer is optional. When multiple layers set the same property the highest-priority
/// non-null / non-empty value wins:
/// <c>Overrides &gt; Context &gt; Defaults</c>.
/// </para>
///
/// <para>Usage example:</para>
/// <code>
/// var utm = UtmsBuilder.Create()
///     .WithDefaults()
///     .WithContext(new Utms { Campaign = "onboarding_day_3" })
///     .WithOverrides(userProvidedUtm)
///     .Build();
/// </code>
/// </summary>
public class UtmsBuilder
{
    /// <summary>Default value used for <c>utm_source</c> when no higher-layer override is set.</summary>
    public const string DefaultSource = "leadcms";

    /// <summary>Default value used for <c>utm_medium</c> when no higher-layer override is set.</summary>
    public const string DefaultMedium = "email";

    private Utms defaults = new();
    private Utms? context;
    private Utms? overrides;

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    public static UtmsBuilder Create() => new();

    /// <summary>
    /// Merges two <see cref="Utms"/> instances. Non-null/non-empty values in
    /// <paramref name="higher"/> override corresponding values in <paramref name="lower"/>.
    /// Returns a new instance; neither input is mutated.
    /// </summary>
    /// <param name="lower">The base layer (lower priority).</param>
    /// <param name="higher">The override layer (higher priority).</param>
    public static Utms Merge(Utms? lower, Utms? higher)
    {
        if (lower == null && higher == null)
        {
            return new Utms();
        }

        if (lower == null)
        {
            return Clone(higher!);
        }

        var result = Clone(lower);
        ApplyLayer(result, higher);
        return result;
    }

    /// <summary>
    /// Extracts <see cref="Utms"/> from a template-arguments dictionary
    /// by reading the well-known <c>utm_*</c> keys. Returns <c>null</c> when no
    /// UTM keys are present.
    /// </summary>
    /// <param name="templateArguments">The template arguments dictionary to extract from.</param>
    public static Utms? FromDictionary(Dictionary<string, object>? templateArguments)
    {
        if (templateArguments == null || templateArguments.Count == 0)
        {
            return null;
        }

        var utm = new Utms();
        bool found = false;

        if (templateArguments.TryGetValue("utm_source", out var source) && source is string s && !string.IsNullOrWhiteSpace(s))
        {
            utm.Source = s;
            found = true;
        }

        if (templateArguments.TryGetValue("utm_medium", out var medium) && medium is string m && !string.IsNullOrWhiteSpace(m))
        {
            utm.Medium = m;
            found = true;
        }

        if (templateArguments.TryGetValue("utm_campaign", out var campaign) && campaign is string c && !string.IsNullOrWhiteSpace(c))
        {
            utm.Campaign = c;
            found = true;
        }

        if (templateArguments.TryGetValue("utm_content", out var content) && content is string ct && !string.IsNullOrWhiteSpace(ct))
        {
            utm.Content = ct;
            found = true;
        }

        if (templateArguments.TryGetValue("utm_term", out var term) && term is string t && !string.IsNullOrWhiteSpace(t))
        {
            utm.Term = t;
            found = true;
        }

        if (templateArguments.TryGetValue("utm_id", out var id) && id is string i && !string.IsNullOrWhiteSpace(i))
        {
            utm.Id = i;
            found = true;
        }

        return found ? utm : null;
    }

    /// <summary>
    /// Applies the standard baseline defaults (<c>utm_source=leadcms</c>, <c>utm_medium=email</c>).
    /// Call this first to ensure every email carries at least these values.
    /// </summary>
    /// <param name="source">The default <c>utm_source</c> value. Defaults to <c>leadcms</c>.</param>
    /// <param name="medium">The default <c>utm_medium</c> value. Defaults to <c>email</c>.</param>
    public UtmsBuilder WithDefaults(string source = DefaultSource, string medium = DefaultMedium)
    {
        defaults = new Utms
        {
            Source = source,
            Medium = medium,
        };
        return this;
    }

    /// <summary>
    /// Sets context-layer UTM values supplied by the calling business-logic layer.
    /// Non-null/non-empty properties in <paramref name="contextParams"/> override the defaults.
    /// </summary>
    /// <param name="contextParams">
    /// Context-specific UTM parameters, or <c>null</c> to skip this layer.
    /// </param>
    public UtmsBuilder WithContext(Utms? contextParams)
    {
        context = contextParams;
        return this;
    }

    /// <summary>
    /// Sets the highest-priority override values. These come from user input, API callers,
    /// or plugin configuration and always win over defaults and context values.
    /// </summary>
    /// <param name="overrideParams">
    /// User-supplied UTM parameter overrides, or <c>null</c> to skip this layer.
    /// </param>
    public UtmsBuilder WithOverrides(Utms? overrideParams)
    {
        overrides = overrideParams;
        return this;
    }

    /// <summary>
    /// Merges all layers (defaults ← context ← overrides) and returns the resulting
    /// <see cref="Utms"/>. Higher layers win when they supply a non-empty value
    /// for the same property.
    /// </summary>
    public Utms Build()
    {
        var result = new Utms
        {
            Source = defaults.Source,
            Medium = defaults.Medium,
            Campaign = defaults.Campaign,
            Content = defaults.Content,
            Term = defaults.Term,
            Id = defaults.Id,
        };

        ApplyLayer(result, context);
        ApplyLayer(result, overrides);

        result.Source = SlugifyUtmValue(result.Source);
        result.Medium = SlugifyUtmValue(result.Medium);
        result.Campaign = SlugifyUtmValue(result.Campaign);
        result.Content = SlugifyUtmValue(result.Content);
        result.Term = SlugifyUtmValue(result.Term);
        result.Id = SlugifyUtmValue(result.Id);

        return result;
    }

    private static Utms Clone(Utms src)
    {
        return new Utms
        {
            Source = src.Source,
            Medium = src.Medium,
            Campaign = src.Campaign,
            Content = src.Content,
            Term = src.Term,
            Id = src.Id,
        };
    }

    /// <summary>
    /// Slugifies a UTM value for consistent, URL-safe formatting.
    /// Transliterates non-ASCII characters, removes special characters,
    /// lowercases, and replaces hyphens with underscores (UTM convention).
    /// Returns <c>null</c> when the input is null or whitespace.
    /// </summary>
    private static string? SlugifyUtmValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Trim().ToTranslit().Slugify().Replace('-', '_').Trim('_');
    }

    private static void ApplyLayer(Utms target, Utms? layer)
    {
        if (layer == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(layer.Source))
        {
            target.Source = layer.Source;
        }

        if (!string.IsNullOrWhiteSpace(layer.Medium))
        {
            target.Medium = layer.Medium;
        }

        if (!string.IsNullOrWhiteSpace(layer.Campaign))
        {
            target.Campaign = layer.Campaign;
        }

        if (!string.IsNullOrWhiteSpace(layer.Content))
        {
            target.Content = layer.Content;
        }

        if (!string.IsNullOrWhiteSpace(layer.Term))
        {
            target.Term = layer.Term;
        }

        if (!string.IsNullOrWhiteSpace(layer.Id))
        {
            target.Id = layer.Id;
        }
    }
}
