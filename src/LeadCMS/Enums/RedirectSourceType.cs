// <copyright file="RedirectSourceType.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Enums;

public enum RedirectSourceType
{
    /// <summary>Source is a full path from the root of the domain (e.g. "/en/old-page").</summary>
    InternalPath,

    /// <summary>Source is content identified by language + slug (language/slug pair).</summary>
    ContentSlug,

    /// <summary>Source is content identified by its numeric ID.</summary>
    ContentId,
}
