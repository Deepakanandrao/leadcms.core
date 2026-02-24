// <copyright file="OpenAIProviderService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ImageMagick;
using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Exceptions;
using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.Plugins.AI.Configuration;
using Serilog;

namespace LeadCMS.Plugins.AI.Services;

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

    private static readonly object LogLock = new object();
    private static readonly string LogDirectory = ResolveLogDirectory();

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

            var requestLog = BuildTextRequestLog(request);

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

            var textResponse = new TextGenerationResponse
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

            var responseLog = BuildTextResponseLog(textResponse, elapsedMs);
            WriteRequestResponseLog("Text", requestLog, responseLog);

            return textResponse;
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
            var sizeValue = SelectOpenAiImageSize(request.Width, request.Height);
            var prompt = BuildViewportGuidance(request.Prompt, request.Width, request.Height);

            var includePrompt = !string.IsNullOrWhiteSpace(request.Prompt);
            var imageRequestLog = BuildImageRequestLog(request, prompt, includePrompt);

            ImageGenerationResponse imageResponse;

            if (request.EditImage != null)
            {
                imageResponse = await GenerateImageEditAsync(
                    prompt,
                    NormalizeImageInputForOpenAi(request.EditImage),
                    request.SampleImages ?? new List<ImageInput>(),
                    quality,
                    sizeValue);
            }
            else if (request.SampleImages != null && request.SampleImages.Count > 0)
            {
                var normalizedSamples = request.SampleImages.Select(NormalizeImageInputForOpenAi).ToList();
                imageResponse = await GenerateImageEditAsync(prompt, null, normalizedSamples, quality, sizeValue);
            }
            else
            {
                imageResponse = await GenerateImageFromPromptAsync(prompt, quality, sizeValue);
            }

            var imageResponseLog = BuildImageResponseLog(imageResponse);
            WriteRequestResponseLog("Image", imageRequestLog, imageResponseLog);

            return imageResponse;
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

    private static void WriteRequestResponseLog(string requestType, string requestBody, string responseBody)
    {
        lock (LogLock)
        {
            try
            {
                var timestamp = DateTimeOffset.UtcNow;
                var fileName = $"openai-{requestType.ToLowerInvariant()}-{timestamp:yyyyMMdd-HHmmss-fff}.log";
                var filePath = Path.Combine(LogDirectory, fileName);

                var entry = new StringBuilder();
                entry.AppendLine($"==== OpenAI Request ({requestType}) | {timestamp:O} ====");
                entry.AppendLine(requestBody);
                entry.AppendLine($"==== Response ====");
                entry.AppendLine(responseBody);
                entry.AppendLine($"==== End ====");

                System.IO.File.WriteAllText(filePath, entry.ToString());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write OpenAI request/response log.");
            }
        }
    }

    private static string BuildTextResponseLog(TextGenerationResponse response, long elapsedMs)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Model: {response.Model}");
        builder.AppendLine($"Tokens Used: {response.TokensUsed}");
        builder.AppendLine($"Finish Reason: {response.FinishReason}");
        builder.AppendLine($"Duration: {elapsedMs}ms");
        builder.AppendLine();
        builder.AppendLine("Generated Text:");
        builder.AppendLine(string.IsNullOrWhiteSpace(response.GeneratedText) ? "(empty)" : response.GeneratedText);
        return builder.ToString();
    }

    private static string BuildImageResponseLog(ImageGenerationResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Model: {response.Model}");
        builder.AppendLine($"Images Generated: {response.Images?.Count ?? 0}");

        if (response.Images != null)
        {
            for (var i = 0; i < response.Images.Count; i++)
            {
                var img = response.Images[i];
                builder.AppendLine($"  Image {i + 1}: {img.ImageData?.Length ?? 0} bytes");
                if (!string.IsNullOrWhiteSpace(img.RevisedPrompt))
                {
                    builder.AppendLine($"  Revised Prompt: {img.RevisedPrompt}");
                }
            }
        }

        return builder.ToString();
    }

    private static string BuildTextRequestLog(TextGenerationRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("System Prompt:");
        builder.AppendLine(string.IsNullOrWhiteSpace(request.SystemPrompt) ? "(empty)" : request.SystemPrompt);
        builder.AppendLine();
        builder.AppendLine("User Prompt:");
        builder.AppendLine(string.IsNullOrWhiteSpace(request.UserPrompt) ? "(empty)" : request.UserPrompt);
        builder.AppendLine();
        var images = request.Images ?? new List<TextImageInput>();
        if (images.Count == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine("Attached Images:");

        for (var i = 0; i < images.Count; i++)
        {
            builder.AppendLine($"- {FormatTextImageInput(images[i], i + 1)}");
        }

        return builder.ToString();
    }

    private static string BuildImageRequestLog(ImageGenerationRequest request, string prompt, bool includePrompt)
    {
        var builder = new StringBuilder();
        if (includePrompt && !string.IsNullOrWhiteSpace(prompt))
        {
            builder.AppendLine("Prompt:");
            builder.AppendLine(prompt);
        }

        if (request.EditImage != null)
        {
            builder.AppendLine();
            builder.AppendLine("Edit Image:");
            builder.AppendLine(FormatImageInput(request.EditImage));
        }

        var samples = request.SampleImages ?? new List<ImageInput>();
        if (samples.Count == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Sample Images:");

        for (var i = 0; i < samples.Count; i++)
        {
            builder.AppendLine($"- {FormatImageInput(samples[i], i + 1)}");
        }

        return builder.ToString();
    }

    private static string FormatImageInput(ImageInput image, int? index = null)
    {
        var defaultName = index.HasValue ? $"image_{index}" : "image";
        var fileName = string.IsNullOrWhiteSpace(image.FileName) ? defaultName : image.FileName;
        var mimeType = string.IsNullOrWhiteSpace(image.MimeType) ? "unknown" : image.MimeType;
        var byteCount = image.Data?.Length ?? 0;
        return $"{fileName} | {mimeType} | {byteCount} bytes";
    }

    private static string FormatTextImageInput(TextImageInput image, int index)
    {
        var fileName = string.IsNullOrWhiteSpace(image.FileName) ? $"image_{index}" : image.FileName;
        var mimeType = string.IsNullOrWhiteSpace(image.MimeType) ? "unknown" : image.MimeType;
        var byteCount = image.Data?.Length ?? 0;
        return $"{fileName} | {mimeType} | {byteCount} bytes";
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

    private static string ResolveLogDirectory()
    {
        var baseDirectory = Directory.GetCurrentDirectory();
        var logDirectory = Path.Combine(baseDirectory, "TestOutputs");
        Directory.CreateDirectory(logDirectory);
        return logDirectory;
    }

    private static ImageGenerationResponse ParseImageResponse(string responseBody, string model)
    {
        using var document = JsonDocument.Parse(responseBody);
        var images = new List<GeneratedImage>();

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

                images.Add(new GeneratedImage
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

    private static string SelectOpenAiImageSize(int? width, int? height)
    {
        if (!width.HasValue || !height.HasValue || width <= 0 || height <= 0)
        {
            return "auto";
        }

        var ratio = (double)width.Value / height.Value;
        var candidates = new Dictionary<string, double>
        {
            ["1024x1024"] = 1d,
            ["1024x1536"] = 1024d / 1536d,
            ["1536x1024"] = 1536d / 1024d,
        };

        return candidates
            .OrderBy(pair => Math.Abs(ratio - pair.Value))
            .First()
            .Key;
    }

    private static string BuildViewportGuidance(string? prompt, int? width, int? height)
    {
        var basePrompt = prompt ?? string.Empty;
        if (!width.HasValue || !height.HasValue || width <= 0 || height <= 0)
        {
            return basePrompt;
        }

        var guidance =
            $"Compose the main subject so it stays fully inside a centered safe rectangle of {width.Value}x{height.Value} px. " +
            $"This allows a {width.Value}x{height.Value} crop without cutting the subject.";

        return string.Join("\n\n", new[] { basePrompt, guidance }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private async Task<ImageGenerationResponse> GenerateImageFromPromptAsync(
        string prompt,
        string quality,
        string sizeValue)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = ImageModel,
            ["prompt"] = prompt,
            ["output_format"] = "png",
            ["quality"] = quality,
            ["moderation"] = "auto",
            ["background"] = "auto",
            ["size"] = sizeValue,
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
        string quality,
        string sizeValue)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(ImageModel), "model");
        content.Add(new StringContent(prompt), "prompt");
        content.Add(new StringContent("png"), "output_format");
        content.Add(new StringContent(quality), "quality");
        content.Add(new StringContent("auto"), "moderation");
        content.Add(new StringContent("auto"), "background");
        content.Add(new StringContent(sizeValue), "size");

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
