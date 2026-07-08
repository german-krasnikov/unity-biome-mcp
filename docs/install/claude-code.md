# Claude Code Setup

The plugin auto-configures Claude Code automatically when you add it to your project. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Claude Code CLI installed and authenticated
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or auto-assigned) free

## Quick Setup

### 1. Install Claude Code CLI

Visit https://claude.com/download and install the native Claude Code app for your OS, or via npm:

```bash
npm install -g @anthropic-ai/claude-code
claude --version
```

Authenticate:

```bash
claude auth login
```

### 2. Add Plugin to Unity

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

The plugin auto-generates your Claude Code MCP config on first load.

### 3. Verify Installation (Optional)

Run the diagnostic:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp doctor
```

Or from Claude Code itself:

```python
await doctor()
```

## Use Claude Code From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **MCP → Chat**.
3. Select **Claude** from the backend dropdown.
4. Type a prompt and press Enter.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `claude: command not found` | Ensure Claude Code is installed and in PATH. Check `which claude` or `where.exe claude`. |
| MCP server fails to start | Run Setup Wizard → Diagnostics. Check that Python 3.10+ is available and TCP port 9500 is free. |
| Setup Wizard doesn't open in Unity | (1) Verify plugin is in Package Manager. (2) Close and reopen Unity. (3) Check Console for errors. |
| MCP tools don't appear in Claude Code | (1) Confirm Setup Wizard configured Claude Code. (2) Restart Claude Code. (3) Check Console for MCP connection errors. |
| Tools fail with "Connection refused" | (1) Ensure Unity is open with the plugin. (2) Run Setup Wizard → Diagnostics to check TCP port. (3) Restart Unity. |
| Python path resolution fails in Chat Settings | Override manually: **Settings > Agent Chat > Claude Binary Path** — enter absolute path to `claude` binary. |
