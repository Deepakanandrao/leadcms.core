// <copyright file="TestAIProviderService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using System.Text;
using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Interfaces;

namespace LeadCMS.Tests.TestServices;

public class TestAIProviderService : IAIProviderService
{
    private static readonly byte[] DefaultPngImage = LoadEmbeddedResource("cover-sample.png");

    private static readonly object LogLock = new object();
    private static readonly List<RecordedRequest> RecordedRequests = new List<RecordedRequest>();
    private static readonly Queue<string> NextTextResponses = new Queue<string>();
    private static readonly string LogFilePath = ResolveLogFilePath();

    public static IReadOnlyList<RecordedRequest> Requests => RecordedRequests.AsReadOnly();

    public string ProviderName => "OpenAI-Test";

    public static void Reset()
    {
        lock (LogLock)
        {
            RecordedRequests.Clear();
            NextTextResponses.Clear();
        }
    }

    public static void EnqueueTextResponse(string response)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        lock (LogLock)
        {
            NextTextResponses.Enqueue(response);
        }
    }

    public static RecordedTextRequest? GetLastTextRequest()
    {
        lock (LogLock)
        {
            return RecordedRequests.OfType<RecordedTextRequest>().LastOrDefault();
        }
    }

    public static RecordedImageRequest? GetLastImageRequest()
    {
        lock (LogLock)
        {
            return RecordedRequests.OfType<RecordedImageRequest>().LastOrDefault();
        }
    }

    public Task<TextGenerationResponse> GenerateTextAsync(TextGenerationRequest request)
    {
        var generatedText = GetNextTextResponse(request.UserPrompt);
        var record = new RecordedTextRequest(
            DateTimeOffset.UtcNow,
            request.SystemPrompt ?? string.Empty,
            request.UserPrompt ?? string.Empty,
            request.Images ?? new List<TextImageInput>());

        RecordAndLog("Text", record, BuildTextRequestLog(record));

        var response = new TextGenerationResponse
        {
            GeneratedText = generatedText,
            Model = "test-openai-text",
            TokensUsed = 0,
            FinishReason = "test",
            Metadata = new Dictionary<string, object>
            {
                ["test"] = true,
                ["timestamp"] = record.Timestamp,
            },
        };

        return Task.FromResult(response);
    }

    public Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request)
    {
        var record = new RecordedImageRequest(
            DateTimeOffset.UtcNow,
            request.Prompt ?? string.Empty,
            request.Quality ?? string.Empty,
            request.Style ?? string.Empty,
            request.Width,
            request.Height,
            request.EditImage,
            request.SampleImages ?? new List<ImageInput>());

        RecordAndLog("Image", record, BuildImageRequestLog(record));

        var response = new ImageGenerationResponse
        {
            Model = "test-openai-image",
            Images = new List<GeneratedImage>
            {
                new GeneratedImage
                {
                    Url = "test://image/1",
                    ImageData = DefaultPngImage,
                    RevisedPrompt = record.Prompt,
                },
            },
            Metadata = new Dictionary<string, object>
            {
                ["test"] = true,
                ["timestamp"] = record.Timestamp,
            },
        };

        return Task.FromResult(response);
    }

    private static void RecordAndLog(string requestType, RecordedRequest record, string logBody)
    {
        lock (LogLock)
        {
            RecordedRequests.Add(record);
            try
            {
                var header = $"==== Test OpenAI Request ({requestType}) | {record.Timestamp:O} ====";
                var entry = string.Join(
                    System.Environment.NewLine,
                    string.Empty,
                    string.Empty,
                    header,
                    logBody,
                    "==== End Request ====",
                    string.Empty,
                    string.Empty);
                System.IO.File.AppendAllText(LogFilePath, entry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write OpenAI test log: {ex.Message}");
            }
        }
    }

    private static string GetNextTextResponse(string? userPrompt)
    {
        lock (LogLock)
        {
            if (NextTextResponses.Count > 0)
            {
                return NextTextResponses.Dequeue();
            }
        }

        return $"[TestAI] {userPrompt}";
    }

    private static string BuildTextRequestLog(RecordedTextRequest record)
    {
        var builder = new StringBuilder();
        builder.AppendLine("System Prompt:");
        builder.AppendLine(string.IsNullOrWhiteSpace(record.SystemPrompt) ? "(empty)" : record.SystemPrompt);
        builder.AppendLine();
        builder.AppendLine("User Prompt:");
        builder.AppendLine(string.IsNullOrWhiteSpace(record.UserPrompt) ? "(empty)" : record.UserPrompt);
        builder.AppendLine();
        builder.AppendLine("Attached Images:");

        if (record.Images.Count == 0)
        {
            builder.AppendLine("(none)");
            return builder.ToString();
        }

        for (var i = 0; i < record.Images.Count; i++)
        {
            var image = record.Images[i];
            var fileName = string.IsNullOrWhiteSpace(image.FileName) ? $"image_{i + 1}" : image.FileName;
            var mimeType = string.IsNullOrWhiteSpace(image.MimeType) ? "unknown" : image.MimeType;
            var byteCount = image.Data?.Length ?? 0;
            builder.AppendLine($"- {fileName} | {mimeType} | {byteCount} bytes");
        }

        return builder.ToString();
    }

    private static string BuildImageRequestLog(RecordedImageRequest record)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Prompt:");
        builder.AppendLine(string.IsNullOrWhiteSpace(record.Prompt) ? "(empty)" : record.Prompt);
        builder.AppendLine();
        builder.AppendLine($"Quality: {record.Quality}");
        builder.AppendLine($"Style: {record.Style}");
        var widthText = record.Width.HasValue ? record.Width.Value.ToString() : "auto";
        var heightText = record.Height.HasValue ? record.Height.Value.ToString() : "auto";
        builder.AppendLine($"Size: {widthText}x{heightText}");
        builder.AppendLine();
        builder.AppendLine("Edit Image:");
        builder.AppendLine(record.EditImage == null ? "(none)" : FormatImageInput(record.EditImage));
        builder.AppendLine();
        builder.AppendLine("Sample Images:");

        if (record.SampleImages.Count == 0)
        {
            builder.AppendLine("(none)");
            return builder.ToString();
        }

        for (var i = 0; i < record.SampleImages.Count; i++)
        {
            builder.AppendLine($"- {FormatImageInput(record.SampleImages[i], i + 1)}");
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

    private static byte[] LoadEmbeddedResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = assembly.GetManifestResourceNames().Single(name => name.EndsWith(fileName));
        using var stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{fileName}' not found.");
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string ResolveLogFilePath()
    {
        var baseDirectory = Directory.GetCurrentDirectory();
        var logDirectory = Path.Combine(baseDirectory, "TestOutputs");
        Directory.CreateDirectory(logDirectory);
        return Path.Combine(logDirectory, "openai-requests.log");
    }

    public abstract class RecordedRequest
    {
        protected RecordedRequest(DateTimeOffset timestamp)
        {
            Timestamp = timestamp;
        }

        public DateTimeOffset Timestamp { get; }
    }

    public sealed class RecordedTextRequest : RecordedRequest
    {
        public RecordedTextRequest(DateTimeOffset timestamp, string systemPrompt, string userPrompt, List<TextImageInput> images)
            : base(timestamp)
        {
            SystemPrompt = systemPrompt;
            UserPrompt = userPrompt;
            Images = images;
        }

        public string SystemPrompt { get; }

        public string UserPrompt { get; }

        public List<TextImageInput> Images { get; }
    }

    public sealed class RecordedImageRequest : RecordedRequest
    {
        public RecordedImageRequest(
            DateTimeOffset timestamp,
            string prompt,
            string quality,
            string style,
            int? width,
            int? height,
            ImageInput? editImage,
            List<ImageInput> sampleImages)
            : base(timestamp)
        {
            Prompt = prompt;
            Quality = quality;
            Style = style;
            Width = width;
            Height = height;
            EditImage = editImage;
            SampleImages = sampleImages;
        }

        public string Prompt { get; }

        public string Quality { get; }

        public string Style { get; }

        public int? Width { get; }

        public int? Height { get; }

        public ImageInput? EditImage { get; }

        public List<ImageInput> SampleImages { get; }
    }
}