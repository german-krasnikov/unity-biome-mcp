# OpenCode

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install and Authenticate

Install OpenCode and configure a model provider using the [official OpenCode documentation](https://opencode.ai/docs).

Restart Unity if the CLI was installed while the Editor was open.

## In-Unity Chat

1. Open **MCP > Settings > Chat Settings > OpenCode Settings**.
2. Confirm that the `opencode` binary is detected.
3. Open **MCP > Chat** and select **OpenCode**.

The relay writes a temporary OpenCode configuration, passes it through
`OPENCODE_CONFIG`, and starts a new `opencode run` process for each turn. See
[Chat Backends](../chat/backends.md#sessions) for current session behavior.

## External MCP Client

The Unity package does not create a project-local OpenCode file. Do not paste
the Setup Wizard's standard `mcpServers` clipboard JSON into OpenCode. Use the
[global configuration command](../getting-started/index.md#python-cli-global-configuration)
with client key `opencode` so the packaged CLI applies OpenCode's
client-specific root and command-array transformation.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| OpenCode is not found | Make `opencode` available on the login-shell `PATH`, then restart Unity |
| Provider or model is rejected | Verify the provider and model with OpenCode before using the same model ID in Chat |
| External MCP server is absent | Inspect the global OpenCode config written by the CLI and check the client's MCP status |
