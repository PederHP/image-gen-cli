# Image Generation Models Reference

This reference provides detailed information about available models across all providers. Use `image-gen --list-models -p <provider>` for a quick summary.

## Gemini (Google)

| Model | Default | Description |
|-------|---------|-------------|
| `gemini-2.5-flash-image` | Yes | Fast generation with good quality. Suitable for most use cases. |
| `gemini-3-pro-image-preview` | No | Higher quality output. Supports `--resolution` parameter for 2K/4K output. |

**Gemini-specific features:**
- `--system-prompt`: Supported (guide model behavior)
- `--temperature`: Supported (0.0-2.0)
- `--resolution`: Pro model only (1K, 2K, 4K)
- Max samples: 4

## OpenAI

| Model | Default | Description |
|-------|---------|-------------|
| `gpt-image-1.5` | Yes | Latest model with exceptional prompt adherence, world knowledge, and quality. |
| `gpt-image-1` | No | Previous generation model. Still very capable. |

**OpenAI-specific features:**
- `--quality`: Supported (low, medium, high)
- Fixed resolution based on aspect ratio
- Max samples: 10
- Reference images use the edits endpoint

## BFL (Black Forest Labs FLUX)

| Model | Default | Speed | Price | Description |
|-------|---------|-------|-------|-------------|
| `flux-2-pro` | Yes | ~10s | $0.03/MP | Production-ready. Fast and cost-effective. |
| `flux-2-flex` | No | Higher | $0.06/MP | Adjustable controls (steps, guidance). Best typography. |
| `flux-2-max` | No | ~15s | $0.07/MP | Highest quality. Supports grounding search for real-time web info. |

**BFL-specific features:**
- `--resolution`: Supported (1K, 2K, 4K)
- Up to 8 reference images
- Max samples: 10
- Async API with polling

## Poe

Poe provides unified access to many image models through a single API and subscription. Model names are **case-sensitive**.

### OpenAI Models (via Poe)

| Model | Description |
|-------|-------------|
| `GPT-Image-1` | (default) OpenAI's ChatGPT image model |
| `GPT-Image-1.5` | OpenAI's latest frontier image model |
| `GPT-Image-1-Mini` | Lighter, faster variant |

### FLUX Models (via Poe)

| Model | Description |
|-------|-------------|
| `FLUX-2-Pro` | Black Forest Labs FLUX.2 Pro |
| `FLUX-2-Flex` | FLUX.2 Flex with adjustable controls |
| `FLUX-2-Dev` | Open-weight model (3.2B parameters) |
| `Flux-Kontext-Pro` | FLUX.1 Kontext Pro - state-of-the-art editing |
| `Flux-Kontext-Max` | FLUX.1 Kontext Max - maximum quality |
| `FLUX-Krea` | FLUX Dev tuned for superior aesthetics |

### Google DeepMind Models (via Poe)

| Model | Description |
|-------|-------------|
| `Imagen-4` | May 2025 model with exceptional prompt adherence |
| `Imagen-4-Fast` | Faster variant of Imagen 4 |
| `Imagen-4-Ultra` | Highest quality Imagen 4 variant |
| `Nano-Banana` | Gemini 2.5 Flash Image model |
| `Nano-Banana-Pro` | Gemini 3 Pro Image Preview |

### Other Models (via Poe)

| Model | Description |
|-------|-------------|
| `Seedream-4.0` | ByteDance's latest model. High fidelity, excellent text rendering. |
| `Qwen-Image` | Alibaba's model. Strong text rendering and complex compositions. |

**Poe-specific notes:**
- `--quality`: Supported (low, medium, high) - passed to underlying models
- `--resolution`: Model-dependent - passed through, API returns error if unsupported
- Model availability may change. Check poe.com for current list.
- All models support aspect ratio and reference images
- Max samples: 10

## Model Selection Guide

### For speed and cost efficiency:
- `gemini-2.5-flash-image` (Gemini)
- `flux-2-pro` (BFL)
- `Imagen-4-Fast` (Poe)

### For highest quality:
- `gemini-3-pro-image-preview` with `--resolution 4K` (Gemini)
- `flux-2-max` (BFL)
- `GPT-Image-1.5` (OpenAI/Poe)
- `Imagen-4-Ultra` (Poe)

### For text rendering:
- `flux-2-flex` (BFL)
- `Seedream-4.0` (Poe)
- `Qwen-Image` (Poe)

### For image editing:
- `Flux-Kontext-Pro` or `Flux-Kontext-Max` (Poe)
- Any model with reference images

### For experimenting with many models:
- Use Poe provider - single subscription, many models
