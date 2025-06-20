// <copyright file="MediaResolutionHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Helpers;

public static class MediaResolutionHelper
{
    public static string GetResolutionMode(HttpContext context)
    {
        var header = context.Request.Headers["X-Media-Resolution"].FirstOrDefault();
        var query = context.Request.Query["mediaResolution"].FirstOrDefault();
        return (header ?? query ?? "relative").ToLowerInvariant();
    }
}

