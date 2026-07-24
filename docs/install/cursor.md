# Cursor Setup

The plugin auto-configures Cursor automatically when you add it to your project. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Cursor IDE installed and authenticated
- Unity 6000.0+ with the `unity-biome-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or auto-assigned) free

## Quick Setup

### 1. Install Cursor

Visit https://cursor.com/download and install Cursor for your OS.

### 2. Add Plugin to Unity

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

The plugin auto-generates your Cursor MCP config on first load.

### 3. Verify Installation (Optional)

Run the diagnostic:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp doctor
```

## Use Cursor With Unity Biome MCP

Once configured, use Unity Biome MCP from your Cursor terminal or chat:

```python
await get_hierarchy()
```

The MCP tools are automatically available in Cursor's MCP context.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Cursor doesn't find MCP tools | Restart Cursor completely. Check that the plugin console shows `[MCP] Server started on port <XXXX>`. |
| MCP config not auto-generated | Run the diagnostic: `uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp doctor` |
| Tools fail with "Connection refused" | Ensure Unity is open with the plugin loaded. Check that TCP port is available and listening. |
