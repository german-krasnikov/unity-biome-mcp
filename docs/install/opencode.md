# OpenCode Setup

The plugin auto-configures OpenCode automatically when you add it to your project. Works on **Windows, macOS, and Linux**.

## Prerequisites

- OpenCode CLI installed and authenticated
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or auto-assigned) free

## Quick Setup

### 1. Install OpenCode CLI

**macOS/Linux:**
```bash
go install github.com/opencode-ai/opencode@latest
opencode --version
```

**Or from GitHub Releases:**

Visit https://github.com/opencode-ai/opencode/releases and download the binary for your OS.

**Windows:**

Download from https://github.com/opencode-ai/opencode/releases and add to PATH.

### 2. Add Plugin to Unity

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

Authenticate OpenCode:

```bash
opencode login
```

The plugin auto-generates your OpenCode MCP config on first load (temporary config, doesn't affect your global `~/.opencode/config.json`).

### 3. Verify Installation (Optional)

Run the diagnostic:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp doctor
```

## Use OpenCode From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **MCP → Chat**.
3. Select **OpenCode** from the backend dropdown.
4. Optionally select a model from the dropdown.
5. Type a prompt and press Send.

The plugin generates a temporary MCP config, injects it via `OPENCODE_CONFIG`, and spawns `opencode run`.

## 4. How the Plugin Wires MCP

Before each turn, the plugin writes a temporary MCP config to the system temp directory:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "<python>",
      "args": ["-m", "unity_mcp.server"],
      "env": { "UNITY_MCP_PORT": "<port>" }
    }
  }
}
```

Then spawns OpenCode with this config injected:

```bash
OPENCODE_CONFIG=/tmp/opencode-unity-mcp-<port>.json opencode run --format json <prompt>
```

The config is per-port and automatically cleaned up. OpenCode reads the config without affecting your global `~/.opencode/config.json`.

## 5. Verify Installation

Open the **Setup Wizard** in Unity (**MCP → Setup Wizard**) and select **Diagnostics**. It will check Python version, MCP server responsiveness, and TCP connectivity.

Alternatively, check manually:

```bash
opencode --version
cat ~/.opencode/config.json  # should contain unity-mcp entry (if manually configured)

# Test (with Unity running)
opencode run "Call the mcp tool get_hierarchy and return the result"
```

## 6. Troubleshooting

| Problem | Fix |
|---------|-----|
| `opencode: command not found` | Ensure `go install` completed successfully. Check `which opencode` or `where.exe opencode`. Restart terminal if needed. |
| Setup Wizard doesn't open in Unity | (1) Verify plugin is in Package Manager. (2) Close and reopen Unity. (3) Check Console for errors. |
| MCP tools don't appear in Chat | Verify OpenCode is authenticated (`opencode login status`). Check that Unity Console shows `[MCP] Server started on port <XXXX>`. Restart Chat session. |
| MCP server fails to start | Run Setup Wizard → Diagnostics to verify Python 3.10+ is available and TCP port 9500 is free. |
| `OPENCODE_CONFIG` not found error | The plugin should auto-write this file. Run Setup Wizard → Diagnostics. If issue persists, check `/tmp` (or `%TEMP%` on Windows) for `opencode-unity-mcp-*.json`. |
| Binary not found in Chat Settings but works in terminal | Terminal sources `~/.zshrc` but Unity doesn't. Override manually: **Settings > Agent Chat > OpenCode Binary Path** — enter absolute path. |
| Tools fail with "Connection refused" | (1) Ensure Unity is open with the plugin loaded. (2) Run Setup Wizard → Diagnostics to check TCP port. (3) Restart Unity. |
