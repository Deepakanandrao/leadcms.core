// <copyright file="RedirectTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Enums;
using LeadCMS.Helpers;

namespace LeadCMS.Tests;

public class RedirectTests : SimpleTableTests<Redirect, TestRedirect, RedirectUpdateDto, IEntityService<Redirect>>
{
    public RedirectTests()
        : base("/api/redirects")
    {
        TrackEntityType<Content>();
    }

    [Fact]
    public async Task Discover_WhenContentSlugChanges_CreatesAutoRedirect()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var oldSlug = $"redir-old-{uid}";
        var newSlug = $"redir-new-{uid}";

        var content = new TestContent(uid);
        content.Slug = oldSlug;

        var location = await PostTest("/api/content", content);
        var created = await GetTest<ContentDetailsDto>(location);

        await PatchTest($"/api/content/{created!.Id}", new ContentUpdateDto { Slug = newSlug });

        var response = await Request(HttpMethod.Post, "/api/redirects/discover", new object());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var redirects = JsonHelper.Deserialize<List<RedirectDetailsDto>>(json);

        redirects.Should().NotBeNull();

        var redirect = redirects!.FirstOrDefault(r =>
            r.SourceType == RedirectSourceType.ContentSlug &&
            r.FromLanguage == "en" &&
            r.FromSlug == oldSlug &&
            r.ToLanguage == "en" &&
            r.ToSlug == newSlug);

        redirect.Should().NotBeNull();
        redirect!.IsAutoDiscovered.Should().BeTrue();
        redirect!.Kind.Should().Be(RedirectKind.Permanent);
        redirect!.TargetType.Should().Be(RedirectTargetType.ContentSlug);
    }

    [Fact]
    public async Task Discover_WhenManualRedirectExists_IsNotOverwritten()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var oldSlug = $"manual-old-{uid}";
        var newSlug = $"manual-new-{uid}";
        var manualToSlug = $"manual-target-{uid}";

        var content = new TestContent(uid);
        content.Slug = oldSlug;

        var location = await PostTest("/api/content", content);
        var created = await GetTest<ContentDetailsDto>(location);

        await PatchTest($"/api/content/{created!.Id}", new ContentUpdateDto { Slug = newSlug });

        var manual = new RedirectCreateDto
        {
            SourceType = RedirectSourceType.ContentSlug,
            FromLanguage = "en",
            FromSlug = oldSlug,
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.ContentSlug,
            ToLanguage = "en",
            ToSlug = manualToSlug,
        };

        await PostTest("/api/redirects", manual);

        var response = await Request(HttpMethod.Post, "/api/redirects/discover", new object());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var redirects = JsonHelper.Deserialize<List<RedirectDetailsDto>>(json);

        redirects.Should().NotBeNull();

        var existing = redirects!.FirstOrDefault(r =>
            r.SourceType == RedirectSourceType.ContentSlug &&
            r.FromLanguage == "en" &&
            r.FromSlug == oldSlug);

        existing.Should().NotBeNull();
        existing!.ToSlug.Should().Be(manualToSlug);
        existing!.IsAutoDiscovered.Should().BeFalse();
    }

    [Fact]
    public async Task Discover_WhenSlugChangedTwice_CreatesChainedRedirects()
    {
        // The discovery SQL produces one redirect per distinct (old_slug, old_language) pair,
        // so a two-step rename old→mid→new produces two separate auto-discovered redirects:
        // old→mid (created on first discover, unchanged on second) and mid→new (created on second).
        var uid = Guid.NewGuid().ToString("N")[..8];
        var oldSlug = $"auto-old-{uid}";
        var midSlug = $"auto-mid-{uid}";
        var newSlug = $"auto-new-{uid}";

        var content = new TestContent(uid);
        content.Slug = oldSlug;

        var location = await PostTest("/api/content", content);
        var created = await GetTest<ContentDetailsDto>(location);

        await PatchTest($"/api/content/{created!.Id}", new ContentUpdateDto { Slug = midSlug });

        var firstDiscoverResponse = await Request(HttpMethod.Post, "/api/redirects/discover", new object());
        firstDiscoverResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await PatchTest($"/api/content/{created!.Id}", new ContentUpdateDto { Slug = newSlug });

        var response = await Request(HttpMethod.Post, "/api/redirects/discover", new object());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var redirects = JsonHelper.Deserialize<List<RedirectDetailsDto>>(json);

        redirects.Should().NotBeNull();

        redirects!.Should().Contain(
            r =>
                r.IsAutoDiscovered &&
                r.SourceType == RedirectSourceType.ContentSlug &&
                r.FromSlug == oldSlug && r.ToSlug == midSlug,
            "first rename should create old→mid redirect");

        redirects!.Should().Contain(
            r =>
                r.IsAutoDiscovered &&
                r.SourceType == RedirectSourceType.ContentSlug &&
                r.FromSlug == midSlug && r.ToSlug == newSlug,
            "second rename should create mid→new redirect");
    }

    [Fact]
    public async Task Discover_ReturnsAllStoredRedirects()
    {
        var manual = new TestRedirect(Guid.NewGuid().ToString("N")[..8]);

        await PostTest("/api/redirects", manual);

        var response = await Request(HttpMethod.Post, "/api/redirects/discover", new object());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var redirects = JsonHelper.Deserialize<List<RedirectDetailsDto>>(json);

        redirects.Should().NotBeNull();
        redirects!.Should().Contain(r => r.FromPath == manual.FromPath);
    }

    // RedirectKind uses non-zero-based values (301/302), so Activator.CreateInstance produces
    // an invalid default (0). Override the base test to use a known-valid filter value instead.
    [Fact]
    public override async Task ValidWherePropertyType()
    {
        await GetTest(
            $"{itemsUrl}?{System.Web.HttpUtility.UrlEncode($"filter[where][Kind][eq]={RedirectKind.Permanent}")}",
            HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // Suppression tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_AutoDiscoveredRedirect_SuppressesAndDoesNotReDiscover()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var oldSlug = $"supp-old-{uid}";
        var newSlug = $"supp-new-{uid}";

        // Create content and rename its slug to trigger auto-discovery.
        var content = new TestContent(uid);
        content.Slug = oldSlug;
        var location = await PostTest("/api/content", content);
        var created = await GetTest<ContentDetailsDto>(location);
        await PatchTest($"/api/content/{created!.Id}", new ContentUpdateDto { Slug = newSlug });

        // Discover: creates auto-discovered redirect old→new.
        var discoverResponse = await Request(HttpMethod.Post, "/api/redirects/discover", new object());
        discoverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var redirects = JsonHelper.Deserialize<List<RedirectDetailsDto>>(
            await discoverResponse.Content.ReadAsStringAsync());
        var autoRedirect = redirects!.First(r =>
            r.SourceType == RedirectSourceType.ContentSlug &&
            r.FromSlug == oldSlug &&
            r.IsAutoDiscovered);

        // Delete the auto-discovered redirect (should suppress, not physically delete).
        await DeleteTest($"/api/redirects/{autoRedirect.Id}");

        // Discover again: the suppressed source must not be re-created.
        var secondDiscover = await Request(HttpMethod.Post, "/api/redirects/discover", new object());
        secondDiscover.StatusCode.Should().Be(HttpStatusCode.OK);
        var redirectsAfter = JsonHelper.Deserialize<List<RedirectDetailsDto>>(
            await secondDiscover.Content.ReadAsStringAsync());

        redirectsAfter!.Should().NotContain(
            r => r.SourceType == RedirectSourceType.ContentSlug && r.FromSlug == oldSlug,
            "suppressed auto-discovered redirects must not be re-created by the discover process");
    }

    // -------------------------------------------------------------------------
    // Duplicate source tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithDuplicateSource_Returns409()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var dto1 = new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = $"/dup-{uid}",
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = $"/dest-a-{uid}",
        };
        var dto2 = new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = $"/dup-{uid}",  // same source
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = $"/dest-b-{uid}",
        };

        await PostTest("/api/redirects", dto1);
        var response = await Request(HttpMethod.Post, "/api/redirects", dto2);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "duplicate source must return 409");
    }

    [Fact]
    public async Task Update_WithDuplicateSource_Returns409()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var dto1 = new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = $"/upd-a-{uid}",
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = $"/dest-a-{uid}",
        };
        var dto2 = new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = $"/upd-b-{uid}",
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = $"/dest-b-{uid}",
        };

        await PostTest("/api/redirects", dto1);
        var r2Location = await PostTest("/api/redirects", dto2);
        var r2 = await GetTest<RedirectDetailsDto>(r2Location);

        // Try to update redirect 2's source to match redirect 1's source.
        var patch = new RedirectUpdateDto { FromPath = dto1.FromPath };
        var response = await Request(HttpMethod.Patch, $"/api/redirects/{r2!.Id}", patch);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "patching to a duplicate source must return 409");
    }

    // -------------------------------------------------------------------------
    // Cycle detection tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithDirectCycle_Returns422()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var pathA = $"/cycle-a-{uid}";
        var pathB = $"/cycle-b-{uid}";

        // Create A→B.
        await PostTest("/api/redirects", new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = pathA,
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = pathB,
        });

        // Try to create B→A: this would form the cycle A→B→A.
        var response = await Request(HttpMethod.Post, "/api/redirects", new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = pathB,
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = pathA,
        });

        response.StatusCode.Should().Be(
            HttpStatusCode.UnprocessableEntity,
            "creating a redirect that would form a direct cycle must return 422");
    }

    [Fact]
    public async Task Update_IntoCycle_Returns422()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var pathA = $"/ucycle-a-{uid}";
        var pathB = $"/ucycle-b-{uid}";
        var pathC = $"/ucycle-c-{uid}";

        // Create A→B and then B→C — a chain but no cycle.
        await PostTest("/api/redirects", new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = pathA,
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = pathB,
        });

        var r2Location = await PostTest("/api/redirects", new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = pathB,
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = pathC,
        });
        var r2 = await GetTest<RedirectDetailsDto>(r2Location);

        // Patch B→C to B→A: this would complete the cycle A→B→A.
        var response = await Request(
            HttpMethod.Patch,
            $"/api/redirects/{r2!.Id}",
            new RedirectUpdateDto { ToPath = pathA });

        response.StatusCode.Should().Be(
            HttpStatusCode.UnprocessableEntity,
            "patching a redirect to create a cycle must return 422");
    }

    // -------------------------------------------------------------------------
    // Discover query-parameter support
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Discover_WithWhereFilter_ReturnsOnlyMatchingRedirects()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var pathMatch = $"/filter-match-{uid}";
        var pathNoMatch = $"/filter-nomatch-{uid}";

        // Create two manual redirects with different paths.
        await PostTest("/api/redirects", new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = pathMatch,
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = $"/target-a-{uid}",
        });
        await PostTest("/api/redirects", new RedirectCreateDto
        {
            SourceType = RedirectSourceType.InternalPath,
            FromPath = pathNoMatch,
            Kind = RedirectKind.Permanent,
            TargetType = RedirectTargetType.InternalPath,
            ToPath = $"/target-b-{uid}",
        });

        // Call /discover with a filter that only matches the first redirect.
        var filter = System.Web.HttpUtility.UrlEncode($"filter[where][FromPath][eq]={pathMatch}");
        var response = await Request(HttpMethod.Post, $"/api/redirects/discover?{filter}", new object());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("X-Total-Count", out var totalCountValues);
        totalCountValues.Should().NotBeNull("discover should return X-Total-Count header");

        var json = await response.Content.ReadAsStringAsync();
        var redirects = JsonHelper.Deserialize<List<RedirectDetailsDto>>(json);

        redirects.Should().NotBeNull();
        redirects!.Should().ContainSingle(r => r.FromPath == pathMatch);
        redirects!.Should().NotContain(r => r.FromPath == pathNoMatch);
    }

    [Fact]
    public async Task Discover_WithLimitAndSkip_RespectsPageSize()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];

        // Create 3 manual redirects.
        for (var i = 1; i <= 3; i++)
        {
            await PostTest("/api/redirects", new RedirectCreateDto
            {
                SourceType = RedirectSourceType.InternalPath,
                FromPath = $"/page-test-{uid}-{i}",
                Kind = RedirectKind.Permanent,
                TargetType = RedirectTargetType.InternalPath,
                ToPath = $"/page-target-{uid}-{i}",
            });
        }

        // Discover with limit=1.
        var response = await Request(HttpMethod.Post, "/api/redirects/discover?filter[limit]=1", new object());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var redirects = JsonHelper.Deserialize<List<RedirectDetailsDto>>(json);

        redirects.Should().NotBeNull();
        redirects!.Should().HaveCount(1);
    }

    protected override RedirectUpdateDto UpdateItem(TestRedirect to)
    {
        var from = new RedirectUpdateDto();
        to.ToPath = from.ToPath = $"{to.ToPath}-updated";
        return from;
    }
}
