// <copyright file="MdxParserTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Services;

namespace LeadCMS.Tests;

/// <summary>
/// Tests for MDX parser functionality, specifically focusing on sample generation that preserves valid MDX structure.
/// </summary>
public class MdxParserTests
{
    /// <summary>
    /// Tests that simple MDX components are preserved when under the truncation limit.
    /// </summary>
    [Fact]
    public void ParseMdx_SimpleComponent_PreservesStructure()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = "<Button variant=\"primary\" onClick={handleClick}>Click me</Button>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Button");
        result[0].Examples.Should().HaveCount(1);
        result[0].Examples[0].Should().Be(mdxContent); // Should be unchanged since it's under 200 chars
    }

    /// <summary>
    /// Tests that long MDX components are truncated while preserving valid structure.
    /// </summary>
    [Fact]
    public void ParseMdx_LongComponent_TruncatesWhilePreservingStructure()
    {
        // Arrange
        var parser = new MdxParser();
        var longText = new string('a', 300); // Create a very long string
        var mdxContent = $"<Button variant=\"primary\" onClick={{handleClick}} className=\"{longText}\">Click me</Button>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Button");
        result[0].Examples.Should().HaveCount(1);

        var example = result[0].Examples[0];
        example.Should().StartWith("<Button");
        example.Should().EndWith(">");
        example.Length.Should().BeLessOrEqualTo(203); // 200 + "..." = 203
    }

    /// <summary>
    /// Tests that self-closing components are handled correctly during truncation.
    /// </summary>
    [Fact]
    public void ParseMdx_SelfClosingComponent_PreservesClosingSlash()
    {
        // Arrange
        var parser = new MdxParser();
        var longText = new string('b', 300);
        var mdxContent = $"<Image src=\"/path/to/image.jpg\" alt=\"{longText}\" width={{800}} height={{600}} />";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Image");
        result[0].Examples.Should().HaveCount(1);

        var example = result[0].Examples[0];
        example.Should().StartWith("<Image");
        example.Should().EndWith("/>"); // Should preserve self-closing structure
        example.Length.Should().BeLessOrEqualTo(203);
    }

    /// <summary>
    /// Tests that JSX expressions in props are properly handled during truncation.
    /// </summary>
    [Fact]
    public void ParseMdx_ComponentWithJSXExpressions_PreservesExpressionStructure()
    {
        // Arrange
        var parser = new MdxParser();
        var longObject = "{ key1: 'value1', key2: 'value2', key3: 'value3', key4: 'value4', key5: 'value5', key6: 'value6', key7: 'value7', key8: 'value8', key9: 'value9', key10: 'value10', key11: 'value11', key12: 'value12' }";
        var mdxContent = $"<ComplexComponent data={{{longObject}}} isVisible={{true}} />";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("ComplexComponent");
        result[0].Examples.Should().HaveCount(1);

        var example = result[0].Examples[0];
        example.Should().StartWith("<ComplexComponent");
        example.Should().EndWith("/>");

        // Should not have unmatched braces
        var openBraces = example.Count(c => c == '{');
        var closeBraces = example.Count(c => c == '}');
        openBraces.Should().Be(closeBraces);
    }

    /// <summary>
    /// Tests that quoted strings in props are properly handled during truncation.
    /// </summary>
    [Fact]
    public void ParseMdx_ComponentWithQuotedStrings_PreservesQuotes()
    {
        // Arrange
        var parser = new MdxParser();
        var longText = new string('c', 300);
        var mdxContent = $"<TextComponent title=\"{longText}\" subtitle='{longText}' />";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("TextComponent");
        result[0].Examples.Should().HaveCount(1);

        var example = result[0].Examples[0];
        example.Should().StartWith("<TextComponent");
        example.Should().EndWith("/>");

        // Should not have unmatched quotes
        var doubleQuotes = example.Count(c => c == '"');
        var singleQuotes = example.Count(c => c == '\'');
        (doubleQuotes % 2).Should().Be(0, "Double quotes should be balanced");
        (singleQuotes % 2).Should().Be(0, "Single quotes should be balanced");
    }

    /// <summary>
    /// Tests that components with children are truncated at appropriate boundaries.
    /// </summary>
    [Fact]
    public void ParseMdx_ComponentWithChildren_TruncatesAtBoundaries()
    {
        // Arrange
        var parser = new MdxParser();
        var longText = new string('d', 300);
        var mdxContent = $"<Card title=\"Card Title\"><CardBody>{longText}</CardBody></Card>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(2); // Card and CardBody

        var cardComponent = result.FirstOrDefault(c => c.Name == "Card");
        cardComponent.Should().NotBeNull();
        cardComponent!.Examples.Should().HaveCount(1);

        var example = cardComponent.Examples[0];
        example.Should().StartWith("<Card");
        // Should end with a valid structure (either self-closing or with closing tag)
        (example.EndsWith('>') || example.EndsWith("/>") || example.EndsWith("</Card>")).Should().BeTrue();
    }

    /// <summary>
    /// Tests that property values are truncated while preserving their type structure.
    /// </summary>
    [Fact]
    public void ParseMdx_ComponentProperties_PreservesPropertyValueStructure()
    {
        // Arrange
        var parser = new MdxParser();
        var longString = new string('e', 150);
        var mdxContent = $"<TestComponent stringProp=\"{longString}\" objectProp={{{string.Join(", ", Enumerable.Range(1, 50).Select(i => $"key{i}: 'value{i}'"))}}} />";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("TestComponent");
        result[0].Properties.Should().HaveCount(2);

        var stringProp = result[0].Properties.FirstOrDefault(p => p.Name == "stringProp");
        stringProp.Should().NotBeNull();
        stringProp!.ExampleValues.Should().HaveCount(1);

        var stringExample = stringProp.ExampleValues[0];
        stringExample.Should().StartWith("\"");
        stringExample.Should().EndWith("\"");

        var objectProp = result[0].Properties.FirstOrDefault(p => p.Name == "objectProp");
        objectProp.Should().NotBeNull();
        objectProp!.ExampleValues.Should().HaveCount(1);

        var objectExample = objectProp.ExampleValues[0];
        objectExample.Should().StartWith("{");
        objectExample.Should().EndWith("}");
    }

    /// <summary>
    /// Tests that components with dotted names (like TwoColumns.HalfWidthColumn) are parsed correctly.
    /// </summary>
    [Fact]
    public void ParseMdx_DottedComponentNames_ParsesCorrectly()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"<Section>
    <TwoColumns>
        <TwoColumns.HalfWidthColumn>
            Irregular shaped plots

            AI-driven design maximises every square metre of buildable space.
        </TwoColumns.HalfWidthColumn>
        <TwoColumns.HalfWidthColumn>
            Complex logistics

            Our process accounts for logistical constraints during the design phase.
        </TwoColumns.HalfWidthColumn>
    </TwoColumns>
</Section>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(3); // Section, TwoColumns, TwoColumns.HalfWidthColumn

        var sectionComponent = result.FirstOrDefault(c => c.Name == "Section");
        sectionComponent.Should().NotBeNull();

        var twoColumnsComponent = result.FirstOrDefault(c => c.Name == "TwoColumns");
        twoColumnsComponent.Should().NotBeNull();

        var halfWidthComponent = result.FirstOrDefault(c => c.Name == "TwoColumns.HalfWidthColumn");
        halfWidthComponent.Should().NotBeNull();
        halfWidthComponent!.UsageCount.Should().Be(2); // Used twice in the example
        halfWidthComponent.AcceptsChildren.Should().BeTrue(); // Has text content inside
    }

    /// <summary>
    /// Tests that components with dotted names are parsed correctly.
    /// </summary>
    [Fact]
    public void ParseMdx_WithDottedComponentNames_ShouldParseCorrectly()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"<Section>
    <TwoColumns>
        <TwoColumns.HalfWidthColumn>
            Content 1
        </TwoColumns.HalfWidthColumn>
        <TwoColumns.HalfWidthColumn>
            Content 2
        </TwoColumns.HalfWidthColumn>
    </TwoColumns>
</Section>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(3);

        var sectionComponent = result.FirstOrDefault(c => c.Name == "Section");
        sectionComponent.Should().NotBeNull();

        var twoColumnsComponent = result.FirstOrDefault(c => c.Name == "TwoColumns");
        twoColumnsComponent.Should().NotBeNull();

        var dottedComponent = result.FirstOrDefault(c => c.Name == "TwoColumns.HalfWidthColumn");
        dottedComponent.Should().NotBeNull();
        dottedComponent!.UsageCount.Should().Be(2);
        dottedComponent.AcceptsChildren.Should().BeTrue();
    }

    /// <summary>
    /// Comprehensive test to verify that MDX parser improvements work correctly.
    /// </summary>
    [Fact]
    public void MdxParser_TruncationImprovements_WorkCorrectly()
    {
        // Arrange
        var parser = new MdxParser();

        // Test 1: Long component should be truncated but remain valid
        var longText = new string('x', 300);
        var longComponent = $"<Button className=\"{longText}\" onClick={{handleClick}}>Click me</Button>";

        // Act
        var result = parser.ParseMdx(longComponent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Examples.Should().HaveCount(1);

        var sample = result[0].Examples[0];

        // Verify it's valid MDX (starts with < and ends with > or />)
        var isValid = sample.StartsWith('<') && (sample.EndsWith('>') || sample.EndsWith("/>"));
        isValid.Should().BeTrue("MDX sample should have valid structure");

        // Test 2: JSX expressions should have balanced braces
        var openBraces = sample.Count(c => c == '{');
        var closeBraces = sample.Count(c => c == '}');
        openBraces.Should().Be(closeBraces, "JSX expressions should have balanced braces");

        // Test 3: Quotes should be balanced
        var doubleQuotes = sample.Count(c => c == '"');
        (doubleQuotes % 2).Should().Be(0, "Double quotes should be balanced");
    }

    /// <summary>
    /// Tests that complex JSX arrays/objects are truncated correctly to maintain valid syntax.
    /// </summary>
    [Fact]
    public void MdxParser_ComplexJsxArraysTruncation_MaintainsValidSyntax()
    {
        // Arrange
        var parser = new MdxParser();

        // Create the exact failing case from the user's report
        var complexComponent = @"<WhySection
  title=""Why LeadCMS?""
  description=""Built with developers in mind, LeadCMS gives you complete control over your sales and content platform.""
  reasons={[
{
icon: ""Code"",
title: ""100% Open Source"",
description: ""Available on GitHub under the MIT license for full code ownership and transparency. No vendor lock‑in or hidden costs.""
},
{
icon: ""Server"",
title: ""Self‑Hosted or Cloud"",
description: ""Docker‑ready for fast, flexible deployment anywhere: your private cloud, on‑premises, or a managed host.""
},
{
icon: ""GitBranch"",
title: ""Developer‑First Workflow"",
description: ""Use Git to version content changes, integrate with CI/CD, and extend via plugins or direct source code edits.""
},
{
icon: ""Package"",
title: ""Modular Architecture"",
description: ""Pick only the features you need. Add new plugins for custom functionality or integrations anytime.""
},
{
icon: ""Lock"",
title: ""Built‑In Licensing"",
description: ""Automatically provision free trials and manage recurring subscriptions. Generate and validate license keys with zero friction.""
},
{
icon: ""Globe"",
title: ""API-First Design"",
description: ""Comprehensive API endpoints for seamless integration with your existing tools and workflows. Build custom solutions with ease.""
}
]}
/>";

        // Act
        var result = parser.ParseMdx(complexComponent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("WhySection");
        result[0].Examples.Should().HaveCount(1);

        var sample = result[0].Examples[0];

        // Verify it's valid MDX structure
        sample.Should().StartWith("<WhySection");
        sample.Should().EndWith("/>");

        // Verify JSX braces are balanced
        var openBraces = sample.Count(c => c == '{');
        var closeBraces = sample.Count(c => c == '}');
        openBraces.Should().Be(closeBraces, "JSX expressions should have balanced braces");

        // Verify quotes are balanced
        var doubleQuotes = sample.Count(c => c == '"');
        (doubleQuotes % 2).Should().Be(0, "Double quotes should be balanced");

        // The sample should be truncated but syntactically valid
        sample.Length.Should().BeLessOrEqualTo(203); // 200 + "..." = 203
        sample.Should().NotContain("...} />", "Should not have broken JSX syntax");
    }

    /// <summary>
    /// Tests that components with nested children always have valid closing tags in truncated examples.
    /// This addresses the specific issue where truncated examples lack proper closing tags.
    /// </summary>
    [Fact]
    public void ParseMdx_NestedComponentsWithTruncation_AlwaysHaveValidClosingTags()
    {
        // Arrange
        var parser = new MdxParser();

        // Create a scenario similar to the user's issue with nested components and long content
        var longContent = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"This is line {i} with some content that makes the example very long."));
        var mdxContent = $@"<Section withLargeTopPadding>
  <SectionTitle>E-Mail <TextLink to=""mailto:info@all3.com"">info@all3.com</TextLink></SectionTitle>

  {longContent}

  <AccentTitle />
</Section>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        var sectionComponent = result.FirstOrDefault(c => c.Name == "Section");
        sectionComponent.Should().NotBeNull();
        sectionComponent!.Examples.Should().HaveCount(1);

        var example = sectionComponent.Examples[0];

        // Verify the example is a valid, complete MDX component
        example.Should().StartWith("<Section");

        // The example should either be self-closing or have a proper closing tag
        var isValidStructure = example.EndsWith("/>") || example.EndsWith("</Section>");
        isValidStructure.Should().BeTrue("Component example should have valid closing structure");

        // Verify JSX braces are balanced
        var openBraces = example.Count(c => c == '{');
        var closeBraces = example.Count(c => c == '}');
        openBraces.Should().Be(closeBraces, "JSX expressions should have balanced braces");

        // Verify quotes are balanced
        var doubleQuotes = example.Count(c => c == '"');
        (doubleQuotes % 2).Should().Be(0, "Double quotes should be balanced");

        // Verify the example doesn't end with broken syntax (incomplete opening tags)
        if (example.EndsWith('>') && !example.EndsWith("/>") && !example.EndsWith("</Section>"))
        {
            // If it ends with ">", it should be a valid closing tag, not an incomplete opening tag
            var endsWithValidClosing = System.Text.RegularExpressions.Regex.IsMatch(example, @"</[A-Za-z][A-Za-z0-9]*>$");
            endsWithValidClosing.Should().BeTrue("If ending with '>', it should be a valid closing tag");
        }
    }

    /// <summary>
    /// Tests that components with multiple levels of nesting maintain valid structure when truncated.
    /// </summary>
    [Fact]
    public void ParseMdx_DeeplyNestedComponents_MaintainValidStructureWhenTruncated()
    {
        // Arrange
        var parser = new MdxParser();

        // Create deeply nested structure with long content that will trigger truncation
        var longText = new string('x', 300);
        var mdxContent = $@"<Container>
  <Header>
    <Navigation>
      <NavItem href=""/home"">Home</NavItem>
      <NavItem href=""/about"">About</NavItem>
    </Navigation>
  </Header>
  <Main>
    <Article>
      <ArticleTitle>Long Article Title That Should Be Truncated</ArticleTitle>
      <ArticleContent>
        {longText}
      </ArticleContent>
    </Article>
  </Main>
</Container>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        // Check that all components have valid examples
        foreach (var component in result)
        {
            component.Examples.Should().NotBeEmpty($"Component {component.Name} should have at least one example");

            // Check each example for valid structure
            foreach (var example in component.Examples)
            {
                // Verify the example starts with the component name
                example.Should().StartWith($"<{component.Name}");

                // Verify the example has valid structure (either self-closing or proper closing tag)
                var isValidStructure = example.EndsWith("/>") ||
                                     example.EndsWith($"</{component.Name}>") ||
                                     System.Text.RegularExpressions.Regex.IsMatch(example, @"</[A-Za-z][A-Za-z0-9]*>$");

                isValidStructure.Should().BeTrue($"Component {component.Name} example should have valid closing structure. Example: {example}");

                // Verify JSX braces are balanced
                var openBraces = example.Count(c => c == '{');
                var closeBraces = example.Count(c => c == '}');
                openBraces.Should().Be(closeBraces, "JSX expressions should have balanced braces");

                // Verify quotes are balanced
                var doubleQuotes = example.Count(c => c == '"');
                (doubleQuotes % 2).Should().Be(0, "Double quotes should be balanced");
            }
        }
    }
}
