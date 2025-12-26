namespace ImageGenCli.Models;

/// <summary>
/// Request parameters for image generation.
/// Provider-agnostic input that gets translated to provider-specific API calls.
/// </summary>
public class GenerationRequest
{
    /// <summary>
    /// The text prompt describing the desired image.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Optional system instruction for the model (Gemini only).
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Paths to reference images for editing or style transfer.
    /// </summary>
    public string[] ReferenceImages { get; init; } = [];

    /// <summary>
    /// Output aspect ratio (e.g., "1:1", "16:9", "9:16").
    /// </summary>
    public string AspectRatio { get; init; } = "1:1";

    /// <summary>
    /// Output resolution: "1K", "2K", or "4K" (provider-dependent support).
    /// </summary>
    public string Resolution { get; init; } = "1K";

    /// <summary>
    /// Quality level: "low", "medium", or "high" (OpenAI, Poe only).
    /// </summary>
    public string? Quality { get; init; }

    /// <summary>
    /// Generation temperature from 0.0 to 2.0 (Gemini only).
    /// </summary>
    public float Temperature { get; init; } = 1.0f;

    /// <summary>
    /// Number of images to generate.
    /// </summary>
    public int NumberOfImages { get; init; } = 1;
}
