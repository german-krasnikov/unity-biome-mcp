# VS Code

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Install

Install [Visual Studio Code](https://code.visualstudio.com/download) and a Chat extension that supports local MCP servers.

## Configure

Unity Biome MCP creates `.vscode/mcp.json` in the Unity project. The file uses VS Code's `servers` root and typed stdio entry.

Open the Unity project as the VS Code workspace, accept the local-server trust prompt when appropriate, and run the [first connection check](../getting-started/index.md#3-verify-the-first-connection).

For user-level configuration instead, use the [global configuration command](../getting-started/index.md#python-cli-global-configuration) with client key `vscode`. The packaged CLI writes the platform-specific user `mcp.json`.

## Client-specific Troubleshooting

| Symptom | Action |
|---|---|
| Server is not listed | Run **MCP: List Servers** from the Command Palette and confirm `.vscode/mcp.json` is loaded |
| Server is not trusted | Review the generated command, then accept the workspace trust prompt |
| Configuration changed | Run **Developer: Reload Window** |
| Tools cannot reach Unity | Keep Unity open and follow [connection diagnostics](../getting-started/index.md#diagnose-a-failure) |
