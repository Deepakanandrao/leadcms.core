// <copyright file="TestEmailService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests.TestServices
{
    public class TestEmailService : IEmailService
    {
        public Task<string> SendAsync(string subject, string fromEmail, string fromName, string[] recipients, string body, List<AttachmentDto>? attachments)
        {
            // Test email sender method returns success.
            return Task.FromResult("test-message-id");
        }
    }
}