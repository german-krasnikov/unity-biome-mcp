# Chat Backends Guide

In-Unity chat supports multiple CLI-based LLM backends.

## Supported Backends

| Backend | CLI | Model | Persistent | Status |
|---------|-----|-------|-----------|--------|
| Claude | claude | Claude 4 (Opus, Sonnet) | Per-turn | ✅ Stable |
| Codex | codex | OpenAI GPT-4 | Per-turn (supports resume) | ✅ Stable |
| Kimi | kimi | Kimi (Moonshot) | Per-turn | ✅ Stable |
| Antigravity | agy | Various | Per-turn | ✅ Stable |
| OpenCode | opencode | Various | Per-turn | ✅ Testing |

## Claude

**Executable:** `claude` (installed via `curl | bash` setup)

**Models:** Claude 4 Opus (reasoning), Claude 4 Sonnet (balanced), Claude 4 Haiku (fast, cost-effective)

**Session:** Per-turn (no session memory between turns in default config)

**Authentication:** Uses cached login from `claude auth login`

```python
# In Unity: MCP → Chat → Select "Claude"
# Then chat in the window
```

**Pricing:** See Anthropic's pricing page for current rates

## Codex (OpenAI)

**Executable:** `codex exec`

**Models:** GPT-4, o3, o4-mini and newer

**Session:** Per-turn (new process each turn; supports session resume via `codex exec resume <session-id>` subcommand)

**Protocol:** NDJSON (line-delimited JSON via `--json` flag, parsed as OpenAI Responses API format)

**Setup:**
```bash
# Install Codex CLI
npm install -g @openai/codex
codex login
```

**Note:** Pricing varies by model; check OpenAI pricing page.

## Kimi (Moonshot AI)

**Executable:** `kimi`

**Models:** Kimi (Chinese LLM), multilingual support

**Session:** Per-turn

**Setup:**
```bash
# Install Kimi CLI
curl -fsSL https://kimi.ai/install.sh | bash
kimi login
```

**Pricing:** Check Moonshot AI's pricing page for current rates.

## Antigravity (Agy)

**Executable:** `agy` (CLI binary)

**Models:** Configurable backend (Claude via API, custom LLM)

**Session:** Per-turn

**Setup:**
```bash
# Install Agy CLI
go install github.com/antigravityai/agy@latest
agy login  # if required by your Agy instance
```

Configure credentials via your Agy instance documentation.

## OpenCode
**Executable:** `opencode` (standalone Go CLI)

**Models:** Configurable (Claude, GitHub Copilot, custom)

**Session:** Per-turn (supports external MCP merge)

**Note:** Experimental; external MCP configs merged additively.

## Switching Backends in Unity

1. Open **MCP → Chat** in the menu
2. Click the **Backend** dropdown
3. Select Claude, Codex, Kimi, Antigravity, or OpenCode
4. Enter API key if needed
5. Start chatting

**Persistence:** Selected backend is saved to EditorPrefs.

## Per-Turn vs Session Persistence

### Per-Turn
- New CLI process started each time (stateless)
- Faster startup (~100ms)
- No memory between turns unless session resume is used
- Example: Claude, Codex, Kimi, OpenCode

**Pros:**
- Simple; no session management
- Works offline (no session server)
- Each backend can optionally support session resume

**Cons:**
- Each turn must re-context the entire conversation (unless resumed)
- Slower for long conversations without resume

### Session Resume (Optional)
- Some backends (Codex, OpenCode) support resuming previous sessions
- Faster recovery after network issues or crash
- Full conversation history maintained if resumed
- Example: `codex resume <session-id>`, `opencode run -s <session-id>`

**Pros:**
- Faster per-turn response
- Rich conversation history
- Supports reasoning models (o3, etc.)

**Cons:**
- Requires process management
- Harder recovery after crash

## Configuration

### API Keys

Store in local settings:

```
Unity Editor → MCP → Settings → Chat → API Keys
```

**NOT stored in code or version control.**

### Environment Variables

**Injected to all backends:**
- `UNITY_MCP_PORT`: TCP port where MCP server is listening
- `UNITY_MCP_SESSION_TIMEOUT`: Default 300 seconds (extended for reasoning models)

**Claude only:** `UNITY_MCP_PORT` is stripped (not passed to Claude CLI; delivered via `--mcp-config` file instead).

## Cost Tracking

**In-window display:**
- Token budget per day
- Current spend vs. cap
- Haiku cost breakdown (intent tools)

**Enable tracking:**
```
UNITY_MCP_BUDGET=1 environment variable
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "CLI not found" | See installation guide for your CLI (`docs/install/`) or add to PATH |
| "Authentication failed" | Run `<backend> auth login` to cache credentials |
| "Connection timeout" | Increase `UNITY_MCP_SESSION_TIMEOUT=600` for reasoning models |
| "No response" | Check Editor.log for connection errors; restart chat window |
| "Tool not available" | Upgrade backend CLI via its own updater — see `docs/install/` for your CLI |

## Switching Mid-Conversation

**Warning:** Switching backends loses current session context.

**Recommended:** Let conversation finish, then select new backend for next chat.

---

**See also:** `docs/chat/annotation.md` for visual tools, `docs/features/intent-tools.md` for LLM-driven automation.
