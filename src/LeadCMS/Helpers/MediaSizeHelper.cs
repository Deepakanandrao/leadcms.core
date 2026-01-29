// <copyright file="MediaSizeHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Helpers;

public static class MediaSizeHelper
{
    public static (int? Width, int? Height) ParseSize(string? size, int? defaultWidth = null, int? defaultHeight = null)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            return (defaultWidth, defaultHeight);
        }

        var parts = size.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var width) &&
            int.TryParse(parts[1], out var height))
        {
            return (width > 0 ? width : defaultWidth, height > 0 ? height : defaultHeight);
        }

        return (defaultWidth, defaultHeight);
    }
}