// <copyright file="MediaUriTransformer.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.RegularExpressions;
using LeadCMS.Interfaces;

namespace LeadCMS.Helpers;
public static class MediaUriTransformer
{
    // Regex matches /api/media/ followed by any non-whitespace, non-quote, non-parenthesis character
    private static readonly Regex MediaRegex = new Regex(@"/api/media/[^\s""')]+", RegexOptions.Compiled);

    public static string Transform(string content, IMediaResolver resolver, HttpContext context, string mode)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        return MediaRegex.Replace(content, match => resolver.Resolve(match.Value, context, mode));
    }
}
