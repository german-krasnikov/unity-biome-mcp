---
hide:
  - navigation
---

# Getting Started

This guide owns the common package installation and verification flow. Install
the Unity package, complete the matching [client guide](../install/index.md),
then return here for the appropriate first-connection check.

## Prerequisites

- Unity 6000.0 or later
- `uvx`, provided by [uv](https://docs.astral.sh/uv/getting-started/installation/)
- An MCP-compatible client, or a supported CLI for [MCP Chat](../chat/index.md)

The server package requires Python 3.10 or later, but `uvx` downloads and
manages it for the standard package flow. Install Python separately only for a
local source checkout or manual Python execution.

The plugin starts on port `9500` by default and automatically selects another available port when necessary. You do not need to reserve port `9500`.

## 1. Install the Unity Package

1. Open **Window > Package Manager**.
2. Select **+ > Add package from git URL**.
3. Enter:

   ```text
   https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin
   ```

4. Wait for Unity to import the package and finish compiling.

The package starts the Unity-side server and maintains these project-local client files:

| Client | File relative to the Unity project |
|---|---|
| Claude Code | `.mcp.json` |
| Cursor | `.cursor/mcp.json` |
| VS Code | `.vscode/mcp.json` |
| Windsurf | `.windsurf/mcp.json` |
| Codex | `.codex/config.toml` |
| Junie | `.junie/mcp/mcp.json` |

Each generated entry is pinned to the installed package version. Unity refreshes
an entry only when its generated ownership marker is present; an unmarked or
hand-edited Unity Biome MCP entry is preserved and reported in the Console.

The JSON clients discover the active Unity port at server startup and do not
store `UNITY_MCP_PORT`. Codex TOML stores the current port explicitly. After
changing the MCP port, restart the MCP server; for Codex, also reopen the Unity
project so `.codex/config.toml` is regenerated, then restart Codex.

## 2. Choose a Configuration Flow

Open **MCP > Setup Wizard** to choose a backend and optionally install the
bundled [AI skills and agents](../install/ai-skills.md). The Wizard configures
clients; it does not test the connection.

Use the flow that matches your client:

### Project-local configuration

Claude Code, Cursor, VS Code, Windsurf, Codex, and Junie use the generated files
listed above. Restart or reload the client after Unity creates the file.

### Chat-start configuration

The in-Unity Chat relay creates temporary backend-specific MCP configuration when it starts a supported CLI. This is separate from configuring that CLI as an external MCP client. See [Using MCP Chat](../chat/index.md).

### Manual clipboard configuration

For a UPM installation, the Wizard can copy standard `mcpServers` JSON for clients without a generated project file, such as Claude Desktop. Paste it into that client's MCP configuration and restart the client.

Some clients use a different root key or file format. Do not paste the standard JSON into VS Code, Codex, or OpenCode; follow the matching [client guide](../install/index.md).

For a backend configured through its own settings UI, the Wizard shows the required location and copies the server command.

### Python CLI global configuration

Use the packaged CLI when you prefer a user-level configuration or when the client does not discover the project-local file:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server \
  unity-biome-mcp configure --tool <client-key>
```

Valid keys include `claude-desktop`, `claude-code`, `cursor`, `windsurf`, `vscode`, `codex`, `kimi`, `junie`, and `opencode`. The CLI writes the client-specific global path and format, preserving other configured servers.

## 3. Verify the First Connection

### External MCP client

1. Keep the Unity project open and compile-clean.
2. Restart or reload the external MCP client after configuration.
3. Ask the client:

   ```text
   Call get_hierarchy for the active Unity scene.
   ```

A successful response contains the active scene's GameObject hierarchy. This first tool call is the connection check; the Setup Wizard does not perform one.

### In-Unity Chat

1. Install and sign in to a supported CLI.
2. Open **MCP > Chat** and select that backend.
3. Send:

   ```text
   Read the active Unity scene hierarchy and summarize its root objects.
   ```

A successful turn shows a hierarchy tool call and a summary in the transcript.
The Chat relay configures its own temporary MCP connection; it does not require
an external client configuration. Continue with [Using MCP Chat](../chat/index.md).

## Diagnose a Failure

Start in Unity:

1. Open **MCP > Status**.
2. Select **Diagnose**.
3. Review the Python, server, compile, and `uv` checks.

`LISTENING` means the Unity server is running without an external MCP client. `ONLINE` means a client is connected.

For a deeper terminal check, run:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server \
  unity-biome-mcp doctor
```

The CLI checks Python, port and lock files, TCP connectivity, and Unity state. To remove stale port and lock files, rerun it with `--fix`.

### Tools are missing

1. Confirm that the client loaded the expected project or global config.
2. Restart the client completely.
3. Open **MCP > Settings > Tools** and confirm the required tools are enabled.
4. Reconnect the MCP client after changing tool visibility.

### Connection is refused

1. Keep Unity open on the intended project.
2. Wait for compilation to finish.
3. Run **MCP > Status > Diagnose**.
4. Run the terminal `doctor` command above.

### A custom port does not apply

Port changes are saved immediately but require an MCP server restart. See [Settings](../settings.md#ports).

## Maintenance

- Use **MCP > Settings > Updates** to check for and install a newer release.
- For UPM installs, use **MCP > Settings > Version Picker** to roll back or realign the plugin and generated server pin.
- To remove a global client entry, run the packaged CLI with `uninstall --tool <client-key>`.
- To remove the Unity package, use Package Manager.

## Next Steps

- [Run a PlayTest workflow](../features/playtest.md)
- [Configure Settings](../settings.md)
- [Use MCP Chat](../chat/index.md)
- [Choose the right tool](../features/tool-guide.md)
- [Browse the tool reference](../tools/index.md)
