// <copyright file="MediaOptimizationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using ImageMagick;
using LeadCMS.Constants;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;

namespace LeadCMS.Services;

public class MediaOptimizationService : IMediaOptimizationService
{
    private const int DefaultMaxWidth = 2048;
    private const int DefaultMaxHeight = 2048;
    private const string DefaultPreferredFormat = "avif";
    private const int DefaultQuality = 75;

    private readonly ISettingService settingService;
    private readonly ILogger<MediaOptimizationService> logger;

    public MediaOptimizationService(ISettingService settingService, ILogger<MediaOptimizationService> logger)
    {
        this.settingService = settingService;
        this.logger = logger;
    }

    public async Task<MediaOptimizationSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var maxDimensions = await settingService.GetSettingWithFallbackAsync(
            SettingKeys.MediaMaxDimensions,
            ConfigurationPaths.GetConfigurationPath(SettingKeys.MediaMaxDimensions));
        var preferredFormat = await settingService.GetSettingWithFallbackAsync(
            SettingKeys.MediaPreferredFormat,
            ConfigurationPaths.GetConfigurationPath(SettingKeys.MediaPreferredFormat));
        var enableOptimisation = await settingService.GetBoolSettingWithFallbackAsync(
            SettingKeys.MediaEnableOptimisation,
            true);

        var formatValue = string.IsNullOrWhiteSpace(preferredFormat)
            ? DefaultPreferredFormat
            : preferredFormat!;

        var resolvedFormat = ResolvePreferredFormat(formatValue);

        return new MediaOptimizationSettings
        {
            MaxDimensions = string.IsNullOrWhiteSpace(maxDimensions) ? $"{DefaultMaxWidth}x{DefaultMaxHeight}" : maxDimensions,
            PreferredFormat = resolvedFormat,
            EnableOptimisation = enableOptimisation,
        };
    }

    public async Task<MediaOptimizationResult> OptimizeAsync(MediaOptimizationRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        // If optimization is disabled, return original data as-is
        if (!settings.EnableOptimisation)
        {
            return new MediaOptimizationResult
            {
                Data = request.Data,
                Size = request.Data.Length,
                Extension = request.Extension,
                MimeType = request.MimeType,
                WasOptimized = false,
            };
        }

        if (request.Data.Length == 0)
        {
            return new MediaOptimizationResult
            {
                Data = request.Data,
                Size = request.Data.Length,
                Extension = request.Extension,
                MimeType = request.MimeType,
                WasOptimized = false,
            };
        }

        if (!request.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return new MediaOptimizationResult
            {
                Data = request.Data,
                Size = request.Data.Length,
                Extension = request.Extension,
                MimeType = request.MimeType,
                WasOptimized = false,
            };
        }

        var targetFormat = ResolveMagickFormat(settings.PreferredFormat);

        try
        {
            using var image = new MagickImage(request.Data);

            if (targetFormat == MagickFormat.Unknown)
            {
                targetFormat = image.Format;
            }

            var (maxWidth, maxHeight) = MediaSizeHelper.ParseSize(settings.MaxDimensions, DefaultMaxWidth, DefaultMaxHeight);
            ApplyResize(image, maxWidth, maxHeight);
            image.Strip();
            image.Quality = DefaultQuality;
            image.Format = targetFormat;

            var optimizedBytes = image.ToByteArray();
            var optimizedExtension = $".{targetFormat.ToString().ToLowerInvariant()}";
            var optimizedMimeType = ResolveMimeType(optimizedExtension) ?? request.MimeType;

            return new MediaOptimizationResult
            {
                Data = optimizedBytes,
                Size = optimizedBytes.Length,
                Extension = optimizedExtension,
                MimeType = optimizedMimeType,
                WasOptimized = true,
            };
        }
        catch (MagickException ex)
        {
            logger.LogWarning(ex, "Failed to optimize image {FileName}. Returning original data.", request.FileName);
            return new MediaOptimizationResult
            {
                Data = request.Data,
                Size = request.Data.Length,
                Extension = request.Extension,
                MimeType = request.MimeType,
                WasOptimized = false,
            };
        }
    }

    private static void ApplyResize(MagickImage image, int? maxWidth, int? maxHeight)
    {
        if ((maxWidth ?? 0) <= 0 && (maxHeight ?? 0) <= 0)
        {
            return;
        }

        var targetWidth = maxWidth.HasValue && maxWidth.Value > 0
            ? (uint)maxWidth.Value
            : image.Width;
        var targetHeight = maxHeight.HasValue && maxHeight.Value > 0
            ? (uint)maxHeight.Value
            : image.Height;

        var geometry = new MagickGeometry(targetWidth, targetHeight)
        {
            IgnoreAspectRatio = false,
            Greater = true,
        };

        image.Resize(geometry);
    }

    private static string ResolvePreferredFormat(string value)
    {
        var supported = MagickNET.SupportedFormats
            .Where(format => format.SupportsWriting)
            .Select(format => format.Format.ToString().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalized = value.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(normalized) || !supported.Contains(normalized))
        {
            return DefaultPreferredFormat;
        }

        return normalized;
    }

    private static MagickFormat ResolveMagickFormat(string format)
    {
        var supported = MagickNET.SupportedFormats
            .Where(item => item.SupportsWriting)
            .Select(item => item.Format)
            .ToHashSet();

        if (Enum.TryParse<MagickFormat>(format, true, out var magickFormat) && supported.Contains(magickFormat))
        {
            return magickFormat;
        }

        return MagickFormat.Unknown;
    }

    private static string? ResolveMimeType(string extension)
    {
        var provider = ContentTypeHelper.CreateCustomizedProvider();
        if (provider.TryGetContentType($"file{extension}", out var mimeType))
        {
            return mimeType;
        }

        var fallbackProvider = new FileExtensionContentTypeProvider();
        return fallbackProvider.TryGetContentType($"file{extension}", out var fallbackMime)
            ? fallbackMime
            : null;
    }
}