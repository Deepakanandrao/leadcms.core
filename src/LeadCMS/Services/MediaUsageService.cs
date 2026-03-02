// <copyright file="MediaUsageService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.RegularExpressions;
using LeadCMS.Data;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

/// <summary>
/// Service for updating media usage metadata based on content references.
/// </summary>
public interface IMediaUsageService
{
    /// <summary>
    /// Updates media usage counts and descriptions from all content items.
    /// </summary>
    /// <returns>Tuple containing the number of contents processed and media items updated.</returns>
    Task<(int ContentsProcessed, int MediaUpdated)> UpdateMediaUsageFromAllContentAsync();

    /// <summary>
    /// Updates media descriptions and content-type tags from a specific content body.
    /// </summary>
    /// <param name="contentBody">The content body to extract image references from.</param>
    /// <param name="contentType">The content type identifier (e.g. "landing", "blog-article") to tag media with.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateMediaDescriptionsFromContentAsync(string? contentBody, string? contentType = null);
}

/// <summary>
/// Service for updating media usage metadata based on content references.
/// </summary>
public class MediaUsageService : IMediaUsageService
{
    private static readonly Regex MdxImageTagRegex = new Regex(@"<Image\b[^>]*?>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlImageTagRegex = new Regex(@"<img\b[^>]*?>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex MdxAttributeRegex = new Regex(@"(\w+)\s*=\s*""([^""]*)""", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex MarkdownImageRegex = new Regex(@"!\[(?<alt>[^\]]*)\]\((?<url>[^)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex MediaPathRegex = new Regex(@"/api/media/[^\s""')]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly PgDbContext dbContext;
    private readonly ILogger<MediaUsageService> logger;

    public MediaUsageService(PgDbContext dbContext, ILogger<MediaUsageService> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(int ContentsProcessed, int MediaUpdated)> UpdateMediaUsageFromAllContentAsync()
    {
        var contentItems = await dbContext.Content!
            .AsNoTracking()
            .Select(c => new { c.Body, c.CoverImageUrl, c.Type })
            .ToListAsync();

        var contentsProcessed = 0;
        var mediaUpdated = 0;
        var mediaUsageCounts = new Dictionary<(string ScopeUid, string FileName), int>();
        var descriptionCandidates = new Dictionary<(string ScopeUid, string FileName), string>();
        var mediaContentTypeTags = new Dictionary<(string ScopeUid, string FileName), HashSet<string>>();

        foreach (var content in contentItems)
        {
            contentsProcessed++;

            var contentTypeTag = !string.IsNullOrWhiteSpace(content.Type)
                ? content.Type.Trim().ToLowerInvariant()
                : null;

            // Count cover image usage
            if (!string.IsNullOrWhiteSpace(content.CoverImageUrl) &&
                TryParseMediaPath(content.CoverImageUrl, out var coverScopeUid, out var coverFileName))
            {
                var coverKey = NormalizeMediaKey(coverScopeUid, coverFileName);
                mediaUsageCounts[coverKey] = mediaUsageCounts.TryGetValue(coverKey, out var coverCount) ? coverCount + 1 : 1;

                if (contentTypeTag != null)
                {
                    if (!mediaContentTypeTags.TryGetValue(coverKey, out var coverTags))
                    {
                        coverTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        mediaContentTypeTags[coverKey] = coverTags;
                    }

                    coverTags.Add(contentTypeTag);
                }
            }

            // Count body image usages
            if (string.IsNullOrWhiteSpace(content.Body))
            {
                continue;
            }

            var urls = ExtractImageUrls(content.Body);
            foreach (var url in urls)
            {
                if (!TryParseMediaPath(url, out var scopeUid, out var fileName))
                {
                    continue;
                }

                var key = NormalizeMediaKey(scopeUid, fileName);
                mediaUsageCounts[key] = mediaUsageCounts.TryGetValue(key, out var count) ? count + 1 : 1;

                if (contentTypeTag != null)
                {
                    if (!mediaContentTypeTags.TryGetValue(key, out var tags))
                    {
                        tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        mediaContentTypeTags[key] = tags;
                    }

                    tags.Add(contentTypeTag);
                }
            }

            var references = ExtractImageReferences(content.Body);
            foreach (var reference in references)
            {
                if (!TryParseMediaPath(reference.Url, out var scopeUid, out var fileName))
                {
                    continue;
                }

                var key = NormalizeMediaKey(scopeUid, fileName);
                if (!descriptionCandidates.ContainsKey(key))
                {
                    descriptionCandidates[key] = reference.Description;
                }
            }
        }

        if (mediaUsageCounts.Count == 0)
        {
            return (contentsProcessed, 0);
        }

        var mediaItems = await dbContext.Media!
            .ToListAsync();

        foreach (var media in mediaItems)
        {
            var key = NormalizeMediaKey(media.ScopeUid, media.Name);
            var count = mediaUsageCounts.TryGetValue(key, out var resolvedCount) ? resolvedCount : 0;

            if (media.UsageCount != count)
            {
                media.UsageCount = count;
                mediaUpdated++;
            }

            if (string.IsNullOrWhiteSpace(media.Description) && descriptionCandidates.TryGetValue(key, out var description))
            {
                media.Description = description;
                mediaUpdated++;
            }

            // Update content-type tags
            if (mediaContentTypeTags.TryGetValue(key, out var contentTypeTags) && contentTypeTags.Count > 0)
            {
                var existingTags = media.Tags ?? Array.Empty<string>();
                var mergedTags = existingTags
                    .Concat(contentTypeTags)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (mergedTags.Length != existingTags.Length)
                {
                    media.Tags = mergedTags;
                    mediaUpdated++;
                }
            }
        }

        if (mediaUpdated > 0)
        {
            await dbContext.SaveChangesAsync();
        }

        return (contentsProcessed, mediaUpdated);
    }

    /// <inheritdoc/>
    public async Task UpdateMediaDescriptionsFromContentAsync(string? contentBody, string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(contentBody))
        {
            return;
        }

        try
        {
            var contentTypeTag = !string.IsNullOrWhiteSpace(contentType)
                ? contentType.Trim().ToLowerInvariant()
                : null;

            // Collect all media URLs from the body (for content-type tagging)
            var allUrls = ExtractImageUrls(contentBody);
            var references = ExtractImageReferences(contentBody);

            // Build a set of all media keys referenced in this content
            var referencedKeys = new HashSet<(string ScopeUid, string FileName)>();
            foreach (var url in allUrls)
            {
                if (TryParseMediaPath(url, out var scopeUid, out var fileName))
                {
                    referencedKeys.Add(NormalizeMediaKey(scopeUid, fileName));
                }
            }

            foreach (var reference in references)
            {
                if (TryParseMediaPath(reference.Url, out var scopeUid, out var fileName))
                {
                    referencedKeys.Add(NormalizeMediaKey(scopeUid, fileName));
                }
            }

            if (referencedKeys.Count == 0 && references.Count == 0)
            {
                return;
            }

            // Build a lookup of descriptions from references
            var descriptionsByKey = new Dictionary<(string ScopeUid, string FileName), string>();
            foreach (var reference in references)
            {
                if (TryParseMediaPath(reference.Url, out var scopeUid, out var fileName))
                {
                    var key = NormalizeMediaKey(scopeUid, fileName);
                    if (!descriptionsByKey.ContainsKey(key) && !string.IsNullOrWhiteSpace(reference.Description))
                    {
                        descriptionsByKey[key] = reference.Description;
                    }
                }
            }

            var updated = false;
            foreach (var key in referencedKeys)
            {
                var media = await dbContext.Media!
                    .FirstOrDefaultAsync(m => m.ScopeUid == key.ScopeUid && m.Name == key.FileName);

                if (media == null)
                {
                    // Try case-insensitive match
                    var allMedia = await dbContext.Media!
                        .Where(m => m.ScopeUid.ToUpper() == key.ScopeUid && m.Name.ToUpper() == key.FileName)
                        .FirstOrDefaultAsync();
                    media = allMedia;
                }

                if (media == null)
                {
                    continue;
                }

                // Update description if empty and we have a candidate
                var normalizedKey = NormalizeMediaKey(media.ScopeUid, media.Name);
                if (string.IsNullOrWhiteSpace(media.Description) && descriptionsByKey.TryGetValue(normalizedKey, out var description))
                {
                    media.Description = description;
                    updated = true;
                }

                // Add content-type tag if provided
                if (contentTypeTag != null)
                {
                    var tags = media.Tags ?? Array.Empty<string>();
                    if (!Array.Exists(tags, tag => string.Equals(tag, contentTypeTag, StringComparison.OrdinalIgnoreCase)))
                    {
                        media.Tags = tags.Concat(new[] { contentTypeTag })
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update media descriptions from content body");
        }
    }

    private static (string ScopeUid, string FileName) NormalizeMediaKey(string scopeUid, string fileName)
    {
        return (scopeUid.Trim().ToUpperInvariant(), fileName.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Extracts all media URLs from content body by finding /api/media/... patterns.
    /// Works with any content format: MDX, JSON, HTML, Markdown, plain text, etc.
    /// </summary>
    private List<string> ExtractImageUrls(string contentBody)
    {
        // Simple approach: find all /api/media/... patterns in the content as plain text
        // This works for any format including JSON fields like "image": "/api/media/..."
        return MediaPathRegex.Matches(contentBody)
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToList();
    }

    private List<(string Url, string Description)> ExtractImageReferences(string contentBody)
    {
        var mdxResults = MdxImageTagRegex.Matches(contentBody)
            .Cast<Match>()
            .Select(match =>
            {
                var attributes = ParseMdxAttributes(match.Value);
                if (!attributes.TryGetValue("src", out var src) || string.IsNullOrWhiteSpace(src))
                {
                    return (Url: string.Empty, Description: string.Empty);
                }

                attributes.TryGetValue("alt", out var alt);
                attributes.TryGetValue("caption", out var caption);

                var description = !string.IsNullOrWhiteSpace(alt) ? alt : caption;
                return string.IsNullOrWhiteSpace(description)
                    ? (Url: string.Empty, Description: string.Empty)
                    : (Url: src, Description: description!);
            })
            .Where(result => !string.IsNullOrWhiteSpace(result.Url) && !string.IsNullOrWhiteSpace(result.Description))
            .ToList();

        var htmlResults = HtmlImageTagRegex.Matches(contentBody)
            .Cast<Match>()
            .Select(match =>
            {
                var attributes = ParseMdxAttributes(match.Value);
                if (!attributes.TryGetValue("src", out var src) || string.IsNullOrWhiteSpace(src))
                {
                    return (Url: string.Empty, Description: string.Empty);
                }

                attributes.TryGetValue("alt", out var alt);
                return string.IsNullOrWhiteSpace(alt)
                    ? (Url: string.Empty, Description: string.Empty)
                    : (Url: src, Description: alt!);
            })
            .Where(result => !string.IsNullOrWhiteSpace(result.Url) && !string.IsNullOrWhiteSpace(result.Description))
            .ToList();

        var markdownResults = MarkdownImageRegex.Matches(contentBody)
            .Cast<Match>()
            .Select(match =>
            {
                var url = match.Groups["url"].Value;
                var alt = match.Groups["alt"].Value;
                return (Url: url, Description: alt);
            })
            .Where(result => !string.IsNullOrWhiteSpace(result.Url) && !string.IsNullOrWhiteSpace(result.Description))
            .ToList();

        if (htmlResults.Count > 0)
        {
            mdxResults.AddRange(htmlResults);
        }

        if (markdownResults.Count == 0)
        {
            return mdxResults;
        }

        mdxResults.AddRange(markdownResults);
        return mdxResults;
    }

    private Dictionary<string, string> ParseMdxAttributes(string tag)
    {
        return MdxAttributeRegex.Matches(tag)
            .Cast<Match>()
            .Select(match => new
            {
                Name = match.Groups[1].Value,
                Value = match.Groups[2].Value,
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .DistinctBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a media URL to extract scopeUid and fileName.
    /// Handles nested folder paths: /api/media/folder/subfolder/something/filename.jpg
    /// where scopeUid = "folder/subfolder/something" and fileName = "filename.jpg".
    /// </summary>
    private bool TryParseMediaPath(string url, out string scopeUid, out string fileName)
    {
        scopeUid = string.Empty;
        fileName = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var path = url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            path = absoluteUri.AbsolutePath;
        }

        var match = MediaPathRegex.Match(path);
        if (!match.Success)
        {
            return false;
        }

        var mediaPath = match.Value.Substring("/api/media/".Length);
        var parts = mediaPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        // scopeUid is all path segments except the last one (which is the filename)
        // e.g., /api/media/folder/subfolder/file.jpg -> scopeUid="folder/subfolder", fileName="file.jpg"
        scopeUid = string.Join('/', parts.Take(parts.Length - 1));
        fileName = parts[^1];
        return true;
    }
}
