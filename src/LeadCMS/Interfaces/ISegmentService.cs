// <copyright file="ISegmentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Entities;

namespace LeadCMS.Interfaces;

public interface ISegmentService
{
    Task<int> CalculateContactCountAsync(Segment segment);

    Task<List<Contact>> GetSegmentContactsAsync(int segmentId, string? query = null, int? limit = null);

    Task<SegmentPreviewResultDto> PreviewSegmentAsync(SegmentDefinition definition, int limit = 100);

    Task<List<Contact>> EvaluateDynamicSegmentAsync(SegmentDefinition definition, int? limit = null);

    Task ValidateSegmentAsync(Segment segment);

    Task SaveAsync(Segment segment);

    Task<int> RecalculateContactCountAsync(int segmentId);
}
