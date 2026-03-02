// <copyright file="PhoneNormalizationServiceTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Geography;
using LeadCMS.Services;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Tests;

public class PhoneNormalizationServiceTests
{
    private readonly PhoneNormalizationService service;

    public PhoneNormalizationServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SupportedLanguages:0", "en-US" },
            })
            .Build();

        service = new PhoneNormalizationService(configuration);
    }

    [Theory]
    [InlineData("+12025551234", "+12025551234")]
    [InlineData("+442071234567", "+442071234567")]
    [InlineData("+81312345678", "+81312345678")]
    [InlineData("2025551234", "+12025551234")] // Falls back to default US region
    [InlineData("(202) 555-1234", "+12025551234")]
    [InlineData("+1 (202) 555-1234", "+12025551234")]
    [InlineData("1-202-555-1234", "+12025551234")]
    public void Normalize_ValidE164OrDefaultFallback_ReturnsE164(string input, string expected)
    {
        var result = service.Normalize(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrEmpty_ReturnsNull(string? input)
    {
        var result = service.Normalize(input);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("2025551234", Country.US, "+12025551234")]
    [InlineData("02071234567", Country.GB, "+442071234567")]
    [InlineData("0312345678", Country.JP, "+81312345678")]
    public void Normalize_NationalFormat_WithCountry_ReturnsE164(string input, Country country, string expected)
    {
        var result = service.Normalize(input, countryCode: country);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2025551234", "en-US", "+12025551234")]
    [InlineData("02071234567", "en-GB", "+442071234567")]
    [InlineData("0312345678", "ja-JP", "+81312345678")]
    public void Normalize_NationalFormat_WithLanguage_ReturnsE164(string input, string language, string expected)
    {
        var result = service.Normalize(input, language: language);
        result.Should().Be(expected);
    }

    [Fact]
    public void Normalize_CountryTakesPrecedenceOverLanguage()
    {
        // National number that's valid in both US and GB — country should take precedence
        var result = service.Normalize("02071234567", countryCode: Country.GB, language: "en-US");
        result.Should().Be("+442071234567");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12")]
    [InlineData("not-a-phone")]
    public void Normalize_Unparseable_ReturnsNull(string input)
    {
        var result = service.Normalize(input);
        result.Should().BeNull();
    }

    [Fact]
    public void Normalize_UnknownCountry_SkipsCountryStrategy()
    {
        // Country.ZZ (Unknown) should be skipped, fall through to other strategies
        var result = service.Normalize("+12025551234", countryCode: Country.ZZ);
        result.Should().Be("+12025551234");
    }

    [Fact]
    public void Normalize_WhitespaceAround_Trimmed()
    {
        var result = service.Normalize("  +12025551234  ");
        result.Should().Be("+12025551234");
    }
}
