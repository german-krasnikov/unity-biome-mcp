# Windsurf

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install

Install and sign in from the
[official Windsurf download page](https://windsurf.com/).

## Configure

For legacy Cascade, use Windsurf's documented global configuration. The
packaged CLI writes the `mcpServers` entry without replacing unrelated servers:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server \
  unity-biome-mcp configure --tool windsurf
```

The path is `~/.codeium/windsurf/mcp_config.json` on macOS and Linux, and
`%APPDATA%\Codeium\windsurf\mcp_config.json` on Windows. Restart Windsurf, open
**Windsurf Settings > Cascade > MCP Servers**, enable the server if necessary,
then run the
[first connection check](../getting-started/index.md#3-verify-the-first-connection).
Windsurf's
[MCP documentation](https://docs.windsurf.com/windsurf/cascade/mcp)
describes server status, tool visibility, and the global configuration path.

Unity Biome MCP may also generate `.windsurf/mcp.json` in the Unity project.
Treat it only as a compatibility artifact, not as authoritative discovery for
legacy Cascade. Devin Local and newer Windsurf tabs are separate surfaces; this
integration does not claim that the project artifact configures them. Use the
configuration UI and documentation for the surface you are running.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Server is absent from legacy Cascade | Run the global configuration command above, then restart Windsurf |
| Server is configured but inactive | Open the client's MCP settings, enable the server, and restart the client |
| Tools cannot reach Unity | Keep Unity open and follow [connection diagnostics](../getting-started/index.md#diagnose-a-failure) |
