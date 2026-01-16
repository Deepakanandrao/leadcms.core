// <copyright file="EnrichmentExecutionResult.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Enrichment.Models;

public class EnrichmentExecutionResult
{
    public bool Success { get; set; }

    public bool NoChanges { get; set; }

    public EnrichmentErrorCategory ErrorCategory { get; set; } = EnrichmentErrorCategory.None;

    public string? ErrorMessage { get; set; }

    public IReadOnlyCollection<EnrichedFieldChange> FieldChanges { get; set; } = Array.Empty<EnrichedFieldChange>();
}

public class EnrichedFieldChange
{
    public string FieldName { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public double? Confidence { get; set; }
}
