// <copyright file="EmailServiceTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using LeadCMS.Services;
using MimeKit;

namespace LeadCMS.Tests;

public class EmailServiceTests
{
    [Fact]
    public async Task GenerateEmailBody_ShouldAssignMessageId()
    {
        var method = typeof(EmailService).GetMethod("GenerateEmailBody", BindingFlags.NonPublic | BindingFlags.Static);
        var parameters = new object?[]
        {
            "Subject",
            "from@example.com",
            "Sender",
            new[] { "to@example.com" },
            "<p>Hello</p>",
            null,
        };

        method.Should().NotBeNull();

        var task = method!.Invoke(null, parameters) as Task<MimeMessage>;

        task.Should().NotBeNull();

        var message = await task!;

        message.MessageId.Should().NotBeNullOrWhiteSpace();
    }
}