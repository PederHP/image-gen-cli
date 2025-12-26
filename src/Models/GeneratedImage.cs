namespace GeminiImageGen.Models;

public class GeneratedImage
{
    public required string MimeType { get; init; }
    public required byte[] Data { get; init; }
}
