// <copyright file="MediaResolver.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Interfaces;

namespace LeadCMS.Helpers;

public class MediaResolver : IMediaResolver
{
    public string Resolve(string uri, HttpContext context, string mode)
    {
        if (!uri.StartsWith("/api/media/") || mode == "relative")
        {
            return uri;
        }

        var request = context.Request;
        var domain = $"{request.Scheme}://{request.Host}";
        return $"{domain}{uri}";
    }
}