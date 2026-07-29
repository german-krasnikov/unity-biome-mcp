# Gemini (Deprecated)

Gemini CLI integration is deprecated and is no longer maintained by Unity Biome MCP. The Setup Wizard and MCP Chat do not offer a Gemini backend.

Choose a supported client from the [client guide index](index.md). Claude Code and Codex support both external MCP use and the in-Unity Chat workflow.

## Legacy Manual Configuration

For an existing Gemini installation, complete the [common Unity package setup](../getting-started/index.md#1-install-the-unity-package), then add a standard `mcpServers` entry to the client manually:

```json
{
  "mcpServers": {
    "unity-biome-mcp": {
      "command": "uvx",
      "args": [
        "--from",
        "git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server",
        "unity-biome-mcp"
      ]
    }
  }
}
```

This path is unsupported and is not covered by current regression tests. Prefer a supported client for new projects.
