using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ImageGenCli.Models;

namespace ImageGenCli;

/// <summary>
/// Image generation client for Poe API.
/// Provides unified access to many image models (OpenAI, FLUX, Imagen, etc.).
/// </summary>
public partial class PoeImageClient : IImageGenerationClient
{
    private static readonly HttpClient ApiHttp = new();
    private static readonly HttpClient DownloadHttp = CreateDownloadHttpClient();
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://api.poe.com/v1/chat/completions";
    private const string DefaultResolution = "1K";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Regex to extract image URLs from markdown or plain text response
    [GeneratedRegex(
        @"https://[^\s\)\""\]]+\.(?:png|jpg|jpeg|webp|gif)(?:\?[^\s\)\""\]]*)?|https://pfst\.cf2\.poecdn\.net/[^\s\)\""\]]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ImageUrlPattern();

    private static HttpClient CreateDownloadHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
        return client;
    }

    /// <summary>
    /// Creates a new Poe image client.
    /// </summary>
    /// <param name="apiKey">The Poe API key.</param>
    /// <param name="model">The model to use (default: GPT-Image-1).</param>
    public PoeImageClient(string apiKey, string model = "GPT-Image-1")
    {
        _apiKey = apiKey;
        _model = model;
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct = default)
    {
        var images = new List<GeneratedImage>();

        // Poe API doesn't support batch generation, loop for multiple samples
        for (int sample = 0; sample < request.NumberOfImages; sample++)
        {
            var content = await GenerateSingleImageAsync(request, ct);
            var image = await ParseAndDownloadImageAsync(content, ct);
            if (image != null)
            {
                images.Add(image);
            }
        }

        return new GenerationResult { Images = images.ToArray() };
    }

    private async Task<string> GenerateSingleImageAsync(GenerationRequest request, CancellationToken ct)
    {
        var messages = new List<object>();

        // Add reference images as base64 content if provided
        if (request.ReferenceImages.Length > 0)
        {
            var contentParts = new List<object>();

            // Add images first
            foreach (var imagePath in request.ReferenceImages)
            {
                var bytes = await File.ReadAllBytesAsync(imagePath, ct);
                var base64 = Convert.ToBase64String(bytes);
                var mimeType = MimeTypeHelper.GetMimeType(imagePath);

                contentParts.Add(new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = $"data:{mimeType};base64,{base64}"
                    }
                });
            }

            // Add text prompt
            contentParts.Add(new
            {
                type = "text",
                text = request.Prompt
            });

            messages.Add(new
            {
                role = "user",
                content = contentParts
            });
        }
        else
        {
            messages.Add(new
            {
                role = "user",
                content = request.Prompt
            });
        }

        // Build extra_body with only provided options
        var extraBody = new Dictionary<string, object>
        {
            ["aspect"] = request.AspectRatio
        };

        if (!string.IsNullOrEmpty(request.Quality))
        {
            extraBody["quality"] = request.Quality;
        }

        // Add resolution if not default (some Poe models support it)
        if (request.Resolution != DefaultResolution)
        {
            extraBody["resolution"] = request.Resolution;
        }

        var body = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["messages"] = messages,
            ["stream"] = false,
            ["extra_body"] = extraBody
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        requestMessage.Content = JsonContent.Create(body, options: JsonOptions);

        var response = await ApiHttp.SendAsync(requestMessage, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new ImageGenerationException($"Poe API returned {(int)response.StatusCode}: {content}");
        }

        return content;
    }

    private static async Task<GeneratedImage?> ParseAndDownloadImageAsync(string responseContent, CancellationToken ct)
    {
        var json = JsonDocument.Parse(responseContent);

        // Extract the content from the response
        if (!json.RootElement.TryGetProperty("choices", out var choices) ||
            choices.GetArrayLength() == 0)
        {
            throw new ImageGenerationException($"Poe API returned no choices: {responseContent}");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var contentProp))
        {
            throw new ImageGenerationException($"Poe API returned invalid message format: {responseContent}");
        }

        var content = contentProp.GetString() ?? "";

        // Extract image URL using regex
        var match = ImageUrlPattern().Match(content);
        if (!match.Success)
        {
            throw new ImageGenerationException($"Could not find image URL in Poe response: {content}");
        }

        var imageUrl = match.Value;

        // Download the image using the download client (no auth headers, browser-like)
        var imageResponse = await DownloadHttp.GetAsync(imageUrl, ct);
        if (!imageResponse.IsSuccessStatusCode)
        {
            throw new ImageGenerationException($"Failed to download image from {imageUrl}: {(int)imageResponse.StatusCode}");
        }

        var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(ct);
        var mimeType = imageResponse.Content.Headers.ContentType?.MediaType
                       ?? MimeTypeHelper.GuessMimeTypeFromUrl(imageUrl);

        return new GeneratedImage
        {
            MimeType = mimeType,
            Data = imageBytes
        };
    }
}
