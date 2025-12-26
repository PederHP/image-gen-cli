namespace ImageGenCli.Models;

/// <summary>
/// A single generated image with its data and format.
/// </summary>
public class GeneratedImage
{
    /// <summary>
    /// MIME type of the image (e.g., "image/png", "image/jpeg").
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// Raw image data as bytes.
    /// </summary>
    public required byte[] Data { get; init; }
}
