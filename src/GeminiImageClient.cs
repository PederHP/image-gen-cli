using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageGenCli.Models;

namespace ImageGenCli;

/// <summary>
/// Image generation client for Google Gemini API.
/// Supports gemini-2.5-flash-image and gemini-3-pro-image-preview models.
/// </summary>
public class GeminiImageClient : IImageGenerationClient
{
    private static readonly HttpClient Http = new();
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Creates a new Gemini image client.
    /// </summary>
    /// <param name="apiKey">The Gemini API key.</param>
    /// <param name="model">The model to use (default: gemini-2.5-flash-image).</param>
    public GeminiImageClient(string apiKey, string model = "gemini-2.5-flash-image")
    {
        _apiKey = apiKey;
        _model = model;
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/{_model}:generateContent?key={_apiKey}";

        var parts = new List<object>
        {
            new { text = request.Prompt }
        };

        // Add reference images
        foreach (var imagePath in request.ReferenceImages)
        {
            var bytes = await File.ReadAllBytesAsync(imagePath, ct);
            var base64 = Convert.ToBase64String(bytes);
            var mimeType = MimeTypeHelper.GetMimeType(imagePath);
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = mimeType,
                    data = base64
                }
            });
        }

        var imageConfig = new Dictionary<string, object>
        {
            ["aspectRatio"] = request.AspectRatio
        };

        // imageSize is only supported by gemini-3-pro-image-preview
        if (_model.Contains("pro", StringComparison.OrdinalIgnoreCase))
        {
            imageConfig["imageSize"] = request.Resolution;
        }

        var body = new Dictionary<string, object>
        {
            ["contents"] = new[] { new { parts } },
            ["generationConfig"] = new Dictionary<string, object>
            {
                ["responseModalities"] = new[] { "TEXT", "IMAGE" },
                ["temperature"] = request.Temperature,
                ["imageConfig"] = imageConfig
            }
        };

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            body["systemInstruction"] = new
            {
                parts = new[] { new { text = request.SystemPrompt } }
            };
        }

        var response = await Http.PostAsJsonAsync(url, body, JsonOptions, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new ImageGenerationException($"API returned {(int)response.StatusCode}: {content}");
        }

        var json = JsonDocument.Parse(content);
        var result = new GenerationResult();
        var images = new List<GeneratedImage>();
        var textParts = new List<string>();

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
                        // Handle both camelCase and snake_case property names
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
}
