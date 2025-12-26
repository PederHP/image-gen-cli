# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build src/ImageGenCli.csproj

# Run (defaults to Gemini provider)
dotnet run --project src/ImageGenCli.csproj -- "Your prompt here"

# Run with OpenAI provider
dotnet run --project src/ImageGenCli.csproj -- -p openai "Your prompt here"

# Run with reference image
dotnet run --project src/ImageGenCli.csproj -- "Edit this image" -i /path/to/image.png
```

## Architecture

Multi-provider image generation CLI using .NET 8 and System.CommandLine.

**Provider Pattern:**
- `IImageGenerationClient` - common interface for all providers
- `GeminiImageClient` - Google Gemini API (gemini-2.5-flash-image, gemini-3-pro-image-preview)
- `OpenAIImageClient` - OpenAI API (gpt-image-1.5, gpt-image-1)

**Models (in `Models/`):**
- `GenerationRequest` - provider-agnostic input (prompt, reference images, aspect ratio, etc.)
- `GenerationResult` - output with images array and optional text response
- `GeneratedImage` - single image with mime type and byte data

**Adding a new provider:**
1. Create `NewProviderImageClient.cs` implementing `IImageGenerationClient`
2. Add provider option to `Program.cs` switch statements (client instantiation, API key lookup)

## Environment Variables

- `GEMINI_API_KEY` - for Gemini provider
- `OPENAI_API_KEY` - for OpenAI provider

## Notes

- `.tmp/` directory is gitignored and can be used for test outputs
- Gemini's `imageSize` parameter only works with Pro models; Flash models ignore it
- OpenAI uses `/v1/images/generations` for text-only, `/v1/images/edits` for reference images
