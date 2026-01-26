// <copyright file="UserQueryResult.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Infrastructure
{
    /// <summary>
    /// Query result for User entities.
    /// </summary>
    public class UserQueryResult
    {
        public UserQueryResult(IList<User>? records, long totalCount)
        {
            Records = records;
            TotalCount = totalCount;
        }

        public IList<User>? Records { get; init; }

        public long TotalCount { get; init; }
    }
}
