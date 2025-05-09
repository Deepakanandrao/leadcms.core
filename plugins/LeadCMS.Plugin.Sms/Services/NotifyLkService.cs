// <copyright file="NotifyLkService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Net.Http.Headers;
using LeadCMS.Plugin.Sms.Configuration;
using LeadCMS.Plugin.Sms.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Serilog;

namespace LeadCMS.Plugin.Sms.Services
{
    public class NotifyLkService : ISmsService
    {
        private readonly NotifyLkConfig notifyLkConfig;

        public NotifyLkService(NotifyLkConfig notifyLkConfig)
        {
            this.notifyLkConfig = notifyLkConfig;
        }

        public async Task SendAsync(string recipient, string message)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var queryParams = new Dictionary<string, string>
            {
                ["user_id"] = notifyLkConfig.UserId,
                ["api_key"] = notifyLkConfig.ApiKey,
                ["sender_id"] = notifyLkConfig.SenderId,
                ["to"] = recipient.Substring(1, recipient.Length - 1),
                ["message"] = message,
            };

            var response = await client.GetAsync(QueryHelpers.AddQueryString(notifyLkConfig.ApiUrl, queryParams!));

            if (response.IsSuccessStatusCode)
            {
                Log.Information("Sms message sent to {0} via NotifyLK gateway: {1}", recipient, message);
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();

                throw new NotifyLkException(responseContent);
            }
        }

        public string GetSender(string recipient)
        {
            return notifyLkConfig.SenderId;
        }
    }
}