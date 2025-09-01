// <copyright file="MdxParser.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using HtmlAgilityPack;

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

        // Only parse components since imports are not used
        var components = ParseComponents(mdxContent);

        return components;
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
                        Examples = new List<string> { TruncateExample(componentMatch.FullMatch) },
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
    /// Extracts JSX components directly from source content.
    /// This approach handles multiline props and complex JSX syntax better than HTML parsing.
    /// </summary>
    private List<ComponentMatch> ExtractComponentsFromSource(string content)
    {
        var components = new List<ComponentMatch>();

        // Pattern to match JSX components (self-closing and opening tags)
        // This handles multiline content and complex props
        var componentPattern = @"<([A-Z][a-zA-Z0-9]*)\s*([^>]*?)(/?)>";
        var matches = System.Text.RegularExpressions.Regex.Matches(
            content,
            componentPattern,
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var componentName = match.Groups[1].Value;
            var propsString = match.Groups[2].Value;
            var isSelfClosing = match.Groups[3].Value == "/";
            var fullMatch = match.Value;

            // Skip standard HTML tags
            if (StandardHtmlTags.Contains(componentName.ToLowerInvariant()))
            {
                continue;
            }

            // For non-self-closing tags, check if they have children
            var hasChildren = false;
            if (!isSelfClosing)
            {
                // Look for closing tag to determine if it has children
                var closingTagPattern = $@"</{componentName}\s*>";
                var closingMatch = System.Text.RegularExpressions.Regex.Match(content, closingTagPattern);
                if (closingMatch.Success && closingMatch.Index > match.Index)
                {
                    hasChildren = true;
                    // Include content up to closing tag in full match
                    var endIndex = closingMatch.Index + closingMatch.Length;
                    fullMatch = content.Substring(match.Index, endIndex - match.Index);
                }
            }

            var properties = ParsePropsFromString(propsString);

            components.Add(new ComponentMatch
            {
                Name = componentName,
                Properties = properties,
                HasChildren = hasChildren,
                FullMatch = fullMatch,
            });
        }

        return components;
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
    /// Extracts individual props from a props string, handling complex JSX expressions.
    /// </summary>
    private List<PropMatch> ExtractPropsFromString(string propsString)
    {
        var props = new List<PropMatch>();
        var i = 0;

        while (i < propsString.Length)
        {
            // Skip whitespace
            while (i < propsString.Length && char.IsWhiteSpace(propsString[i]))
            {
                i++;
            }

            if (i >= propsString.Length)
            {
                break;
            }

            // Find prop name
            var nameStart = i;
            while (i < propsString.Length &&
                   (char.IsLetterOrDigit(propsString[i]) || propsString[i] == '_' || propsString[i] == '-'))
            {
                i++;
            }

            if (i <= nameStart)
            {
                break;
            }

            var propName = propsString.Substring(nameStart, i - nameStart);

            // Skip whitespace
            while (i < propsString.Length && char.IsWhiteSpace(propsString[i]))
            {
                i++;
            }

            string propValue = string.Empty;

            // Check if there's a value (=)
            if (i < propsString.Length && propsString[i] == '=')
            {
                i++; // Skip =

                // Skip whitespace
                while (i < propsString.Length && char.IsWhiteSpace(propsString[i]))
                {
                    i++;
                }

                if (i < propsString.Length)
                {
                    if (propsString[i] == '{')
                    {
                        // JSX expression - find matching closing brace
                        var braceCount = 0;
                        var valueStart = i;

                        do
                        {
                            if (propsString[i] == '{')
                            {
                                braceCount++;
                            }
                            else if (propsString[i] == '}')
                            {
                                braceCount--;
                            }

                            i++;
                        }
                        while (i < propsString.Length && braceCount > 0);

                        propValue = propsString.Substring(valueStart, i - valueStart);
                    }
                    else if (propsString[i] == '"')
                    {
                        // String literal
                        i++; // Skip opening quote
                        var valueStart = i;

                        while (i < propsString.Length && propsString[i] != '"')
                        {
                            if (propsString[i] == '\\')
                            {
                                i++; // Skip escaped character
                            }

                            i++;
                        }

                        if (i < propsString.Length)
                        {
                            propValue = '"' + propsString.Substring(valueStart, i - valueStart) + '"';
                            i++; // Skip closing quote
                        }
                    }
                    else if (propsString[i] == '\'')
                    {
                        // String literal with single quotes
                        i++; // Skip opening quote
                        var valueStart = i;

                        while (i < propsString.Length && propsString[i] != '\'')
                        {
                            if (propsString[i] == '\\')
                            {
                                i++; // Skip escaped character
                            }

                            i++;
                        }

                        if (i < propsString.Length)
                        {
                            propValue = '\'' + propsString.Substring(valueStart, i - valueStart) + '\'';
                            i++; // Skip closing quote
                        }
                    }
                    else
                    {
                        // Unquoted value (boolean props, etc.)
                        var valueStart = i;
                        while (i < propsString.Length &&
                               !char.IsWhiteSpace(propsString[i]) &&
                               propsString[i] != '=' &&
                               propsString[i] != '/')
                        {
                            i++;
                        }

                        propValue = propsString.Substring(valueStart, i - valueStart);
                    }
                }
            }
            else
            {
                // Boolean prop without value
                propValue = "true";
            }

            props.Add(new PropMatch { Name = propName, Value = propValue });
        }

        return props;
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
    /// Truncates a property value to a reasonable length for examples.
    /// </summary>
    private string TruncatePropertyValue(string value)
    {
        if (value.Length <= 100)
        {
            return value;
        }

        // For JSX expressions, try to keep them readable
        if (value.StartsWith('{') && value.EndsWith('}'))
        {
            return value.Length > 100 ? value.Substring(0, 97) + "..." : value;
        }

        return value.Substring(0, 97) + "...";
    }

    /// <summary>
    /// Adds an example to a component if it doesn't already exist.
    /// </summary>
    private void AddExampleIfNotExists(MdxComponentInfo component, string example)
    {
        var truncated = TruncateExample(example);
        if (!component.Examples.Contains(truncated) && component.Examples.Count < 5)
        {
            component.Examples.Add(truncated);
        }
    }

    /// <summary>
    /// Truncates an example to a reasonable length.
    /// </summary>
    private string TruncateExample(string example)
    {
        return example.Length > 200 ? example.Substring(0, 200) + "..." : example;
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

        // Array literals
        if (expression.StartsWith('[') && expression.EndsWith(']'))
        {
            return "array";
        }

        // Object literals
        if (expression.StartsWith('{') && expression.EndsWith('}'))
        {
            return "object";
        }

        // Function calls, variables, etc.
        return "expression";
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
