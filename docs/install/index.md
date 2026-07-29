# Choose an MCP Client

Complete the [prerequisites](../getting-started/index.md#prerequisites) and
[Unity package installation](../getting-started/index.md#1-install-the-unity-package),
choose the matching client setup below, then return to
[first-connection verification](../getting-started/index.md#3-verify-the-first-connection).

| Client | Primary configuration | In-Unity Chat |
|---|---|---|
| [Claude Code](claude-code.md) | Project `.mcp.json` | Claude backend |
| [Claude Desktop](claude-desktop.md) | Wizard clipboard or global CLI | Not used |
| [Codex](codex.md) | Project `.codex/config.toml` | Codex backend |
| [Cursor](cursor.md) | Project `.cursor/mcp.json` | Not used |
| [Windsurf](windsurf.md) | Project `.windsurf/mcp.json` or global CLI | Not used |
| [VS Code](vscode.md) | Project `.vscode/mcp.json` | Not used |
| [Kimi](kimi.md) | Global CLI | Kimi backend |
| [OpenCode](opencode.md) | Global CLI | OpenCode backend |
| [Junie](junie.md) | Project `.junie/mcp/mcp.json` | Not used |
| [Rider AI Assistant](rider.md) | Rider Settings UI | Not used |
| [Gemini](gemini.md) | Deprecated | Not supported |

Project-local configuration stays with one Unity project. For global
configuration, use the canonical
[Python CLI command](../getting-started/index.md#python-cli-global-configuration)
with the client key from the matching guide.

Do not copy configuration between clients: Codex uses TOML, VS Code uses a `servers` object, OpenCode uses its own local-server shape, and the remaining supported JSON clients use `mcpServers`.

## Optional Project Guidance

After configuring Claude Code or Codex, install the bundled
[AI skills and agents](ai-skills.md) when you want project-local MCP workflows
and focused Unity subagents. This installation is separate from the MCP server
configuration above.
