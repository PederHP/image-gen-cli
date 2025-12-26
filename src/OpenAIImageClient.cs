using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImageGenCli.Models;

namespace ImageGenCli;

/// <summary>
/// Image generation client for OpenAI API.
/// Supports gpt-image-1.5 and gpt-image-1 models.
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
    /// <param name="model">The model to use (default: gpt-image-1.5).</param>
    public OpenAIImageClient(string apiKey, string model = "gpt-image-1.5")
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
            ["size"] = MapSize(request.AspectRatio)
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
        form.Add(new StringContent(MapSize(request.AspectRatio)), "size");

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
}
