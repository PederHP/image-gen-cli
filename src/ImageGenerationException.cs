namespace ImageGenCli;

/// <summary>
/// Exception thrown when an image generation API returns an error.
/// </summary>
public class ImageGenerationException : Exception
{
    /// <summary>
    /// Creates a new image generation exception with the specified message.
    /// </summary>
    /// <param name="message">The error message describing the API failure.</param>
    public ImageGenerationException(string message) : base(message) { }

    /// <summary>
    /// Creates a new image generation exception with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The error message describing the API failure.</param>
    /// <param name="innerException">The underlying exception that caused this error.</param>
    public ImageGenerationException(string message, Exception innerException) : base(message, innerException) { }
}
