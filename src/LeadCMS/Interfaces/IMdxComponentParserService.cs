// <copyright file="IMdxComponentParserService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for analyzing MDX content to extract component information and metadata.
/// </summary>
public interface IMdxComponentParserService
{
    /// <summary>
    /// Analyzes all content of a specific type to extract MDX component information.
    /// </summary>
    /// <param name="contentType">The content type to analyze (e.g., "blog-post", "landing").</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Analysis result containing discovered components and their metadata.</returns>
    Task<MdxComponentAnalysisDto> AnalyzeContentTypeAsync(string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a single piece of MDX content to extract component information.
    /// </summary>
    /// <param name="mdxContent">The MDX content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>List of components found in the content.</returns>
    Task<List<MdxComponentDto>> AnalyzeMdxContentAsync(string mdxContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached analysis results for a content type if available.
    /// </summary>
    /// <param name="contentType">The content type to get cached results for.</param>
    /// <param name="maxAge">Maximum age of cached results to consider valid.</param>
    /// <returns>Cached analysis result or null if not available or expired.</returns>
    Task<MdxComponentAnalysisDto?> GetCachedAnalysisAsync(string contentType, TimeSpan? maxAge = null);

    /// <summary>
    /// Clears cached analysis results for a specific content type.
    /// </summary>
    /// <param name="contentType">The content type to clear cache for.</param>
    /// <returns>Task representing the async operation.</returns>
    Task ClearCacheAsync(string contentType);
}
