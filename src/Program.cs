using System.CommandLine;
using ImageGenCli;
using ImageGenCli.Models;

var promptArg = new Argument<string>("prompt", "The text prompt for image generation");

var providerOption = new Option<string>(
    ["--provider", "-p"],
    () => "gemini",
    "Provider: gemini, openai, bfl, or poe");

var systemPromptOption = new Option<string?>(
    ["--system-prompt", "-s"],
    "Optional system instruction for the model (Gemini only)");

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
    "Output resolution: 1K, 2K, 4K (Gemini Pro only)");

var temperatureOption = new Option<float>(
    ["--temperature", "-t"],
    () => 1.0f,
    "Generation temperature (0.0-2.0, Gemini only)");

var modelOption = new Option<string?>(
    ["--model", "-m"],
    "Model name (use --list-models to see available models per provider)");

var samplesOption = new Option<int>(
    ["--samples", "-n"],
    () => 1,
    "Number of images to generate (1-4 for Gemini, 1-10 for OpenAI)");

var outputOption = new Option<DirectoryInfo>(
    ["--output", "-o"],
    () => new DirectoryInfo(Directory.GetCurrentDirectory()),
    "Output directory for generated images");

var apiKeyOption = new Option<string?>(
    ["--api-key", "-k"],
    "API key (or set GEMINI_API_KEY / OPENAI_API_KEY / BFL_API_KEY / POE_API_KEY env var)");

var listModelsOption = new Option<bool>(
    ["--list-models", "-l"],
    "List available models for the specified provider");

var rootCommand = new RootCommand("Generate images using Gemini, OpenAI, BFL (FLUX), or Poe image models")
{
    promptArg,
    providerOption,
    systemPromptOption,
    imagesOption,
    aspectRatioOption,
    resolutionOption,
    temperatureOption,
    modelOption,
    samplesOption,
    outputOption,
    apiKeyOption,
    listModelsOption
};

// Make prompt optional when --list-models is used
promptArg.SetDefaultValue("");

rootCommand.SetHandler(async (context) =>
{
    var prompt = context.ParseResult.GetValueForArgument(promptArg);
    var provider = context.ParseResult.GetValueForOption(providerOption)!.ToLowerInvariant();
    var systemPrompt = context.ParseResult.GetValueForOption(systemPromptOption);
    var images = context.ParseResult.GetValueForOption(imagesOption) ?? [];
    var aspectRatio = context.ParseResult.GetValueForOption(aspectRatioOption)!;
    var resolution = context.ParseResult.GetValueForOption(resolutionOption)!;
    var temperature = context.ParseResult.GetValueForOption(temperatureOption);
    var modelOverride = context.ParseResult.GetValueForOption(modelOption);
    var samples = context.ParseResult.GetValueForOption(samplesOption);
    var output = context.ParseResult.GetValueForOption(outputOption)!;
    var apiKeyOverride = context.ParseResult.GetValueForOption(apiKeyOption);
    var listModels = context.ParseResult.GetValueForOption(listModelsOption);

    // Handle --list-models
    if (listModels)
    {
        PrintModelsForProvider(provider);
        return;
    }

    // Validate prompt is provided when not listing models
    if (string.IsNullOrEmpty(prompt))
    {
        Console.Error.WriteLine("Error: Prompt is required. Use --help for usage information.");
        context.ExitCode = 1;
        return;
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
        context.ExitCode = 1;
        return;
    }

    // Determine model based on provider
    var model = modelOverride ?? provider switch
    {
        "openai" => "gpt-image-1.5",
        "bfl" => "flux-2-pro",
        "poe" => "GPT-Image-1",
        _ => "gemini-2.5-flash-image"
    };

    // Validate provider
    if (provider != "gemini" && provider != "openai" && provider != "bfl" && provider != "poe")
    {
        Console.Error.WriteLine($"Error: Invalid provider '{provider}'. Valid: gemini, openai, bfl, poe");
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
        context.ExitCode = 1;
        return;
    }

    // Validate provider-specific parameter compatibility
    if (provider == "openai")
    {
        if (resolution != "1K")
        {
            Console.Error.WriteLine("Error: --resolution is not supported by OpenAI. Remove this option (OpenAI uses fixed sizes based on aspect ratio).");
            context.ExitCode = 1;
            return;
        }
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            Console.Error.WriteLine("Error: --system-prompt is not supported by OpenAI.");
            context.ExitCode = 1;
            return;
        }
        if (temperature != 1.0f)
        {
            Console.Error.WriteLine("Error: --temperature is not supported by OpenAI.");
            context.ExitCode = 1;
            return;
        }
    }

    if (provider == "bfl")
    {
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            Console.Error.WriteLine("Error: --system-prompt is not supported by BFL.");
            context.ExitCode = 1;
            return;
        }
        if (temperature != 1.0f)
        {
            Console.Error.WriteLine("Error: --temperature is not supported by BFL.");
            context.ExitCode = 1;
            return;
        }
        if (images.Length > 8)
        {
            Console.Error.WriteLine("Error: BFL supports a maximum of 8 reference images.");
            context.ExitCode = 1;
            return;
        }
    }

    if (provider == "poe")
    {
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            Console.Error.WriteLine("Error: --system-prompt is not supported by Poe.");
            context.ExitCode = 1;
            return;
        }
        if (temperature != 1.0f)
        {
            Console.Error.WriteLine("Error: --temperature is not supported by Poe.");
            context.ExitCode = 1;
            return;
        }
        if (resolution != "1K")
        {
            Console.Error.WriteLine("Error: --resolution is not supported by Poe. Use quality parameter in model selection.");
            context.ExitCode = 1;
            return;
        }
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
            var ext = img.MimeType switch
            {
                "image/png" => "png",
                "image/jpeg" => "jpg",
                "image/webp" => "webp",
                _ => "png"
            };
            var filename = result.Images.Length == 1
                ? $"{prefix}-{timestamp}.{ext}"
                : $"{prefix}-{timestamp}-{i + 1}.{ext}";
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
            Console.WriteLine("  gpt-image-1.5    (default) Latest model, exceptional quality");
            Console.WriteLine("  gpt-image-1                Previous generation model");
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
