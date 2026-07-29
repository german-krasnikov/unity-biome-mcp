# Claude Code

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install and Sign In

Install Claude Code using the [official setup guide](https://docs.anthropic.com/en/docs/claude-code/getting-started), then run:

```bash
claude
```

Complete the interactive sign-in. Unity's Chat Settings uses `claude auth status` to report the cached CLI authentication state.

## External MCP Client

Unity Biome MCP creates `.mcp.json` in the Unity project root. Open Claude Code from that project, restart it after the file is created, and run the [first connection check](../getting-started/index.md#3-verify-the-first-connection).

For user-level configuration instead, use the [global configuration command](../getting-started/index.md#python-cli-global-configuration) with client key `claude-code`.

For optional project-local MCP workflows and focused Unity subagents, follow
[Install AI Skills and Agents](ai-skills.md).

## In-Unity Chat

1. Open **MCP > Settings > Chat Settings**.
2. Confirm that the Claude binary is detected and authentication reports success.
3. Open **MCP > Chat** and select **Claude**.

MCP Chat reuses the cached Claude CLI login. It does not ask for an API key.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Claude is not found | Make `claude` available on the login-shell `PATH`, then restart Unity |
| Authentication is not ready | Run `claude`, complete sign-in, and verify with `claude auth status` |
| Tools are absent in Claude Code | Start Claude Code from the Unity project and confirm that `.mcp.json` exists |
| Configuration changed | Restart Claude Code so it reloads the MCP server entry |
