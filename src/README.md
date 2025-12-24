# GeminiImageGen CLI

A simple CLI tool for Gemini's native image generation models.

## Build

```bash
dotnet build -c Release
```

## Usage

```bash
# Set API key (or use --api-key)
export GEMINI_API_KEY=your-key-here

# Basic generation
gemini-imagegen "A majestic dragon flying over a castle at sunset"

# With parameters
gemini-imagegen "A cyberpunk cat" \
  --aspect-ratio 16:9 \
  --resolution 2K \
  --temperature 1.2 \
  --samples 2 \
  --output ./images

# With reference images (style transfer, editing, etc.)
gemini-imagegen "Transform this into a watercolor painting" \
  --images photo1.jpg photo2.png \
  --aspect-ratio 1:1

# With system prompt
gemini-imagegen "A forest scene" \
  --system-prompt "You are an expert landscape artist. Generate highly detailed, photorealistic images."

# Using Pro model for higher resolution
gemini-imagegen "Detailed cityscape" \
  --model gemini-2.0-pro-preview-image-generation \
  --resolution 4K
```

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--system-prompt` | `-s` | - | System instruction for the model |
| `--images` | `-i` | - | Reference images (space-separated paths) |
| `--aspect-ratio` | `-a` | `1:1` | 1:1, 2:3, 3:2, 3:4, 4:3, 4:5, 5:4, 9:16, 16:9, 21:9 |
| `--resolution` | `-r` | `1K` | 1K, 2K, 4K (2K/4K need Pro model) |
| `--temperature` | `-t` | `1.0` | 0.0-2.0 |
| `--model` | `-m` | `gemini-2.0-flash-preview-image-generation` | Model identifier |
| `--samples` | `-n` | `1` | Number of images (1-4) |
| `--output` | `-o` | `.` | Output directory |
| `--api-key` | `-k` | env | API key (or GEMINI_API_KEY) |

## Models

- **Flash** (`gemini-2.0-flash-preview-image-generation`): Fast, 1K output
- **Pro** (`gemini-2.0-pro-preview-image-generation`): 1K/2K/4K, up to 14 reference images

> **Note**: Model names may change as Google updates their API. Check the [Gemini API docs](https://ai.google.dev/gemini-api/docs/image-generation) for current model identifiers.

## Output

Images are saved as `gemini-{timestamp}.{ext}` (or `gemini-{timestamp}-{n}.{ext}` for multiple samples).
