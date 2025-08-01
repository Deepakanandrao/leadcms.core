// <copyright file="SmscService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using System.Text.Json;
using LeadCMS.Plugin.Sms.Configuration;
using LeadCMS.Plugin.Sms.Exceptions;

namespace LeadCMS.Plugin.Sms.Services;

public class SmscService : ISmsService
{
    private readonly SmscConfig smscConfig;

    public SmscService(SmscConfig smscConfig)
    {
        this.smscConfig = smscConfig;
    }

    public async Task SendAsync(string recipient, string message)
    {
        var responseString = string.Empty;
        var data = JsonSerializer.Serialize(new
        {
            login = smscConfig.Login,
            psw = smscConfig.Password,
            phones = recipient,
            mes = message,
            sender = smscConfig.SenderId,
            fmt = 3,
            charset = "utf-8",
        });

        using (var httpClient = new HttpClient())
        {
            var response = await httpClient.PostAsync(smscConfig.ApiUrl, new StringContent(data, Encoding.UTF8));
            responseString = await response.Content.ReadAsStringAsync();
        }

        using var jsonDocument = JsonDocument.Parse(responseString);
        if (jsonDocument.RootElement.TryGetProperty("error", out var errorElement))
        {
            throw new SmscException($"Failed to send message to {recipient} ( {errorElement.GetString()} )");
        }
    }

    public string GetSender(string recipient)
    {
        return smscConfig.SenderId;
    }
}