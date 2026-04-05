// <copyright file="DeploymentNotifyTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class DeploymentNotifyTests : BaseTest
{
    private const string ApiKey = "test-deploy-key-12345";
    private string? apiKeyHeader;

    public DeploymentNotifyTests()
        : base()
    {
        TrackEntityType<Setting>();
    }

    [Fact]
    public async Task Notify_WithoutApiKeyHeader_Returns401()
    {
        apiKeyHeader = null;

        var response = await Request(HttpMethod.Post, "/api/deployments/notify", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Notify_WithInvalidApiKey_Returns401()
    {
        await SeedDeploymentApiKey(ApiKey);
        apiKeyHeader = "wrong-key";

        var response = await Request(HttpMethod.Post, "/api/deployments/notify", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Notify_WithNoConfiguredKey_Returns401()
    {
        // No key seeded in DB
        apiKeyHeader = ApiKey;

        var response = await Request(HttpMethod.Post, "/api/deployments/notify", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Notify_WithValidApiKey_Returns200AndUpdatesLastReleaseDate()
    {
        await SeedDeploymentApiKey(ApiKey);
        apiKeyHeader = ApiKey;

        var beforeNotify = DateTime.UtcNow;

        var response = await Request(HttpMethod.Post, "/api/deployments/notify", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify General.LastReleaseDate was set
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        var setting = await dbContext.Settings!
            .FirstOrDefaultAsync(s => s.Key == SettingKeys.DeploymentLastSuccessDate && s.UserId == null);

        Assert.NotNull(setting);
        Assert.NotNull(setting.Value);

        var releaseDateParsed = DateTime.Parse(setting.Value, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.True(releaseDateParsed >= beforeNotify.AddSeconds(-1));
        Assert.True(releaseDateParsed <= DateTime.UtcNow.AddSeconds(1));
    }

    protected override Task<HttpResponseMessage> Request(HttpMethod method, string url, object? payload)
    {
        var request = new HttpRequestMessage(method, url);

        if (payload != null)
        {
            request.Content = PayloadToStringContent(payload);
        }

        if (!string.IsNullOrEmpty(apiKeyHeader))
        {
            request.Headers.Add("X-Deployment-Api-Key", apiKeyHeader);
        }

        return client.SendAsync(request);
    }

    private async Task SeedDeploymentApiKey(string key)
    {
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.PgDbContext>();

        dbContext.Settings!.Add(new Setting
        {
            Key = SettingKeys.DeploymentWebhooksApiKey,
            Value = key,
            Type = SettingValueTypes.Secret,
            UserId = null,
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();
    }
}
