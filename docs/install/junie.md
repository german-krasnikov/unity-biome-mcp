# Junie Setup

The plugin auto-configures Junie automatically when you add it to your project. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Junie installed (via JetBrains IDE plugin)
- Unity 6000.0+ with the `unity-biome-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or auto-assigned) free

## Quick Setup

### 1. Install Junie

Install the Junie plugin from within your JetBrains IDE:

1. Open **Settings/Preferences > Plugins**
2. Search for **Junie** and install it
3. Restart the IDE

### 2. Add Plugin to Unity

1. Open **Window > Package Manager**
2. Click **+ > Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

The plugin auto-generates your Junie MCP config on first load.

### 3. Verify Installation (Optional)

Run the diagnostic:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp doctor
```

## Config Location

The plugin writes MCP config to:

- **All platforms:** `~/.junie/mcp/mcp.json`

Config format (standard `mcpServers` JSON):

```json
{
  "mcpServers": {
    "unity-biome-mcp": {
      "command": "uvx",
      "args": ["--from", "git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server", "unity-biome-mcp"]
    }
  }
}
```

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Junie plugin not found | Ensure you are using a compatible JetBrains IDE. Check **Settings/Preferences > Plugins** for Junie. |
| Setup Wizard doesn't run in Unity | (1) Check plugin is in Package Manager. (2) Close/reopen Unity. (3) Check Console for errors. |
| MCP server fails to start | Run Setup Wizard > Diagnostics to verify Python 3.10+ and TCP port availability. |
| MCP config not auto-generated | Run the diagnostic: `uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp doctor` |
| Tools fail with "Connection refused" | Ensure Unity is open with the plugin loaded. Check that TCP port is available and listening. |
