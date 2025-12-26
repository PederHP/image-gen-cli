# image-gen-cli

A multi-provider image generation CLI supporting Google Gemini and OpenAI image models.

## Features

- Generate images from text prompts
- Edit existing images with text instructions
- Multiple provider support (Gemini, OpenAI)
- Configurable aspect ratios and output sizes
- Generate multiple images in a single request
- Includes an [Agent Skill](https://agentskills.io) for AI agent integration

## Installation

**Requirements:** .NET 8.0 SDK

### Install as Global Tool (Recommended)

```bash
# Clone the repository
git clone https://github.com/your-username/image-gen-cli.git
cd image-gen-cli

# Build and install
dotnet pack src/ImageGenCli.csproj -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg ImageGenCli
```

Or use the install script:

```bash
./image-gen/scripts/install.sh      # Linux/macOS
./image-gen/scripts/install.ps1     # Windows PowerShell
```

### Run from Source

```bash
dotnet run --project src/ImageGenCli.csproj -- "Your prompt here"
```

## Configuration

Set your API key as an environment variable:

```bash
# For Gemini (default provider)
export GEMINI_API_KEY=your-api-key

# For OpenAI
export OPENAI_API_KEY=your-api-key
```

Or pass it directly with `--api-key`.

## Usage

```bash
# Basic image generation (uses Gemini by default)
image-gen "A sunset over mountains with a lake reflection"

# Use OpenAI provider
image-gen -p openai "A futuristic city skyline at night"

# Specify aspect ratio
image-gen -a 16:9 "Desktop wallpaper, abstract geometric art"

# Generate multiple images
image-gen -n 4 "Logo concepts for a coffee shop"

# Edit an existing image
image-gen -i photo.jpg "Remove the background and add a beach scene"

# Use multiple reference images
image-gen -i style.png -i content.jpg "Apply the style to the content"

# Save to specific directory
image-gen -o ./output "Product photography, ceramic mug"

# High quality with Gemini Pro
image-gen -m gemini-3-pro-image-preview -r 2K "Detailed architectural blueprint"
```

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--provider` | `-p` | `gemini` | Provider: `gemini` or `openai` |
| `--model` | `-m` | auto | Model name |
| `--images` | `-i` | none | Reference image paths |
| `--aspect-ratio` | `-a` | `1:1` | Aspect ratio (1:1, 2:3, 3:2, 3:4, 4:3, 9:16, 16:9, etc.) |
| `--resolution` | `-r` | `1K` | Resolution: 1K, 2K, 4K (Gemini Pro only) |
| `--temperature` | `-t` | `1.0` | Temperature 0.0-2.0 (Gemini only) |
| `--system-prompt` | `-s` | none | System instruction (Gemini only) |
| `--samples` | `-n` | `1` | Number of images (1-4 Gemini, 1-10 OpenAI) |
| `--output` | `-o` | current dir | Output directory |
| `--api-key` | `-k` | env var | API key override |

## Provider Comparison

| Feature | Gemini | OpenAI |
|---------|--------|--------|
| Default model | gemini-2.5-flash-image | gpt-image-1.5 |
| System prompts | Yes | No |
| Temperature control | Yes | No |
| Resolution control | Pro models | No (fixed sizes) |
| Max images per request | 4 | 10 |
| Reference image editing | Yes | Yes |

**Note:** Using unsupported options with a provider will result in an error. For example, `--temperature` with OpenAI will fail.

### Poe Provider Privacy Warning

**Important:** Images generated through the Poe provider are returned as URLs that are **publicly accessible without authentication**. While the URLs are not easily guessable, anyone with the link can view the image. Do not use the Poe provider for generating sensitive or private content if URL confidentiality is a concern.

## Output

Generated images are saved with the naming pattern:
- Single image: `{provider}-{timestamp}.{ext}`
- Multiple images: `{provider}-{timestamp}-{n}.{ext}`

## For AI Agents

This project includes an [Agent Skill](https://agentskills.io) definition at `image-gen/SKILL.md`. The skill provides structured instructions that AI agents can load on-demand to use this CLI effectively.

See also:
- `AGENTS.md` - General AI agent instructions
- `CLAUDE.md` - Claude Code specific guidance
- `.github/copilot-instructions.md` - GitHub Copilot instructions

## Development

```bash
# Build
dotnet build src/ImageGenCli.csproj

# Run tests (output to .tmp/)
dotnet run --project src/ImageGenCli.csproj -- -o .tmp "test prompt"
```

### Adding a New Provider

1. Create `NewProviderClient.cs` implementing `IImageGenerationClient`
2. Update `Program.cs` with provider options and validation
3. Update documentation files

## License

MIT
