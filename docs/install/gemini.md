# Gemini Setup (Deprecated)

Gemini CLI support was deprecated in **v0.68.0** and is no longer maintained.

**Use [Claude Code](claude-code.md) instead** — it's the recommended backend with full support.

Other supported backends: [Codex](codex.md) · [Cursor](cursor.md) · [Windsurf](windsurf.md) · [VS Code](vscode.md) · [Kimi](kimi.md) · [OpenCode](opencode.md)

---

## Legacy Setup (Unsupported)

If you still need Gemini CLI:

1. Install: `npm install -g @google/gemini-cli` (or `brew install gemini-cli` on macOS)
2. Authenticate: `gemini` (opens browser OAuth)
3. Add plugin to Unity via UPM: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`
4. Run Setup Wizard (**MCP → Setup Wizard**) and select **Gemini** to auto-configure `~/.gemini/settings.json`
