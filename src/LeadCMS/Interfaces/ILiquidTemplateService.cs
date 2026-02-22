// <copyright file="ILiquidTemplateService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Renders a Liquid template (Fluid dialect) with the provided variable values.
/// Legacy placeholder formats (<c>&lt;%var%&gt;</c>, <c>${var}</c>) are normalised to Liquid <c>{{ var }}</c>
/// syntax before rendering, ensuring backwards compatibility with older templates.
/// </summary>
public interface ILiquidTemplateService
{
    /// <summary>
    /// Renders the template string with the supplied variables.
    /// </summary>
    /// <param name="template">The template source (may contain Liquid syntax or legacy placeholders).</param>
    /// <param name="variables">Key/value pairs that map variable names to their runtime values.
    /// Values may be strings, numbers, collections, or complex objects — Fluid will
    /// convert them automatically via <c>FluidValue.Create</c>.</param>
    /// <returns>The rendered output string.</returns>
    Task<string> RenderAsync(string template, Dictionary<string, object>? variables);
}
