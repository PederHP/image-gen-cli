namespace ImageGenCli.Models;

public class GenerationRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public string[] ReferenceImages { get; init; } = [];
    public string AspectRatio { get; init; } = "1:1";
    public string Resolution { get; init; } = "1K";
    public string? Quality { get; init; }
    public float Temperature { get; init; } = 1.0f;
    public int NumberOfImages { get; init; } = 1;
}
