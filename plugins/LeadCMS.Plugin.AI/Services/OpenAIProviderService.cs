// <copyright file="OpenAIProviderService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ClientModel;
using System.Diagnostics;
using LeadCMS.Plugin.AI.Configuration;
using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Exceptions;
using LeadCMS.Plugin.AI.Interfaces;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using Serilog;

namespace LeadCMS.Plugin.AI.Services;

public class OpenAIProviderService : IAIProviderService
{
    private readonly OpenAIClient client;

    public OpenAIProviderService(OpenAIConfig config)
    {
        var options = new OpenAIClientOptions();
        client = new OpenAIClient(new ApiKeyCredential(config.ApiKey), options);
    }

    public string ProviderName => "OpenAI";

    public async Task<TextGenerationResponse> GenerateTextAsync(TextGenerationRequest request)
    {
        try
        {
            var messages = new List<ChatMessage>();

            if (!string.IsNullOrEmpty(request.SystemPrompt))
            {
                messages.Add(ChatMessage.CreateSystemMessage(request.SystemPrompt));
            }

            messages.Add(ChatMessage.CreateUserMessage(request.UserPrompt));

            // Calculate input character counts for logging
            var systemPromptChars = request.SystemPrompt?.Length ?? 0;
            var userPromptChars = request.UserPrompt?.Length ?? 0;
            var totalInputChars = systemPromptChars + userPromptChars;

            Log.Information(
                "AI Request - Input: SystemPrompt={SystemPromptChars} chars, UserPrompt={UserPromptChars} chars, Total={TotalInputChars} chars",
                systemPromptChars,
                userPromptChars,
                totalInputChars);

            var chatRequest = new ChatCompletionOptions();

            // Always use the best available model
            var modelToUse = "gpt-5.2";

            var stopwatch = Stopwatch.StartNew();
            var response = await client.GetChatClient(modelToUse).CompleteChatAsync(messages, chatRequest);
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var generatedText = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
            var inputTokens = response.Value.Usage?.InputTokenCount ?? 0;
            var outputTokens = response.Value.Usage?.OutputTokenCount ?? 0;
            var totalTokens = response.Value.Usage?.TotalTokenCount ?? 0;
            var outputChars = generatedText.Length;

            Log.Information(
                "AI Response - Duration: {ElapsedMs}ms, Input: {InputTokens} tokens ({InputChars} chars), Output: {OutputTokens} tokens ({OutputChars} chars), Total: {TotalTokens} tokens, Model: {Model}",
                elapsedMs,
                inputTokens,
                totalInputChars,
                outputTokens,
                outputChars,
                totalTokens,
                modelToUse);

            return new TextGenerationResponse
            {
                GeneratedText = generatedText,
                Model = modelToUse,
                TokensUsed = totalTokens,
                FinishReason = response.Value.FinishReason.ToString(),
                Metadata = new Dictionary<string, object>
                {
                    ["usage"] = new
                    {
                        prompt_tokens = inputTokens,
                        completion_tokens = outputTokens,
                        total_tokens = totalTokens,
                    },
                    ["char_counts"] = new
                    {
                        system_prompt_chars = systemPromptChars,
                        user_prompt_chars = userPromptChars,
                        total_input_chars = totalInputChars,
                        output_chars = outputChars,
                    },
                    ["performance"] = new
                    {
                        duration_ms = elapsedMs,
                        tokens_per_second = elapsedMs > 0 ? (double)outputTokens / elapsedMs * 1000 : 0,
                    },
                },
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating text with OpenAI provider");

            // If there's an inner exception, throw it instead to get the root cause
            if (ex.InnerException != null)
            {
                throw new AIProviderException(ProviderName, "Failed to generate text", ex.InnerException);
            }

            throw new AIProviderException(ProviderName, "Failed to generate text", ex);
        }
    }

    public async Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request)
    {
        try
        {
            var imageRequest = new ImageGenerationOptions
            {
                Quality = request.Quality == "hd" ? GeneratedImageQuality.High : GeneratedImageQuality.Standard,
                Size = request.Size switch
                {
                    "256x256" => GeneratedImageSize.W256xH256,
                    "512x512" => GeneratedImageSize.W512xH512,
                    "1024x1024" => GeneratedImageSize.W1024xH1024,
                    "1792x1024" => GeneratedImageSize.W1792xH1024,
                    "1024x1792" => GeneratedImageSize.W1024xH1792,
                    _ => GeneratedImageSize.W1024xH1024,
                },
                Style = request.Style == "natural" ? GeneratedImageStyle.Natural : GeneratedImageStyle.Vivid,
                ResponseFormat = GeneratedImageFormat.Uri,
            };

            // Always use the best available image model
            var response = await client.GetImageClient("dall-e-3").GenerateImageAsync(request.Prompt, imageRequest);

            var images = new List<DTOs.GeneratedImage>();
            if (response.Value != null)
            {
                images.Add(new DTOs.GeneratedImage
                {
                    Url = response.Value.ImageUri?.ToString() ?? string.Empty,
                    RevisedPrompt = response.Value.RevisedPrompt,
                });
            }

            return new ImageGenerationResponse
            {
                Images = images,
                Model = "dall-e-3",
                Metadata = new Dictionary<string, object>
                {
                    ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                },
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating image with OpenAI provider");

            // If there's an inner exception, throw it instead to get the root cause
            if (ex.InnerException != null)
            {
                throw new AIProviderException(ProviderName, "Failed to generate image", ex.InnerException);
            }

            throw new AIProviderException(ProviderName, "Failed to generate image", ex);
        }
    }
}
