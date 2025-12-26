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
2. Add provider option to `Program.cs` switch statements (client instantiation, API key lookup, validation)

## Provider-Specific Constraints

Parameters validated at runtime - using unsupported options with a provider causes a hard error:

| Parameter | Gemini | OpenAI |
|-----------|--------|--------|
| `--system-prompt` | Supported | Error |
| `--temperature` | Supported (0.0-2.0) | Error (if not default 1.0) |
| `--resolution` | Pro models only | Error (if not default 1K) |
| `--samples` max | 4 | 10 |

## Environment Variables

- `GEMINI_API_KEY` - for Gemini provider
- `OPENAI_API_KEY` - for OpenAI provider

## Agent Skill

This project includes an [Agent Skill](https://agentskills.io) definition in `image-gen/SKILL.md` for AI agent integration. The skill provides structured instructions for agents to use the CLI effectively.

## Notes

- `.tmp/` directory is gitignored and can be used for test outputs
- Gemini's `imageSize` parameter only works with Pro models; Flash models ignore it
- OpenAI maps aspect ratios to fixed pixel dimensions (no resolution control)
