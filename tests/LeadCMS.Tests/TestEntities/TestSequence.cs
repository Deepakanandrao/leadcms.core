// <copyright file="TestSequence.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Models;

namespace LeadCMS.Tests.TestEntities;

public class TestSequence : SequenceCreateDto
{
    public TestSequence(string uid = "")
    {
        Name = $"TestSequence{uid}";
        Description = $"Test sequence description {uid}";
        Language = "en";
        StopOnReply = false;
        UseContactTimeZone = false;
        TimeZone = 0;
        Enrollment = new SequenceEnrollmentConfig
        {
            Modes = new[] { "manual", "api" },
            ReentryPolicy = ReentryPolicy.OnceEver,
        };
    }
}
