using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageGenCli.Models;

namespace ImageGenCli;

/// <summary>
/// Image generation client for Black Forest Labs FLUX API.
/// Supports flux-2-pro, flux-2-flex, and flux-2-max models.
/// </summary>
public class BflImageClient : IImageGenerationClient
{
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://api.bfl.ml";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxWaitTime = TimeSpan.FromMinutes(5);

    private const int FlexDefaultSteps = 50;
    private const double FlexDefaultGuidance = 4.5;
    private const double MaxMegapixels = 4.0;
    private const int DimensionMultiple = 16;
    private const int MinDimension = 64;
    private const int MaxReferenceImages = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient();
    }

    /// <summary>
    /// Creates a new BFL FLUX image client.
    /// </summary>
    /// <param name="apiKey">The BFL API key.</param>
    /// <param name="model">The model to use (default: flux-2-pro).</param>
    public BflImageClient(string apiKey, string model = "flux-2-pro")
    {
        _apiKey = apiKey;
        _model = model;
    }

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct = default)
    {
        var endpoint = GetEndpoint();
        var url = $"{BaseUrl}{endpoint}";

        var (width, height) = MapAspectRatioToSize(request.AspectRatio, request.Resolution);

        var body = new Dictionary<string, object>
        {
            ["prompt"] = request.Prompt,
            ["width"] = width,
            ["height"] = height,
            ["output_format"] = "png"
        };

        // Add reference images if provided (up to max limit)
        for (int i = 0; i < Math.Min(request.ReferenceImages.Length, MaxReferenceImages); i++)
        {
            var imagePath = request.ReferenceImages[i];
            var bytes = await File.ReadAllBytesAsync(imagePath, ct);
            var base64 = Convert.ToBase64String(bytes);
            var mimeType = MimeTypeHelper.GetMimeType(imagePath);
            var dataUri = $"data:{mimeType};base64,{base64}";

            var key = i == 0 ? "input_image" : $"input_image_{i + 1}";
            body[key] = dataUri;
        }

        // Flex-specific parameters
        if (_model.Contains("flex", StringComparison.OrdinalIgnoreCase))
        {
            body["steps"] = FlexDefaultSteps;
            body["guidance"] = FlexDefaultGuidance;
        }

        var images = new List<GeneratedImage>();

        // BFL doesn't support batch generation, so we loop for multiple samples
        for (int sample = 0; sample < request.NumberOfImages; sample++)
        {
            // Use different seeds for each sample
            if (sample > 0)
            {
                body["seed"] = Random.Shared.Next();
            }

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Add("x-key", _apiKey);
            requestMessage.Content = JsonContent.Create(body, options: JsonOptions);

            var response = await Http.SendAsync(requestMessage, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new ImageGenerationException($"BFL API returned {(int)response.StatusCode}: {content}");
            }

            var asyncResponse = JsonSerializer.Deserialize<AsyncResponse>(content, JsonOptions);
            if (asyncResponse?.Id == null)
            {
                throw new ImageGenerationException($"BFL API returned invalid response: {content}");
            }

            // Poll for result
            var image = await PollForResultAsync(asyncResponse.Id, ct);
            if (image != null)
            {
                images.Add(image);
            }
        }

        return new GenerationResult { Images = images.ToArray() };
    }

    private async Task<GeneratedImage?> PollForResultAsync(string taskId, CancellationToken ct)
    {
        var pollUrl = $"{BaseUrl}/v1/get_result?id={taskId}";
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < MaxWaitTime)
        {
            ct.ThrowIfCancellationRequested();

            var response = await Http.GetAsync(pollUrl, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new ImageGenerationException($"BFL polling failed with {(int)response.StatusCode}: {content}");
            }

            var json = JsonDocument.Parse(content);
            var root = json.RootElement;

            if (root.TryGetProperty("status", out var statusProp))
            {
                var status = statusProp.GetString();

                switch (status)
                {
                    case "Ready":
                        if (root.TryGetProperty("result", out var resultProp) &&
                            resultProp.TryGetProperty("sample", out var sampleProp))
                        {
                            var imageUrl = sampleProp.GetString();
                            if (!string.IsNullOrEmpty(imageUrl))
                            {
                                return await DownloadImageAsync(imageUrl, ct);
                            }
                        }
                        throw new ImageGenerationException($"BFL returned Ready status but no image URL: {content}");

                    case "Pending":
                        await Task.Delay(PollInterval, ct);
                        continue;

                    case "Request Moderated":
                    case "Content Moderated":
                        throw new ImageGenerationException($"BFL content moderation blocked the request: {status}");

                    case "Error":
                        throw new ImageGenerationException($"BFL generation failed: {content}");

                    case "Task not found":
                        throw new ImageGenerationException($"BFL task not found: {taskId}");

                    default:
                        await Task.Delay(PollInterval, ct);
                        continue;
                }
            }

            await Task.Delay(PollInterval, ct);
        }

        throw new ImageGenerationException($"BFL generation timed out after {MaxWaitTime.TotalMinutes} minutes");
    }

    private static async Task<GeneratedImage> DownloadImageAsync(string imageUrl, CancellationToken ct)
    {
        var response = await Http.GetAsync(imageUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ImageGenerationException($"Failed to download generated image: {(int)response.StatusCode}");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";

        return new GeneratedImage
        {
            MimeType = contentType,
            Data = bytes
        };
    }

    private string GetEndpoint()
    {
        return _model.ToLowerInvariant() switch
        {
            "flux-2-pro" or "flux2-pro" => "/v1/flux-2-pro",
            "flux-2-flex" or "flux2-flex" => "/v1/flux-2-flex",
            "flux-2-max" or "flux2-max" => "/v1/flux-2-max",
            _ => "/v1/flux-2-pro" // Default to pro
        };
    }

    private static (int width, int height) MapAspectRatioToSize(string aspectRatio, string resolution)
    {
        // Determine base megapixels from resolution
        var megapixels = resolution.ToUpperInvariant() switch
        {
            "4K" => 4.0,
            "2K" => 2.0,
            _ => 1.0 // 1K default
        };

        // Constrain to max megapixels
        megapixels = Math.Min(megapixels, MaxMegapixels);
        var totalPixels = megapixels * 1_000_000;

        var (ratioW, ratioH) = ParseAspectRatio(aspectRatio);
        var scale = Math.Sqrt(totalPixels / (ratioW * ratioH));
        var width = (int)(ratioW * scale);
        var height = (int)(ratioH * scale);

        // Round to nearest multiple (BFL requirement)
        width = (width / DimensionMultiple) * DimensionMultiple;
        height = (height / DimensionMultiple) * DimensionMultiple;

        // Ensure minimum dimension
        width = Math.Max(MinDimension, width);
        height = Math.Max(MinDimension, height);

        return (width, height);
    }

    private static (double w, double h) ParseAspectRatio(string aspectRatio)
    {
        var parts = aspectRatio.Split(':');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out var w) &&
            double.TryParse(parts[1], out var h))
        {
            return (w, h);
        }
        return (1, 1); // Default to square
    }

    private class AsyncResponse
    {
        public string? Id { get; set; }
        public string? PollingUrl { get; set; }
        public double? Cost { get; set; }
    }
}
