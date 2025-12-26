namespace ImageGenCli.Models;

/// <summary>
/// Result from an image generation request.
/// </summary>
public class GenerationResult
{
    /// <summary>
    /// Array of generated images.
    /// </summary>
    public GeneratedImage[] Images { get; set; } = [];

    /// <summary>
    /// Optional text response from the model (Gemini may include commentary).
    /// </summary>
    public string? TextResponse { get; set; }
}
