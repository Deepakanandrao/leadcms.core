// <copyright file="ActivityLogService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Elastic;
using LeadCMS.Entities;
using Nest;

namespace LeadCMS.Services
{
    public class ActivityLogService
    {
        private readonly string indexName;

        private readonly EsDbContext esDbContext;

        public ActivityLogService(IConfiguration configuration, EsDbContext esDbContext)
        {
            var indexPrefix = configuration.GetSection("Elastic:IndexPrefix").Get<string>() ?? "LeadCMS";
            indexName = ElasticHelper.GetIndexName(indexPrefix, "activitylog");
            this.esDbContext = esDbContext;
        }

        public async Task<int> GetMaxId(string source)
        {
            if (!esDbContext.IsElasticsearchEnabled)
            {
                return 0;
            }

            try
            {
                var sr = new SearchRequest<ActivityLog>(indexName);
                sr.Query = new TermQuery() { Field = "source.keyword", Value = source };
                sr.Sort = new List<ISort>() { new FieldSort { Field = "sourceId", Order = Nest.SortOrder.Descending } };
                sr.Size = 1;
                var res = await esDbContext.ElasticClient.SearchAsync<ActivityLog>(sr);
                if (res != null)
                {
                    var doc = res.Documents.FirstOrDefault();
                    if (doc != null)
                    {
                        return doc.SourceId;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to get max ID from Elasticsearch: {Error}", ex.Message);
            }

            return 0;
        }

        public async Task<bool> AddActivityRecords(List<ActivityLog> records)
        {
            if (!esDbContext.IsElasticsearchEnabled)
            {
                // When Elasticsearch is disabled, just return true to not break the flow
                return true;
            }

            if (records.Count > 0)
            {
                try
                {
                    var responce = await esDbContext.ElasticClient.IndexManyAsync<ActivityLog>(records, indexName);

                    if (!responce.IsValid)
                    {
                        Log.Error("Cannot save logs in Elastic Search. Reason: " + responce.DebugInformation);
                    }

                    return responce.IsValid;
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to add activity records to Elasticsearch: {Error}", ex.Message);
                    return false;
                }
            }
            else
            {
                return true;
            }
        }
    }
}