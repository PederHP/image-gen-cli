# Install image-gen CLI tool
# Requires: .NET 8.0 SDK

$ErrorActionPreference = "Stop"

Write-Host "Installing image-gen CLI..."

# Check for dotnet
try {
    $dotnetVersion = dotnet --version
} catch {
    Write-Error "Error: .NET SDK not found. Install from https://dot.net/download"
    exit 1
}

# Check .NET version
$majorVersion = [int]($dotnetVersion -split '\.')[0]
if ($majorVersion -lt 8) {
    Write-Error "Error: .NET 8.0 or later required (found: $dotnetVersion)"
    exit 1
}

# Install as global tool
# If installing from local source:
if (Test-Path "src/ImageGenCli.csproj") {
    Write-Host "Installing from local source..."
    dotnet pack src/ImageGenCli.csproj -c Release -o ./nupkg
    dotnet tool install --global --add-source ./nupkg ImageGenCli
}
# If installing from NuGet (when published):
else {
    dotnet tool install --global ImageGenCli
}

Write-Host ""
Write-Host "Installation complete. Run 'image-gen --help' to get started."
Write-Host ""
Write-Host "Set at least one API key:"
Write-Host "  `$env:GEMINI_API_KEY = 'your-key'    # for Gemini (default)"
Write-Host "  `$env:OPENAI_API_KEY = 'your-key'    # for OpenAI"
Write-Host "  `$env:BFL_API_KEY = 'your-key'       # for BFL (FLUX)"
Write-Host "  `$env:POE_API_KEY = 'your-key'       # for Poe"
