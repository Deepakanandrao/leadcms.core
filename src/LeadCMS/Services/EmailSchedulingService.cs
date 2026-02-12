// <copyright file="EmailSchedulingService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

public class EmailSchedulingService : IEmailSchedulingService
{
    private readonly IConfiguration configuration;

    private PgDbContext dbContext;

    public EmailSchedulingService(PgDbContext dbContext, IConfiguration configuration)
    {
        this.dbContext = dbContext;
        this.configuration = configuration;
    }

    public async Task<EmailSchedule?> FindByGroupAndLanguage(string groupName, string languageCode)
    {
        EmailSchedule? result;

        // Check if contact.Language is in two-letter format and adjust query accordingly
        var emailSchedulesQuery = dbContext.EmailSchedules!
            .Include(c => c.Group)
            .Where(e => e.Group!.Name == groupName);

        if (languageCode.Length == 2)
        {
            result = await emailSchedulesQuery.FirstOrDefaultAsync(e => e.Group!.Language.StartsWith(languageCode));
        }
        else
        {
            result = await emailSchedulesQuery.FirstOrDefaultAsync(e => e.Group!.Language == languageCode);

            if (result == null)
            {
                var lang = languageCode.Split('-')[0];

                result = await emailSchedulesQuery.FirstOrDefaultAsync(e => e.Group!.Language.StartsWith(lang));
            }
        }

        if (result == null)
        {
            result = await emailSchedulesQuery.FirstOrDefaultAsync(e => e.Group!.Language == LanguageHelper.GetDefaultLanguage(configuration));
        }

        return result;
    }

    public void SetDBContext(PgDbContext pgDbContext)
    {
        dbContext = pgDbContext;
    }
}