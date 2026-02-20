// <copyright file="PluginSettings.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.Site.Configuration;

public class PluginSettings
{
    public string SiteUrl { get; set; } = "https://leadcms.ai";

    public ContactUsConfig ContactUs { get; set; } = new ContactUsConfig();

    public string SupportEmail { get; set; } = "support@leadcms.ai";

    public string RecaptchaSecretKey { get; set; } = string.Empty;

    public string ConfirmationUrlTemplate { get; set; } = "{siteUrl}/confirm-subscription?token={token}";

    public string SubscriptionTokenSecret { get; set; } = string.Empty;
}

public class ContactUsConfig
{
    public string[] To { get; set; } = Array.Empty<string>();
}