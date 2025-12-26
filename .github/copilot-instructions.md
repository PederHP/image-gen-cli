# Copilot Instructions for image-gen-cli

Multi-provider image generation CLI using .NET 8 and System.CommandLine.

## Quick Commands

```bash
dotnet build src/ImageGenCli.csproj                              # Build
dotnet run --project src/ImageGenCli.csproj -- "prompt"          # Run (Gemini default)
dotnet run --project src/ImageGenCli.csproj -- -p openai "prompt" # OpenAI provider
```

## Architecture

**Provider pattern** - all providers implement `IImageGenerationClient` with a single method:
```csharp
Task<GenerationResult> GenerateImagesAsync(GenerationRequest request, CancellationToken ct)
```

**Key files:**
- [src/Program.cs](../src/Program.cs) - CLI parsing, provider instantiation, output handling
- [src/IImageGenerationClient.cs](../src/IImageGenerationClient.cs) - Provider interface
- [src/GeminiImageClient.cs](../src/GeminiImageClient.cs) - Google Gemini API (direct HTTP, no SDK)
- [src/OpenAIImageClient.cs](../src/OpenAIImageClient.cs) - OpenAI API (direct HTTP, no SDK)
- [src/Models/](../src/Models/) - Provider-agnostic request/response types

## Adding a New Provider

1. Create `NewProviderImageClient.cs` implementing `IImageGenerationClient`
2. In `Program.cs`, add to:
   - API key resolution switch (~line 82)
   - Default model switch (~line 92)
   - Provider validation array (~line 100)
   - Client instantiation switch (~line 148)

## Provider-Specific Notes

**Gemini:**
- Uses `generativelanguage.googleapis.com/v1beta` API directly
- `imageSize` parameter only works with Pro models; Flash models ignore it
- Supports `systemInstruction` for system prompts
- Returns both images and optional text in response

**OpenAI:**
- `/v1/images/generations` for text-only prompts
- `/v1/images/edits` with multipart form for reference images
- No system prompt support
- Maps aspect ratios to fixed pixel dimensions in `MapSize()` (e.g., `16:9` → `1536x1024`)
- **Extension point**: `MapSize()` currently ignores the `resolution` parameter; could be enhanced to support multiple output sizes per aspect ratio

## Conventions

- **No SDK dependencies** - both clients use raw `HttpClient` with direct JSON serialization
- **Models use `required` and `init`** - immutable request types with required fields
- **Error handling** - throw `ImageGenerationException` for API errors; let `HttpRequestException` propagate for network issues
- **Output naming** - `{provider}-{timestamp}.{ext}` or `{provider}-{timestamp}-{n}.{ext}` for multiple

## Environment Variables

- `GEMINI_API_KEY` - Gemini provider
- `OPENAI_API_KEY` - OpenAI provider

## Test Outputs

Use `.tmp/` directory for test outputs (gitignored).
