// <copyright file="EmailGroupData.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Plugin.Site.Data.Seed;

public class EmailGroupData
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailGroup>().HasData(
            new EmailGroup { Id = 1, Name = "Transactional", Language = "en" });
    }
}
