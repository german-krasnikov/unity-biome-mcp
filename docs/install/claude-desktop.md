# Claude Desktop Setup

The plugin auto-configures Claude Desktop automatically when you add it to your project. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Claude Desktop app installed
- Unity 6000.0+ with the `unity-biome-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or auto-assigned) free

## Quick Setup

### 1. Install Claude Desktop

Visit https://claude.com/download and install Claude Desktop for your OS.

### 2. Add Plugin to Unity

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

The plugin auto-generates your Claude Desktop MCP config on first load.

### 3. Verify Installation (Optional)

Run the diagnostic:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp doctor
```

Or from Claude Desktop, try:

```python
await get_hierarchy()
```

## Use Claude Desktop With Unity Biome MCP

Once configured, restart Claude Desktop completely, then use Unity Biome MCP from the chat:

```python
await create_object("MyObject")
await get_hierarchy()
await batch("""
create_object name=Enemy
set_property path=Enemy component=Transform prop=position value=0,1,0
""")
```

## Config Location

The plugin writes MCP config to:

- **macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
- **Linux:** `~/.config/Claude/claude_desktop_config.json`

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Claude Desktop doesn't see MCP tools | **Restart Claude Desktop completely** (close via menu bar, reopen). Check that the plugin console shows `[MCP] Server started on port <XXXX>`. |
| MCP config not auto-generated | Run the diagnostic: `uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp doctor` |
| Tools fail with "Connection refused" | Ensure Unity is open with the plugin loaded. Check that TCP port is available and listening. |
| "Unknown server: unity-biome-mcp" error | Claude Desktop cached the old config. Clear config and restart: `rm ~/Library/Application\ Support/Claude/claude_desktop_config.json` (macOS), then restart Claude and re-add plugin to trigger auto-config. |
