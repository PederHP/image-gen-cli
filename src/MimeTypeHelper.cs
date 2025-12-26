namespace ImageGenCli;

/// <summary>
/// Provides MIME type detection utilities for image files.
/// </summary>
public static class MimeTypeHelper
{
    /// <summary>
    /// Gets the MIME type based on file extension.
    /// </summary>
    /// <param name="path">The file path to determine MIME type for.</param>
    /// <returns>The MIME type string (e.g., "image/png").</returns>
    public static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Gets the file extension for a MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type to get extension for.</param>
    /// <returns>The file extension without dot (e.g., "png").</returns>
    public static string GetExtension(string mimeType)
    {
        return mimeType switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/webp" => "webp",
            "image/gif" => "gif",
            "image/bmp" => "bmp",
            _ => "png"
        };
    }

    /// <summary>
    /// Guesses MIME type from a URL based on file extension in the URL.
    /// </summary>
    /// <param name="url">The URL to analyze.</param>
    /// <returns>The guessed MIME type.</returns>
    public static string GuessMimeTypeFromUrl(string url)
    {
        if (url.Contains(".png", StringComparison.OrdinalIgnoreCase)) return "image/png";
        if (url.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
            url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase)) return "image/jpeg";
        if (url.Contains(".webp", StringComparison.OrdinalIgnoreCase)) return "image/webp";
        if (url.Contains(".gif", StringComparison.OrdinalIgnoreCase)) return "image/gif";
        return "image/png"; // Default
    }
}
