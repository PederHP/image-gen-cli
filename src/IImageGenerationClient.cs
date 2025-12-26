using ImageGenCli.Models;

namespace ImageGenCli;

/// <summary>
/// Interface for image generation providers.
/// Implementations handle provider-specific API communication and response parsing.
/// </summary>
public interface IImageGenerationClient
{
    /// <summary>
    /// Generates images based on the provided request.
    /// </summary>
    /// <param name="request">The generation request containing prompt and parameters.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A result containing generated images and optional text response.</returns>
    /// <exception cref="ImageGenerationException">Thrown when the API returns an error.</exception>
    Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct = default);
}
