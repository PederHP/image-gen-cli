# Copilot Instructions for image-gen-cli

Multi-provider image generation CLI using .NET 10 and System.CommandLine.

## Quick Commands

```bash
# Build
dotnet build src/ImageGenCli.csproj

# Run directly (development)
dotnet run --project src/ImageGenCli.csproj -- "prompt"

# Install as global tool
dotnet pack src/ImageGenCli.csproj -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg ImageGenCli

# Then use anywhere
image-gen "prompt"
image-gen -p openai "prompt"    # OpenAI provider
image-gen -i image.png "edit"   # With reference image
```

## Architecture

**Provider pattern** - all providers implement `IImageGenerationClient` with a single method:
```csharp
Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct)
```

**Key files:**
- [src/Program.cs](../src/Program.cs) - CLI parsing, provider instantiation, validation, output handling
- [src/IImageGenerationClient.cs](../src/IImageGenerationClient.cs) - Provider interface
- [src/GeminiImageClient.cs](../src/GeminiImageClient.cs) - Google Gemini API (direct HTTP, no SDK)
- [src/OpenAIImageClient.cs](../src/OpenAIImageClient.cs) - OpenAI API (direct HTTP, no SDK)
- [src/Models/](../src/Models/) - Provider-agnostic request/response types

## Adding a New Provider

1. Create `NewProviderImageClient.cs` implementing `IImageGenerationClient`
2. In `Program.cs`, add to:
   - API key resolution switch (~line 84)
   - Default model switch (~line 99)
   - Provider validation array (~line 106)
   - Provider-specific parameter validation (~line 131)
   - Client instantiation switch (~line 171)

## Provider-Specific Notes

**Gemini:**
- Uses `generativelanguage.googleapis.com/v1beta` API directly
- `imageSize` parameter only works with Pro models; Flash models ignore it
- Supports `systemInstruction` for system prompts
- Returns both images and optional text in response

**OpenAI:**
- `/v1/images/generations` for text-only prompts
- `/v1/images/edits` with multipart form for reference images
- No system prompt, temperature, or resolution support (validated at runtime - causes error)
- Maps aspect ratios to fixed pixel dimensions in `MapSize()` (e.g., `16:9` → `1536x1024`)

## Conventions

- **No SDK dependencies** - both clients use raw `HttpClient` with direct JSON serialization
- **Models use `required` and `init`** - immutable request types with required fields
- **Error handling** - throw `ImageGenerationException` for API errors; let `HttpRequestException` propagate for network issues
- **Provider validation** - unsupported parameter combinations cause hard errors (not warnings) for agent-friendliness
- **Output naming** - `{provider}-{timestamp}.{ext}` or `{provider}-{timestamp}-{n}.{ext}` for multiple

## Environment Variables

- `GEMINI_API_KEY` - Gemini provider
- `OPENAI_API_KEY` - OpenAI provider

## Agent Skill

See `image-gen/SKILL.md` for the [Agent Skills](https://agentskills.io) definition with complete usage documentation.

## Test Outputs

Use `.tmp/` directory for test outputs (gitignored).
