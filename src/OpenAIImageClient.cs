using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImageGenCli.Models;

namespace ImageGenCli;

/// <summary>
/// Image generation client for OpenAI API.
/// Supports gpt-image-2, gpt-image-1.5, gpt-image-1, and gpt-image-1-mini models.
/// </summary>
public class OpenAIImageClient : IImageGenerationClient
{
    private static readonly HttpClient Http = new();
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://api.openai.com/v1/images";

    /// <summary>
    /// Creates a new OpenAI image client.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="model">The model to use (default: gpt-image-2).</param>
    public OpenAIImageClient(string apiKey, string model = "gpt-image-2")
    {
        _apiKey = apiKey;
        _model = model;
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct = default)
    {
        var hasReferenceImages = request.ReferenceImages.Length > 0;

        string content;
        if (hasReferenceImages)
        {
            content = await GenerateWithImagesAsync(request, ct);
        }
        else
        {
            content = await GenerateTextOnlyAsync(request, ct);
        }

        return ParseResponse(content);
    }

    private async Task<string> GenerateTextOnlyAsync(GenerationRequest request, CancellationToken ct)
    {
        var url = $"{BaseUrl}/generations";

        var body = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["prompt"] = request.Prompt,
            ["n"] = request.NumberOfImages,
            ["size"] = ResolveSize(request)
        };

        if (!string.IsNullOrEmpty(request.Quality))
        {
            body["quality"] = request.Quality;
        }

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        requestMessage.Content = JsonContent.Create(body);

        var response = await Http.SendAsync(requestMessage, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new ImageGenerationException($"OpenAI API returned {(int)response.StatusCode}: {content}");
        }

        return content;
    }

    private async Task<string> GenerateWithImagesAsync(GenerationRequest request, CancellationToken ct)
    {
        var url = $"{BaseUrl}/edits";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_model), "model");
        form.Add(new StringContent(request.Prompt), "prompt");
        form.Add(new StringContent(request.NumberOfImages.ToString()), "n");
        form.Add(new StringContent(ResolveSize(request)), "size");

        if (!string.IsNullOrEmpty(request.Quality))
        {
            form.Add(new StringContent(request.Quality), "quality");
        }

        // Add reference images
        foreach (var imagePath in request.ReferenceImages)
        {
            var bytes = await File.ReadAllBytesAsync(imagePath, ct);
            var imageContent = new ByteArrayContent(bytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(MimeTypeHelper.GetMimeType(imagePath));
            form.Add(imageContent, "image[]", Path.GetFileName(imagePath));
        }

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await Http.SendAsync(requestMessage, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new ImageGenerationException($"OpenAI API returned {(int)response.StatusCode}: {content}");
        }

        return content;
    }

    private static GenerationResult ParseResponse(string content)
    {
        var json = JsonDocument.Parse(content);
        var result = new GenerationResult();
        var images = new List<GeneratedImage>();

        if (json.RootElement.TryGetProperty("data", out var data))
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("b64_json", out var b64))
                {
                    var base64 = b64.GetString() ?? "";
                    images.Add(new GeneratedImage
                    {
                        MimeType = "image/png",
                        Data = Convert.FromBase64String(base64)
                    });
                }
            }
        }

        result.Images = images.ToArray();
        return result;
    }

    private string ResolveSize(GenerationRequest request)
    {
        if (_model == "gpt-image-2" && !string.IsNullOrEmpty(request.Resolution) && request.Resolution != "1K")
        {
            if (TryResolveSize(request.Resolution, out var size, out _))
            {
                return size;
            }
        }
        return MapSize(request.AspectRatio);
    }

    private static string MapSize(string aspectRatio)
    {
        // OpenAI uses pixel dimensions, map from aspect ratio
        return aspectRatio switch
        {
            "1:1" => "1024x1024",
            "2:3" or "9:16" => "1024x1536",
            "3:2" or "16:9" => "1536x1024",
            "3:4" => "1024x1536",
            "4:3" => "1536x1024",
            _ => "1024x1024"
        };
    }

    /// <summary>
    /// Resolves a --resolution value for gpt-image-2, accepting either WxH (e.g. "2048x1152")
    /// or an aspect ratio (e.g. "3:2"). Validates gpt-image-2 constraints client-side:
    /// each edge a multiple of 16, max edge ≤ 3840, long:short ratio ≤ 3:1,
    /// total pixels in [655,360, 8,294,400].
    /// </summary>
    public static bool TryResolveSize(string input, out string size, out string error)
    {
        size = "";
        error = "";

        // Aspect-ratio form → pick a sensible preset.
        if (input.Contains(':') && !input.Contains('x', StringComparison.OrdinalIgnoreCase))
        {
            size = input switch
            {
                "1:1" => "2048x2048",
                "3:2" or "16:9" => "2048x1152",
                "2:3" or "9:16" => "1152x2048",
                "4:3" => "2048x1536",
                "3:4" => "1536x2048",
                _ => ""
            };
            if (size == "")
            {
                error = $"Unsupported aspect ratio '{input}' for gpt-image-2 --resolution. Try WxH (e.g. 2048x1152) or 1:1, 3:2, 2:3, 16:9, 9:16, 4:3, 3:4.";
                return false;
            }
            return true;
        }

        // WxH form.
        var parts = input.ToLowerInvariant().Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h))
        {
            error = $"Invalid --resolution '{input}'. Expected WxH (e.g. 2048x1152) or aspect ratio (e.g. 3:2).";
            return false;
        }

        if (w % 16 != 0 || h % 16 != 0)
        {
            error = $"gpt-image-2 requires both dimensions be multiples of 16 (got {w}x{h}).";
            return false;
        }
        if (w > 3840 || h > 3840)
        {
            error = $"gpt-image-2 max edge length is 3840px (got {w}x{h}).";
            return false;
        }
        var longEdge = Math.Max(w, h);
        var shortEdge = Math.Min(w, h);
        if (longEdge > shortEdge * 3)
        {
            error = $"gpt-image-2 long:short edge ratio must not exceed 3:1 (got {w}x{h}).";
            return false;
        }
        long pixels = (long)w * h;
        if (pixels < 655_360 || pixels > 8_294_400)
        {
            error = $"gpt-image-2 total pixels must be between 655,360 and 8,294,400 (got {pixels:N0} from {w}x{h}).";
            return false;
        }

        size = $"{w}x{h}";
        return true;
    }
}
