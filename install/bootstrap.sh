#!/usr/bin/env bash
set -euo pipefail

_color_ok() { [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; }

ok()   { _color_ok && printf "  \033[32m✓\033[0m  %s\n" "$1" || printf "  [OK]  %s\n" "$1"; }
fail() { _color_ok && printf "  \033[31m✗\033[0m  %s\n" "$1" || printf "  [FAIL]  %s\n" "$1"; }
info() { _color_ok && printf "  \033[33m○\033[0m  %s\n" "$1" || printf "  [-]  %s\n" "$1"; }

# 1. Check/install uv
if command -v uv &>/dev/null; then
    ok "uv $(uv --version 2>/dev/null || echo '') found"
else
    info "Installing uv..."
    # curl|sh is the official uv install method (same as rustup, homebrew).
    # Verify: sha256sum ~/.local/bin/uv after install, compare with astral.sh release checksums.
    curl -LsSf https://astral.sh/uv/install.sh | sh
    export PATH="$HOME/.local/bin:$PATH"
    ok "uv installed"
fi

# 2. Clone/update repo
INSTALL_DIR="${UNITY_MCP_DIR:-$HOME/.unity-biome-mcp/server}"
if [ -d "$INSTALL_DIR/.git" ]; then
    info "Updating existing installation..."
    git -C "$INSTALL_DIR" pull --ff-only
    ok "Updated to $(git -C "$INSTALL_DIR" describe --tags 2>/dev/null || echo 'latest')"
else
    info "Cloning unity-biome-mcp..."
    mkdir -p "$(dirname "$INSTALL_DIR")"
    git clone https://github.com/german-krasnikov/unity-biome-mcp.git "$INSTALL_DIR"
    ok "Cloned"
fi

# 3. macOS: remove Gatekeeper quarantine on venv dylibs
if [ "$(uname)" = "Darwin" ]; then
    info "Removing quarantine attributes (macOS)..."
    xattr -dr com.apple.quarantine "$INSTALL_DIR" 2>/dev/null || true
    ok "Quarantine cleared"
fi

# 4. Run install.py
cd "$INSTALL_DIR"
uv run python install.py setup
ok "Installation complete"

# 5. Next steps
printf "\n"
printf "  Next steps:\n"
printf "    1. Open Unity → Package Manager → Add git URL:\n"
printf "       https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin\n"
printf "    2. The Setup Wizard will guide you through the rest.\n"
printf "\n"
