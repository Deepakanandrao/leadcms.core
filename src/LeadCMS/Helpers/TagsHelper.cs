// <copyright file="TagsHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Helpers;

public static class TagsHelper
{
    public static string[] ToDistinctTags(IEnumerable<string[]?> tagSets)
    {
        return tagSets
            .Where(tags => tags != null)
            .SelectMany(tags => tags!)
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Distinct()
            .ToArray();
    }
}