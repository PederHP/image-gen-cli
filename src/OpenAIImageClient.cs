using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImageGenCli.Models;

namespace ImageGenCli;

public class OpenAIImageClient : IImageGenerationClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://api.openai.com/v1/images";

    public OpenAIImageClient(string apiKey, string model = "gpt-image-1.5")
    {
        _apiKey = apiKey;
        _model = model;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

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
            ["size"] = MapSize(request.AspectRatio, request.Resolution)
        };

        var response = await _http.PostAsJsonAsync(url, body, ct);
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
        form.Add(new StringContent(MapSize(request.AspectRatio, request.Resolution)), "size");

        // Add reference images
        foreach (var imagePath in request.ReferenceImages)
        {
            var bytes = await File.ReadAllBytesAsync(imagePath, ct);
            var imageContent = new ByteArrayContent(bytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(imagePath));
            form.Add(imageContent, "image[]", Path.GetFileName(imagePath));
        }

        var response = await _http.PostAsync(url, form, ct);
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

    private static string MapSize(string aspectRatio, string resolution)
    {
        // OpenAI uses pixel dimensions, map from aspect ratio
        // Common sizes: 1024x1024, 1024x1536, 1536x1024, etc.
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

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
