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
        var longText = new string('a', 2500); // Create a very long string exceeding the 2000 limit
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
        example.Length.Should().BeLessOrEqualTo(2003); // 2000 + "..." = 2003
    }

    /// <summary>
    /// Tests that self-closing components are handled correctly during truncation.
    /// </summary>
    [Fact]
    public void ParseMdx_SelfClosingComponent_PreservesClosingSlash()
    {
        // Arrange
        var parser = new MdxParser();
        var longText = new string('b', 2500);
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
        example.Length.Should().BeLessOrEqualTo(2003);
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
    /// Tests that only top-level components are returned, with nested components included in parent examples.
    /// </summary>
    [Fact]
    public void ParseMdx_ComponentWithChildren_ReturnsOnlyTopLevel()
    {
        // Arrange
        var parser = new MdxParser();
        var longText = new string('d', 50);
        var mdxContent = $"<Card title=\"Card Title\"><CardBody>{longText}</CardBody></Card>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Only the top-level Card component should be returned
        result.Should().HaveCount(1);

        var cardComponent = result.FirstOrDefault(c => c.Name == "Card");
        cardComponent.Should().NotBeNull();
        cardComponent!.Examples.Should().HaveCount(1);

        var example = cardComponent.Examples[0];
        example.Should().StartWith("<Card");
        example.Should().EndWith("</Card>");

        // The example should contain the nested CardBody component
        example.Should().Contain("<CardBody>");
        example.Should().Contain("</CardBody>");
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
    /// Tests that only top-level components with dotted names (like TwoColumns.HalfWidthColumn) are parsed.
    /// Nested components are included in parent examples but not returned individually.
    /// </summary>
    [Fact]
    public void ParseMdx_DottedComponentNames_ReturnsOnlyTopLevel()
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

        // Assert - Only Section should be returned as it's the top-level component
        result.Should().HaveCount(1);

        var sectionComponent = result.FirstOrDefault(c => c.Name == "Section");
        sectionComponent.Should().NotBeNull();
        sectionComponent!.AcceptsChildren.Should().BeTrue();

        // The example should contain all nested components
        var example = sectionComponent.Examples[0];
        example.Should().Contain("<TwoColumns>");
        example.Should().Contain("<TwoColumns.HalfWidthColumn>");
        example.Should().Contain("</TwoColumns.HalfWidthColumn>");
        example.Should().Contain("</TwoColumns>");
        example.Should().Contain("</Section>");
    }

    /// <summary>
    /// Tests that multiple sibling top-level components are all returned.
    /// </summary>
    [Fact]
    public void ParseMdx_MultipleSiblingComponents_ReturnsAllTopLevel()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"<Section>
    Content 1
</Section>
<Card>
    <CardBody>Content 2</CardBody>
</Card>
<Button onClick={handleClick}>Click me</Button>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - All three top-level components should be returned
        result.Should().HaveCount(3);

        var sectionComponent = result.FirstOrDefault(c => c.Name == "Section");
        sectionComponent.Should().NotBeNull();

        var cardComponent = result.FirstOrDefault(c => c.Name == "Card");
        cardComponent.Should().NotBeNull();
        // Card example should include nested CardBody
        cardComponent!.Examples[0].Should().Contain("<CardBody>");

        var buttonComponent = result.FirstOrDefault(c => c.Name == "Button");
        buttonComponent.Should().NotBeNull();
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
    /// Tests that complex JSX arrays/objects are preserved when under the 2000 char limit.
    /// </summary>
    [Fact]
    public void MdxParser_ComplexJsxArrays_PreservedWhenUnderLimit()
    {
        // Arrange
        var parser = new MdxParser();

        // Create a complex component with JSX array props (under 2000 chars)
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

        // Since this is under 2000 chars, the full example should be preserved
        sample.Should().Be(complexComponent);
    }

    /// <summary>
    /// Tests that only top-level Section component is returned, with all nested components in example.
    /// </summary>
    [Fact]
    public void ParseMdx_NestedComponents_ReturnsOnlyTopLevelWithFullExample()
    {
        // Arrange
        var parser = new MdxParser();

        var mdxContent = @"<Section withLargeTopPadding>
  <SectionTitle>E-Mail <TextLink to=""mailto:info@all3.com"">info@all3.com</TextLink></SectionTitle>
  <AccentTitle />
</Section>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Only Section should be returned as the top-level component
        result.Should().HaveCount(1);

        var sectionComponent = result.FirstOrDefault(c => c.Name == "Section");
        sectionComponent.Should().NotBeNull();
        sectionComponent!.Examples.Should().HaveCount(1);

        var example = sectionComponent.Examples[0];

        // Verify the example contains all nested components
        example.Should().StartWith("<Section");
        example.Should().EndWith("</Section>");
        example.Should().Contain("<SectionTitle>");
        example.Should().Contain("<TextLink");
        example.Should().Contain("<AccentTitle />");

        // Verify no nested components are returned separately
        result.FirstOrDefault(c => c.Name == "SectionTitle").Should().BeNull();
        result.FirstOrDefault(c => c.Name == "TextLink").Should().BeNull();
        result.FirstOrDefault(c => c.Name == "AccentTitle").Should().BeNull();
    }

    /// <summary>
    /// Tests that deeply nested components are captured in the top-level component's example.
    /// </summary>
    [Fact]
    public void ParseMdx_DeeplyNestedComponents_ReturnsOnlyTopLevelWithAllNested()
    {
        // Arrange
        var parser = new MdxParser();

        var mdxContent = @"<Container>
  <Header>
    <Navigation>
      <NavItem href=""/home"">Home</NavItem>
      <NavItem href=""/about"">About</NavItem>
    </Navigation>
  </Header>
  <Main>
    <Article>
      <ArticleTitle>Article Title</ArticleTitle>
      <ArticleContent>
        Some content here
      </ArticleContent>
    </Article>
  </Main>
</Container>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Only Container should be returned
        result.Should().HaveCount(1);

        var containerComponent = result.FirstOrDefault(c => c.Name == "Container");
        containerComponent.Should().NotBeNull();
        containerComponent!.AcceptsChildren.Should().BeTrue();

        var example = containerComponent.Examples[0];

        // The example should contain all nested components
        example.Should().Contain("<Header>");
        example.Should().Contain("<Navigation>");
        example.Should().Contain("<NavItem");
        example.Should().Contain("<Main>");
        example.Should().Contain("<Article>");
        example.Should().Contain("<ArticleTitle>");
        example.Should().Contain("<ArticleContent>");
        example.Should().EndWith("</Container>");

        // No nested components should be returned separately
        result.FirstOrDefault(c => c.Name == "Header").Should().BeNull();
        result.FirstOrDefault(c => c.Name == "Navigation").Should().BeNull();
        result.FirstOrDefault(c => c.Name == "NavItem").Should().BeNull();
    }

    /// <summary>
    /// Tests that the user's original use case works - only top-level Callout is returned.
    /// </summary>
    [Fact]
    public void ParseMdx_UserScenario_ReturnsOnlyTopLevelCallout()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"# Header

<table>
<tr></tr>
</table>

Text

<Callout>Callout <a href=""https://example.com"">link</a></Callout>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Only Callout should be returned (table/tr/a are standard HTML)
        result.Should().HaveCount(1);

        var calloutComponent = result.FirstOrDefault(c => c.Name == "Callout");
        calloutComponent.Should().NotBeNull();
        calloutComponent!.AcceptsChildren.Should().BeTrue();

        var example = calloutComponent.Examples[0];
        example.Should().StartWith("<Callout>");
        example.Should().EndWith("</Callout>");
        // The example should contain the nested anchor tag
        example.Should().Contain("<a href");
    }

    /// <summary>
    /// Tests that same component used at multiple top levels is counted correctly.
    /// </summary>
    [Fact]
    public void ParseMdx_SameComponentMultipleTimes_CountsUsageCorrectly()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"<Button variant=""primary"">First</Button>
<Card><Button variant=""secondary"">Nested - should not count separately</Button></Card>
<Button variant=""tertiary"">Third</Button>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Button at top level appears twice, Card appears once
        result.Should().HaveCount(2);

        var buttonComponent = result.FirstOrDefault(c => c.Name == "Button");
        buttonComponent.Should().NotBeNull();
        buttonComponent!.UsageCount.Should().Be(2); // Only top-level usages count

        var cardComponent = result.FirstOrDefault(c => c.Name == "Card");
        cardComponent.Should().NotBeNull();
        cardComponent!.UsageCount.Should().Be(1);
        // Card's example should contain the nested Button
        cardComponent.Examples[0].Should().Contain("<Button");
    }

    /// <summary>
    /// Tests handling of nested components of the same type.
    /// </summary>
    [Fact]
    public void ParseMdx_NestedSameTypeComponents_HandlesCorrectly()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"<Container>
  <Container>
    Inner content
  </Container>
</Container>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Only outer Container should be returned
        result.Should().HaveCount(1);

        var containerComponent = result.FirstOrDefault(c => c.Name == "Container");
        containerComponent.Should().NotBeNull();

        var example = containerComponent!.Examples[0];
        example.Should().StartWith("<Container>");
        example.Should().EndWith("</Container>");
        // Should contain the inner Container
        example.Should().Contain("<Container>", Exactly.Twice());
        example.Should().Contain("</Container>", Exactly.Twice());
    }

    /// <summary>
    /// Tests that empty and whitespace MDX content returns empty list.
    /// </summary>
    [Fact]
    public void ParseMdx_EmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var parser = new MdxParser();

        // Act & Assert
        parser.ParseMdx(string.Empty).Should().BeEmpty();
        parser.ParseMdx("   ").Should().BeEmpty();
        parser.ParseMdx(null!).Should().BeEmpty();
    }

    /// <summary>
    /// Tests that pure markdown content (no components) returns empty list.
    /// </summary>
    [Fact]
    public void ParseMdx_PureMarkdown_ReturnsEmptyList()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"# Header

This is some **bold** and *italic* text.

- List item 1
- List item 2

[A link](https://example.com)";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - No JSX components, only markdown
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that self-closing components at top level are correctly identified.
    /// </summary>
    [Fact]
    public void ParseMdx_SelfClosingTopLevel_ReturnsComponent()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"Some text

<Divider />

More text

<Image src=""/path/to/image.jpg"" alt=""description"" />";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Two self-closing components
        result.Should().HaveCount(2);

        var divider = result.FirstOrDefault(c => c.Name == "Divider");
        divider.Should().NotBeNull();
        divider!.AcceptsChildren.Should().BeFalse();

        var image = result.FirstOrDefault(c => c.Name == "Image");
        image.Should().NotBeNull();
        image!.AcceptsChildren.Should().BeFalse();
    }

    /// <summary>
    /// Tests mixed content with markdown, HTML, and JSX components.
    /// </summary>
    [Fact]
    public void ParseMdx_MixedContent_ReturnsOnlyJsxComponents()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"# Title

<div class=""container"">
  <p>Some HTML paragraph</p>
</div>

<Alert type=""warning"">
  This is an alert with <strong>bold text</strong> inside.
</Alert>

Regular paragraph.

<table>
  <tr><td>Cell</td></tr>
</table>

<CustomTable data={tableData}>
  <CustomRow />
</CustomTable>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Only JSX components (Alert, CustomTable), not HTML tags
        result.Should().HaveCount(2);

        var alert = result.FirstOrDefault(c => c.Name == "Alert");
        alert.Should().NotBeNull();
        alert!.AcceptsChildren.Should().BeTrue();
        // Example should contain the nested strong tag
        alert.Examples[0].Should().Contain("<strong>");

        var customTable = result.FirstOrDefault(c => c.Name == "CustomTable");
        customTable.Should().NotBeNull();
        customTable!.AcceptsChildren.Should().BeTrue();
        // Example should contain the nested CustomRow
        customTable.Examples[0].Should().Contain("<CustomRow />");
    }

    /// <summary>
    /// Tests that fenced code blocks are ignored when parsing components.
    /// </summary>
    [Fact]
    public void ParseMdx_CodeBlocks_IgnoresComponentsInsideCode()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"# Title

    ```tsx
    <CodeBlockComponent prop=""value"" />
    ```

    ```
    <PlainCodeComponent />
    ```

    <Alert type=""info"">Text</Alert>

    ~~~js
    <AnotherFakeComponent />
    ~~~";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert - Only the real component outside code fences should be parsed
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Alert");
    }

    /// <summary>
    /// Tests that template literal props with ">" characters are parsed as a single prop value.
    /// </summary>
    [Fact]
    public void ParseMdx_MermaidTemplateLiteral_ParsesChartPropOnly()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"<MermaidDiagram chart={`
graph TB
    subgraph VM[""Virtual Machine Architecture""]
        Host[""Host Operating System""]
        Hypervisor[""Hypervisor (VMware)""]
        VM1[""VM 1<br/>Guest OS (2GB)<br/>App A""]
        VM2[""VM 2<br/>Guest OS (2GB)<br/>App B""]
        VM3[""VM 3<br/>Guest OS (2GB)<br/>App C""]

        Host --> Hypervisor
        Hypervisor --> VM1
        Hypervisor --> VM2
        Hypervisor --> VM3
    end

    Note[""Total overhead: 6GB+ just for OS""]

    style VM fill:#f9f9f9,stroke:#333,stroke-width:2px
    style Host fill:#e1f5ff,stroke:#0288d1
    style Hypervisor fill:#fff3e0,stroke:#f57c00
    style VM1 fill:#ffebee,stroke:#c62828
    style VM2 fill:#e8f5e9,stroke:#2e7d32
    style VM3 fill:#f3e5f5,stroke:#6a1b9a
    style Note fill:#fff9c4,stroke:#f57f17
`} />";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("MermaidDiagram");
        result[0].Properties.Select(p => p.Name).Should().BeEquivalentTo("chart");
        result[0].Properties[0].ExampleValues.Should().NotBeEmpty();
        result[0].Properties[0].ExampleValues[0].Should().StartWith("{");
    }

    /// <summary>
    /// Tests that inline code spans do not produce components.
    /// </summary>
    [Fact]
    public void ParseMdx_InlineCode_IgnoresComponentsInCodeSpans()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"<Callout type=""info"" title=""Static export considerations"">
    When using Next.js with `output: ""export""` for static sites, built-in image optimization is disabled. You can still use the `<Image>` component with `unoptimized={true}` or implement your own optimization during the build process.
    </Callout>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Callout");
    }

    /// <summary>
    /// Tests that props are only recognized when they have valid prop syntax (name=value or valid boolean prop names).
    /// This prevents text content from being incorrectly parsed as boolean props.
    /// </summary>
    [Fact]
    public void ParseMdx_PropsWithTextContent_DoesNotParseTextAsProps()
    {
        // Arrange
        var parser = new MdxParser();

        // This simulates malformed MDX where text appears on a new line before the closing />
        // The parser should NOT interpret Russian words as props
        var mdxContent = @"<Image src=""/api/media/image.png"" alt=""Description"" caption=""Caption""
В личном кабинете исполнителя Обучать работе с мобильным приложением не придётся
/>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        var imageComponent = result[0];
        imageComponent.Name.Should().Be("Image");

        // Should only have the valid props: src, alt, caption
        imageComponent.Properties.Should().HaveCount(3);
        imageComponent.Properties.Select(p => p.Name).Should().BeEquivalentTo("src", "alt", "caption");

        // Should NOT have props like "в", "личном", "кабинете", etc.
        imageComponent.Properties.Should().NotContain(p => p.Name == "в");
        imageComponent.Properties.Should().NotContain(p => p.Name == "личном");
        imageComponent.Properties.Should().NotContain(p => p.Name == "кабинете");
    }

    /// <summary>
    /// Tests that valid boolean props (like 'disabled', 'required') are still recognized.
    /// </summary>
    [Fact]
    public void ParseMdx_ValidBooleanProps_AreRecognized()
    {
        // Arrange
        var parser = new MdxParser();
        var mdxContent = @"<Button disabled primary large onClick={handleClick}>Click me</Button>";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1);
        var buttonComponent = result[0];
        buttonComponent.Name.Should().Be("Button");

        // Should have boolean props: disabled, primary, large, and onClick
        buttonComponent.Properties.Select(p => p.Name).Should().Contain("disabled");
        buttonComponent.Properties.Select(p => p.Name).Should().Contain("primary");
        buttonComponent.Properties.Select(p => p.Name).Should().Contain("large");
        buttonComponent.Properties.Select(p => p.Name).Should().Contain("onClick");
    }

    /// <summary>
    /// Tests that text following a valid prop is not parsed as additional props.
    /// </summary>
    [Fact]
    public void ParseMdx_MultipleImageComponents_MergesOnlyValidProps()
    {
        // Arrange
        var parser = new MdxParser();

        // Two valid Image usages
        var mdxContent = @"<Image src=""/path/to/image1.png"" alt=""First image"" />

Some text in between

<Image src=""/path/to/image2.png"" alt=""Second image"" caption=""A caption"" />";

        // Act
        var result = parser.ParseMdx(mdxContent);

        // Assert
        result.Should().HaveCount(1); // Same component merged
        var imageComponent = result[0];
        imageComponent.Name.Should().Be("Image");
        imageComponent.UsageCount.Should().Be(2);

        // Should only have valid props
        var propNames = imageComponent.Properties.Select(p => p.Name).ToList();
        propNames.Should().Contain("src");
        propNames.Should().Contain("alt");
        propNames.Should().Contain("caption");

        // Should NOT have any props that look like text content
        propNames.Should().NotContain(p => p.Length == 1); // Single-char props are suspicious
        propNames.TrueForAll(p => System.Text.RegularExpressions.Regex.IsMatch(p, @"^[a-zA-Z][a-zA-Z0-9]*$")).Should().BeTrue();
    }
}
