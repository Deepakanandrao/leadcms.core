// <copyright file="IQueryProvider.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Interfaces
{
    public interface IQueryProvider<T>
        where T : BaseEntityWithId
    {
        public Task<QueryResult<T>> GetResult();
    }

    public class QueryResult<T>
        where T : BaseEntityWithId
    {
        public QueryResult(IList<T>? records, long totalCount, string? servedFrom = null)
        {
            Records = records;
            TotalCount = totalCount;
            ServedFrom = servedFrom;
        }

        public IList<T>? Records { get; init; }

        public long TotalCount { get; init; }

        public string? ServedFrom { get; set; } // "DB", "ES", or "ES,DB"
        
        public Array? DynamicResults { get; set; }
    }
}