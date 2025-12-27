---
name: image-gen
description: Generate and edit images using AI models (Gemini, OpenAI, BFL FLUX, or Poe). Use this skill when you need to create images from text prompts, edit existing images, or generate variations. Supports multiple providers with automatic API key detection from environment variables.
license: MIT
compatibility: Requires .NET 10.0 runtime and at least one of GEMINI_API_KEY, OPENAI_API_KEY, BFL_API_KEY, or POE_API_KEY environment variable
---

# Image Generation Skill

Generate images from text prompts or edit existing images using Gemini, OpenAI, BFL (FLUX), or Poe image models.

For detailed model information, see [MODELS.md](references/MODELS.md) or run `image-gen --list-models -p <provider>`.

## When to Use This Skill

- Creating images from text descriptions
- Editing or modifying existing images with text instructions
- Generating multiple variations of an image concept
- Creating images with specific aspect ratios

## Workflows

Modern image models (Gemini and GPT-Image-1.5) are highly capable at instruction following. They can render readable text, create diagrams, produce technical illustrations, and handle complex multi-element compositions. The quality of output depends heavily on prompt articulation and iterative refinement.

### Articulating the Desired Output

Before generating, clearly define the intent:

**Well-defined outputs** (diagrams, UI mockups, illustrations with specific requirements):
- Be explicit about every element: layout, colors, text content, style
- Specify what should NOT appear if relevant
- Example: "A flowchart showing user authentication flow. Three boxes labeled 'Login Form', 'Validate Credentials', 'Dashboard'. Arrows connecting left to right. Clean minimal style, black lines on white background, sans-serif font."

**Creative exploration** (concept art, aesthetic exploration, inspiration):
- Provide mood, style references, and general direction
- Leave room for interpretation
- Example: "Concept art for a solarpunk city. Organic architecture integrated with nature. Morning light. Studio Ghibli inspired, painterly style."

**Balanced approach** (most common):
- Specify the critical elements precisely
- Allow flexibility on secondary details
- Example: "Product photo of a ceramic coffee mug, matte black finish, on a wooden table. Natural lighting, shallow depth of field. The mug should be the clear focus."

### Generation Budget Strategy

Image generation has an inference cost, either API credits or use of compute resources. Establish a budget strategy based on context:

**Conversational mode** (working with a user):
- Ask the user about their budget tolerance before starting
- Start with 1-2 generations to validate direction
- Show results and gather feedback before generating more
- Offer choices: "Should I generate 4 variations to explore, or refine this single direction?"

**Autonomous mode** (agentic task execution):
- Infer budget from task context and constraints
- For well-defined outputs: start with 1 generation, evaluate, iterate if needed
- For creative exploration: generate 2-4 variations upfront to compare
- Set a reasonable maximum (e.g., 6-10 total generations) unless context suggests otherwise
- Document your generation decisions for transparency

**Budget guidelines by task type:**
| Task Type | Initial | Max Iterations | Notes |
|-----------|---------|----------------|-------|
| Precise diagram/text | 1 | 3-4 | Refine prompt if text/layout wrong |
| Product/marketing image | 2-3 | 5-6 | Variations help find best composition |
| Creative concept art | 3-4 | 8-10 | Exploration is the point |
| Quick illustration | 1 | 2 | Speed over perfection |

### Iterative Refinement

After each generation, evaluate the output and decide the next step:

**Option 1: Accept** — Output meets requirements. Done.

**Option 2: Regenerate with refined prompt** — When the concept or composition needs adjustment:
- Analyze what's wrong: style, composition, missing elements, wrong interpretation
- Adjust the prompt to address specific issues
- Add negative guidance if something unwanted keeps appearing
- Example: First attempt has wrong style → add "photorealistic, not illustrated, not cartoon"

**Option 3: Edit with reference image (-i)** — When the base is good but needs modification:
- Use when composition/layout is correct but details need changes
- Use for removing/adding specific elements
- Use for style adjustments while preserving structure
- Example: `image-gen -i first-output.png "Same composition but change the sky to sunset colors"`

**Decision guide:**
```
Is the overall composition/layout acceptable?
├─ No → Regenerate with refined prompt
└─ Yes → Are specific elements wrong?
         ├─ Yes → Edit with -i flag
         └─ No → Accept or minor prompt refinement
```

### Using Reference Images

The `-i` flag serves multiple purposes beyond editing:

**Style transfer:**
```bash
image-gen -i style-reference.jpg "A portrait of a woman in this artistic style"
```

**Character/subject consistency:**
```bash
# First, generate a character
image-gen "A robot mascot, friendly rounded design, blue and white" -o ./character

# Then use it as reference for new scenes
image-gen -i ./character/gemini-*.png "The same robot mascot waving hello, standing in a garden"
```

**Composition guidance:**
```bash
image-gen -i layout-sketch.png "A detailed illustration following this layout. Fantasy landscape with castle on the left, forest on the right"
```

**Multi-reference combination:**
```bash
image-gen -i style.jpg -i subject.jpg "Combine the artistic style of the first image with the subject matter of the second"
```

### Workflow Example: Logo Design

```
1. Understand requirements (conversational) or extract from context (autonomous)
   - Brand name, industry, style preferences, colors

2. Initial exploration (budget: 3-4 images)
   image-gen -n 4 "Minimalist logo for 'Horizon Analytics', data/analytics company.
   Abstract geometric mark. Professional, modern. Blue and gray palette."

3. Evaluate outputs
   - Which direction resonates? Which elements work?

4. Refine best direction (budget: 2-3 more)
   image-gen -n 2 "Minimalist logo for 'Horizon Analytics'. Abstract rising line
   forming a subtle 'H' shape. Single weight lines. Navy blue on white.
   No gradients, no text, mark only."

5. Polish with editing if needed
   image-gen -i best-logo.png "Same logo but adjust proportions to be more
   horizontally balanced. Maintain exact style."

6. Final variations (optional)
   image-gen -i final-logo.png "Create a reversed version, white mark on navy background"
```

### Tips for Better Results

- **Be specific about style**: "watercolor", "3D render", "flat vector", "photograph" yield very different results
- **Specify viewpoint**: "top-down view", "isometric", "eye-level", "close-up"
- **Include context for text**: These models can render text well—specify font style, size relationship, placement
- **Use negative guidance**: "no text", "no watermark", "not photorealistic" when needed
- **Aspect ratio matters**: Choose based on content (portraits → 2:3 or 9:16, landscapes → 16:9, social → 1:1)

## Prerequisites

The `image-gen` command must be installed. Install with:

```bash
dotnet tool install --global ImageGenCli
```

Ensure at least one of these environment variables is set:
- `GEMINI_API_KEY` - for Gemini provider (default)
- `OPENAI_API_KEY` - for OpenAI provider
- `BFL_API_KEY` - for BFL (FLUX) provider
- `POE_API_KEY` - for Poe provider (access to many models)

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
| `--provider` | `-p` | `gemini` | Provider: `gemini`, `openai`, `bfl`, or `poe` |
| `--model` | `-m` | auto | Model name (use `--list-models` to see options) |
| `--images` | `-i` | none | Reference image paths for editing (can specify multiple) |
| `--aspect-ratio` | `-a` | `1:1` | Output aspect ratio |
| `--resolution` | `-r` | `1K` | Resolution: `1K`, `2K`, `4K` (Gemini Pro, BFL, some Poe) |
| `--quality` | `-q` | none | Quality: `low`, `medium`, `high` (OpenAI, Poe) |
| `--temperature` | `-t` | `1.0` | Generation temperature 0.0-2.0 (Gemini only) |
| `--samples` | `-n` | `1` | Number of images (1-4 Gemini, 1-10 others) |
| `--output` | `-o` | current dir | Output directory for generated images |
| `--api-key` | `-k` | env var | Override API key |
| `--list-models` | `-l` | - | List available models for the provider |

### Aspect Ratios

Supported values: `1:1`, `2:3`, `3:2`, `3:4`, `4:3`, `4:5`, `5:4`, `9:16`, `16:9`, `21:9`

### Provider Models

**Gemini (default):**
- `gemini-2.5-flash-image` (default) - Fast generation
- `gemini-3-pro-image-preview` - Higher quality, supports resolution parameter

**OpenAI:**
- `gpt-image-1.5` (default) - Latest frontier model
- `gpt-image-1` - Previous generation

**BFL (FLUX):**
- `flux-2-pro` (default) - Fast, production-ready
- `flux-2-flex` - Adjustable controls, best typography
- `flux-2-max` - Highest quality, web grounding

**Poe:**
Access to many models via single API. Use `--list-models -p poe` for full list.
Popular: `GPT-Image-1`, `FLUX-2-Pro`, `Imagen-4`, `Seedream-4.0` (case-sensitive)

## Provider Differences

| Feature | Gemini | OpenAI | BFL | Poe |
|---------|--------|--------|-----|-----|
| System prompt | Supported | Error | Error | Error |
| Temperature | Supported (0.0-2.0) | Error | Error | Error |
| Resolution | Pro models only | Error | Supported | Model-dependent |
| Quality | Error | Supported | Error | Supported |
| Max samples | 4 | 10 | 10 | 10 |
| Reference images | Supported | Supported | Up to 8 | Supported |

**Important:** Provider-specific parameters cause errors if used with incompatible providers. Use only supported options.

## Examples

### Basic Generation

```bash
# Generate a simple image with Gemini (default)
image-gen "A sunset over mountains with a lake reflection"

# Generate with OpenAI
image-gen -p openai "A futuristic city skyline at night"

# Generate with BFL FLUX
image-gen -p bfl "A cyberpunk street scene with neon signs"

# Generate with Poe (access to many models)
image-gen -p poe "A watercolor landscape"
image-gen -p poe -m Imagen-4 "A photorealistic portrait"
image-gen -p poe -m Seedream-4.0 "A poster with bold typography saying 'HELLO WORLD'"
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
