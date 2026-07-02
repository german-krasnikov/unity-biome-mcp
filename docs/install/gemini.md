# Gemini Setup (Deprecated)

**Note: Gemini support has been deprecated as of v0.68.0. Please use one of the actively supported backends instead:**

- [Claude Code](claude-code.md) — recommended
- [Codex](codex.md)
- [Cursor](cursor.md)
- [Windsurf](windsurf.md)
- [VS Code](vscode.md)
- [Claude Desktop](claude-desktop.md)
- [Kimi](kimi.md)
- [OpenCode](opencode.md)

---

Since v0.30.1. The in-Unity Chat window previously supported Gemini as a backend (deprecated v0.68.0).

## Prerequisites

- Gemini CLI installed and authenticated
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or assigned by wizard) free

## 1. Quick Setup

Bootstrap script handles everything:

```bash
curl -fsSL https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/install/bootstrap.sh | bash
```

Then open Unity and follow the Setup Wizard.

## 2. Manual Setup

### Install Gemini CLI

**macOS (Homebrew — recommended):**

```bash
brew install gemini-cli
gemini --version
```

**All platforms (npm):**

```bash
npm install -g @google/gemini-cli
gemini --version
```

### Install Python Server

The Python MCP server runs on-demand via `uvx` — no installation needed. The setup wizard will auto-discover it.

### Add Plugin to Unity

1. Open Package Manager (Window → Package Manager)
2. Click `+` → **Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`

### Authenticate Gemini

```bash
gemini
```

Gemini opens a browser for Google OAuth. Credentials are stored in `~/.gemini/oauth_creds.json`.

### Configure Gemini (Automatic)

Open Unity, then open the **Setup Wizard** via **MCP → Setup Wizard** menu. Select **Gemini** and follow the prompts. The wizard will merge the `unity-mcp` entry into `~/.gemini/settings.json` automatically.

**Manual configuration:**

Edit `~/.gemini/settings.json` and add:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "uvx",
      "args": ["--from", "git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server", "unity-mcp"],
      "env": { "UNITY_MCP_PORT": "9500" },
      "trust": true
    }
  }
}
```

If the file already exists, merge only the `unity-mcp` entry — preserve other MCP servers.

<details>
<summary><b>Alternative: Manual Installation (git clone)</b></summary>

```bash
git clone https://github.com/german-krasnikov/unity-kiss-mcp.git
cd unity-kiss-mcp
python install.py setup
```

This clones the repository locally, creates a Python venv, and installs dependencies. After setup, verify:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp --help
```

Then configure your AI tool:

```bash
python install.py configure --tool gemini
# Or project-scoped:
python install.py configure --tool gemini --project-dir /path/to/unity/project
```

Verify installation:

```bash
python install.py doctor
```

</details>

## 3. Use From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **MCP → Chat**.
3. Select **Gemini** from the backend dropdown.
4. Optionally choose a model from the dropdown (defaults to Gemini CLI default).
5. Type a prompt and press Send.

## 4. How the Plugin Wires MCP

Before each turn, the plugin merges the `unity-mcp` entry into `~/.gemini/settings.json`:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "<python>",
      "args": ["-m", "unity_mcp.server"],
      "env": { "UNITY_MCP_PORT": "<port>" },
      "trust": true
    }
  }
}
```

If the file already exists, only the `unity-mcp` entry is modified — other MCP servers are preserved.

The plugin spawns:

```bash
gemini -p "<prompt>" --output-format stream-json [--model <id>] [--yolo] [--sandbox]
```

## 5. Verify Installation

Open the **Setup Wizard** in Unity (**MCP → Setup Wizard**) and select **Diagnostics**.

Or check manually:

```bash
gemini --version
cat ~/.gemini/settings.json  # should contain unity-mcp entry

# Test (with Unity running)
gemini -p "Call the mcp tool get_hierarchy and return the result" --output-format stream-json
```

## 6. Verify Connectivity (Manual CLI Test)

Expected: NDJSON stream with `tool_use` line containing `"tool_name": "mcp_unity-mcp_get_hierarchy"`.

## 7. Troubleshooting

| Problem | Fix |
|---------|-----|
| `gemini: command not found` | Check `which gemini`. On macOS, run `brew install gemini-cli`. Restart terminal after install. |
| Setup Wizard doesn't open | (1) Verify plugin is in Package Manager. (2) Close/reopen Unity. (3) Check Console for errors. |
| MCP tools not available in Chat | Verify `~/.gemini/settings.json` contains `unity-mcp` entry. Check Unity Console shows `[MCP] Server started on port <XXXX>`. Restart Chat session. |
| MCP server fails to start | Run Setup Wizard → Diagnostics to verify Python 3.10+ and TCP port availability. |
| Binary not found in Chat Settings but works in terminal | Terminal sources `~/.zshrc` but Unity doesn't. Override: **Settings > Agent Chat > Gemini Binary Path** — enter absolute path. |
| Settings file broke other MCP servers | The plugin merges intelligently — it only modifies the `unity-mcp` entry. If other servers disappeared, they likely had malformed JSON. Restore from backup if needed. |
