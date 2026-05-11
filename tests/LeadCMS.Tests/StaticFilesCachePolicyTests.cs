// <copyright file="StaticFilesCachePolicyTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Tests;

public sealed class StaticFilesCachePolicyTests : IDisposable
{
    private static string configuredWebRootPath = string.Empty;

    private readonly string webRootPath;
    private readonly StaticFilesTestApplication app;
    private readonly HttpClient client;

    public StaticFilesCachePolicyTests()
    {
        webRootPath = Path.Combine(Path.GetTempPath(), $"leadcms-static-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRootPath);

        System.IO.File.WriteAllText(Path.Combine(webRootPath, "index.html"), "<html><body>LeadCMS</body></html>");
        System.IO.File.WriteAllText(Path.Combine(webRootPath, "main.3a42981b.js"), "console.log('bundle');");
        System.IO.File.WriteAllBytes(Path.Combine(webRootPath, "favicon.ico"), new byte[] { 0x00, 0x01, 0x00, 0x00 });

        app = StaticFilesTestApplication.Create(webRootPath);
        client = app.CreateClient();
    }

    [Fact]
    public async Task StaticFiles_ShouldSetCacheHeadersByAssetType()
    {
        var indexResponse = await client.GetAsync("/index.html");
        indexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCacheControlDirectives(indexResponse, "no-cache", "must-revalidate");

        var bundleResponse = await client.GetAsync("/main.3a42981b.js");
        bundleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCacheControlDirectives(bundleResponse, "public", "max-age=31536000", "immutable");

        var publicAssetResponse = await client.GetAsync("/favicon.ico");
        publicAssetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCacheControlDirectives(publicAssetResponse, "no-cache", "must-revalidate");
    }

    [Fact]
    public async Task StaticFiles_ShouldServeCompressedBundle_WhenClientAcceptsBrotli()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/main.3a42981b.js");
        request.Headers.AcceptEncoding.ParseAdd("br");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.Should().Contain("br");
        response.Headers.Vary.Should().Contain("Accept-Encoding");
    }

    public void Dispose()
    {
        client.Dispose();

        try
        {
            app.Dispose();
        }
        catch (ObjectDisposedException ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is ObjectDisposedException))
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }

        if (Directory.Exists(webRootPath))
        {
            Directory.Delete(webRootPath, true);
        }
    }

    private static void AssertCacheControlDirectives(HttpResponseMessage response, params string[] expectedDirectives)
    {
        var actualDirectives = response.Headers.GetValues("Cache-Control")
            .Single()
            .Split(',')
            .Select(part => part.Trim())
            .ToArray();

        actualDirectives.Should().BeEquivalentTo(expectedDirectives);
    }

    private sealed class StaticFilesTestApplication : WebApplicationFactory<Program>
    {
        private StaticFilesTestApplication()
        {
        }

        public static StaticFilesTestApplication Create(string webRootPath)
        {
            configuredWebRootPath = webRootPath;
            return new StaticFilesTestApplication();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Disable DB migration: this factory only serves static files and has no database needs.
            // Without this override, Program.Main picks up MigrateOnStart=true from the base
            // appsettings.json and tries port 5432, which is unavailable in CI (Docker exposes 15432).
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["MigrateOnStart"] = "false" }));

            builder.UseWebRoot(configuredWebRootPath);
        }
    }
}