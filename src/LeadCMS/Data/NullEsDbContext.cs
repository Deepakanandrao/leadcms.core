// <copyright file="NullEsDbContext.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Elastic;
using Nest;

namespace LeadCMS.Data;

/// <summary>
/// Null implementation of ElasticDbContext for when Elasticsearch is disabled.
/// </summary>
public class NullEsDbContext : ElasticDbContext
{
    public override ElasticClient ElasticClient => throw new InvalidOperationException("Elasticsearch is disabled. This operation is not supported.");

    public override string IndexPrefix => string.Empty;

    public override bool IsElasticsearchEnabled => false;

    protected override List<Type> EntityTypes => new List<Type>();

    public override void Migrate()
    {
        // Do nothing when Elasticsearch is disabled
    }
}
