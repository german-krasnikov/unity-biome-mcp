# Unity Biome MCP

Unity Editor package for structured scene editing, asset workflows, Play Mode
verification, screenshots, diagnostics, and In-Unity Chat through
MCP-compatible AI clients.

## Requirements

- Unity `6000.0` or newer
- [uv](https://docs.astral.sh/uv/) for the standard server launch
- An MCP-compatible client or a supported CLI for In-Unity Chat

## Install

1. Open **Window > Package Manager**.
2. Select **+ > Add package from git URL**.
3. Enter:

   ```text
   https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin
   ```

4. Wait for Unity to finish importing and compiling the package.
5. Open **MCP > Setup Wizard** and configure the intended client.

The package starts the Unity-side server automatically. The default MCP port is
`9500`; another available port is selected when necessary.

## Verify

1. Keep the Unity project open and compile-clean.
2. Restart the configured external client.
3. Ask it to call `get_hierarchy` for the active scene.

A successful response contains the active scene hierarchy. If the connection
fails, open **MCP > Status > Diagnose**.

## Optional AI Skills

Open **MCP > Install AI Skills** to install the bundled project-local guidance
for Claude Code and Codex. The package contains 12 domain skills, 4 focused
agents, and a Claude-to-Codex conversion script.

The installer checks ownership and conflicts before replacement, preserves
modified legacy files for review, and writes its version marker only after the
requested installation and Codex sync succeed. Read the
[AI Skills and Agents guide](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/docs/install/ai-skills.md)
before overwriting existing project guidance.

## Main Editor Surfaces

- **MCP > Chat** - work with a supported CLI inside Unity
- **MCP > Status** - inspect connection state and run diagnostics
- **MCP > Settings** - configure ports, security, tools, Chat, and updates
- **MCP > Setup Wizard** - configure supported clients
- **MCP > Install AI Skills** - install or update project-local guidance
- **MCP > Playtest Composer** - author PlayTest DSL workflows

## Documentation

- [Getting Started](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/docs/getting-started/index.md)
- [Client Guides](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/docs/install/index.md)
- [Tool Guide](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/docs/features/tool-guide.md)
- [Diagnostics](https://github.com/german-krasnikov/unity-biome-mcp/blob/master/docs/tools/diagnostics.md)

## Troubleshooting

| Symptom | Action |
|---|---|
| Unity reports compilation errors | Resolve them before testing the MCP connection |
| Status remains `LISTENING` | Restart the external client and confirm it loaded this project's configuration |
| The default port is occupied | Let the package select another port, then restart the client |
| Skills installation reports a conflict | Compare the existing project file with the packaged file; do not delete ownership metadata to bypass the check |

The TCP bridge uses length-prefixed UTF-8 JSON over localhost. Protocol and
extension details are documented in the repository's
[developer guides](https://github.com/german-krasnikov/unity-biome-mcp/tree/master/docs/plugins).
