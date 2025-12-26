using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageGenCli.Models;

namespace ImageGenCli;

public class GeminiImageClient : IImageGenerationClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GeminiImageClient(string apiKey, string model = "gemini-2.5-flash-image")
    {
        _apiKey = apiKey;
        _model = model;
        _http = new HttpClient();
    }

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

        var response = await _http.PostAsJsonAsync(url, body, JsonOptions, ct);
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
