# Codex

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install and Sign In

Install Codex and complete its first-run sign-in using the [official Codex CLI guide](https://learn.chatgpt.com/docs/codex/cli):

```bash
codex
```

## External MCP Client

Unity Biome MCP creates `.codex/config.toml` in the Unity project. Restart Codex
after Unity creates or updates this file, open Codex from that project, and run
the [first connection check](../getting-started/index.md#3-verify-the-first-connection).

Codex uses TOML. Do not paste the standard `mcpServers` JSON into `config.toml`.

For user-level configuration instead, use the [global configuration command](../getting-started/index.md#python-cli-global-configuration) with client key `codex`. It writes `~/.codex/config.toml`.

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
| External Codex reports an unknown server | Confirm `.codex/config.toml` exists in the Unity project and restart Codex |
| A Chat turn times out too early | Codex uses a higher inactivity floor; review Chat Settings before increasing it further |
