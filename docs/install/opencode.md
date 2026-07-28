# OpenCode Setup

The plugin auto-configures OpenCode automatically when you add it to your project. Works on **Windows, macOS, and Linux**.

## Prerequisites

- OpenCode CLI installed and authenticated
- Unity 6000.0+ with the `unity-biome-mcp` plugin installed (via UPM git URL)
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

### 2. Add Plugin to Unity

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

Authenticate OpenCode:

```bash
opencode login
```

The plugin auto-generates your OpenCode MCP config on first load as a project-local config file.

### 3. Verify Installation (Optional)

Run the diagnostic:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp doctor
```

## Use OpenCode From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **MCP → Chat**.
3. Select **OpenCode** from the backend dropdown.
4. Optionally select a model from the dropdown.
5. Type a prompt and press Send.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `opencode: command not found` | Ensure `go install` completed successfully. Check `which opencode` or `where.exe opencode`. Restart terminal if needed. |
| Setup Wizard doesn't open in Unity | (1) Verify plugin is in Package Manager. (2) Close and reopen Unity. (3) Check Console for errors. |
| MCP tools don't appear in Chat | Verify OpenCode is authenticated (`opencode login status`). Check that Unity Console shows `[MCP] Server started on port <XXXX>`. Restart Chat session. |
| MCP server fails to start | Run Setup Wizard → Diagnostics to verify Python 3.10+ is available and TCP port 9500 is free. |
| `OPENCODE_CONFIG` not found error | The plugin should auto-write this file. Run Setup Wizard → Diagnostics. If issue persists, check `/tmp` (or `%TEMP%` on Windows) for `opencode-unity-biome-mcp-*.json`. |
| Binary not found in Chat Settings but works in terminal | Terminal sources `~/.zshrc` but Unity doesn't. Override manually: **Settings > Agent Chat > OpenCode Binary Path** — enter absolute path. |
| Tools fail with "Connection refused" | (1) Ensure Unity is open with the plugin loaded. (2) Run Setup Wizard → Diagnostics to check TCP port. (3) Restart Unity. |
