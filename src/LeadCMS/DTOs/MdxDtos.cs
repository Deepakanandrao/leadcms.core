// <copyright file="MdxDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.DTOs;

/// <summary>
/// Represents an MDX component with its properties and metadata.
/// </summary>
public class MdxComponentDto
{
    /// <summary>
    /// Gets or sets the name of the MDX component.
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the component (if available from JSDoc comments).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the list of available properties for this component.
    /// </summary>
    public List<MdxComponentPropertyDto> Properties { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this component accepts children.
    /// </summary>
    public bool AcceptsChildren { get; set; }

    /// <summary>
    /// Gets or sets example usage patterns for this component.
    /// </summary>
    public List<string> Examples { get; set; } = new();

    /// <summary>
    /// Gets or sets the frequency of usage across all content of this type.
    /// </summary>
    public int UsageCount { get; set; }
}

/// <summary>
/// Represents a property of an MDX component.
/// </summary>
public class MdxComponentPropertyDto
{
    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TypeScript/JavaScript type of the property.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the default value of the property (if any).
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the description of the property.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets possible values for enum-like properties.
    /// </summary>
    public List<string> PossibleValues { get; set; } = new();

    /// <summary>
    /// Gets or sets example values for this property.
    /// </summary>
    public List<string> ExampleValues { get; set; } = new();
}

/// <summary>
/// Represents the complete analysis result for MDX components in a content type.
/// </summary>
public class MdxComponentAnalysisDto
{
    /// <summary>
    /// Gets or sets the content type that was analyzed.
    /// </summary>
    [Required]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of discovered MDX components.
    /// </summary>
    public List<MdxComponentDto> Components { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of content items analyzed.
    /// </summary>
    public int TotalContentAnalyzed { get; set; }

    /// <summary>
    /// Gets or sets when this analysis was performed.
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
