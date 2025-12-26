# AGENTS.md

Instructions for AI agents working with this codebase.

## Quick Start

```bash
# If image-gen is installed globally
image-gen "Your prompt here"

# If working from source
dotnet run --project src/ImageGenCli.csproj -- "Your prompt here"
```

## Installation

```bash
# From source
dotnet pack src/ImageGenCli.csproj -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg ImageGenCli
```

## Usage Examples

```bash
# Basic generation (Gemini default)
image-gen "A sunset over mountains"

# OpenAI provider
image-gen -p openai "A futuristic cityscape"

# With aspect ratio
image-gen -a 16:9 "Desktop wallpaper, abstract art"

# Multiple images
image-gen -n 4 "Logo concepts for a coffee shop"

# Edit existing image
image-gen -i photo.jpg "Remove the background"

# Specify output directory
image-gen -o ./output "Product photo mockup"
```

## Provider Constraints

**When using OpenAI (`-p openai`), do NOT use:**
- `--resolution` / `-r` (causes error)
- `--system-prompt` / `-s` (causes error)
- `--temperature` / `-t` with non-default value (causes error)

These parameters are Gemini-only. The CLI will return a non-zero exit code with an error message if you use them with OpenAI.

## Architecture

**Provider Pattern** - all providers implement `IImageGenerationClient`:
```csharp
Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct)
```

**Key files:**
- `src/Program.cs` - CLI parsing, provider instantiation, validation
- `src/IImageGenerationClient.cs` - Provider interface
- `src/GeminiImageClient.cs` - Google Gemini API
- `src/OpenAIImageClient.cs` - OpenAI API
- `src/Models/` - Provider-agnostic request/response types

## Adding a New Provider

1. Create `NewProviderImageClient.cs` implementing `IImageGenerationClient`
2. In `Program.cs`, add to:
   - API key resolution switch (~line 84)
   - Default model switch (~line 99)
   - Provider validation (~line 106)
   - Provider-specific parameter validation (~line 131)
   - Client instantiation switch (~line 171)

## Environment Variables

- `GEMINI_API_KEY` - for Gemini provider
- `OPENAI_API_KEY` - for OpenAI provider

## Agent Skill

See `image-gen/SKILL.md` for the [Agent Skills](https://agentskills.io) definition with complete parameter documentation.
