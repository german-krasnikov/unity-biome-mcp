#Requires -Version 5.1
# Run with: iex (iwr https://raw.githubusercontent.com/german-krasnikov/unity-biome-mcp/master/install/bootstrap.ps1).Content
# If execution policy blocks this: Set-ExecutionPolicy Bypass -Scope CurrentUser
#
# If your antivirus blocks this script, run these commands manually:
#   winget install astral-sh.uv
#   git clone https://github.com/german-krasnikov/unity-biome-mcp.git "$HOME\.unity-biome-mcp\server"
#   cd "$HOME\.unity-biome-mcp\server" && uv run python install.py setup
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ErrorActionPreference = "Stop"

function ok($msg)   { Write-Host "  [OK]  $msg" -ForegroundColor Green }
function fail($msg) { Write-Host "  [FAIL]  $msg" -ForegroundColor Red }
function info($msg) { Write-Host "  [-]  $msg" -ForegroundColor Yellow }

# 1. Check/install uv
if (Get-Command uv -ErrorAction SilentlyContinue) {
    ok "uv found"
} else {
    info "Installing uv..."
    try {
        winget install astral-sh.uv --accept-package-agreements --accept-source-agreements 2>&1 | Out-Null
        # Refresh PATH after winget
        $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                    [Environment]::GetEnvironmentVariable("Path", "User")
        ok "uv installed"
    } catch {
        # Fallback: PowerShell installer from astral.sh/uv/install.ps1
        $uvScript = Invoke-RestMethod 'https://astral.sh/uv/install.ps1'
        & ([scriptblock]::Create($uvScript))
        # Refresh PATH after script-based install
        $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                    [Environment]::GetEnvironmentVariable("Path", "User")
        ok "uv installed (via script)"
    }
}

# 2. Clone/update repo
$installDir = if ($env:UNITY_MCP_DIR) { $env:UNITY_MCP_DIR } else { "$HOME\.unity-biome-mcp\server" }

# Windows: enable long paths (>260 chars) before clone
git config --global core.longpaths true

if (Test-Path "$installDir\.git") {
    info "Updating existing installation..."
    git -C "$installDir" pull --ff-only
    ok "Updated"
} else {
    info "Cloning unity-biome-mcp..."
    $parent = Split-Path "$installDir" -Parent
    if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    git clone https://github.com/german-krasnikov/unity-biome-mcp.git "$installDir"
    ok "Cloned"
}

# 3. Run install.py
Push-Location $installDir
try {
    uv run python install.py setup
    ok "Installation complete"
} finally {
    Pop-Location
}

# 4. Next steps
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor Cyan
Write-Host "    1. Open Unity -> Package Manager -> Add git URL:"
Write-Host "       https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin"
Write-Host "    2. The Setup Wizard will guide you through the rest."
Write-Host ""
