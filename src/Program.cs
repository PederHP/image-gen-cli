using System.CommandLine;
using ImageGenCli;
using ImageGenCli.Models;

var promptArg = new Argument<string>("prompt")
{
    Description = "The text prompt for image generation"
};

var providerOption = new Option<string>("--provider", "-p")
{
    Description = "Provider: gemini, openai, bfl, or poe",
    DefaultValueFactory = _ => "gemini"
};

var systemPromptOption = new Option<string?>("--system-prompt", "-s")
{
    Description = "Optional system instruction for the model (Gemini only)"
};

var imagesOption = new Option<FileInfo[]>("--images", "-i")
{
    Description = "Reference image file paths (0-N images)",
    AllowMultipleArgumentsPerToken = true
};

var aspectRatioOption = new Option<string>("--aspect-ratio", "-a")
{
    Description = "Aspect ratio: 1:1, 2:3, 3:2, 3:4, 4:3, 4:5, 5:4, 9:16, 16:9, 21:9",
    DefaultValueFactory = _ => "1:1"
};

var resolutionOption = new Option<string>("--resolution", "-r")
{
    Description = "Output resolution: 1K, 2K, 4K (Gemini Pro, BFL, some Poe models); WxH or aspect ratio for OpenAI gpt-image-2",
    DefaultValueFactory = _ => "1K"
};

var qualityOption = new Option<string?>("--quality", "-q")
{
    Description = "Quality level: low, medium, high (OpenAI, Poe)"
};

var temperatureOption = new Option<float>("--temperature", "-t")
{
    Description = "Generation temperature (0.0-2.0, Gemini only)",
    DefaultValueFactory = _ => 1.0f
};

var modelOption = new Option<string?>("--model", "-m")
{
    Description = "Model name (use --list-models to see available models per provider)"
};

var samplesOption = new Option<int>("--samples", "-n")
{
    Description = "Number of images to generate (1-4 for Gemini, 1-10 for OpenAI)",
    DefaultValueFactory = _ => 1
};

var outputOption = new Option<DirectoryInfo>("--output", "-o")
{
    Description = "Output directory for generated images",
    DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory())
};

var apiKeyOption = new Option<string?>("--api-key", "-k")
{
    Description = "API key (or set GEMINI_API_KEY / OPENAI_API_KEY / BFL_API_KEY / POE_API_KEY env var)"
};

var listModelsOption = new Option<bool>("--list-models", "-l")
{
    Description = "List available models for the specified provider"
};

var rootCommand = new RootCommand("Generate images using Gemini, OpenAI, BFL (FLUX), or Poe image models");
rootCommand.Arguments.Add(promptArg);
rootCommand.Options.Add(providerOption);
rootCommand.Options.Add(systemPromptOption);
rootCommand.Options.Add(imagesOption);
rootCommand.Options.Add(aspectRatioOption);
rootCommand.Options.Add(resolutionOption);
rootCommand.Options.Add(qualityOption);
rootCommand.Options.Add(temperatureOption);
rootCommand.Options.Add(modelOption);
rootCommand.Options.Add(samplesOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(apiKeyOption);
rootCommand.Options.Add(listModelsOption);

// Make prompt optional when --list-models is used
promptArg.DefaultValueFactory = _ => "";

rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
    var prompt = parseResult.GetValue(promptArg) ?? "";
    var provider = (parseResult.GetValue(providerOption) ?? "gemini").ToLowerInvariant();
    var systemPrompt = parseResult.GetValue(systemPromptOption);
    var images = parseResult.GetValue(imagesOption) ?? [];
    var aspectRatio = parseResult.GetValue(aspectRatioOption) ?? "1:1";
    var resolution = parseResult.GetValue(resolutionOption) ?? "1K";
    var quality = parseResult.GetValue(qualityOption);
    var temperature = parseResult.GetValue(temperatureOption);
    var modelOverride = parseResult.GetValue(modelOption);
    var samples = parseResult.GetValue(samplesOption);
    var output = parseResult.GetValue(outputOption) ?? new DirectoryInfo(Directory.GetCurrentDirectory());
    var apiKeyOverride = parseResult.GetValue(apiKeyOption);
    var listModels = parseResult.GetValue(listModelsOption);

    // Handle --list-models
    if (listModels)
    {
        PrintModelsForProvider(provider);
        return 0;
    }

    // Validate prompt is provided when not listing models
    if (string.IsNullOrEmpty(prompt))
    {
        Console.Error.WriteLine("Error: Prompt is required. Use --help for usage information.");
        return 1;
    }

    // Determine API key based on provider
    var apiKey = apiKeyOverride ?? provider switch
    {
        "openai" => Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        "bfl" => Environment.GetEnvironmentVariable("BFL_API_KEY"),
        "poe" => Environment.GetEnvironmentVariable("POE_API_KEY"),
        _ => Environment.GetEnvironmentVariable("GEMINI_API_KEY")
    };

    if (string.IsNullOrEmpty(apiKey))
    {
        var envVar = provider switch
        {
            "openai" => "OPENAI_API_KEY",
            "bfl" => "BFL_API_KEY",
            "poe" => "POE_API_KEY",
            _ => "GEMINI_API_KEY"
        };
        Console.Error.WriteLine($"Error: API key required. Use --api-key or set {envVar} env var.");
        return 1;
    }

    // Determine model based on provider
    var model = modelOverride ?? provider switch
    {
        "openai" => "gpt-image-2",
        "bfl" => "flux-2-pro",
        "poe" => "GPT-Image-1",
        _ => "gemini-2.5-flash-image"
    };

    // Validate provider
    if (provider != "gemini" && provider != "openai" && provider != "bfl" && provider != "poe")
    {
        Console.Error.WriteLine($"Error: Invalid provider '{provider}'. Valid: gemini, openai, bfl, poe");
        return 1;
    }

    // Validate inputs
    var validAspectRatios = new[] { "1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9" };
    if (!validAspectRatios.Contains(aspectRatio))
    {
        Console.Error.WriteLine($"Error: Invalid aspect ratio '{aspectRatio}'. Valid: {string.Join(", ", validAspectRatios)}");
        return 1;
    }

    // Validate quality if provided
    if (!string.IsNullOrEmpty(quality))
    {
        var validQualities = new[] { "low", "medium", "high" };
        if (!validQualities.Contains(quality.ToLowerInvariant()))
        {
            Console.Error.WriteLine($"Error: Invalid quality '{quality}'. Valid: {string.Join(", ", validQualities)}");
            return 1;
        }
        quality = quality.ToLowerInvariant();
    }

    var maxSamples = provider switch
    {
        "openai" => 10,
        "bfl" => 10,
        "poe" => 10,
        _ => 4 // Gemini
    };
    if (samples < 1 || samples > maxSamples)
    {
        Console.Error.WriteLine($"Error: Samples must be between 1 and {maxSamples}.");
        return 1;
    }

    // Validate provider-specific parameter compatibility
    if (provider == "gemini")
    {
        if (!string.IsNullOrEmpty(quality))
        {
            Console.Error.WriteLine("Error: --quality is not supported by Gemini. Use --resolution instead.");
            return 1;
        }
    }

    if (provider == "openai")
    {
        if (resolution != "1K" && model != "gpt-image-2")
        {
            Console.Error.WriteLine($"Error: --resolution is only supported on gpt-image-2 (got model '{model}'). Use --quality instead.");
            return 1;
        }
        if (model == "gpt-image-2" && resolution != "1K")
        {
            if (!OpenAIImageClient.TryResolveSize(resolution, out _, out var sizeError))
            {
                Console.Error.WriteLine($"Error: {sizeError}");
                return 1;
            }
        }
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            Console.Error.WriteLine("Error: --system-prompt is not supported by OpenAI.");
            return 1;
        }
        if (temperature != 1.0f)
        {
            Console.Error.WriteLine("Error: --temperature is not supported by OpenAI.");
            return 1;
        }
    }

    if (provider == "bfl")
    {
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            Console.Error.WriteLine("Error: --system-prompt is not supported by BFL.");
            return 1;
        }
        if (temperature != 1.0f)
        {
            Console.Error.WriteLine("Error: --temperature is not supported by BFL.");
            return 1;
        }
        if (!string.IsNullOrEmpty(quality))
        {
            Console.Error.WriteLine("Error: --quality is not supported by BFL. Use --resolution instead.");
            return 1;
        }
        if (images.Length > 8)
        {
            Console.Error.WriteLine("Error: BFL supports a maximum of 8 reference images.");
            return 1;
        }
    }

    if (provider == "poe")
    {
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            Console.Error.WriteLine("Error: --system-prompt is not supported by Poe.");
            return 1;
        }
        if (temperature != 1.0f)
        {
            Console.Error.WriteLine("Error: --temperature is not supported by Poe.");
            return 1;
        }
        // Note: resolution passed through to API - some models may support it
    }

    // Validate reference images exist
    foreach (var image in images)
    {
        if (!image.Exists)
        {
            Console.Error.WriteLine($"Error: Reference image not found: {image.FullName}");
            return 1;
        }
    }

    // Ensure output directory exists
    if (!output.Exists)
    {
        try
        {
            output.Create();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Console.Error.WriteLine($"Error: Cannot create output directory '{output.FullName}': {ex.Message}");
            return 1;
        }
    }

    try
    {
        IImageGenerationClient client = provider switch
        {
            "openai" => new OpenAIImageClient(apiKey, model),
            "bfl" => new BflImageClient(apiKey, model),
            "poe" => new PoeImageClient(apiKey, model),
            _ => new GeminiImageClient(apiKey, model)
        };

        var result = await client.GenerateImagesAsync(new GenerationRequest
        {
            Prompt = prompt,
            SystemPrompt = systemPrompt,
            ReferenceImages = images.Select(f => f.FullName).ToArray(),
            AspectRatio = aspectRatio,
            Resolution = resolution.ToUpperInvariant(),
            Quality = quality,
            Temperature = temperature,
            NumberOfImages = samples
        }, cancellationToken);

        if (result.Images.Length == 0)
        {
            Console.Error.WriteLine("Error: No images were generated.");
            if (!string.IsNullOrEmpty(result.TextResponse))
            {
                Console.Error.WriteLine($"Model response: {result.TextResponse}");
            }
            return 1;
        }

        var prefix = provider switch
        {
            "openai" => "openai",
            "bfl" => "flux",
            "poe" => "poe",
            _ => "gemini"
        };
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        for (int i = 0; i < result.Images.Length; i++)
        {
            var img = result.Images[i];
            var ext = MimeTypeHelper.GetExtension(img.MimeType);
            var filename = result.Images.Length == 1
                ? $"{prefix}-{timestamp}.{ext}"
                : $"{prefix}-{timestamp}-{i + 1}.{ext}";
            var path = Path.Combine(output.FullName, filename);

            try
            {
                await File.WriteAllBytesAsync(path, img.Data, cancellationToken);
                Console.WriteLine($"Saved: {path}");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Console.Error.WriteLine($"Error: Cannot save image to '{path}': {ex.Message}");
                return 1;
            }
        }

        if (!string.IsNullOrEmpty(result.TextResponse))
        {
            Console.WriteLine($"\nModel commentary: {result.TextResponse}");
        }

        return 0;
    }
    catch (ImageGenerationException ex)
    {
        Console.Error.WriteLine($"API Error: {ex.Message}");
        return 1;
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine($"Network Error: {ex.Message}");
        return 1;
    }
});

return await rootCommand.Parse(args).InvokeAsync();

static void PrintModelsForProvider(string provider)
{
    Console.WriteLine($"Available models for {provider}:\n");

    switch (provider.ToLowerInvariant())
    {
        case "gemini":
            Console.WriteLine("  gemini-2.5-flash-image    (default) Fast generation, good quality");
            Console.WriteLine("  gemini-3-pro-image-preview          Higher quality, supports --resolution");
            break;

        case "openai":
            Console.WriteLine("  gpt-image-2      (default) Latest model, supports --resolution (WxH or aspect ratio)");
            Console.WriteLine("  gpt-image-1.5              Previous generation model");
            Console.WriteLine("  gpt-image-1                Earlier generation model");
            Console.WriteLine("  gpt-image-1-mini           Lightweight model for basic generation");
            Console.WriteLine();
            Console.WriteLine("gpt-image-2 --resolution constraints:");
            Console.WriteLine("  WxH form (e.g. 2048x1152): each edge multiple of 16, max edge 3840px,");
            Console.WriteLine("  long:short ratio ≤ 3:1, total pixels between 655,360 and 8,294,400.");
            Console.WriteLine("  Aspect ratio form (e.g. 3:2) maps to a preset near 2K.");
            break;

        case "bfl":
            Console.WriteLine("  flux-2-pro     (default) Fast (~10s), production-ready, $0.03/MP");
            Console.WriteLine("  flux-2-flex              Adjustable controls, best typography, $0.06/MP");
            Console.WriteLine("  flux-2-max               Highest quality, web grounding, $0.07/MP");
            break;

        case "poe":
            Console.WriteLine("Poe provides access to many image models via a single API.\n");
            Console.WriteLine("Popular models:");
            Console.WriteLine("  GPT-Image-1        (default) OpenAI's ChatGPT image model");
            Console.WriteLine("  GPT-Image-1.5               OpenAI's latest image model");
            Console.WriteLine("  GPT-Image-1-Mini            OpenAI's lighter image model");
            Console.WriteLine("  FLUX-2-Pro                  Black Forest Labs FLUX.2 Pro");
            Console.WriteLine("  FLUX-2-Flex                 Black Forest Labs FLUX.2 Flex");
            Console.WriteLine("  FLUX-2-Dev                  Black Forest Labs FLUX.2 Dev (open-weight)");
            Console.WriteLine("  Flux-Kontext-Pro            FLUX.1 Kontext Pro (editing focus)");
            Console.WriteLine("  Flux-Kontext-Max            FLUX.1 Kontext Max");
            Console.WriteLine("  FLUX-Krea                   FLUX Dev tuned for aesthetics");
            Console.WriteLine("  Imagen-4                    Google DeepMind Imagen 4");
            Console.WriteLine("  Imagen-4-Fast               Google DeepMind Imagen 4 (faster)");
            Console.WriteLine("  Imagen-4-Ultra              Google DeepMind Imagen 4 (highest quality)");
            Console.WriteLine("  Nano-Banana                 Gemini 2.5 Flash Image model");
            Console.WriteLine("  Nano-Banana-Pro             Gemini 3 Pro Image Preview");
            Console.WriteLine("  Seedream-4.0                ByteDance's latest model, great text rendering");
            Console.WriteLine("  Qwen-Image                  Alibaba's model, strong text rendering");
            Console.WriteLine("\nNote: Model availability may change. Check poe.com for current list.");
            Console.WriteLine("Model names are case-sensitive (e.g., GPT-Image-1, not gpt-image-1).");
            break;

        default:
            Console.Error.WriteLine($"Unknown provider: {provider}");
            Console.Error.WriteLine("Valid providers: gemini, openai, bfl, poe");
            break;
    }
}
