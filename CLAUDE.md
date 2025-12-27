# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Quick Start

```bash
# Build
dotnet build src/ImageGenCli.csproj

# Run directly (development)
dotnet run --project src/ImageGenCli.csproj -- "Your prompt here"

# Install as global tool (recommended)
dotnet pack src/ImageGenCli.csproj -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg ImageGenCli

# Then use anywhere
image-gen "Your prompt here"
```

## Architecture

Multi-provider image generation CLI using .NET 10 and System.CommandLine.

**Provider Pattern:**
- `IImageGenerationClient` - common interface for all providers
- `GeminiImageClient` - Google Gemini API (gemini-2.5-flash-image, gemini-3-pro-image-preview)
- `OpenAIImageClient` - OpenAI API (gpt-image-1.5, gpt-image-1)
- `BflImageClient` - Black Forest Labs FLUX API (flux-2-pro, flux-2-flex, flux-2-max)
- `PoeImageClient` - Poe.com API (unified access to many models)

**Models (in `Models/`):**
- `GenerationRequest` - provider-agnostic input (prompt, reference images, aspect ratio, etc.)
- `GenerationResult` - output with images array and optional text response
- `GeneratedImage` - single image with mime type and byte data

**Adding a new provider:**
1. Create `NewProviderImageClient.cs` implementing `IImageGenerationClient`
2. Add provider option to `Program.cs` switch statements (client instantiation, API key lookup, validation)

## Provider-Specific Constraints

Parameters validated at runtime - using unsupported options with a provider causes a hard error:

| Parameter | Gemini | OpenAI | BFL | Poe |
|-----------|--------|--------|-----|-----|
| `--system-prompt` | Supported | Error | Error | Error |
| `--temperature` | Supported (0.0-2.0) | Error | Error | Error |
| `--resolution` | Pro models only | Error | Supported | Model-dependent |
| `--quality` | Error | Supported | Error | Supported |
| `--samples` max | 4 | 10 | 10 | 10 |
| `--images` max | N/A | N/A | 8 | N/A |

**BFL Model Comparison:**
- `flux-2-pro` (default) - Fast (~10s), production-ready, $0.03/MP
- `flux-2-flex` - Adjustable controls (steps, guidance), best typography, $0.06/MP
- `flux-2-max` - Highest quality, web grounding, $0.07/MP

**Poe Model Access:**
Poe provides unified access to many image models (OpenAI, FLUX, Imagen, etc.) via single API.
Use `image-gen --list-models -p poe` to see available models. Model names are case-sensitive.

## Environment Variables

- `GEMINI_API_KEY` - for Gemini provider
- `OPENAI_API_KEY` - for OpenAI provider
- `BFL_API_KEY` - for BFL (FLUX) provider
- `POE_API_KEY` - for Poe provider

## Agent Skill

This project includes an [Agent Skill](https://agentskills.io) definition in `image-gen/SKILL.md` for AI agent integration. The skill provides structured instructions for agents to use the CLI effectively.

## Notes

- `.tmp/` directory is gitignored and can be used for test outputs
- Gemini's `imageSize` parameter only works with Pro models; Flash models ignore it
- OpenAI maps aspect ratios to fixed pixel dimensions (no resolution control)
- BFL uses async API with polling; dimensions must be multiples of 16
- BFL doesn't support batch generation; multiple samples are generated sequentially
- Poe returns images as markdown URLs in response content; client parses and downloads
- Use `--list-models -p <provider>` to see available models for each provider
