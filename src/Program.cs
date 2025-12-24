using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeminiImageGen;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var promptArg = new Argument<string>("prompt", "The text prompt for image generation");

        var systemPromptOption = new Option<string?>(
            ["--system-prompt", "-s"],
            "Optional system instruction for the model");

        var imagesOption = new Option<FileInfo[]>(
            ["--images", "-i"],
            "Reference image file paths (0-N images)")
        { AllowMultipleArgumentsPerToken = true };

        var aspectRatioOption = new Option<string>(
            ["--aspect-ratio", "-a"],
            () => "1:1",
            "Aspect ratio: 1:1, 2:3, 3:2, 3:4, 4:3, 4:5, 5:4, 9:16, 16:9, 21:9");

        var resolutionOption = new Option<string>(
            ["--resolution", "-r"],
            () => "1K",
            "Output resolution: 1K, 2K, 4K (2K/4K require Pro model)");

        var temperatureOption = new Option<float>(
            ["--temperature", "-t"],
            () => 1.0f,
            "Generation temperature (0.0-2.0)");

        var modelOption = new Option<string>(
            ["--model", "-m"],
            () => "gemini-2.0-flash-preview-image-generation",
            "Model: gemini-2.0-flash-preview-image-generation or gemini-2.0-pro-preview-image-generation");

        var samplesOption = new Option<int>(
            ["--samples", "-n"],
            () => 1,
            "Number of images to generate (1-4)");

        var outputOption = new Option<DirectoryInfo>(
            ["--output", "-o"],
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Output directory for generated images");

        var apiKeyOption = new Option<string?>(
            ["--api-key", "-k"],
            "Gemini API key (or set GEMINI_API_KEY env var)");

        var rootCommand = new RootCommand("Generate images using Gemini's native image generation models")
        {
            promptArg,
            systemPromptOption,
            imagesOption,
            aspectRatioOption,
            resolutionOption,
            temperatureOption,
            modelOption,
            samplesOption,
            outputOption,
            apiKeyOption
        };

        rootCommand.SetHandler(async (context) =>
        {
            var prompt = context.ParseResult.GetValueForArgument(promptArg);
            var systemPrompt = context.ParseResult.GetValueForOption(systemPromptOption);
            var images = context.ParseResult.GetValueForOption(imagesOption) ?? [];
            var aspectRatio = context.ParseResult.GetValueForOption(aspectRatioOption)!;
            var resolution = context.ParseResult.GetValueForOption(resolutionOption)!;
            var temperature = context.ParseResult.GetValueForOption(temperatureOption);
            var model = context.ParseResult.GetValueForOption(modelOption)!;
            var samples = context.ParseResult.GetValueForOption(samplesOption);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption)
                         ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.Error.WriteLine("Error: API key required. Use --api-key or set GEMINI_API_KEY env var.");
                context.ExitCode = 1;
                return;
            }

            // Validate inputs
            var validAspectRatios = new[] { "1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9" };
            if (!validAspectRatios.Contains(aspectRatio))
            {
                Console.Error.WriteLine($"Error: Invalid aspect ratio '{aspectRatio}'. Valid: {string.Join(", ", validAspectRatios)}");
                context.ExitCode = 1;
                return;
            }

            var validResolutions = new[] { "1K", "2K", "4K" };
            if (!validResolutions.Contains(resolution.ToUpperInvariant()))
            {
                Console.Error.WriteLine($"Error: Invalid resolution '{resolution}'. Valid: {string.Join(", ", validResolutions)}");
                context.ExitCode = 1;
                return;
            }

            if (samples < 1 || samples > 4)
            {
                Console.Error.WriteLine("Error: Samples must be between 1 and 4.");
                context.ExitCode = 1;
                return;
            }

            // Validate reference images exist
            foreach (var image in images)
            {
                if (!image.Exists)
                {
                    Console.Error.WriteLine($"Error: Reference image not found: {image.FullName}");
                    context.ExitCode = 1;
                    return;
                }
            }

            // Ensure output directory exists
            if (!output.Exists)
            {
                output.Create();
            }

            try
            {
                var client = new GeminiImageClient(apiKey);
                var result = await client.GenerateImagesAsync(new GenerationRequest
                {
                    Prompt = prompt,
                    SystemPrompt = systemPrompt,
                    ReferenceImages = images.Select(f => f.FullName).ToArray(),
                    AspectRatio = aspectRatio,
                    Resolution = resolution.ToUpperInvariant(),
                    Temperature = temperature,
                    Model = model,
                    NumberOfImages = samples
                });

                if (result.Images.Length == 0)
                {
                    Console.Error.WriteLine("Error: No images were generated.");
                    if (!string.IsNullOrEmpty(result.TextResponse))
                    {
                        Console.Error.WriteLine($"Model response: {result.TextResponse}");
                    }
                    context.ExitCode = 1;
                    return;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                for (int i = 0; i < result.Images.Length; i++)
                {
                    var img = result.Images[i];
                    var ext = img.MimeType switch
                    {
                        "image/png" => "png",
                        "image/jpeg" => "jpg",
                        "image/webp" => "webp",
                        _ => "png"
                    };
                    var filename = result.Images.Length == 1
                        ? $"gemini-{timestamp}.{ext}"
                        : $"gemini-{timestamp}-{i + 1}.{ext}";
                    var path = Path.Combine(output.FullName, filename);

                    await File.WriteAllBytesAsync(path, img.Data);
                    Console.WriteLine($"Saved: {path}");
                }

                if (!string.IsNullOrEmpty(result.TextResponse))
                {
                    Console.WriteLine($"\nModel commentary: {result.TextResponse}");
                }
            }
            catch (GeminiApiException ex)
            {
                Console.Error.WriteLine($"API Error: {ex.Message}");
                context.ExitCode = 1;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Network Error: {ex.Message}");
                context.ExitCode = 1;
            }
        });

        return await rootCommand.InvokeAsync(args);
    }
}

// --- Client and Models ---

public class GeminiImageClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GeminiImageClient(string apiKey)
    {
        _apiKey = apiKey;
        _http = new HttpClient();
    }

    public async Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/{request.Model}:generateContent?key={_apiKey}";

        var parts = new List<object>
        {
            new { text = request.Prompt }
        };

        // Add reference images
        foreach (var imagePath in request.ReferenceImages)
        {
            var bytes = await File.ReadAllBytesAsync(imagePath, ct);
            var base64 = Convert.ToBase64String(bytes);
            var mimeType = GetMimeType(imagePath);
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = mimeType,
                    data = base64
                }
            });
        }

        var body = new Dictionary<string, object>
        {
            ["contents"] = new[] { new { parts } },
            ["generationConfig"] = new Dictionary<string, object>
            {
                ["responseModalities"] = new[] { "TEXT", "IMAGE" },
                ["temperature"] = request.Temperature,
                ["candidateCount"] = request.NumberOfImages,
                ["imageConfig"] = new Dictionary<string, object>
                {
                    ["aspectRatio"] = request.AspectRatio,
                    ["imageSize"] = request.Resolution
                }
            }
        };

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            body["systemInstruction"] = new
            {
                parts = new[] { new { text = request.SystemPrompt } }
            };
        }

        var response = await _http.PostAsJsonAsync(url, body, JsonOptions, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new GeminiApiException($"API returned {(int)response.StatusCode}: {content}");
        }

        var json = JsonDocument.Parse(content);
        var result = new GenerationResult();
        var images = new List<GeneratedImage>();
        var textParts = new List<string>();

        // Parse response - handle candidates array
        if (json.RootElement.TryGetProperty("candidates", out var candidates))
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (candidate.TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var partsArr))
                {
                    foreach (var part in partsArr.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textProp))
                        {
                            textParts.Add(textProp.GetString() ?? "");
                        }
                        else if (part.TryGetProperty("inlineData", out var inlineData) ||
                                 part.TryGetProperty("inline_data", out inlineData))
                        {
                            var mimeType = inlineData.GetProperty("mimeType").GetString()
                                           ?? inlineData.GetProperty("mime_type").GetString()
                                           ?? "image/png";
                            var data = inlineData.GetProperty("data").GetString() ?? "";
                            images.Add(new GeneratedImage
                            {
                                MimeType = mimeType,
                                Data = Convert.FromBase64String(data)
                            });
                        }
                    }
                }
            }
        }

        result.Images = images.ToArray();
        result.TextResponse = string.Join("\n", textParts.Where(t => !string.IsNullOrWhiteSpace(t)));

        return result;
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }
}

public class GenerationRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public string[] ReferenceImages { get; init; } = [];
    public string AspectRatio { get; init; } = "1:1";
    public string Resolution { get; init; } = "1K";
    public float Temperature { get; init; } = 1.0f;
    public string Model { get; init; } = "gemini-2.0-flash-preview-image-generation";
    public int NumberOfImages { get; init; } = 1;
}

public class GenerationResult
{
    public GeneratedImage[] Images { get; set; } = [];
    public string? TextResponse { get; set; }
}

public class GeneratedImage
{
    public required string MimeType { get; init; }
    public required byte[] Data { get; init; }
}

public class GeminiApiException : Exception
{
    public GeminiApiException(string message) : base(message) { }
}
