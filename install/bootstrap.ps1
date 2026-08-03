$ErrorActionPreference = "Stop"

function Write-Info([string] $Message) {
    Write-Host "  [-]  $Message" -ForegroundColor Yellow
}

function Write-Ok([string] $Message) {
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

# Run with: powershell -ExecutionPolicy Bypass -File .\bootstrap.ps1
if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
    Write-Info "Installing uv..."
    Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression
    $env:Path = "$HOME\.local\bin;$env:USERPROFILE\.local\bin;$env:Path"
}
Write-Ok "uv found"

$installDir = if ($env:UNITY_MCP_DIR) {
    $env:UNITY_MCP_DIR
} else {
    Join-Path $HOME ".unity-biome-mcp\server"
}

if (Test-Path (Join-Path $installDir ".git")) {
    Write-Info "Updating existing installation..."
    git -C "$installDir" -c core.longpaths=true pull --ff-only
    Write-Ok "Updated"
} else {
    Write-Info "Cloning unity-biome-mcp..."
    $parent = Split-Path -Parent $installDir
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    git clone -c core.longpaths=true `
        https://github.com/german-krasnikov/unity-biome-mcp.git "$installDir"
    Write-Ok "Cloned"
}

Push-Location "$installDir"
try {
    uv run python install.py setup
} finally {
    Pop-Location
}

Write-Ok "Installation complete"
Write-Host "Add the Unity package from:"
Write-Host "https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin"
