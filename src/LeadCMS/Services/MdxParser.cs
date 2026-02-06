// <copyright file="MdxParser.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using System.Text.Json;

namespace LeadCMS.Services;

/// <summary>
/// Advanced MDX parser that analyzes JSX components in MDX content.
/// Components are assumed to be pre-registered on the client side, so no imports are parsed.
/// </summary>
public class MdxParser
{
    private static readonly HashSet<string> StandardHtmlTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "div", "span", "p", "h1", "h2", "h3", "h4", "h5", "h6",
        "a", "img", "ul", "ol", "li", "table", "tr", "td", "th",
        "form", "input", "button", "textarea", "select", "option",
        "header", "footer", "nav", "main", "section", "article",
        "aside", "figure", "figcaption", "code", "pre", "blockquote",
        "strong", "em", "i", "b", "u", "small", "sub", "sup",
        "br", "hr", "meta", "link", "title", "script", "style",
    };

    public MdxParser()
    {
        // Parser initialized for MDX component analysis
    }

    /// <summary>
    /// Parses MDX content and extracts component information.
    /// Since components are pre-registered on the client, only JSX components are analyzed.
    /// </summary>
    /// <param name="mdxContent">The MDX content to parse.</param>
    /// <returns>Parsed MDX information.</returns>
    public List<MdxComponentInfo> ParseMdx(string mdxContent)
    {
        if (string.IsNullOrWhiteSpace(mdxContent))
        {
            return new List<MdxComponentInfo>();
        }

        var sanitizedContent = StripInlineCodeSpans(StripFencedCodeBlocks(mdxContent));
        if (string.IsNullOrWhiteSpace(sanitizedContent))
        {
            return new List<MdxComponentInfo>();
        }

        // Only parse components since imports are not used
        var components = ParseComponents(sanitizedContent);

        return components;
    }

    private static string StripFencedCodeBlocks(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var withoutBackticks = System.Text.RegularExpressions.Regex.Replace(
            content,
            "```[\\s\\S]*?```",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline);

        return System.Text.RegularExpressions.Regex.Replace(
            withoutBackticks,
            "~~~[\\s\\S]*?~~~",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    private static string StripInlineCodeSpans(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(
            content,
            "(`+)([\\s\\S]*?)\\1",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    /// <summary>
    /// Parses JSX components from MDX content.
    /// </summary>
    private List<MdxComponentInfo> ParseComponents(string content)
    {
        var components = new Dictionary<string, MdxComponentInfo>();

        try
        {
            // Parse components directly from source to handle JSX properly
            var componentMatches = ExtractComponentsFromSource(content);

            foreach (var componentMatch in componentMatches)
            {
                var componentName = componentMatch.Name;

                if (components.TryGetValue(componentName, out var existingComponent))
                {
                    // Update existing component
                    existingComponent.UsageCount++;
                    MergeComponentPropertiesFromSource(existingComponent, componentMatch.Properties);
                    AddExampleIfNotExists(existingComponent, componentMatch.FullMatch);
                }
                else
                {
                    // Create new component
                    components[componentName] = new MdxComponentInfo
                    {
                        Name = componentName,
                        Properties = componentMatch.Properties,
                        AcceptsChildren = componentMatch.HasChildren,
                        Examples = new List<string> { TruncateExample(componentMatch.FullMatch, componentMatch.HasChildren) },
                        UsageCount = 1,
                    };
                }
            }
        }
        catch
        {
            // If parsing fails, return empty list rather than crash
            return new List<MdxComponentInfo>();
        }

        return components.Values.ToList();
    }

    /// <summary>
    /// Extracts only top-level JSX components from source content.
    /// Nested components are included in the parent's full match but not returned individually.
    /// This ensures we only capture components that can be used at the root level.
    /// </summary>
    private List<ComponentMatch> ExtractComponentsFromSource(string content)
    {
        var components = new List<ComponentMatch>();
        var processedRanges = new List<(int Start, int End)>();

        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] != '<')
            {
                continue;
            }

            if (!TryParseOpeningTag(content, index, out var tag))
            {
                continue;
            }

            // Skip if this match is within an already processed top-level component
            if (IsWithinProcessedRange(tag.StartIndex, processedRanges))
            {
                index = Math.Max(index, tag.EndIndex - 1);
                continue;
            }

            var componentName = tag.Name;
            var propsString = tag.Props;
            var isSelfClosing = tag.IsSelfClosing;
            var fullMatch = content.Substring(tag.StartIndex, tag.EndIndex - tag.StartIndex);

            // Skip standard HTML tags (only if they are lowercase, not React components)
            if (StandardHtmlTags.Contains(componentName) && componentName == componentName.ToLowerInvariant())
            {
                continue;
            }

            var hasChildren = false;
            var componentEndIndex = tag.EndIndex;

            if (!isSelfClosing)
            {
                // Find the matching closing tag, accounting for nested components of the same type
                var closingTagEndIndex = FindMatchingClosingTag(content, tag.EndIndex, componentName);

                if (closingTagEndIndex > 0)
                {
                    hasChildren = true;
                    fullMatch = content.Substring(tag.StartIndex, closingTagEndIndex - tag.StartIndex);
                    componentEndIndex = closingTagEndIndex;
                }
                else
                {
                    // No closing tag found, but since it's not self-closing, assume it has children
                    hasChildren = true;
                    fullMatch = fullMatch.TrimEnd('>') + $">...</{componentName}>";
                }
            }

            // Mark this range as processed so nested components are skipped
            processedRanges.Add((tag.StartIndex, componentEndIndex));

            var properties = ParsePropsFromString(propsString);

            components.Add(new ComponentMatch
            {
                Name = componentName,
                Properties = properties,
                HasChildren = hasChildren,
                FullMatch = fullMatch,
            });

            index = Math.Max(index, componentEndIndex - 1);
        }

        return components;
    }

    private bool TryParseOpeningTag(string content, int startIndex, out (int StartIndex, int EndIndex, string Name, string Props, bool IsSelfClosing) tag)
    {
        tag = default;

        if (startIndex < 0 || startIndex >= content.Length || content[startIndex] != '<')
        {
            return false;
        }

        if (startIndex + 1 >= content.Length || content[startIndex + 1] == '/')
        {
            return false;
        }

        var nameStart = startIndex + 1;
        if (!char.IsUpper(content[nameStart]))
        {
            return false;
        }

        var nameEnd = nameStart;
        while (nameEnd < content.Length)
        {
            var ch = content[nameEnd];
            if (char.IsLetterOrDigit(ch) || ch == '.')
            {
                nameEnd++;
                continue;
            }

            break;
        }

        if (nameEnd == nameStart)
        {
            return false;
        }

        var componentName = content.Substring(nameStart, nameEnd - nameStart);

        var propsStart = nameEnd;
        var inDouble = false;
        var inSingle = false;
        var inBacktick = false;
        var braceDepth = 0;

        for (var i = propsStart; i < content.Length; i++)
        {
            var ch = content[i];
            var prev = i > 0 ? content[i - 1] : '\0';

            if (inBacktick)
            {
                if (ch == '`' && prev != '\\')
                {
                    inBacktick = false;
                }

                continue;
            }

            if (inDouble)
            {
                if (ch == '"' && prev != '\\')
                {
                    inDouble = false;
                }

                continue;
            }

            if (inSingle)
            {
                if (ch == '\'' && prev != '\\')
                {
                    inSingle = false;
                }

                continue;
            }

            if (ch == '`')
            {
                inBacktick = true;
                continue;
            }

            if (ch == '"')
            {
                inDouble = true;
                continue;
            }

            if (ch == '\'')
            {
                inSingle = true;
                continue;
            }

            if (ch == '{')
            {
                braceDepth++;
                continue;
            }

            if (ch == '}' && braceDepth > 0)
            {
                braceDepth--;
                continue;
            }

            if (ch == '>' && braceDepth == 0)
            {
                var props = content.Substring(propsStart, i - propsStart);
                var trimmedProps = props.TrimEnd();
                var isSelfClosing = trimmedProps.EndsWith('/');

                if (isSelfClosing)
                {
                    trimmedProps = trimmedProps.TrimEnd().TrimEnd('/');
                }

                tag = (startIndex, i + 1, componentName, trimmedProps, isSelfClosing);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a position is within any of the already processed component ranges.
    /// </summary>
    private bool IsWithinProcessedRange(int position, List<(int Start, int End)> processedRanges)
    {
        foreach (var range in processedRanges)
        {
            if (position > range.Start && position < range.End)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the matching closing tag for a component, properly handling nested components of the same type.
    /// </summary>
    private int FindMatchingClosingTag(string content, int startIndex, string componentName)
    {
        var escapedComponentName = System.Text.RegularExpressions.Regex.Escape(componentName);

        // Pattern to match opening tags of the same component (including self-closing)
        var openingTagPattern = $@"<{escapedComponentName}(?:\s[^>]*)?(/?)\s*>";

        // Pattern to match closing tags
        var closingTagPattern = $@"</{escapedComponentName}\s*>";

        var nestingLevel = 1; // We start after the opening tag, so level is 1
        var currentIndex = startIndex;

        while (currentIndex < content.Length && nestingLevel > 0)
        {
            // Find the next opening or closing tag
            var openingMatch = System.Text.RegularExpressions.Regex.Match(
                content.Substring(currentIndex),
                openingTagPattern);
            var closingMatch = System.Text.RegularExpressions.Regex.Match(
                content.Substring(currentIndex),
                closingTagPattern);

            // Determine which comes first
            var openingIndex = openingMatch.Success ? currentIndex + openingMatch.Index : int.MaxValue;
            var closingIndex = closingMatch.Success ? currentIndex + closingMatch.Index : int.MaxValue;

            if (closingIndex == int.MaxValue && openingIndex == int.MaxValue)
            {
                // No more tags found
                break;
            }

            if (closingIndex < openingIndex)
            {
                // Closing tag comes first
                nestingLevel--;
                currentIndex = closingIndex + closingMatch.Length;

                if (nestingLevel == 0)
                {
                    return currentIndex;
                }
            }
            else
            {
                // Opening tag comes first
                var isSelfClosing = openingMatch.Groups[1].Value == "/";
                if (!isSelfClosing)
                {
                    nestingLevel++;
                }

                currentIndex = openingIndex + openingMatch.Length;
            }
        }

        return -1; // No matching closing tag found
    }

    /// <summary>
    /// Parses JSX props from a props string, handling complex expressions and multiline content.
    /// </summary>
    private List<MdxComponentPropertyInfo> ParsePropsFromString(string propsString)
    {
        var properties = new List<MdxComponentPropertyInfo>();

        if (string.IsNullOrWhiteSpace(propsString))
        {
            return properties;
        }

        // Parse props using a more sophisticated approach that handles JSX expressions
        var propMatches = ExtractPropsFromString(propsString);

        foreach (var propMatch in propMatches)
        {
            var property = new MdxComponentPropertyInfo
            {
                Name = propMatch.Name,
                Type = InferPropertyType(propMatch.Value),
                IsRequired = false, // Cannot determine statically
                DefaultValue = null,
                PossibleValues = new List<string>(),
                ExampleValues = new List<string>(),
            };

            if (!string.IsNullOrEmpty(propMatch.Value))
            {
                property.ExampleValues.Add(TruncatePropertyValue(propMatch.Value));
            }

            properties.Add(property);
        }

        return properties;
    }

    /// <summary>
    /// Extracts individual props from a props string using a strict regex approach.
    /// Only recognizes valid JSX prop names: ASCII letters, numbers, starting with a letter.
    /// </summary>
    private List<PropMatch> ExtractPropsFromString(string propsString)
    {
        var props = new List<PropMatch>();

        if (string.IsNullOrWhiteSpace(propsString))
        {
            return props;
        }

        // Pattern explanation:
        // - Prop names must start with a letter (a-zA-Z) and can contain letters, digits, hyphens
        // - This explicitly excludes Unicode word characters (like Cyrillic) which are not valid JSX prop names
        // - Props can be: name="value", name='value', name={expr}, or standalone boolean (like 'disabled')
        // - Standalone boolean props must be followed by whitespace, =, /, or end of string to be valid
        var propWithValuePattern = @"([a-zA-Z][a-zA-Z0-9]*(?:-[a-zA-Z0-9]+)*)\s*=\s*(""[^""]*""|'[^']*'|\{[^}]*\}|\S+)";
        var booleanPropPattern = @"([a-zA-Z][a-zA-Z0-9]*(?:-[a-zA-Z0-9]+)*)(?=\s+[a-zA-Z]|\s*[/>=]|\s*$)";

        // First, extract all props with values
        var valueMatches = System.Text.RegularExpressions.Regex.Matches(propsString, propWithValuePattern);
        var processedRanges = new List<(int Start, int End)>();

        foreach (System.Text.RegularExpressions.Match match in valueMatches)
        {
            var propName = match.Groups[1].Value;
            var propValue = match.Groups[2].Value;

            // Handle complex JSX expressions that might contain nested braces
            if (propValue.StartsWith('{'))
            {
                propValue = ExtractBalancedBraces(propsString, match.Groups[2].Index);
            }

            props.Add(new PropMatch { Name = propName, Value = propValue });
            processedRanges.Add((match.Index, match.Index + match.Length));
        }

        // Then, extract standalone boolean props (not already part of a value prop)
        var booleanMatches = System.Text.RegularExpressions.Regex.Matches(propsString, booleanPropPattern);

        foreach (System.Text.RegularExpressions.Match match in booleanMatches)
        {
            // Skip if this match overlaps with an already processed prop
            var matchStart = match.Index;
            var matchEnd = match.Index + match.Length;
            var isOverlapping = processedRanges.Exists(r =>
                (matchStart >= r.Start && matchStart < r.End) ||
                (matchEnd > r.Start && matchEnd <= r.End));

            if (!isOverlapping)
            {
                var propName = match.Groups[1].Value;

                // Skip if we already have this prop
                if (!props.Exists(p => p.Name == propName))
                {
                    props.Add(new PropMatch { Name = propName, Value = "true" });
                }
            }
        }

        return props;
    }

    /// <summary>
    /// Extracts balanced JSX expressions starting from a given index.
    /// </summary>
    private string ExtractBalancedBraces(string input, int startIndex)
    {
        if (startIndex >= input.Length || input[startIndex] != '{')
        {
            return string.Empty;
        }

        var braceCount = 0;
        var endIndex = startIndex;

        for (int i = startIndex; i < input.Length; i++)
        {
            if (input[i] == '{')
            {
                braceCount++;
            }
            else if (input[i] == '}')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    endIndex = i;
                    break;
                }
            }
        }

        return input.Substring(startIndex, endIndex - startIndex + 1);
    }

    /// <summary>
    /// Merges properties from source parsing into an existing component.
    /// </summary>
    private void MergeComponentPropertiesFromSource(MdxComponentInfo component, List<MdxComponentPropertyInfo> newProperties)
    {
        foreach (var newProp in newProperties)
        {
            var existingProp = component.Properties.FirstOrDefault(p => p.Name == newProp.Name);
            if (existingProp == null)
            {
                component.Properties.Add(newProp);
            }
            else
            {
                // Merge example values
                foreach (var exampleValue in newProp.ExampleValues)
                {
                    if (!existingProp.ExampleValues.Contains(exampleValue) &&
                        existingProp.ExampleValues.Count < 10)
                    {
                        existingProp.ExampleValues.Add(exampleValue);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Truncates a property value to a reasonable length for examples while preserving MDX structure.
    /// </summary>
    private string TruncatePropertyValue(string value)
    {
        if (value.Length <= 100)
        {
            return value;
        }

        // For JSX expressions, ensure we keep the closing brace and create valid syntax
        if (value.StartsWith('{') && value.EndsWith('}'))
        {
            if (value.Length > 100)
            {
                var innerContent = value.Substring(1, value.Length - 2); // Remove outer braces
                var truncatedInner = TruncateJsxExpression(innerContent, 95);
                return '{' + truncatedInner + '}';
            }

            return value;
        }

        // For quoted strings, preserve the quotes
        if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            if (value.Length > 100)
            {
                var quote = value[0];
                var innerContent = value.Substring(1, value.Length - 2); // Remove quotes
                var truncatedInner = innerContent.Length > 95 ? innerContent.Substring(0, 95) + "..." : innerContent;
                return quote + truncatedInner + quote;
            }

            return value;
        }

        return value.Substring(0, 97) + "...";
    }

    /// <summary>
    /// Truncates JSX expression content while maintaining valid syntax.
    /// </summary>
    private string TruncateJsxExpression(string jsxContent, int maxLength)
    {
        if (jsxContent.Length <= maxLength)
        {
            return jsxContent;
        }

        // Detect the type of JSX content and truncate appropriately
        var trimmed = jsxContent.Trim();

        // Try to parse as JSON first for arrays and objects
        if (TryTruncateJsonExpression(trimmed, maxLength, out var truncatedJson))
        {
            return truncatedJson;
        }

        // String literal
        if ((trimmed.StartsWith('"') && trimmed.IndexOf('"', 1) >= 0) ||
            (trimmed.StartsWith('\'') && trimmed.IndexOf('\'', 1) >= 0))
        {
            var quote = trimmed[0];
            return quote + "..." + quote;
        }

        // Variable name or simple expression
        if (maxLength > 10)
        {
            return trimmed.Substring(0, Math.Min(maxLength - 3, trimmed.Length)) + "...";
        }

        return "...";
    }

    /// <summary>
    /// Attempts to truncate a JSON expression using System.Text.Json.
    /// </summary>
    private bool TryTruncateJsonExpression(string expression, int maxLength, out string truncated)
    {
        truncated = expression;

        try
        {
            using var document = JsonDocument.Parse(expression, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            });
            var root = document.RootElement;

            switch (root.ValueKind)
            {
                case JsonValueKind.Array:
                    truncated = TruncateArrayLiteral(expression, maxLength);
                    return true;

                case JsonValueKind.Object:
                    truncated = TruncateObjectLiteral(expression, maxLength);
                    return true;

                default:
                    // For other JSON types, serialize back with potential truncation
                    var serialized = JsonSerializer.Serialize(root);
                    if (serialized.Length <= maxLength)
                    {
                        truncated = serialized;
                        return true;
                    }

                    break;
            }
        }
        catch (JsonException)
        {
            // Not valid JSON, continue with other methods
        }

        return false;
    }

    /// <summary>
    /// Truncates array literals while maintaining valid syntax.
    /// </summary>
    private string TruncateArrayLiteral(string arrayContent, int maxLength)
    {
        if (arrayContent.Length <= maxLength)
        {
            return arrayContent;
        }

        // For arrays, try to keep at least the opening bracket and show it's an array
        if (maxLength < 10)
        {
            return "[...]";
        }

        // Try to parse and extract first elements using System.Text.Json
        try
        {
            using var document = JsonDocument.Parse(arrayContent, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            });
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var truncated = TruncateJsonArray(root, maxLength - 10); // Leave room for ...]
                return truncated + "...]";
            }
        }
        catch (JsonException)
        {
            // If JSON parsing fails, fall back to simple truncation
        }

        // Fallback to simple truncation with proper closing
        return "[...]";
    }

    /// <summary>
    /// Truncates a JSON array while preserving structure.
    /// </summary>
    private string TruncateJsonArray(JsonElement arrayElement, int maxLength)
    {
        var result = new StringBuilder("[");
        var isFirst = true;

        foreach (var element in arrayElement.EnumerateArray())
        {
            if (!isFirst)
            {
                result.Append(", ");
            }

            var elementJson = JsonSerializer.Serialize(element);

            // Check if adding this element would exceed the limit
            if (result.Length + elementJson.Length > maxLength)
            {
                break;
            }

            result.Append(elementJson);
            isFirst = false;
        }

        return result.ToString();
    }

    /// <summary>
    /// Truncates object literals while maintaining valid syntax.
    /// </summary>
    private string TruncateObjectLiteral(string objectContent, int maxLength)
    {
        if (objectContent.Length <= maxLength)
        {
            return objectContent;
        }

        // For objects, try to keep at least the opening brace and show it's an object
        if (maxLength < 10)
        {
            return "{...}";
        }

        // Try to parse and extract first properties using System.Text.Json
        try
        {
            using var document = JsonDocument.Parse(objectContent, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            });
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var truncated = TruncateJsonObject(root, maxLength - 10); // Leave room for ...}
                return truncated + "...}";
            }
        }
        catch (JsonException)
        {
            // If JSON parsing fails, fall back to simple truncation
        }

        // Fallback to simple truncation with proper closing
        return "{...}";
    }

    /// <summary>
    /// Truncates a JSON object while preserving structure.
    /// </summary>
    private string TruncateJsonObject(JsonElement objectElement, int maxLength)
    {
        var result = new StringBuilder("{");
        var isFirst = true;

        foreach (var property in objectElement.EnumerateObject())
        {
            if (!isFirst)
            {
                result.Append(", ");
            }

            var propertyJson = $"\"{property.Name}\": {JsonSerializer.Serialize(property.Value)}";

            // Check if adding this property would exceed the limit
            if (result.Length + propertyJson.Length > maxLength)
            {
                break;
            }

            result.Append(propertyJson);
            isFirst = false;
        }

        return result.ToString();
    }

    /// <summary>
    /// Adds an example to a component if it doesn't already exist.
    /// </summary>
    private void AddExampleIfNotExists(MdxComponentInfo component, string example)
    {
        // Infer if component has children from the example structure
        var hasChildren = InferHasChildrenFromExample(example);
        var truncated = TruncateExample(example, hasChildren);
        if (!component.Examples.Contains(truncated) && component.Examples.Count < 5)
        {
            component.Examples.Add(truncated);
        }
    }

    /// <summary>
    /// Infers whether a component has children based on its structure.
    /// </summary>
    private bool InferHasChildrenFromExample(string example)
    {
        // Check if it's self-closing (ends with />)
        if (example.TrimEnd().EndsWith("/>"))
        {
            return false;
        }

        // Check if it has a closing tag
        var componentPattern = @"<([A-Z][a-zA-Z0-9]*(?:\.[A-Z][a-zA-Z0-9]*)*)";
        var match = System.Text.RegularExpressions.Regex.Match(example, componentPattern);
        if (match.Success)
        {
            var componentName = match.Groups[1].Value;
            var escapedComponentName = System.Text.RegularExpressions.Regex.Escape(componentName);
            var closingPattern = $@"</{escapedComponentName}\s*>";
            return System.Text.RegularExpressions.Regex.IsMatch(example, closingPattern);
        }

        return true; // Default to having children if uncertain
    }

    /// <summary>
    /// Truncates an example to a reasonable length while preserving valid MDX structure.
    /// Since examples now include full nested component structure, we use a larger limit.
    /// </summary>
    private string TruncateExample(string example, bool hasChildren)
    {
        // Use a larger limit for examples since they now include full nested component structure
        // This is important for providing meaningful examples showing how to use components with children
        const int maxExampleLength = 2000;

        if (example.Length <= maxExampleLength)
        {
            return example;
        }

        // Try to find a valid truncation point that preserves MDX structure
        var truncated = TruncateMdxSafely(example, maxExampleLength, hasChildren);
        return truncated;
    }

    /// <summary>
    /// Safely truncates MDX content while preserving component structure.
    /// </summary>
    private string TruncateMdxSafely(string mdxContent, int maxLength, bool hasChildren)
    {
        if (mdxContent.Length <= maxLength)
        {
            return mdxContent;
        }

        // Extract the component name from the opening tag
        var componentPattern = @"<([A-Z][a-zA-Z0-9]*(?:\.[A-Z][a-zA-Z0-9]*)*)";
        var match = System.Text.RegularExpressions.Regex.Match(mdxContent, componentPattern);

        if (!match.Success)
        {
            // Fallback for non-component content
            return mdxContent.Substring(0, Math.Min(maxLength - 3, mdxContent.Length)) + "...";
        }

        var componentName = match.Groups[1].Value;

        // If it's a self-closing component, try to preserve the self-closing structure
        if (!hasChildren)
        {
            // For self-closing components, try to find a good truncation point before the />
            var selfClosingTruncated = mdxContent.Substring(0, Math.Min(maxLength - 5, mdxContent.Length));
            return EnsureValidMdxStructure(selfClosingTruncated, false, componentName);
        }

        // For components with children, we need to be more careful
        // Try to find a good truncation point that doesn't break the structure
        var truncated = FindGoodTruncationPoint(mdxContent, maxLength - $"</{componentName}>".Length - 5);
        return EnsureValidMdxStructure(truncated, true, componentName);
    }

    /// <summary>
    /// Finds a good truncation point that doesn't break component or attribute boundaries.
    /// </summary>
    private string FindGoodTruncationPoint(string content, int maxLength)
    {
        if (maxLength >= content.Length)
        {
            return content;
        }

        // Look for safe truncation points (end of attributes, end of nested components, etc.)
        var safeTruncationPoints = new List<int>();

        // Add points after complete nested component tags
        var nestedComponentPattern = @"</[^>]+>";
        var nestedMatches = System.Text.RegularExpressions.Regex.Matches(content, nestedComponentPattern);
        foreach (System.Text.RegularExpressions.Match match in nestedMatches)
        {
            var endPos = match.Index + match.Length;
            if (endPos <= maxLength)
            {
                safeTruncationPoints.Add(endPos);
            }
        }

        // Add points after self-closing tags
        var selfClosingPattern = @"/>";
        var selfClosingMatches = System.Text.RegularExpressions.Regex.Matches(content, selfClosingPattern);
        foreach (System.Text.RegularExpressions.Match match in selfClosingMatches)
        {
            var endPos = match.Index + match.Length;
            if (endPos <= maxLength)
            {
                safeTruncationPoints.Add(endPos);
            }
        }

        // Add points after complete opening tags
        var openingTagPattern = @"<[^/>]+>";
        var openingMatches = System.Text.RegularExpressions.Regex.Matches(content, openingTagPattern);
        foreach (System.Text.RegularExpressions.Match match in openingMatches)
        {
            var endPos = match.Index + match.Length;
            if (endPos <= maxLength)
            {
                safeTruncationPoints.Add(endPos);
            }
        }

        // Use the latest safe truncation point
        if (safeTruncationPoints.Any())
        {
            var bestPoint = safeTruncationPoints.Max();
            return content.Substring(0, bestPoint);
        }

        // If no safe points found, truncate at maxLength but try to avoid breaking attributes
        var fallbackTruncation = content.Substring(0, maxLength);

        // Try to truncate before an incomplete attribute if possible
        var lastSpace = fallbackTruncation.LastIndexOf(' ');
        var lastQuote = Math.Max(fallbackTruncation.LastIndexOf('"'), fallbackTruncation.LastIndexOf('\''));
        var lastBrace = fallbackTruncation.LastIndexOf('{');

        // If we're in the middle of an attribute, truncate before it
        if (lastSpace > lastQuote && lastSpace > lastBrace && lastSpace > maxLength - 50)
        {
            return content.Substring(0, lastSpace);
        }

        return fallbackTruncation;
    }

    /// <summary>
    /// Ensures the truncated MDX has valid structure by balancing braces, quotes, and adding proper closing tags.
    /// </summary>
    private string EnsureValidMdxStructure(string mdxContent, bool hasChildren, string componentName)
    {
        var result = new StringBuilder(mdxContent);

        // Count unbalanced structures
        var openBraces = mdxContent.Count(c => c == '{');
        var closeBraces = mdxContent.Count(c => c == '}');
        var doubleQuotes = mdxContent.Count(c => c == '"');
        var singleQuotes = mdxContent.Count(c => c == '\'');

        // Close unbalanced braces
        while (openBraces > closeBraces)
        {
            result.Append('}');
            closeBraces++;
        }

        // Close unbalanced quotes
        if (doubleQuotes % 2 != 0)
        {
            result.Append('"');
        }

        if (singleQuotes % 2 != 0)
        {
            result.Append('\'');
        }

        // Ensure proper component closing
        var content = result.ToString();

        if (hasChildren)
        {
            // For components with children, ensure we have a proper closing tag
            if (!content.Contains($"</{componentName}>"))
            {
                // Check if we're in the middle of an opening tag
                var lastOpenAngle = content.LastIndexOf('<');
                var lastCloseAngle = content.LastIndexOf('>');

                if (lastOpenAngle > lastCloseAngle)
                {
                    // We're in the middle of an opening tag, close it first
                    result.Append(">");
                }
                else if (!content.EndsWith('>'))
                {
                    // If not ending with >, we might be in text content, which is fine
                    // But we still need to ensure we close the component
                }

                // Always add the closing tag for components with children
                result.Append($"</{componentName}>");
            }
        }
        else
        {
            // For self-closing components, ensure proper self-closing syntax
            if (content.StartsWith('<') && !content.EndsWith("/>"))
            {
                // Check if we're in the middle of an opening tag
                var lastOpenAngle = content.LastIndexOf('<');
                var lastCloseAngle = content.LastIndexOf('>');

                if (lastOpenAngle > lastCloseAngle)
                {
                    // We're in the middle of an opening tag
                    // Remove any trailing > if present and add proper self-closing
                    if (content.EndsWith('>'))
                    {
                        result.Length -= 1;
                    }

                    // Ensure it ends with />
                    if (!content.TrimEnd().EndsWith('/'))
                    {
                        result.Append(" />");
                    }
                    else
                    {
                        result.Append(">");
                    }
                }
                else
                {
                    // We have a complete opening tag, convert to self-closing
                    if (content.EndsWith('>') && !content.EndsWith("/>"))
                    {
                        result.Length -= 1; // Remove the >
                        result.Append(" />");
                    }
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Infers the property type from its value.
    /// </summary>
    private string InferPropertyType(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "string";
        }

        // JSX expression
        if (value.StartsWith('{') && value.EndsWith('}'))
        {
            var expression = value.Substring(1, value.Length - 2).Trim();
            return InferExpressionType(expression);
        }

        // String literal
        return InferLiteralType(value);
    }

    /// <summary>
    /// Infers type from a JSX expression.
    /// </summary>
    private string InferExpressionType(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "expression";
        }

        // Boolean literals
        if (expression == "true" || expression == "false")
        {
            return "boolean";
        }

        // Number literals
        if (int.TryParse(expression, out _) || double.TryParse(expression, out _))
        {
            return "number";
        }

        // String literals
        if ((expression.StartsWith('"') && expression.EndsWith('"')) ||
            (expression.StartsWith('\'') && expression.EndsWith('\'')))
        {
            return "string";
        }

        // Try to parse as JSON to detect arrays and objects
        if (TryParseJsonExpression(expression, out var jsonType))
        {
            return jsonType;
        }

        // Function calls, variables, etc.
        return "expression";
    }

    /// <summary>
    /// Attempts to parse a JSX expression as JSON to determine its type.
    /// </summary>
    private bool TryParseJsonExpression(string expression, out string type)
    {
        type = "expression";

        // Quick structural checks first
        if (expression.StartsWith('[') && expression.EndsWith(']'))
        {
            type = "array";
            return TryValidateJsonStructure(expression);
        }

        if (expression.StartsWith('{') && expression.EndsWith('}'))
        {
            type = "object";
            return TryValidateJsonStructure(expression);
        }

        return false;
    }

    /// <summary>
    /// Validates if a string has valid JSON structure using System.Text.Json.
    /// </summary>
    private bool TryValidateJsonStructure(string jsonString)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonString, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Infers type from a literal value.
    /// </summary>
    private string InferLiteralType(string value)
    {
        if (bool.TryParse(value, out _))
        {
            return "boolean";
        }

        if (int.TryParse(value, out _) || double.TryParse(value, out _))
        {
            return "number";
        }

        return "string";
    }
}

/// <summary>
/// Information about an MDX component.
/// </summary>
public class MdxComponentInfo
{
    public string Name { get; set; } = string.Empty;

    public List<MdxComponentPropertyInfo> Properties { get; set; } = new();

    public bool AcceptsChildren { get; set; }

    public List<string> Examples { get; set; } = new();

    public int UsageCount { get; set; }
}

/// <summary>
/// Information about a component property.
/// </summary>
public class MdxComponentPropertyInfo
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public string? DefaultValue { get; set; }

    public List<string> PossibleValues { get; set; } = new();

    public List<string> ExampleValues { get; set; } = new();
}

/// <summary>
/// Helper class for component matching during parsing.
/// </summary>
internal class ComponentMatch
{
    public string Name { get; set; } = string.Empty;

    public List<MdxComponentPropertyInfo> Properties { get; set; } = new();

    public bool HasChildren { get; set; }

    public string FullMatch { get; set; } = string.Empty;
}

/// <summary>
/// Helper class for property matching during parsing.
/// </summary>
internal class PropMatch
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
