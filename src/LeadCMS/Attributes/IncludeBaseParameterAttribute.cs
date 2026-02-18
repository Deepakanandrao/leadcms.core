// <copyright file="IncludeBaseParameterAttribute.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Attributes;

/// <summary>
/// Marks a controller action as supporting the includeBase query parameter
/// for returning base versions of modified entities during sync.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class IncludeBaseParameterAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the description for the parameter in Swagger documentation.
    /// </summary>
    public string Description { get; set; } = "Include base versions of modified items for three-way merge support";
}
