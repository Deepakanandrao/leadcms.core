// <copyright file="RedirectDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.DTOs;

public class RedirectDetailsDto
{
    public int ContentId { get; set; }

    public string FromSlug { get; set; } = string.Empty;

    public string ToSlug { get; set; } = string.Empty;

    public string FromLanguage { get; set; } = string.Empty;

    public string ToLanguage { get; set; } = string.Empty;
}
