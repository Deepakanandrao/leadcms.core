// <copyright file="IRedirectService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Interfaces;

public interface IRedirectService
{
    /// <summary>
    /// Discovers redirects from Content change history, upserts them into the database
    /// (without overwriting manually created or edited entries, and skipping suppressed sources).
    /// </summary>
    /// <returns>A task that completes when all newly discovered redirects have been persisted.</returns>
    Task DiscoverAsync();

    /// <summary>
    /// Validates a redirect DTO before create or update.
    /// Throws <see cref="Exceptions.RedirectCycleException"/> when the new redirect would
    /// create a cycle in the redirect chain. Duplicate source validation is enforced by the
    /// database unique indexes; constraint violations are surfaced as 409 responses by the
    /// central error handler.
    /// </summary>
    /// <param name="dto">The redirect to validate.</param>
    /// <param name="excludeId">When updating, the ID of the record being updated so it is not flagged as a duplicate of itself.</param>
    /// <returns>A task that completes when validation succeeds, or throws on violation.</returns>
    Task ValidateAsync(RedirectCreateDto dto, int? excludeId = null);
}
