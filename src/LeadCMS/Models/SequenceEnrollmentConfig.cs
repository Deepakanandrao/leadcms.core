// <copyright file="SequenceEnrollmentConfig.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Models;

public enum ReentryPolicy
{
    OnceEver = 0,
    AllowAfterCompletion = 1,
    Always = 2,
}

/// <summary>
/// JSONB model for sequence enrollment configuration.
/// Defines which enrollment modes are active and reentry behaviour.
/// </summary>
public class SequenceEnrollmentConfig
{
    /// <summary>
    /// Gets or sets the enabled enrollment modes (e.g. "manual", "api", "segment").
    /// </summary>
    public string[] Modes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the segment IDs to include for segment-based enrollment.
    /// Required when "segment" is in <see cref="Modes"/>.
    /// </summary>
    public int[]? IncludeSegmentIds { get; set; }

    /// <summary>
    /// Gets or sets the segment IDs to exclude from segment-based enrollment.
    /// </summary>
    public int[]? ExcludeSegmentIds { get; set; }

    /// <summary>
    /// Gets or sets the reentry policy for contacts who have already been through this sequence.
    /// </summary>
    public ReentryPolicy ReentryPolicy { get; set; } = ReentryPolicy.OnceEver;
}
