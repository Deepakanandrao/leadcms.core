// <copyright file="IRedirectService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Interfaces;

public interface IRedirectService
{
    Task<List<RedirectDetailsDto>> DiscoverRedirectsAsync();
}
