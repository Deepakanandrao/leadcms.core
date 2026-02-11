// <copyright file="QueryProviderFactory.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Web;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.Extensions.Options;
using Nest;

namespace LeadCMS.Infrastructure
{
    public class QueryProviderFactory<T>
        where T : BaseEntityWithId, new()
    {
        protected readonly IOptions<ApiSettingsConfig> apiSettingsConfig;
        protected readonly IHttpContextHelper httpContextHelper;
        protected readonly ElasticClient? elasticClient;
        protected readonly EsDbContext esDbContext;

        protected PgDbContext dbContext;

        public QueryProviderFactory(PgDbContext dbContext, EsDbContext esDbContext, IOptions<ApiSettingsConfig> apiSettingsConfig, IHttpContextHelper? httpContextHelper)
        {
            this.dbContext = dbContext;
            this.esDbContext = esDbContext;
            this.apiSettingsConfig = apiSettingsConfig;

            // Only assign ElasticClient if Elasticsearch is enabled
            elasticClient = esDbContext.IsElasticsearchEnabled ? esDbContext.ElasticClient : null;

            ArgumentNullException.ThrowIfNull(httpContextHelper);
            this.httpContextHelper = httpContextHelper;
        }

        public virtual IQueryProvider<T> BuildQueryProvider(int limit = -1, string? additionalQueryString = null)
        {
            var rawQueryString = httpContextHelper.Request.QueryString.HasValue
                ? HttpUtility.UrlDecode(httpContextHelper.Request.QueryString.ToString())
                : string.Empty;

            if (!string.IsNullOrEmpty(additionalQueryString))
            {
                rawQueryString = string.IsNullOrEmpty(rawQueryString)
                    ? additionalQueryString
                    : $"{rawQueryString}&{additionalQueryString}";
            }

            var queryCommands = QueryStringParser.Parse(rawQueryString);

            var queryBuilder = new QueryModelBuilder<T>(queryCommands, limit == -1 ? apiSettingsConfig.Value.MaxListSize : limit, dbContext);

            var dbSet = dbContext.Set<T>();

            // Only use Elasticsearch if it's enabled and available
            if (esDbContext.IsElasticsearchEnabled && 
                elasticClient != null && 
                typeof(T).GetCustomAttributes(typeof(SupportsElasticAttribute), true).Any() && 
                queryBuilder.SearchData.Count > 0)
            {
                var indexPrefix = dbContext.Configuration.GetSection("Elastic:IndexPrefix").Get<string>();
                return new MixedQueryProvider<T>(queryBuilder, dbSet!.AsQueryable<T>(), elasticClient, indexPrefix!);
            }
            else
            {
                return new DBQueryProvider<T>(dbSet!.AsQueryable<T>(), queryBuilder);
            }
        }

        public void SetDBContext(PgDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}