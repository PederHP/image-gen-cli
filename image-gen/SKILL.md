---
name: image-gen
description: Generate and edit images using AI models (Gemini or OpenAI). Use this skill when you need to create images from text prompts, edit existing images, or generate variations. Supports multiple providers with automatic API key detection from environment variables.
license: MIT
compatibility: Requires .NET 8.0 runtime and either GEMINI_API_KEY or OPENAI_API_KEY environment variable
---

# Image Generation Skill

Generate images from text prompts or edit existing images using Gemini or OpenAI image models.

## When to Use This Skill

- Creating images from text descriptions
- Editing or modifying existing images with text instructions
- Generating multiple variations of an image concept
- Creating images with specific aspect ratios

## Prerequisites

The `image-gen` command must be installed. Install with:

```bash
dotnet tool install --global ImageGenCli
```

Ensure one of these environment variables is set:
- `GEMINI_API_KEY` - for Gemini provider (default)
- `OPENAI_API_KEY` - for OpenAI provider

## Command Reference

```
image-gen <prompt> [options]
```

### Arguments

| Argument | Description |
|----------|-------------|
| `prompt` | The text prompt describing the image to generate (required) |

### Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--provider` | `-p` | `gemini` | Provider: `gemini` or `openai` |
| `--model` | `-m` | auto | Model name (see Provider Models below) |
| `--images` | `-i` | none | Reference image paths for editing (can specify multiple) |
| `--aspect-ratio` | `-a` | `1:1` | Output aspect ratio |
| `--resolution` | `-r` | `1K` | Resolution: `1K`, `2K`, `4K` (Gemini Pro only) |
| `--temperature` | `-t` | `1.0` | Generation temperature 0.0-2.0 (Gemini only) |
| `--samples` | `-n` | `1` | Number of images (1-4 Gemini, 1-10 OpenAI) |
| `--output` | `-o` | current dir | Output directory for generated images |
| `--api-key` | `-k` | env var | Override API key |

### Aspect Ratios

Supported values: `1:1`, `2:3`, `3:2`, `3:4`, `4:3`, `4:5`, `5:4`, `9:16`, `16:9`, `21:9`

### Provider Models

**Gemini (default):**
- `gemini-2.5-flash-image` (default) - Fast generation
- `gemini-3-pro-image-preview` - Higher quality, supports resolution parameter

**OpenAI:**
- `gpt-image-1.5` (default)
- `gpt-image-1`

## Provider Differences

| Feature | Gemini | OpenAI |
|---------|--------|--------|
| System prompt | Supported | Not supported |
| Temperature | Supported (0.0-2.0) | Not supported |
| Resolution | Pro models only | Fixed (based on aspect ratio) |
| Max samples | 4 | 10 |
| Reference images | Supported | Supported (uses edits endpoint) |

**Important:** When using OpenAI, do not specify `--resolution`, `--system-prompt`, or `--temperature` - these will cause an error.

## Examples

### Basic Generation

```bash
# Generate a simple image with Gemini (default)
image-gen "A sunset over mountains with a lake reflection"

# Generate with OpenAI
image-gen -p openai "A futuristic city skyline at night"
```

### Aspect Ratios

```bash
# Portrait image (good for mobile wallpapers)
image-gen -a 9:16 "Abstract geometric patterns in blue and gold"

# Landscape image (good for desktop wallpapers)
image-gen -a 16:9 "Rolling hills with wildflowers"

# Square image (good for social media)
image-gen -a 1:1 "Minimalist logo design, coffee cup"
```

### Multiple Images

```bash
# Generate 4 variations
image-gen -n 4 "Watercolor painting of a cat"

# Generate to specific directory
image-gen -n 3 -o ./generated-images "Product photo of a ceramic mug"
```

### Image Editing

```bash
# Edit an existing image
image-gen -i photo.jpg "Remove the background and replace with a beach scene"

# Use multiple reference images
image-gen -i style.png -i content.jpg "Apply the style from the first image to the second"
```

### High Quality (Gemini Pro)

```bash
# Use Pro model with higher resolution
image-gen -m gemini-3-pro-image-preview -r 2K "Detailed architectural blueprint"
```

## Output

Generated images are saved to the output directory with filenames:
- `{provider}-{timestamp}.{ext}` for single images
- `{provider}-{timestamp}-{n}.{ext}` for multiple images

The command prints the full path of each saved image to stdout.

## Error Handling

Exit code `0` indicates success. Non-zero exit codes indicate errors:
- Invalid parameters (unsupported options for provider)
- Missing API key
- API errors (rate limits, content policy, etc.)
- Network errors

Error messages are written to stderr with descriptive text.
