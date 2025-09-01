// <copyright file="MdxComponentParserService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LeadCMS.Services;

/// <summary>
/// Service for analyzing MDX content to extract component information and metadata.
/// </summary>
public class MdxComponentParserService : IMdxComponentParserService
{
    private const string CacheKeyPrefix = "mdx_components_";

    private readonly PgDbContext dbContext;
    private readonly IMemoryCache cache;
    private readonly ILogger<MdxComponentParserService> logger;
    private readonly MdxParser mdxParser;

    public MdxComponentParserService(
        PgDbContext dbContext,
        IMemoryCache cache,
        ILogger<MdxComponentParserService> logger)
    {
        this.dbContext = dbContext;
        this.cache = cache;
        this.logger = logger;
        mdxParser = new MdxParser();
    }

    /// <inheritdoc/>
    public async Task<MdxComponentAnalysisDto> AnalyzeContentTypeAsync(string contentType, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting MDX component analysis for content type: {ContentType}", contentType);

        // Check if content type exists
        var contentTypeEntity = await dbContext.ContentTypes!
            .Where(ct => ct.Uid == contentType)
            .FirstOrDefaultAsync(cancellationToken);

        if (contentTypeEntity == null)
        {
            throw new ArgumentException($"Content type '{contentType}' not found", nameof(contentType));
        }

        // Only analyze MDX content
        if (contentTypeEntity.Format != ContentFormat.MDX)
        {
            logger.LogWarning("Content type {ContentType} is not MDX format (Format: {Format})", contentType, contentTypeEntity.Format);
            return new MdxComponentAnalysisDto
            {
                ContentType = contentType,
                Components = new List<MdxComponentDto>(),
                TotalContentAnalyzed = 0,
            };
        }

        // Get all content of this type
        var contentItems = await dbContext.Content!
            .Where(c => c.Type == contentType && !string.IsNullOrEmpty(c.Body))
            .Select(c => new { c.Id, c.Body, c.Title })
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} content items to analyze for type {ContentType}", contentItems.Count, contentType);

        var allComponents = new Dictionary<string, MdxComponentDto>();

        foreach (var content in contentItems)
        {
            try
            {
                var components = await AnalyzeMdxContentAsync(content.Body, cancellationToken);

                // Merge components
                foreach (var component in components)
                {
                    if (allComponents.TryGetValue(component.Name, out var existing))
                    {
                        // Merge usage counts and properties
                        existing.UsageCount += component.UsageCount;
                        existing.AcceptsChildren = existing.AcceptsChildren || component.AcceptsChildren;

                        // Merge examples
                        foreach (var example in component.Examples)
                        {
                            if (!existing.Examples.Contains(example) && existing.Examples.Count < 5)
                            {
                                existing.Examples.Add(example);
                            }
                        }

                        // Merge properties
                        foreach (var sourceProp in component.Properties)
                        {
                            var targetProp = existing.Properties.FirstOrDefault(p => p.Name == sourceProp.Name);
                            if (targetProp == null)
                            {
                                existing.Properties.Add(sourceProp);
                            }
                            else
                            {
                                // Merge property examples and possible values
                                foreach (var example in sourceProp.ExampleValues)
                                {
                                    if (!targetProp.ExampleValues.Contains(example) && targetProp.ExampleValues.Count < 10)
                                    {
                                        targetProp.ExampleValues.Add(example);
                                    }
                                }

                                foreach (var value in sourceProp.PossibleValues)
                                {
                                    if (!targetProp.PossibleValues.Contains(value) && targetProp.PossibleValues.Count < 20)
                                    {
                                        targetProp.PossibleValues.Add(value);
                                    }
                                }

                                // Update type if not set
                                if (string.IsNullOrEmpty(targetProp.Type) && !string.IsNullOrEmpty(sourceProp.Type))
                                {
                                    targetProp.Type = sourceProp.Type;
                                }
                            }
                        }
                    }
                    else
                    {
                        allComponents[component.Name] = component;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to analyze content item {ContentId} ({Title})", content.Id, content.Title);
            }
        }

        var result = new MdxComponentAnalysisDto
        {
            ContentType = contentType,
            Components = allComponents.Values.OrderByDescending(c => c.UsageCount).ToList(),
            TotalContentAnalyzed = contentItems.Count,
        };

        // Cache the results
        var cacheKey = CacheKeyPrefix + contentType;
        cache.Set(cacheKey, result, TimeSpan.FromHours(1));

        logger.LogInformation(
            "Completed MDX analysis for {ContentType}. Found {ComponentCount} unique components",
            contentType,
            result.Components.Count);

        return result;
    }

    /// <inheritdoc/>
    public async Task<List<MdxComponentDto>> AnalyzeMdxContentAsync(string mdxContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mdxContent))
        {
            return new List<MdxComponentDto>();
        }

        await Task.Yield(); // Make method async for future extensibility

        try
        {
            var components = mdxParser.ParseMdx(mdxContent);
            var componentDtos = new List<MdxComponentDto>();

            foreach (var component in components)
            {
                var componentDto = new MdxComponentDto
                {
                    Name = component.Name,
                    Properties = component.Properties.Select(p => new MdxComponentPropertyDto
                    {
                        Name = p.Name,
                        Type = p.Type,
                        IsRequired = p.IsRequired,
                        DefaultValue = p.DefaultValue,
                        PossibleValues = p.PossibleValues,
                        ExampleValues = p.ExampleValues,
                    }).ToList(),
                    AcceptsChildren = component.AcceptsChildren,
                    Examples = component.Examples,
                    UsageCount = component.UsageCount,
                };

                componentDtos.Add(componentDto);
            }

            return componentDtos.OrderByDescending(c => c.UsageCount).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse MDX content");
            return new List<MdxComponentDto>();
        }
    }

    /// <inheritdoc/>
    public Task<MdxComponentAnalysisDto?> GetCachedAnalysisAsync(string contentType, TimeSpan? maxAge = null)
    {
        var cacheKey = CacheKeyPrefix + contentType;

        if (cache.TryGetValue(cacheKey, out MdxComponentAnalysisDto? cached) && cached != null)
        {
            if (maxAge.HasValue)
            {
                var age = DateTime.UtcNow - cached.AnalyzedAt;
                if (age > maxAge.Value)
                {
                    cache.Remove(cacheKey);
                    return Task.FromResult<MdxComponentAnalysisDto?>(null);
                }
            }

            return Task.FromResult<MdxComponentAnalysisDto?>(cached);
        }

        return Task.FromResult<MdxComponentAnalysisDto?>(null);
    }

    /// <inheritdoc/>
    public async Task ClearCacheAsync(string contentType)
    {
        var cacheKey = CacheKeyPrefix + contentType;
        cache.Remove(cacheKey);
        await Task.CompletedTask;
    }
}
