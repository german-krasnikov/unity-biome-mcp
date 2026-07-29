# Rider AI Assistant

Prerequisite: complete the
[Unity package installation](../getting-started/index.md#1-install-the-unity-package).

## Configure

Rider uses its Settings UI rather than a generated project file. The current
JetBrains AI Assistant expects an `mcpServers` JSON object:

1. Open **MCP > Setup Wizard** in Unity.
2. Select **Rider AI Assistant** to confirm the prerequisite.
3. In Rider, open
   **Settings > Tools > AI Assistant > Model Context Protocol (MCP)**.
4. Select **+**, choose the STDIO JSON configuration, and paste:

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

5. Select **OK**, then **Apply**. Rider starts the server and shows its status.

Keep the Unity project open while using the server. The configuration is owned
by Rider; Unity Biome MCP does not generate a Rider JSON or TOML file.

See the
[JetBrains MCP configuration guide](https://www.jetbrains.com/help/ai-assistant/mcp.html)
for Rider-specific controls and logs.

## Troubleshooting

| Symptom | Action |
|---|---|
| Configuration is rejected | Paste the complete `mcpServers` object, not a shell command |
| Server does not start | Confirm `uvx` is available to Rider's environment |
| Tools cannot reach Unity | Keep Unity open and run [connection diagnostics](../getting-started/index.md#diagnose-a-failure) |
