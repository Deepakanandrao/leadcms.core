// <copyright file="TestSequenceStep.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Models;

namespace LeadCMS.Tests.TestEntities;

public class TestSequenceStep : SequenceStepCreateDto
{
    public TestSequenceStep(string uid = "", int emailTemplateId = 0, int delayMinutes = 0)
    {
        Name = $"Test Step {uid}";
        EmailTemplateId = emailTemplateId;
        Timing = new SequenceStepTiming
        {
            Delay = new SequenceStepDelay { Value = delayMinutes, Unit = "minutes" },
        };
    }
}
