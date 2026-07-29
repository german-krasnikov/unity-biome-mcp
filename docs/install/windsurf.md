# Windsurf

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install

Install and sign in from the
[official Windsurf download page](https://windsurf.com/).

## Configure

Unity Biome MCP creates `.windsurf/mcp.json` in the Unity project. Reload the
project in Windsurf, then run the
[first connection check](../getting-started/index.md#3-verify-the-first-connection).

If Windsurf does not discover the project file, use the
[global configuration command](../getting-started/index.md#python-cli-global-configuration)
with client key `windsurf`. The packaged CLI writes the platform-specific
`mcp_config.json` using the `mcpServers` shape.

Restart the client after changing either configuration.

Windsurf also exposes MCP controls under
**Windsurf Settings > Cascade > MCP Servers**. Its
[MCP documentation](https://docs.windsurf.com/windsurf/cascade/mcp)
describes server status, tool visibility, and the global configuration path.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Project-local server is not discovered | Use the global Python CLI flow above |
| Server is configured but inactive | Open the client's MCP settings, enable the server, and restart the client |
| Tools cannot reach Unity | Keep Unity open and follow [connection diagnostics](../getting-started/index.md#diagnose-a-failure) |
