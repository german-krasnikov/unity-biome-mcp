# Codex

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install and Sign In

Install Codex and complete its first-run sign-in using the
[official Codex CLI guide](https://developers.openai.com/codex/cli):

```bash
codex
```

## External MCP Client

Unity Biome MCP creates `.codex/config.toml` in the Unity project. Codex loads
project-scoped MCP configuration only for a trusted project, so review the
generated command before trusting the checkout. Restart Codex after Unity
creates or updates the file, open Codex from that project, and run:

```bash
codex mcp list
```

Confirm that `unity-biome-mcp` is listed, then run the
[first connection check](../getting-started/index.md#3-verify-the-first-connection).
Inside an interactive Codex session, `/mcp` shows the same connection and tool
status.

Codex uses TOML. Do not paste the standard `mcpServers` JSON into `config.toml`.

For user-level configuration instead, use the [global configuration command](../getting-started/index.md#python-cli-global-configuration) with client key `codex`. It writes `~/.codex/config.toml`.

Codex's ChatGPT desktop app, CLI, and IDE extension share the Codex-host MCP
configuration. See the
[official Codex MCP guide](https://developers.openai.com/codex/mcp/) for current
client behavior; use this guide for the Unity-generated entry.

For optional project-local skills and generated Codex agents, follow
[Install AI Skills and Agents](ai-skills.md). The guide explains ownership
checks and the Claude-to-Codex sync.

## In-Unity Chat

1. Open **MCP > Settings > Chat Settings > Codex Settings**.
2. Confirm that the Codex binary is detected.
3. Open **MCP > Chat** and select **Codex**.

MCP Chat uses the Codex CLI's existing authentication and starts `codex exec`
for each turn. See [Chat Backends](../chat/backends.md#sessions) for current
session behavior.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Codex is not found | Make `codex` available on the login-shell `PATH`, then restart Unity |
| External Codex reports an unknown server | Confirm the project is trusted, `.codex/config.toml` exists, and `codex mcp list` includes `unity-biome-mcp` |
| A Chat turn times out too early | Codex uses a higher inactivity floor; review Chat Settings before increasing it further |
