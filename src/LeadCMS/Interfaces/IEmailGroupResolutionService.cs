// <copyright file="IEmailGroupResolutionService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Resolves the target email group in a given language by matching TranslationKey or Name.
/// </summary>
public interface IEmailGroupResolutionService
{
    /// <summary>
    /// Tries to find an email group in the target language that shares the same TranslationKey
    /// (or the same Name) as the source group. Returns the matching group ID, or 0 if not found.
    /// </summary>
    /// <param name="sourceEmailGroupId">The source email group ID to resolve from.</param>
    /// <param name="targetLanguage">The target language to find a matching group in.</param>
    /// <returns>The matching group ID, or 0 if no match is found.</returns>
    Task<int> ResolveTargetEmailGroupIdAsync(int sourceEmailGroupId, string targetLanguage);
}
