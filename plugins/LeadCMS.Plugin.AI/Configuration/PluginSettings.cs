// <copyright file="PluginSettings.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugins.AI.Configuration;

public class PluginConfig
{
    public OpenAIConfig OpenAI { get; set; } = new OpenAIConfig();
}

/// <summary>
/// API Documentation: https://platform.openai.com/docs/api-reference.
/// </summary>
public class OpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
}
