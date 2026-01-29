// <copyright file="OpenAIProviderService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImageMagick;
using LeadCMS.Plugin.AI.Configuration;
using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Exceptions;
using LeadCMS.Plugin.AI.Interfaces;
using Serilog;

namespace LeadCMS.Plugin.AI.Services;

public class OpenAIProviderService : IAIProviderService
{
    private const string ImageModel = "gpt-image-1.5";
    private static readonly HashSet<string> SupportedOpenAiImageMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
        };

    private readonly HttpClient httpClient;
    private readonly string apiKey;

    public OpenAIProviderService(OpenAIConfig config)
    {
        apiKey = config.ApiKey;
        httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string ProviderName => "OpenAI";

    public async Task<TextGenerationResponse> GenerateTextAsync(TextGenerationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new AIProviderException(ProviderName, "OpenAI API key is not configured.");
            }

            // Calculate input character counts for logging
            var systemPromptChars = request.SystemPrompt?.Length ?? 0;
            var userPromptChars = request.UserPrompt?.Length ?? 0;
            var totalInputChars = systemPromptChars + userPromptChars;

            Log.Information(
                "AI Request - Input: SystemPrompt={SystemPromptChars} chars, UserPrompt={UserPromptChars} chars, Total={TotalInputChars} chars",
                systemPromptChars,
                userPromptChars,
                totalInputChars);

            // Always use the best available model
            var modelToUse = "gpt-5.2";

            var userContent = new List<object>
            {
                new
                {
                    type = "input_text",
                    text = request.UserPrompt,
                },
            };

            foreach (var image in request.Images ?? new List<TextImageInput>())
            {
                if (image.Data == null || image.Data.Length == 0)
                {
                    continue;
                }

                var normalized = NormalizeVisionImage(image);
                var base64 = Convert.ToBase64String(normalized.Data);
                var dataUrl = $"data:{normalized.MimeType};base64,{base64}";

                userContent.Add(new
                {
                    type = "input_image",
                    image_url = dataUrl,
                    detail = "low",
                });
            }

            var input = new List<object>
            {
                new
                {
                    role = "user",
                    content = userContent,
                },
            };

            var payload = new Dictionary<string, object>
            {
                ["model"] = modelToUse,
                ["input"] = input,
            };

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                payload["instructions"] = request.SystemPrompt;
            }

            var stopwatch = Stopwatch.StartNew();
            var response = await httpClient.PostAsJsonAsync("responses", payload);
            stopwatch.Stop();

            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = ExtractOpenAiErrorMessage(responseText);
                Log.Error("OpenAI responses error: {StatusCode} {Response}", response.StatusCode, responseText);
                throw new AIProviderException(ProviderName, errorMessage);
            }

            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            var generatedText = ExtractResponseOutputText(root);
            var usage = root.TryGetProperty("usage", out var usageProp) ? usageProp : default;
            var inputTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("input_tokens", out var promptTokens)
                ? promptTokens.GetInt32()
                : 0;
            var outputTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("output_tokens", out var completionTokens)
                ? completionTokens.GetInt32()
                : 0;
            var totalTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("total_tokens", out var totalTokensProp)
                ? totalTokensProp.GetInt32()
                : 0;

            var elapsedMs = stopwatch.ElapsedMilliseconds;
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
                FinishReason = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "completed" : "completed",
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
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new AIProviderException(ProviderName, "OpenAI API key is not configured.");
            }

            var quality = BuildQualityString(request.Quality);
            var hasReferenceImages = (request.SampleImages != null && request.SampleImages.Count > 0) || request.EditImage != null;
            var prompt = BuildPromptWithSizeGuidance(request.Prompt, request.Width, request.Height, hasReferenceImages);

            if (request.EditImage != null)
            {
                return await GenerateImageEditAsync(
                    prompt,
                    NormalizeImageInputForOpenAi(request.EditImage),
                    request.SampleImages ?? new List<ImageInput>(),
                    quality);
            }

            if (request.SampleImages != null && request.SampleImages.Count > 0)
            {
                var normalizedSamples = request.SampleImages.Select(NormalizeImageInputForOpenAi).ToList();
                return await GenerateImageEditAsync(prompt, null, normalizedSamples, quality);
            }

            return await GenerateImageFromPromptAsync(prompt, quality);
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

    private static string ExtractResponseOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var outputProp) || outputProp.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var texts = new List<string>();

        foreach (var item in outputProp.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "message")
            {
                continue;
            }

            if (!item.TryGetProperty("content", out var contentProp) || contentProp.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var content in contentProp.EnumerateArray())
            {
                if (content.TryGetProperty("type", out var contentType) && contentType.GetString() == "output_text" &&
                    content.TryGetProperty("text", out var textProp))
                {
                    var textValue = textProp.GetString();
                    if (!string.IsNullOrWhiteSpace(textValue))
                    {
                        texts.Add(textValue);
                    }
                }
            }
        }

        return string.Join("\n", texts);
    }

    private static TextImageInput NormalizeVisionImage(TextImageInput image)
    {
        if (IsPngImage(image))
        {
            return new TextImageInput
            {
                Data = image.Data,
                MimeType = "image/png",
                FileName = string.IsNullOrWhiteSpace(image.FileName) ? "image.png" : image.FileName,
            };
        }

        using var magick = new MagickImage(image.Data);
        magick.Format = MagickFormat.Png;
        var pngBytes = magick.ToByteArray();

        return new TextImageInput
        {
            Data = pngBytes,
            MimeType = "image/png",
            FileName = string.IsNullOrWhiteSpace(image.FileName) ? "image.png" : image.FileName,
        };
    }

    private static bool IsPngImage(TextImageInput image)
    {
        if (!string.IsNullOrWhiteSpace(image.MimeType) &&
            string.Equals(image.MimeType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(image.FileName) &&
            string.Equals(Path.GetExtension(image.FileName), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static ImageInput NormalizeImageInputForOpenAi(ImageInput image)
    {
        if (image.Data == null || image.Data.Length == 0)
        {
            return image;
        }

        var resolvedMimeType = ResolveMimeType(image.FileName, image.MimeType);
        if (SupportedOpenAiImageMimeTypes.Contains(resolvedMimeType))
        {
            var targetExtension = ResolveExtensionForMimeType(resolvedMimeType);
            return new ImageInput
            {
                Data = image.Data,
                FileName = AdjustFileNameExtension(image.FileName, targetExtension),
                MimeType = resolvedMimeType,
            };
        }

        using var magick = new MagickImage(image.Data);
        magick.Format = MagickFormat.Png;
        var pngBytes = magick.ToByteArray();

        return new ImageInput
        {
            Data = pngBytes,
            FileName = AdjustFileNameExtension(image.FileName, ".png"),
            MimeType = "image/png",
        };
    }

    private static string ResolveExtensionForMimeType(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".png",
        };
    }

    private static string AdjustFileNameExtension(string? originalFileName, string targetExtension)
    {
        var extension = string.Empty;
        if (!string.IsNullOrWhiteSpace(targetExtension))
        {
            extension = targetExtension.StartsWith('.') ? targetExtension : "." + targetExtension;
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return string.IsNullOrWhiteSpace(extension) ? "image" : "image" + extension;
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            return originalFileName;
        }

        return Path.ChangeExtension(originalFileName, extension);
    }

    private static string BuildPromptWithSizeGuidance(string prompt, int? width, int? height, bool hasSampleImages)
    {
        var sizeHint = width.HasValue && height.HasValue ? $"{width.Value}x{height.Value}" : null;
        if (string.IsNullOrWhiteSpace(sizeHint) && !hasSampleImages)
        {
            return prompt;
        }

        var guidance = new List<string>();
        if (hasSampleImages)
        {
            guidance.Add("Match the output dimensions and aspect ratio of the provided sample images when possible.");
        }

        if (!string.IsNullOrWhiteSpace(sizeHint))
        {
            guidance.Add($"If no sample dimensions apply, target image size: {sizeHint}.");
        }

        return string.Join("\n\n", new[] { prompt, "Image size guidance:", string.Join(" ", guidance) }.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string BuildQualityString(string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality))
        {
            return "auto";
        }

        return quality.Trim().ToLowerInvariant() switch
        {
            "hd" => "high",
            "high" => "high",
            "medium" => "medium",
            "low" => "low",
            _ => "auto",
        };
    }

    private static string ResolveMimeType(string fileName, string? mimeType)
    {
        if (!string.IsNullOrWhiteSpace(mimeType))
        {
            return mimeType;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
    }

    private static ImageGenerationResponse ParseImageResponse(string responseBody, string model)
    {
        using var document = JsonDocument.Parse(responseBody);
        var images = new List<DTOs.GeneratedImage>();

        if (document.RootElement.TryGetProperty("data", out var dataElement) &&
            dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataElement.EnumerateArray())
            {
                byte[]? imageBytes = null;
                string? revisedPrompt = null;

                if (item.TryGetProperty("b64_json", out var b64Element))
                {
                    var b64 = b64Element.GetString();
                    if (!string.IsNullOrWhiteSpace(b64))
                    {
                        imageBytes = Convert.FromBase64String(b64);
                    }
                }

                if (item.TryGetProperty("revised_prompt", out var revisedElement))
                {
                    revisedPrompt = revisedElement.GetString();
                }

                images.Add(new DTOs.GeneratedImage
                {
                    ImageData = imageBytes,
                    RevisedPrompt = revisedPrompt,
                });
            }
        }

        return new ImageGenerationResponse
        {
            Images = images,
            Model = model,
            Metadata = new Dictionary<string, object>
            {
                ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            },
        };
    }

    private static string ExtractOpenAiErrorMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.TryGetProperty("message", out var messageElement))
            {
                var message = messageElement.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore and fall back to raw response.
        }

        return responseBody;
    }

    private async Task<ImageGenerationResponse> GenerateImageFromPromptAsync(string prompt, string quality)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = ImageModel,
            ["prompt"] = prompt,
            ["output_format"] = "png",
            ["quality"] = quality,
        };

        using var response = await httpClient.PostAsJsonAsync("images/generations", payload);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new AIProviderException(ProviderName, ExtractOpenAiErrorMessage(responseBody));
        }

        return ParseImageResponse(responseBody, ImageModel);
    }

    private async Task<ImageGenerationResponse> GenerateImageEditAsync(
        string prompt,
        ImageInput? editImage,
        List<ImageInput> sampleImages,
        string quality)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(ImageModel), "model");
        content.Add(new StringContent(prompt), "prompt");
        content.Add(new StringContent("png"), "output_format");
        content.Add(new StringContent(quality), "quality");

        if (editImage != null)
        {
            editImage = NormalizeImageInputForOpenAi(editImage);
        }

        var imagesToSend = new List<ImageInput>();
        if (editImage != null)
        {
            imagesToSend.Add(editImage);
        }

        if (sampleImages.Count > 0)
        {
            imagesToSend.AddRange(sampleImages);
        }

        imagesToSend = imagesToSend.Take(5).ToList();
        for (var i = 0; i < imagesToSend.Count; i++)
        {
            var image = NormalizeImageInputForOpenAi(imagesToSend[i]);
            var imageContent = new ByteArrayContent(image.Data);
            var contentType = ResolveMimeType(image.FileName, image.MimeType);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(imageContent, "image[]", image.FileName);
        }

        using var response = await httpClient.PostAsync("images/edits", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new AIProviderException(ProviderName, ExtractOpenAiErrorMessage(responseBody));
        }

        return ParseImageResponse(responseBody, ImageModel);
    }
}
