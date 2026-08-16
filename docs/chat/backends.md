# Chat Backends

MCP Chat runs supported CLI tools through a local Python relay. Install and sign in to a CLI outside Unity first; Chat reuses that CLI's cached authentication. There is no API-key field in Unity.

## Supported Backends

| Backend | CLI | Process model | Resume argument | Chat-start MCP configuration |
|---|---|---|---|---|
| Claude | `claude` | One process; turns are written to stdin | `--resume` | Temporary Claude MCP JSON |
| Codex | `codex` | New `codex exec` process for each turn | `exec resume` | Inline Codex configuration |
| Kimi | `kimi` | New process for each turn | Not defined | Temporary Kimi MCP JSON |
| Antigravity | `agy` | New process for each turn | Not defined | Temporary Antigravity settings |
| OpenCode | `opencode` | New `opencode run` process for each turn | `-s` | Temporary config through `OPENCODE_CONFIG` |

The selected model comes from **MCP > Settings > Chat Settings** and is applied
when the relay starts the backend. Other launch-option fields shown there are
currently saved by the UI but are not forwarded by `RelayBackend`; do not rely
on them to configure CLI permissions, sandboxing, or extra arguments. Model
availability and authentication are owned by the selected CLI, so verify custom
model IDs in that CLI before using them in Chat.

## Process Lifecycle

### Claude

The relay starts Claude with stream-json input and output. The process remains available between turns, and Chat writes each new turn to stdin. A returned session ID can be passed back with `--resume`.

### Codex, Kimi, Antigravity, and OpenCode

These CLIs receive the prompt as a command argument instead of reading turns from stdin. The relay therefore defers startup until a prompt is available, starts one process for that turn, and closes stdin.

Switching the backend or model stops the current backend. Stopping a turn also kills its process; Chat creates a fresh backend for the next send.

## Ask and Agent Modes (and Subagent Delegation)

**Ask** is the default Chat mode. When a backend emits a permission request, Chat shows **Allow**, **Deny**, **Session**, and **Always** choices. **Agent** automatically approves permission requests emitted by the backend.

Mode behavior remains backend-specific. Some CLIs use their own non-interactive or permission-bypass flags and do not emit the same prompts as Claude. Ask and Agent are not a replacement for server-side tool visibility or code-execution security; see [Settings](../settings.md).

To delegate work to a subagent, mention it in the request using `@agent-name` syntax. The subagent name is resolved from `.claude/agents/*.md` files in your project and home directory. The model will invoke the Agent tool with the subagent's type and task description.

## Sessions

The backend definitions accept resume IDs for Claude, Codex, and OpenCode. The Chat session picker scans local stores for Claude, Codex, Kimi, and Antigravity; it does not scan OpenCode's SQLite store.

These are separate capabilities: a picker entry means Chat can find an ID, while
a resume argument means the backend can consume one. A resume ID selected for a
per-turn backend is not currently applied to the next turn. Reliable in-Chat
resume is therefore limited to Claude.

When a backend returns a session ID, **To CLI** copies that backend's resume command. See [Using MCP Chat](index.md#sessions) for the user workflow.

## Authentication and Configuration

- Sign in with the backend CLI outside Unity.
- Ensure the CLI executable is available on the login-shell `PATH`, then restart
  Unity after installing it.
- Claude exposes a cached-auth status check through `claude auth status`.
- Chat writes temporary or inline MCP configuration at backend start; this does not configure the same CLI as a standalone external MCP client.
- For external-client setup, follow the matching [client guide](../install/index.md).

### Antigravity

The Antigravity backend requires the `agy` executable and its CLI
authentication/configuration to be available before Chat starts. Verify that
`agy --help` runs in a login shell, restart Unity, then select **Antigravity** in
MCP Chat. The relay writes temporary MCP settings for each turn.

For common recovery steps, see
[Using MCP Chat](index.md#troubleshooting). If a custom model is rejected,
test its ID with the backend CLI before saving it in Chat Settings.
