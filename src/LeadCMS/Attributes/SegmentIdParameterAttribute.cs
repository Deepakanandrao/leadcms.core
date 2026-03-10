// <copyright file="SegmentIdParameterAttribute.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Attributes;

/// <summary>
/// Marks a controller action as supporting the <c>segmentId</c> query parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SegmentIdParameterAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the description for the parameter in Swagger documentation.
    /// </summary>
    public string Description { get; set; } = "Filter contacts to the evaluated members of the specified segment before applying the remaining query filters";
}
