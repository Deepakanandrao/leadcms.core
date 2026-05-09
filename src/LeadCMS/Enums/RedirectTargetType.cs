// <copyright file="RedirectTargetType.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Enums;

public enum RedirectTargetType
{
    /// <summary>Redirect to an absolute external URL.</summary>
    ExternalUrl,

    /// <summary>Redirect to a path within the same site (e.g. "/about").</summary>
    InternalPath,

    /// <summary>Redirect to content identified by language + slug.</summary>
    ContentSlug,

    /// <summary>Redirect to content identified by its numeric ID.</summary>
    ContentId,
}
