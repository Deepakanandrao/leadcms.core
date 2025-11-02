// <copyright file="AppUrlHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using Microsoft.Extensions.Primitives;

namespace LeadCMS.Helpers;

public static class AppUrlHelper
{
    /// <summary>
    /// Build the admin base URL. If configuration contains "AdminUrl" (non-empty), it is used (trimmed of trailing '/').
    /// Otherwise, try to use Origin header from the request (if valid), falling back to Request.Scheme://Request.Host.
    /// </summary>
    public static string GetAdminBaseUrl(IConfiguration configuration, HttpRequest request)
    {
        var adminUrl = configuration.GetValue<string>("AdminUrl");
        if (!string.IsNullOrWhiteSpace(adminUrl))
        {
            return adminUrl.TrimEnd('/');
        }

        // Try Origin header first
        if (request.Headers.TryGetValue("Origin", out StringValues originValue) &&
            !StringValues.IsNullOrEmpty(originValue) &&
            Uri.TryCreate(originValue.ToString(), UriKind.Absolute, out var originUri))
        {
            return $"{originUri.Scheme}://{originUri.Authority}";
        }

        return $"{request.Scheme}://{request.Host.Value}";
    }
}
