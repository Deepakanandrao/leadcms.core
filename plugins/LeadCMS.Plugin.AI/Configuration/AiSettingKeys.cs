// <copyright file="AiSettingKeys.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.AI.Configuration;

public static class AiSettingKeys
{
    // AI site profile settings
    public const string SiteTopic = "AI.SiteProfile.Topic";

    public const string SiteAudience = "AI.SiteProfile.Audience";

    public const string BrandVoice = "AI.SiteProfile.BrandVoice";

    public const string PreferredTerms = "AI.SiteProfile.PreferredTerms";

    public const string AvoidTerms = "AI.SiteProfile.AvoidTerms";

    public const string StyleExamples = "AI.SiteProfile.StyleExamples";

    // AI file search sync metadata
    public const string VectorStoreId = "AI.FileSearch.VectorStoreId";

    public const string ContentFileId = "AI.FileSearch.ContentFileId";

    public const string MediaFileId = "AI.FileSearch.MediaFileId";

    public const string ContentLastSyncAt = "AI.FileSearch.ContentLastSyncAt";

    public const string MediaLastSyncAt = "AI.FileSearch.MediaLastSyncAt";

    public const string FileSyncStatus = "AI.FileSearch.FileSyncStatus";
}
