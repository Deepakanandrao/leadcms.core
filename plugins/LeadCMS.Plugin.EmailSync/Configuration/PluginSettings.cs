// <copyright file="PluginSettings.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.EmailSync.Configuration;

public class EmailSyncConfig
{
    public string[] InternalDomains { get; set; } = Array.Empty<string>();

    public string[] IgnoredEmails { get; set; } = Array.Empty<string>();

    public string[] WhitelistedFolders { get; set; } = Array.Empty<string>();

    public string[] IgnoredFolderKeywords { get; set; } = Array.Empty<string>();

    public string EncryptionKey { get; set; } = string.Empty;

    public bool CreateContactsForUnknownEmails { get; set; } = true;

    public string[] AutoCreatedContactTags { get; set; } = Array.Empty<string>();
}