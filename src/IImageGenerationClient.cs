using GeminiImageGen.Models;

namespace GeminiImageGen;

public interface IImageGenerationClient
{
    Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct = default);
}
