# Codex Setup

The plugin auto-configures Codex automatically when you add it to your project. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Codex CLI installed and authenticated
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or auto-assigned) free

## Quick Setup

### 1. Install Codex CLI

```bash
npm install -g @openai/codex
codex --version
```

Authenticate:

```bash
codex login
```

### 2. Add Plugin to Unity

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

The plugin auto-generates your Codex MCP config on first load.

### 3. Verify Installation (Optional)

Run the diagnostic:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp doctor
```

## Use Codex From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **MCP → Chat**.
3. Select **Codex** from the backend dropdown.
4. Type a prompt and press Enter.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `codex: command not found` | Ensure `npm install -g @openai/codex` completed. Check `which codex` or `where.exe codex`. |
| `unknown MCP server 'unity'` | Run Setup Wizard to auto-configure Codex MCP settings. |
| MCP server fails to start | Run Setup Wizard → Diagnostics to check Python version and TCP port availability. |
| Setup Wizard doesn't open in Unity | (1) Verify plugin is in Package Manager. (2) Close/reopen Unity. (3) Check Console for errors. |
| Tools don't respond in Chat | Confirm Unity is open and Console shows `[MCP] Server started on port <XXXX>`. Run Setup Wizard → Diagnostics. |
| `codex exec` blocks at startup (macOS/Linux) | Redirect stdin: append `</dev/null`. The plugin handles this automatically. |
| Binary path resolution fails in Settings | Override manually: **Settings > Agent Chat > Codex Binary Path** — enter absolute path. |
