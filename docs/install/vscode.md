# VS Code Setup

The plugin auto-configures VS Code automatically when you add it to your project. Works on **Windows, macOS, and Linux**.

## Prerequisites

- VS Code installed (latest version recommended)
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or auto-assigned) free

## Quick Setup

### 1. Install VS Code

Visit https://code.visualstudio.com/download and install VS Code for your OS.

### 2. Add Plugin to Unity

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

The plugin auto-generates your VS Code MCP config on first load.

### 3. Verify Installation (Optional)

Run the diagnostic:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp doctor
```

## Use VS Code With Unity MCP

VS Code's MCP support requires appropriate extensions. Once configured, the plugin generates a `mcp.json` file that VS Code reads automatically.

The MCP tools become available through the MCP context in VS Code.

## Config Location

The plugin writes MCP config to:

- **macOS/Linux:** `~/.config/Code/User/mcp.json`
- **Windows:** `%APPDATA%\Code\User\mcp.json`

## Troubleshooting

| Problem | Fix |
|---------|-----|
| VS Code doesn't find MCP tools | Reload VS Code window: `Ctrl+Shift+P` → "Developer: Reload Window". Check that the plugin console shows `[MCP] Server started on port <XXXX>`. |
| MCP config not auto-generated | Run the diagnostic: `uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp doctor` |
| Tools fail with "Connection refused" | Ensure Unity is open with the plugin loaded. Check that TCP port is available and listening. |
