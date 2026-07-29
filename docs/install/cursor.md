# Cursor

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install

Install and sign in to Cursor from the [official download page](https://cursor.com/downloads).

## Configure

Unity Biome MCP creates `.cursor/mcp.json` in the Unity project. Open that project in Cursor, restart or reload Cursor after the file is created, and run the [first connection check](../getting-started/index.md#3-verify-the-first-connection).

For user-level configuration instead, use the [global configuration command](../getting-started/index.md#python-cli-global-configuration) with client key `cursor`. It writes `~/.cursor/mcp.json`.

Both files use Cursor's `mcpServers` JSON shape. Project configuration takes effect only for the project that contains it.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Cursor does not list the server | Confirm the Unity project is the open workspace and `.cursor/mcp.json` exists |
| A global entry works but the project entry does not | Reload the Cursor window and inspect the project's MCP settings |
| Tools cannot reach Unity | Keep Unity open and follow [connection diagnostics](../getting-started/index.md#diagnose-a-failure) |
