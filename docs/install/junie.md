# Junie

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install

Install Junie in a compatible JetBrains IDE and sign in. See the [Junie MCP settings guide](https://junie.jetbrains.com/docs/junie-plugin-mcp-settings.html) for the client UI.

## Configure

Unity Biome MCP creates this project-local file:

```text
.junie/mcp/mcp.json
```

Open the Unity project in the IDE, allow Junie to load project configuration, and run the [first connection check](../getting-started/index.md#3-verify-the-first-connection).

For user-level configuration instead, use the [global configuration command](../getting-started/index.md#python-cli-global-configuration) with client key `junie`. The packaged CLI writes `~/.junie/mcp/mcp.json`.

The project-local and global paths are different scopes. Check which scope Junie reports before editing a file.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Junie does not load the project entry | Confirm the project is trusted and project MCP configuration is enabled |
| The wrong file was edited | Check the scope shown in Junie's MCP Servers list |
| Tools cannot reach Unity | Keep Unity open and follow [connection diagnostics](../getting-started/index.md#diagnose-a-failure) |
