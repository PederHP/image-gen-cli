using System.CommandLine;
using GeminiImageGen;
using GeminiImageGen.Models;

var promptArg = new Argument<string>("prompt", "The text prompt for image generation");

var systemPromptOption = new Option<string?>(
    ["--system-prompt", "-s"],
    "Optional system instruction for the model");

var imagesOption = new Option<FileInfo[]>(
    ["--images", "-i"],
    "Reference image file paths (0-N images)")
{ AllowMultipleArgumentsPerToken = true };

var aspectRatioOption = new Option<string>(
    ["--aspect-ratio", "-a"],
    () => "1:1",
    "Aspect ratio: 1:1, 2:3, 3:2, 3:4, 4:3, 4:5, 5:4, 9:16, 16:9, 21:9");

var resolutionOption = new Option<string>(
    ["--resolution", "-r"],
    () => "1K",
    "Output resolution: 1K, 2K, 4K (2K/4K require Nano Banana Pro)");

var temperatureOption = new Option<float>(
    ["--temperature", "-t"],
    () => 1.0f,
    "Generation temperature (0.0-2.0)");

var modelOption = new Option<string>(
    ["--model", "-m"],
    () => "gemini-2.5-flash-image",
    "Model: gemini-2.5-flash-image (Nano Banana) or gemini-3-pro-image-preview (Nano Banana Pro)");

var samplesOption = new Option<int>(
    ["--samples", "-n"],
    () => 1,
    "Number of images to generate (1-4)");

var outputOption = new Option<DirectoryInfo>(
    ["--output", "-o"],
    () => new DirectoryInfo(Directory.GetCurrentDirectory()),
    "Output directory for generated images");

var apiKeyOption = new Option<string?>(
    ["--api-key", "-k"],
    "Gemini API key (or set GEMINI_API_KEY env var)");

var rootCommand = new RootCommand("Generate images using Google's Gemini image models")
{
    promptArg,
    systemPromptOption,
    imagesOption,
    aspectRatioOption,
    resolutionOption,
    temperatureOption,
    modelOption,
    samplesOption,
    outputOption,
    apiKeyOption
};

rootCommand.SetHandler(async (context) =>
{
    var prompt = context.ParseResult.GetValueForArgument(promptArg);
    var systemPrompt = context.ParseResult.GetValueForOption(systemPromptOption);
    var images = context.ParseResult.GetValueForOption(imagesOption) ?? [];
    var aspectRatio = context.ParseResult.GetValueForOption(aspectRatioOption)!;
    var resolution = context.ParseResult.GetValueForOption(resolutionOption)!;
    var temperature = context.ParseResult.GetValueForOption(temperatureOption);
    var model = context.ParseResult.GetValueForOption(modelOption)!;
    var samples = context.ParseResult.GetValueForOption(samplesOption);
    var output = context.ParseResult.GetValueForOption(outputOption)!;
    var apiKey = context.ParseResult.GetValueForOption(apiKeyOption)
                 ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    if (string.IsNullOrEmpty(apiKey))
    {
        Console.Error.WriteLine("Error: API key required. Use --api-key or set GEMINI_API_KEY env var.");
        context.ExitCode = 1;
        return;
    }

    // Validate inputs
    var validAspectRatios = new[] { "1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9" };
    if (!validAspectRatios.Contains(aspectRatio))
    {
        Console.Error.WriteLine($"Error: Invalid aspect ratio '{aspectRatio}'. Valid: {string.Join(", ", validAspectRatios)}");
        context.ExitCode = 1;
        return;
    }

    var validResolutions = new[] { "1K", "2K", "4K" };
    if (!validResolutions.Contains(resolution.ToUpperInvariant()))
    {
        Console.Error.WriteLine($"Error: Invalid resolution '{resolution}'. Valid: {string.Join(", ", validResolutions)}");
        context.ExitCode = 1;
        return;
    }

    if (samples < 1 || samples > 4)
    {
        Console.Error.WriteLine("Error: Samples must be between 1 and 4.");
        context.ExitCode = 1;
        return;
    }

    // Validate reference images exist
    foreach (var image in images)
    {
        if (!image.Exists)
        {
            Console.Error.WriteLine($"Error: Reference image not found: {image.FullName}");
            context.ExitCode = 1;
            return;
        }
    }

    // Ensure output directory exists
    if (!output.Exists)
    {
        output.Create();
    }

    try
    {
        IImageGenerationClient client = new GeminiImageClient(apiKey, model);
        var result = await client.GenerateImagesAsync(new GenerationRequest
        {
            Prompt = prompt,
            SystemPrompt = systemPrompt,
            ReferenceImages = images.Select(f => f.FullName).ToArray(),
            AspectRatio = aspectRatio,
            Resolution = resolution.ToUpperInvariant(),
            Temperature = temperature,
            NumberOfImages = samples
        });

        if (result.Images.Length == 0)
        {
            Console.Error.WriteLine("Error: No images were generated.");
            if (!string.IsNullOrEmpty(result.TextResponse))
            {
                Console.Error.WriteLine($"Model response: {result.TextResponse}");
            }
            context.ExitCode = 1;
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        for (int i = 0; i < result.Images.Length; i++)
        {
            var img = result.Images[i];
            var ext = img.MimeType switch
            {
                "image/png" => "png",
                "image/jpeg" => "jpg",
                "image/webp" => "webp",
                _ => "png"
            };
            var filename = result.Images.Length == 1
                ? $"gemini-{timestamp}.{ext}"
                : $"gemini-{timestamp}-{i + 1}.{ext}";
            var path = Path.Combine(output.FullName, filename);

            await File.WriteAllBytesAsync(path, img.Data);
            Console.WriteLine($"Saved: {path}");
        }

        if (!string.IsNullOrEmpty(result.TextResponse))
        {
            Console.WriteLine($"\nModel commentary: {result.TextResponse}");
        }
    }
    catch (ImageGenerationException ex)
    {
        Console.Error.WriteLine($"API Error: {ex.Message}");
        context.ExitCode = 1;
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine($"Network Error: {ex.Message}");
        context.ExitCode = 1;
    }
});

return await rootCommand.InvokeAsync(args);
