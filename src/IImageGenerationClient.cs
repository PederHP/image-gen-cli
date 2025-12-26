using ImageGenCli.Models;

namespace ImageGenCli;

public interface IImageGenerationClient
{
    Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct = default);
}
