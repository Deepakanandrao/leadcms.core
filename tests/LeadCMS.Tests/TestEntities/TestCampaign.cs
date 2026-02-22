// <copyright file="TestCampaign.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests.TestEntities;

public class TestCampaign : CampaignCreateDto
{
    public TestCampaign(string uid = "", int templateId = 0, int[]? segmentIds = null)
    {
        Name = $"TestCampaign{uid}";
        Description = $"Test campaign description {uid}";
        EmailTemplateId = templateId;
        SegmentIds = segmentIds ?? Array.Empty<int>();
    }
}
