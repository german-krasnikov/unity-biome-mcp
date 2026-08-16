# Claude Desktop

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install

Install the current Claude Desktop release from [Claude downloads](https://claude.com/download).

## Configure the Global Client

Claude Desktop does not use one of the generated project-local files. Choose one of these flows:

### Setup Wizard clipboard

1. Open **MCP > Setup Wizard**.
2. Select **Claude Desktop**.
3. Select **Configure**.
4. Follow the Wizard result. A local source checkout can update the configuration
   directly; a UPM installation copies a complete JSON object.
5. If JSON was copied, open the platform configuration file below and merge the
   `unity-biome-mcp` entry into its existing `mcpServers` object. Preserve other
   servers and top-level settings instead of replacing the file.

### Python CLI

Use the [global configuration command](../getting-started/index.md#python-cli-global-configuration)
with client key `claude-desktop`. The packaged CLI writes the platform path
listed below.

### Configuration files

| Platform | Configuration file |
|---|---|
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |
| Linux | `~/.config/Claude/claude_desktop_config.json` |

Quit Claude Desktop completely and reopen it after configuration, then run the [first connection check](../getting-started/index.md#3-verify-the-first-connection).

Claude Desktop is an external MCP client. The **Claude** option in MCP Chat uses Claude Code CLI instead.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Tools do not appear | Quit Claude Desktop completely, reopen it, and verify the global config contains `unity-biome-mcp` |
| Configuration was edited incorrectly | Restore the `.bak` file created by the Python CLI, then configure again |
| Tools cannot reach Unity | Keep the Unity project open and follow [connection diagnostics](../getting-started/index.md#diagnose-a-failure) |
