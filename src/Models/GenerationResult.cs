namespace ImageGenCli.Models;

public class GenerationResult
{
    public GeneratedImage[] Images { get; set; } = [];
    public string? TextResponse { get; set; }
}
