// <copyright file="UtmParametersTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using LeadCMS.Models;

namespace LeadCMS.Tests;

/// <summary>
/// Unit tests for <see cref="UtmParameters"/>, <see cref="UtmParametersBuilder"/>,
/// and the <see cref="TemplateArgumentsBuilder.WithUtmParameters"/> integration.
/// </summary>
public class UtmParametersTests
{
    // ────────────────────────────────────────────────────────
    //  UtmParameters — ToDictionary / ToQueryString / HasValues
    // ────────────────────────────────────────────────────────

    [Fact]
    public void ToDictionary_AllFieldsPopulated_ReturnsAllKeys()
    {
        var utm = new UtmParameters
        {
            Source = "sendgrid",
            Medium = "email",
            Campaign = "spring_sale",
            Content = "cta_top",
            Term = "keyword",
            Id = "camp_123",
        };

        var dict = utm.ToDictionary();

        dict.Should().ContainKey("utm_source").WhoseValue.Should().Be("sendgrid");
        dict.Should().ContainKey("utm_medium").WhoseValue.Should().Be("email");
        dict.Should().ContainKey("utm_campaign").WhoseValue.Should().Be("spring_sale");
        dict.Should().ContainKey("utm_content").WhoseValue.Should().Be("cta_top");
        dict.Should().ContainKey("utm_term").WhoseValue.Should().Be("keyword");
        dict.Should().ContainKey("utm_id").WhoseValue.Should().Be("camp_123");
        dict.Should().ContainKey("utm_query");
    }

    [Fact]
    public void ToDictionary_PartialFields_OmitsEmpty()
    {
        var utm = new UtmParameters
        {
            Source = "leadcms",
            Medium = "email",
        };

        var dict = utm.ToDictionary();

        dict.Should().ContainKey("utm_source");
        dict.Should().ContainKey("utm_medium");
        dict.Should().NotContainKey("utm_campaign");
        dict.Should().NotContainKey("utm_content");
        dict.Should().NotContainKey("utm_term");
        dict.Should().NotContainKey("utm_id");
    }

    [Fact]
    public void ToDictionary_Empty_ReturnsEmptyDictionary()
    {
        var utm = new UtmParameters();

        var dict = utm.ToDictionary();

        dict.Should().BeEmpty();
    }

    [Fact]
    public void ToQueryString_TypicalEmail_ReturnsWellFormedString()
    {
        var utm = new UtmParameters
        {
            Source = "leadcms",
            Medium = "email",
            Campaign = "onboarding_day_3",
            Content = "primary_cta",
        };

        var qs = utm.ToQueryString();

        qs.Should().Be("utm_source=leadcms&utm_medium=email&utm_campaign=onboarding_day_3&utm_content=primary_cta");
    }

    [Fact]
    public void ToQueryString_SpecialCharacters_UrlEncodesValues()
    {
        var utm = new UtmParameters
        {
            Source = "lead cms",
            Campaign = "spring & summer",
        };

        var qs = utm.ToQueryString();

        qs.Should().Contain("utm_source=lead+cms");
        qs.Should().Contain("utm_campaign=spring+%26+summer");
    }

    [Fact]
    public void ToQueryString_Empty_ReturnsEmptyString()
    {
        var utm = new UtmParameters();

        utm.ToQueryString().Should().BeEmpty();
    }

    [Fact]
    public void HasValues_WhenAtLeastOneField_ReturnsTrue()
    {
        new UtmParameters { Campaign = "test" }.HasValues.Should().BeTrue();
        new UtmParameters { Source = "x" }.HasValues.Should().BeTrue();
    }

    [Fact]
    public void HasValues_WhenAllEmpty_ReturnsFalse()
    {
        new UtmParameters().HasValues.Should().BeFalse();
        new UtmParameters { Source = string.Empty, Medium = null }.HasValues.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────
    //  UtmParametersBuilder — three-layer override model
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Build_DefaultsOnly_SetsSourceAndMedium()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .Build();

        utm.Source.Should().Be("leadcms");
        utm.Medium.Should().Be("email");
        utm.Campaign.Should().BeNull();
    }

    [Fact]
    public void Build_DefaultsWithCustomValues_OverridesStandardDefaults()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults(source: "sendgrid", medium: "sms")
            .Build();

        utm.Source.Should().Be("sendgrid");
        utm.Medium.Should().Be("sms");
    }

    [Fact]
    public void Build_ContextOverridesDefaults()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "spring_sale", Content = "hero_button" })
            .Build();

        utm.Source.Should().Be("leadcms");
        utm.Medium.Should().Be("email");
        utm.Campaign.Should().Be("spring_sale");
        utm.Content.Should().Be("hero_button");
    }

    [Fact]
    public void Build_ContextCanOverrideDefaultSource()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Source = "newsletter" })
            .Build();

        utm.Source.Should().Be("newsletter");
        utm.Medium.Should().Be("email");
    }

    [Fact]
    public void Build_OverridesWinOverContextAndDefaults()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "spring_sale", Content = "hero_button" })
            .WithOverrides(new UtmParameters { Campaign = "user_campaign", Source = "custom_source" })
            .Build();

        utm.Source.Should().Be("custom_source");
        utm.Medium.Should().Be("email");
        utm.Campaign.Should().Be("user_campaign");
        utm.Content.Should().Be("hero_button");
    }

    [Fact]
    public void Build_NullOverrides_DoNotClearLowerLayers()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "spring_sale" })
            .WithOverrides(null)
            .Build();

        utm.Source.Should().Be("leadcms");
        utm.Campaign.Should().Be("spring_sale");
    }

    [Fact]
    public void Build_EmptyStringOverrides_DoNotClearLowerLayers()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "spring_sale" })
            .WithOverrides(new UtmParameters { Campaign = string.Empty })
            .Build();

        utm.Campaign.Should().Be("spring_sale");
    }

    [Fact]
    public void Build_NoLayers_ReturnsEmptyParameters()
    {
        var utm = UtmParametersBuilder.Create().Build();

        utm.HasValues.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────
    //  UtmParametersBuilder.Merge (static)
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Merge_BothNull_ReturnsEmpty()
    {
        var result = UtmParametersBuilder.Merge(null, null);

        result.HasValues.Should().BeFalse();
    }

    [Fact]
    public void Merge_LowerNull_ClonesHigher()
    {
        var higher = new UtmParameters { Source = "x", Campaign = "y" };

        var result = UtmParametersBuilder.Merge(null, higher);

        result.Source.Should().Be("x");
        result.Campaign.Should().Be("y");

        // Must be a clone, not the same instance
        result.Should().NotBeSameAs(higher);
    }

    [Fact]
    public void Merge_HigherOverridesLower()
    {
        var lower = new UtmParameters { Source = "leadcms", Medium = "email", Campaign = "default" };
        var higher = new UtmParameters { Campaign = "override" };

        var result = UtmParametersBuilder.Merge(lower, higher);

        result.Source.Should().Be("leadcms");
        result.Medium.Should().Be("email");
        result.Campaign.Should().Be("override");
    }

    [Fact]
    public void Merge_DoesNotMutateInputs()
    {
        var lower = new UtmParameters { Campaign = "original" };
        var higher = new UtmParameters { Campaign = "override" };

        UtmParametersBuilder.Merge(lower, higher);

        lower.Campaign.Should().Be("original");
        higher.Campaign.Should().Be("override");
    }

    // ────────────────────────────────────────────────────────
    //  UtmParametersBuilder.FromDictionary
    // ────────────────────────────────────────────────────────

    [Fact]
    public void FromDictionary_ExtractsUtmKeys()
    {
        var dict = new Dictionary<string, object>
        {
            ["utm_source"] = "sendgrid",
            ["utm_campaign"] = "test",
            ["unrelatedKey"] = "ignored",
        };

        var utm = UtmParametersBuilder.FromDictionary(dict);

        utm.Should().NotBeNull();
        utm!.Source.Should().Be("sendgrid");
        utm.Campaign.Should().Be("test");
        utm.Medium.Should().BeNull();
    }

    [Fact]
    public void FromDictionary_NullOrEmpty_ReturnsNull()
    {
        UtmParametersBuilder.FromDictionary(null).Should().BeNull();
        UtmParametersBuilder.FromDictionary(new Dictionary<string, object>()).Should().BeNull();
    }

    [Fact]
    public void FromDictionary_NoUtmKeys_ReturnsNull()
    {
        var dict = new Dictionary<string, object>
        {
            ["firstName"] = "Alice",
            ["email"] = "alice@test.com",
        };

        UtmParametersBuilder.FromDictionary(dict).Should().BeNull();
    }

    [Fact]
    public void FromDictionary_NonStringValues_AreIgnored()
    {
        var dict = new Dictionary<string, object>
        {
            ["utm_source"] = 42,
            ["utm_campaign"] = "valid",
        };

        var utm = UtmParametersBuilder.FromDictionary(dict);

        utm.Should().NotBeNull();
        utm!.Source.Should().BeNull();
        utm.Campaign.Should().Be("valid");
    }

    // ────────────────────────────────────────────────────────
    //  TemplateArgumentsBuilder.WithUtmParameters
    // ────────────────────────────────────────────────────────

    [Fact]
    public void WithUtmParameters_MergesIntoArgs()
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["firstName"] = "Alice",
        };

        var utm = new UtmParameters
        {
            Source = "leadcms",
            Medium = "email",
            Campaign = "test",
        };

        TemplateArgumentsBuilder.WithUtmParameters(args, utm);

        args.Should().ContainKey("firstName").WhoseValue.Should().Be("Alice");
        args.Should().ContainKey("utm_source").WhoseValue.Should().Be("leadcms");
        args.Should().ContainKey("utm_medium").WhoseValue.Should().Be("email");
        args.Should().ContainKey("utm_campaign").WhoseValue.Should().Be("test");
        args.Should().ContainKey("utm_query");
    }

    [Fact]
    public void WithUtmParameters_NullUtm_LeavesArgsUnchanged()
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["firstName"] = "Alice",
        };

        TemplateArgumentsBuilder.WithUtmParameters(args, null);

        args.Should().HaveCount(1);
        args.Should().ContainKey("firstName");
    }

    [Fact]
    public void WithUtmParameters_EmptyUtm_LeavesArgsUnchanged()
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["firstName"] = "Alice",
        };

        TemplateArgumentsBuilder.WithUtmParameters(args, new UtmParameters());

        args.Should().HaveCount(1);
    }

    [Fact]
    public void WithUtmParameters_ReturnsSameDictionary()
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var utm = new UtmParameters { Source = "x" };

        var result = TemplateArgumentsBuilder.WithUtmParameters(args, utm);

        result.Should().BeSameAs(args);
    }

    // ────────────────────────────────────────────────────────
    //  End-to-end: builder → template args
    // ────────────────────────────────────────────────────────

    [Fact]
    public void EndToEnd_BuilderToTemplateArgs_ProducesExpectedKeys()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "onboarding_day_3" })
            .Build();

        var args = TemplateArgumentsBuilder.FromContact(null);
        TemplateArgumentsBuilder.WithUtmParameters(args, utm);

        args.Should().ContainKey("utm_source").WhoseValue.Should().Be("leadcms");
        args.Should().ContainKey("utm_medium").WhoseValue.Should().Be("email");
        args.Should().ContainKey("utm_campaign").WhoseValue.Should().Be("onboarding_day_3");

        var query = args["utm_query"] as string;
        query.Should().Contain("utm_source=leadcms");
        query.Should().Contain("utm_campaign=onboarding_day_3");
    }

    [Fact]
    public void EndToEnd_ThreeLayerOverride_HighestPriorityWins()
    {
        var userOverride = new UtmParameters
        {
            Campaign = "user_custom_campaign",
            Content = "user_cta",
        };

        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "system_campaign", Content = "system_cta" })
            .WithOverrides(userOverride)
            .Build();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        TemplateArgumentsBuilder.WithUtmParameters(args, utm);

        args["utm_source"].Should().Be("leadcms");
        args["utm_medium"].Should().Be("email");
        args["utm_campaign"].Should().Be("user_custom_campaign");
        args["utm_content"].Should().Be("user_cta");
    }

    [Fact]
    public void ToDictionary_UtmQueryContainsAllPopulatedFields()
    {
        var utm = new UtmParameters
        {
            Source = "leadcms",
            Medium = "email",
            Campaign = "test",
        };

        var dict = utm.ToDictionary();
        var query = dict["utm_query"] as string;

        query.Should().NotBeNull();
        query.Should().Contain("utm_source=leadcms");
        query.Should().Contain("utm_medium=email");
        query.Should().Contain("utm_campaign=test");
        query.Should().NotContain("utm_content");
        query.Should().NotContain("utm_term");
    }

    // ────────────────────────────────────────────────────────
    //  Campaign-level UTM overrides
    // ────────────────────────────────────────────────────────

    [Fact]
    public void CampaignOverride_FullOverride_ReplacesAllDefaults()
    {
        var campaignUtm = new UtmParameters
        {
            Source = "partner_site",
            Medium = "sponsored",
            Campaign = "spring_sale_2026",
            Content = "hero_banner",
            Term = "discount",
            Id = "camp_42",
        };

        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "ignored_context" })
            .WithOverrides(campaignUtm)
            .Build();

        utm.Source.Should().Be("partner_site");
        utm.Medium.Should().Be("sponsored");
        utm.Campaign.Should().Be("spring_sale_2026");
        utm.Content.Should().Be("hero_banner");
        utm.Term.Should().Be("discount");
        utm.Id.Should().Be("camp_42");
    }

    [Fact]
    public void CampaignOverride_PartialOverride_PreservesNonOverriddenValues()
    {
        var campaignUtm = new UtmParameters
        {
            Campaign = "spring_sale_2026",
            Content = "promo_header",
        };

        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "original_campaign_name" })
            .WithOverrides(campaignUtm)
            .Build();

        utm.Source.Should().Be("leadcms");
        utm.Medium.Should().Be("email");
        utm.Campaign.Should().Be("spring_sale_2026");
        utm.Content.Should().Be("promo_header");
        utm.Term.Should().BeNull();
    }

    [Fact]
    public void CampaignOverride_NullUtmParameters_FallsBackToContextAndDefaults()
    {
        UtmParameters? campaignUtm = null;

        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "winter_promo" })
            .WithOverrides(campaignUtm)
            .Build();

        utm.Source.Should().Be("leadcms");
        utm.Medium.Should().Be("email");
        utm.Campaign.Should().Be("winter_promo");
    }

    [Fact]
    public void CampaignOverride_OnlyCampaignName_OverridesContextCampaign()
    {
        var campaignUtm = new UtmParameters
        {
            Campaign = "custom_campaign_slug",
        };

        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "My Campaign Name" })
            .WithOverrides(campaignUtm)
            .Build();

        utm.Campaign.Should().Be("custom_campaign_slug");
        utm.Source.Should().Be("leadcms");
        utm.Medium.Should().Be("email");
    }

    [Fact]
    public void CampaignOverride_SourceOverride_ChangesTrafficAttribution()
    {
        var campaignUtm = new UtmParameters
        {
            Source = "partner_newsletter",
        };

        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "co_branded_promo" })
            .WithOverrides(campaignUtm)
            .Build();

        utm.Source.Should().Be("partner_newsletter");
        utm.Medium.Should().Be("email");
        utm.Campaign.Should().Be("co_branded_promo");
    }

    [Fact]
    public void CampaignOverride_EmptyStrings_DoNotClearLowerLayers()
    {
        var campaignUtm = new UtmParameters
        {
            Source = string.Empty,
            Campaign = string.Empty,
        };

        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "context_campaign" })
            .WithOverrides(campaignUtm)
            .Build();

        utm.Source.Should().Be("leadcms");
        utm.Campaign.Should().Be("context_campaign");
    }

    [Fact]
    public void CampaignOverride_EndToEnd_ProducesCorrectTemplateArgs()
    {
        var campaignUtm = new UtmParameters
        {
            Campaign = "holiday_promo_2026",
            Content = "main_cta",
        };

        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "Original Campaign" })
            .WithOverrides(campaignUtm)
            .Build();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        TemplateArgumentsBuilder.WithUtmParameters(args, utm);

        args["utm_source"].Should().Be("leadcms");
        args["utm_medium"].Should().Be("email");
        args["utm_campaign"].Should().Be("holiday_promo_2026");
        args["utm_content"].Should().Be("main_cta");

        var query = args["utm_query"] as string;
        query.Should().Contain("utm_source=leadcms");
        query.Should().Contain("utm_campaign=holiday_promo_2026");
        query.Should().Contain("utm_content=main_cta");
    }

    // ────────────────────────────────────────────────────────
    //  Slugification of Campaign and Content
    // ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Spring Sale 2026", "spring_sale_2026")]
    [InlineData("My Campaign Name", "my_campaign_name")]
    [InlineData("UPPER_CASE", "upper_case")]
    [InlineData("already_slugified", "already_slugified")]
    [InlineData("Mixed-Hyphens_And_Underscores", "mixed_hyphens_and_underscores")]
    [InlineData("  Extra   Spaces  ", "extra_spaces")]
    public void Build_SlugifiesCampaign(string rawCampaign, string expected)
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = rawCampaign })
            .Build();

        utm.Campaign.Should().Be(expected);
    }

    [Theory]
    [InlineData("Hero Button", "hero_button")]
    [InlineData("CTA_Top", "cta_top")]
    [InlineData("footer-link", "footer_link")]
    [InlineData("already_correct", "already_correct")]
    public void Build_SlugifiesContent(string rawContent, string expected)
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Content = rawContent })
            .Build();

        utm.Content.Should().Be(expected);
    }

    [Fact]
    public void Build_SlugifiesCampaignFromOverrides()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithOverrides(new UtmParameters { Campaign = "Holiday Promo 2026" })
            .Build();

        utm.Campaign.Should().Be("holiday_promo_2026");
    }

    [Fact]
    public void Build_SlugifiesNonAsciiCampaign()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Campaign = "Распродажа весна" })
            .Build();

        utm.Campaign.Should().Be("rasprodazha_vesna");
    }

    [Fact]
    public void Build_SlugifiesSourceAndMedium()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults(source: "My Source", medium: "My Medium")
            .Build();

        utm.Source.Should().Be("my_source");
        utm.Medium.Should().Be("my_medium");
    }

    [Fact]
    public void Build_SlugifiesTermAndId()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .WithContext(new UtmParameters { Term = "Paid Search", Id = "Camp 42" })
            .Build();

        utm.Term.Should().Be("paid_search");
        utm.Id.Should().Be("camp_42");
    }

    [Fact]
    public void Build_NullCampaignAndContent_RemainNull()
    {
        var utm = UtmParametersBuilder.Create()
            .WithDefaults()
            .Build();

        utm.Campaign.Should().BeNull();
        utm.Content.Should().BeNull();
    }
}
