// <copyright file="TestPipelineStage.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests.TestEntities;

public class TestPipelineStage : DealPipelineStageCreateDto
{
    private static int nextOrder = 1;

    public TestPipelineStage(string uid = "", int pipelineId = 0)
    {
        Name = $"TestPipelineStage{uid}";
        Order = nextOrder++;
        DealPipelineId = pipelineId;
    }
}