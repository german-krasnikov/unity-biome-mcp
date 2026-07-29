# Kimi

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install and Sign In

Install Kimi Code using the [official CLI guide](https://www.kimi.com/help/kimi-code/cli-getting-started), then sign in:

```bash
kimi login
```

Restart Unity if the CLI was installed while the Editor was open.

## In-Unity Chat

Kimi is a Chat-start backend:

1. Open **MCP > Settings > Chat Settings > Kimi Settings**.
2. Confirm that the `kimi` binary is detected.
3. Open **MCP > Chat** and select **Kimi**.

The relay writes Kimi's temporary MCP configuration when a turn starts. Each
turn runs a new Kimi process. See [Chat Backends](../chat/backends.md#sessions)
for current session behavior.

## External MCP Client

The Unity package does not create a project-local Kimi file. To configure Kimi as an external MCP client, use the [global configuration command](../getting-started/index.md#python-cli-global-configuration) with client key `kimi`. It writes `~/.kimi-code/mcp.json`.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Kimi is not found | Make `kimi` available on the login-shell `PATH`, then restart Unity |
| Authentication fails | Run `kimi login` in a terminal and complete the login flow |
| A custom model is rejected | Verify the model in Kimi's own configuration before selecting it in Chat |
