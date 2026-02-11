// <copyright file="ESOnlyQueryProviderFactory.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Web;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.Extensions.Options;

namespace LeadCMS.Infrastructure
{
    public class ESOnlyQueryProviderFactory<T> : QueryProviderFactory<T>
        where T : BaseEntityWithId, new()
    {
        public ESOnlyQueryProviderFactory(PgDbContext dbContext, EsDbContext esDbContext, IOptions<ApiSettingsConfig> apiSettingsConfig, IHttpContextHelper? httpContextHelper)
            : base(dbContext, esDbContext, apiSettingsConfig, httpContextHelper)
        {
        }

        public override IQueryProvider<T> BuildQueryProvider(int limit = -1, string? additionalQueryString = null)
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

            // If Elasticsearch is disabled, fall back to database query provider
            if (!esDbContext.IsElasticsearchEnabled || elasticClient == null)
            {
                var dbSet = dbContext.Set<T>();
                var queryCommands = QueryStringParser.Parse(rawQueryString);
                var queryBuilder = new QueryModelBuilder<T>(queryCommands, limit == -1 ? apiSettingsConfig.Value.MaxListSize : limit, dbContext);
                return new DBQueryProvider<T>(dbSet!.AsQueryable<T>(), queryBuilder);
            }

            var queryCommands2 = QueryStringParser.Parse(rawQueryString);

            var queryBuilder2 = new QueryModelBuilder<T>(queryCommands2, limit == -1 ? apiSettingsConfig.Value.MaxListSize : limit, dbContext);

            var indexPrefix = dbContext.Configuration.GetSection("Elastic:IndexPrefix").Get<string>();
            return new ESQueryProvider<T>(elasticClient, queryBuilder2, indexPrefix!);
        }
    }
}