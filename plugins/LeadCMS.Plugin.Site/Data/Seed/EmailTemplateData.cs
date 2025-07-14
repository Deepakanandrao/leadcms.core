// <copyright file="EmailTemplateData.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using LeadCMS.Entities;
using LeadCMS.Plugin.Site;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Plugin.Site.Data.Seed;

public class EmailTemplateData
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailTemplate>().HasData(
            new EmailTemplate { Id = 1, EmailGroupId = 1, Language = "en", Name = "Contact_Us", Subject = "New Contact Us Submission", FromEmail = SitePlugin.Settings.SupportEmail, FromName = "Support Team", BodyTemplate = ReadResource("En_Contact_Us.html") },
            new EmailTemplate { Id = 2, EmailGroupId = 1, Language = "en", Name = "Acknowledgment", Subject = "Thank You for Contacting Us", FromEmail = SitePlugin.Settings.SupportEmail, FromName = "Support Team", BodyTemplate = ReadResource("En_Acknowledgment.html") },
            new EmailTemplate { Id = 3, EmailGroupId = 1, Language = "en", Name = "Subscription_Confirmation", Subject = "Subscription Confirmed", FromEmail = SitePlugin.Settings.SupportEmail, FromName = "Support Team", BodyTemplate = ReadResource("En_Subscription_Confirmation.html") });
    }

    public static string ReadResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var resourcePath = assembly.GetManifestResourceNames()
                .Single(str => str.EndsWith(fileName));

        if (resourcePath is null)
        {
            return string.Empty;
        }

        using var stream = assembly!.GetManifestResourceStream(resourcePath!) !;
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
