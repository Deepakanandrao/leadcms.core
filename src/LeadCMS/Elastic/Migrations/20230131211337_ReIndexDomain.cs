// <copyright file="20230131211337_ReIndexDomain.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Elastic;
using LeadCMS.Entities;

namespace LeadCMS.Elastic.Migrations;

[ElasticMigration("20230131211337_ReIndexDomain")]
public class ReIndexDomain : ElasticMigration
{
    public override async Task Up(ElasticDbContext context)
    {
        await ElasticHelper.MigrateIndex(context, typeof(Domain));
    }
}