// <copyright file="IncludeTranslationsParameterAttribute.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Attributes;

/// <summary>
/// Marks a controller action as supporting the includeTranslations query parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class IncludeTranslationsParameterAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the description for the parameter in Swagger documentation.
    /// </summary>
    public string Description { get; set; } = "Include translation mappings in the response";
}
