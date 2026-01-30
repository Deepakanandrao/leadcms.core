// <copyright file="SyncEsTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using System.Text;
using System.Text.Json;
using Elasticsearch.Net;
using LeadCMS.Data;
using LeadCMS.DataAnnotations;
using LeadCMS.Elastic;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Services;
using Microsoft.EntityFrameworkCore;
using Nest;

namespace LeadCMS.Tasks
{
    public class SyncEsTask : ChangeLogTask
    {
        private readonly string changeLogId = "change_log_id";
        private readonly EsDbContext esDbContext;
        private readonly string prefix = string.Empty;

        public SyncEsTask(IConfiguration configuration, PgDbContext dbContext, IEnumerable<PluginDbContextBase> pluginDbContexts, EsDbContext esDbContext, TaskStatusService taskStatusService)
            : base("Tasks:SyncEsTask", configuration, dbContext, pluginDbContexts, taskStatusService)
        {
            var elasticPrefix = configuration.GetSection("Elastic:IndexPrefix").Get<string>();

            if (!string.IsNullOrEmpty(elasticPrefix))
            {
                prefix = elasticPrefix;
            }

            this.esDbContext = esDbContext;
        }

        protected override string? ExecuteLogTask(List<ChangeLog> nextBatch, Type loggedType)
        {
            // Skip processing if Elasticsearch is disabled
            if (!esDbContext.IsElasticsearchEnabled)
            {
                return null;
            }

            var bulkPayload = new StringBuilder();
            int addedCount = 0;
            int modifiedCount = 0;
            int deletedCount = 0;

            var existedIndices = GetExistedIndices();

            var indexName = ElasticHelper.GetIndexName(prefix, loggedType);

            if (!existedIndices.Contains(indexName))
            {
                var resp = esDbContext.ElasticClient.Indices.Create(indexName, c => c.Settings(s => s.Analysis(a => a.Analyzers(an => an.Custom("default", ca => ca.Tokenizer("uax_url_email").Filters("lowercase"))))));

                if (!resp.IsValid)
                {
                    throw new ESSyncTaskException($"Cannot create index {indexName}");
                }
            }

            foreach (var item in nextBatch)
            {
                var entityState = item.EntityState;

                if (entityState == EntityState.Added || entityState == EntityState.Modified)
                {
                    var createItem = new { index = new { _index = indexName, _id = item.ObjectId } };
                    var data = JsonHelper.Deserialize<Dictionary<string, object>>(item.Data);
                    data!.Add(changeLogId, item.Id);
                    bulkPayload.AppendLine(JsonHelper.Serialize(createItem));
                    bulkPayload.AppendLine(JsonHelper.Serialize(data));

                    if (entityState == EntityState.Added)
                    {
                        addedCount++;
                    }
                    else
                    {
                        modifiedCount++;
                    }
                }

                if (entityState == EntityState.Deleted)
                {
                    var deleteItem = new { delete = new { _index = indexName, _id = item.ObjectId } };
                    bulkPayload.AppendLine(JsonHelper.Serialize(deleteItem));
                    deletedCount++;
                }
            }

            var bulkRequestParameters = new BulkRequestParameters();
            bulkRequestParameters.Refresh = Refresh.WaitFor;

            var bulkResponse = esDbContext.ElasticClient.LowLevel.Bulk<StringResponse>(bulkPayload.ToString(), bulkRequestParameters);

            // Check HTTP-level success
            if (!bulkResponse.Success)
            {
                throw bulkResponse.OriginalException;
            }

            // Parse and check Elasticsearch-level errors
            var responseBody = bulkResponse.Body;
            var bulkResult = JsonSerializer.Deserialize<BulkResponseBodyDto>(responseBody);
            if (bulkResult != null && bulkResult.Errors)
            {
                var failedItems = bulkResult.Items?.Where(i => i.Index?.Status >= 400).ToList();
                var sampleError = failedItems?.FirstOrDefault()?.Index?.Error?.Reason;
                throw new ESSyncTaskException($"Bulk request failed for {failedItems?.Count ?? 0} items. Sample error: {sampleError}");
            }

            Log.Information("ES Sync Bulk Saved : {0}", bulkResponse.ToString());

            return $"Synced to Elasticsearch: {addedCount} added, {modifiedCount} modified, {deletedCount} deleted";
        }

        protected override int GetMinLogId(ChangeLogTaskLog lastProcessedTask, Type loggedType)
        {
            // Return minimum ID if Elasticsearch is disabled
            if (!esDbContext.IsElasticsearchEnabled)
            {
                return 1;
            }

            var minLogId = 1;

            var indexName = ElasticHelper.GetIndexName(prefix, loggedType);

            if (esDbContext.ElasticClient.Indices.Exists(indexName).Exists)
            {
                var requestResponse = esDbContext.ElasticClient.Search<Dictionary<string, object>>(s => s.Query(q => new MatchAllQuery { })
                .Index(indexName)
                .Sort(s => s.Descending(d => d[changeLogId]))
                .Size(1));
                if (requestResponse.IsValid && requestResponse.Documents.Count > 0)
                {
                    minLogId = (int)((long)requestResponse.Documents.First()[changeLogId] + 1);
                }
            }

            return minLogId;
        }

        protected override bool IsTypeSupported(Type type)
        {
            return type.GetCustomAttribute<SupportsElasticAttribute>() != null;
        }

        private HashSet<string> GetExistedIndices()
        {
            // Return empty set if Elasticsearch is disabled
            if (!esDbContext.IsElasticsearchEnabled)
            {
                return new HashSet<string>();
            }

            var response = esDbContext.ElasticClient.Indices.GetAlias(Indices.All);

            if (!response.IsValid)
            {
                throw new ESSyncTaskException("Failed to read all existing indices and aliases from Elastic database");
            }

            return response.Indices
                .SelectMany(index => new[] { index.Key.Name }.Concat(index.Value.Aliases.Keys))
                .ToHashSet();
        }

        public class ESSyncTaskException : Exception
        {
            public ESSyncTaskException(string message)
                : base(message)
            {
            }
        }
    }
}