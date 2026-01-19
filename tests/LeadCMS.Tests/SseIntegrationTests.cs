// <copyright file="SseIntegrationTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using System.Text.Json;

namespace LeadCMS.Tests;

public class SseIntegrationTests : BaseTestAutoLogin
{
    public SseIntegrationTests()
        : base()
    {
        TrackEntityType<Content>();
    }

    [Fact]
    public async Task CheckIfSseSendsContentUpdatesAfterReconnection()
    {
        var query = "?entities=Content&includeContent=true&includeLiveDrafts=true";
        var (resp, stream, reader) = await OpenSseStreamAsync(query);

        try
        {
            // verify connected
            var connected = await GetConnectedClientsAsync();
            Assert.True(connected >= 1, "Expected at least one connected SSE client after connect");

            // create one content and wait for the SSE event triggered by creation
            string contentLocation = string.Empty;
            var createPayload = new TestContent();
            var (evt, data) = await PostAndReadEventAsync(async () => { contentLocation = await PostTest("/api/content", createPayload, HttpStatusCode.Created); }, reader, TimeSpan.FromSeconds(10));

            Assert.False(string.IsNullOrEmpty(evt), "Expected an SSE event after content creation");
            Assert.NotNull(data);

            var contentId = ExtractIdFromLocation(contentLocation);

            // close and wait disconnected
            var connectedAfter = await CloseStreamAndWaitDisconnectedAsync(resp, stream, reader);
            Assert.Equal(0, connectedAfter);

            await Task.Delay(2000);

            // reconnect and update draft body
            var (resp2, stream2, reader2) = await OpenSseStreamAsync(query);
            try
            {
                var updatedPayload = new TestContent { Body = "Updated body after reconnect" };

                var (evt2, data2) = await PostAndReadEventAsync(async () => await PatchTest($"/api/content/{contentId}", updatedPayload, HttpStatusCode.OK), reader2, TimeSpan.FromSeconds(10));

                Assert.False(string.IsNullOrEmpty(evt2), "Expected an SSE event after content update on reconnect");
                Assert.NotNull(data2);
            }
            finally
            {
                await stream2.DisposeAsync();
                reader2.Dispose();
                resp2.Dispose();
            }
        }
        finally
        {
            try
            {
                if (resp != null)
                {
                    resp.Dispose();
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public async Task CheckIfSseSendsDraftUpdatesAfterReconnection()
    {
        var query = "?entities=Content&includeContent=true&includeLiveDrafts=true";
        var (resp, stream, reader) = await OpenSseStreamAsync(query);

        try
        {
            var connected = await GetConnectedClientsAsync();
            Assert.True(connected == 1, "Expected at least one connected SSE client after connect");

            // create one content
            var contentLocation = await CreateContentAsync();
            var contentId = ExtractIdFromLocation(contentLocation);

            // create one draft for that content
            var draftPayload = new TestContent();
            var (evt, data) = await PostAndReadEventAsync(async () => await PatchTest($"/api/content/{contentId}/draft", draftPayload, HttpStatusCode.OK), reader, TimeSpan.FromSeconds(10));

            Assert.False(string.IsNullOrEmpty(evt), "Expected an SSE event after draft creation");
            Assert.NotNull(data);

            // close and ensure disconnected
            var connectedAfter = await CloseStreamAndWaitDisconnectedAsync(resp, stream, reader);
            Assert.Equal(0, connectedAfter);

            await Task.Delay(2000);

            // reconnect and update draft body
            var (resp2, stream2, reader2) = await OpenSseStreamAsync(query);
            try
            {
                var updatedPayload = new TestContent { Body = "Draft update body after reconnect" };
                var (evt2, data2) = await PostAndReadEventAsync(async () => await PatchTest($"/api/content/{contentId}/draft", updatedPayload, HttpStatusCode.OK), reader2, TimeSpan.FromSeconds(10));

                Assert.False(string.IsNullOrEmpty(evt2), "Expected an SSE event after draft update on reconnect");
                Assert.NotNull(data2);
            }
            finally
            {
                await stream2.DisposeAsync();
                reader2.Dispose();
                resp2.Dispose();
            }
        }
        finally
        {
            try
            {
                if (resp != null)
                {
                    resp.Dispose();
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    // Helpers
    private async Task<int> GetConnectedClientsAsync()
    {
        var statsResp = await GetTest("/api/sse/stats", HttpStatusCode.OK);
        var statsStr = await statsResp.Content.ReadAsStringAsync();
        using var statsDoc = JsonDocument.Parse(statsStr);
        return statsDoc.RootElement.GetProperty("connectedClients").GetInt32();
    }

    private int ExtractIdFromLocation(string location)
    {
        if (string.IsNullOrEmpty(location))
        {
            throw new InvalidOperationException("Location header is empty");
        }

        var parts = location.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var last = parts[parts.Length - 1];
        if (int.TryParse(last, out var id))
        {
            return id;
        }

        throw new InvalidOperationException($"Cannot extract id from location '{location}'");
    }

    private async Task<string> CreateContentAsync()
    {
        var createPayload = new TestContent();
        var location = await PostTest("/api/content", createPayload, HttpStatusCode.Created);
        return location;
    }

    /// <summary>
    /// Performs the provided action (usually an API POST/PATCH) and waits for the next meaningful SSE event on the reader.
    /// Returns the event and parsed JSON payload.
    /// </summary>
    private async Task<(string? Event, JsonDocument? Data)> PostAndReadEventAsync(Func<Task> action, StreamReader reader, TimeSpan timeout)
    {
        await action();
        var (evt, data) = await ReadNextMeaningfulSseEventAsync(reader, timeout);
        return (evt, data);
    }

    /// <summary>
    /// Dispose stream/reader/response and wait until SSE stats report 0 connected clients (with small retries).
    /// Returns the final connected clients count.
    /// </summary>
    private async Task<int> CloseStreamAndWaitDisconnectedAsync(HttpResponseMessage resp, Stream stream, StreamReader reader)
    {
        await stream.DisposeAsync();
        reader.Dispose();
        resp.Dispose();

        var attempts = 0;
        var connectedAfter = -1;
        while (attempts++ < 10)
        {
            var statsResp2 = await GetTest("/api/sse/stats", HttpStatusCode.OK);
            var statsStr2 = await statsResp2.Content.ReadAsStringAsync();
            using var statsDoc2 = JsonDocument.Parse(statsStr2);
            connectedAfter = statsDoc2.RootElement.GetProperty("connectedClients").GetInt32();
            if (connectedAfter == 0)
            {
                break;
            }

            await Task.Delay(150);
        }

        return connectedAfter;
    }

    private async Task<(HttpResponseMessage Resp, Stream Stream, StreamReader Reader)> OpenSseStreamAsync(string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/sse/stream{query}");
        // Setting Authorization to null is allowed; assign directly to simplify logic
        request.Headers.Authorization = GetAuthenticationHeaderValue();

        var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        var stream = await resp.Content.ReadAsStreamAsync();
        var reader = new StreamReader(stream, Encoding.UTF8);
        return (resp, stream, reader);
    }

    private async Task<(string? Event, JsonDocument? Data)> ReadSseEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? eventType = null;
        var dataBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null)
            {
                // stream closed
                return (null, null);
            }

            if (line.StartsWith("event:"))
            {
                eventType = line.Substring("event:".Length).Trim();
            }
            else if (line.StartsWith("data:"))
            {
                dataBuilder.AppendLine(line.Substring("data:".Length).Trim());
            }
            else if (string.IsNullOrWhiteSpace(line) && dataBuilder.Length > 0)
            {
                try
                {
                    var json = dataBuilder.ToString();
                    var doc = JsonDocument.Parse(json);
                    return (eventType, doc);
                }
                finally
                {
                    dataBuilder.Clear();
                }
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Reads SSE events, skipping initial noisy events like "connected" and "heartbeat",
    /// and returns the first non-noise event or null when the stream closes or times out.
    /// </summary>
    private async Task<(string? Event, JsonDocument? Data)> ReadNextMeaningfulSseEventAsync(StreamReader reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            var (evt, data) = await ReadSseEventAsync(reader, cts.Token);
            if (evt == null && data == null)
            {
                return (null, null);
            }

            if (string.Equals(evt, "connected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(evt, "heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                // skip and keep waiting
                continue;
            }

            return (evt, data);
        }

        return (null, null);
    }
}
