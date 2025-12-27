#!/bin/bash
set -e

# Install image-gen CLI tool
# Requires: .NET 8.0 SDK

echo "Installing image-gen CLI..."

# Check for dotnet
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK not found. Install from https://dot.net/download" >&2
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version | cut -d. -f1)
if [ "$DOTNET_VERSION" -lt 8 ]; then
    echo "Error: .NET 8.0 or later required (found: $(dotnet --version))" >&2
    exit 1
fi

# Install as global tool
# If installing from local source:
if [ -f "src/ImageGenCli.csproj" ]; then
    echo "Installing from local source..."
    dotnet pack src/ImageGenCli.csproj -c Release -o ./nupkg
    dotnet tool install --global --add-source ./nupkg ImageGenCli
# If installing from NuGet (when published):
else
    dotnet tool install --global ImageGenCli
fi

echo "Installation complete. Run 'image-gen --help' to get started."
echo ""
echo "Set at least one API key:"
echo "  export GEMINI_API_KEY=your-key    # for Gemini (default)"
echo "  export OPENAI_API_KEY=your-key    # for OpenAI"
echo "  export BFL_API_KEY=your-key       # for BFL (FLUX)"
echo "  export POE_API_KEY=your-key       # for Poe"
