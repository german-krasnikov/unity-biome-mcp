# Feature: Optional In-Unity Agent Chat

## Overview

An optional Editor window that brings agentic chat directly into Unity, spawning the user's local `claude` CLI as a child process. Zero new MCP tools — reuses all 142 existing tools via the spawn-the-CLI architecture.

**Isolation:** `UnityMCP.Editor.Chat.asmdef` is always compiled. Deleting the `Chat/` folder leaves core untouched.

## Architecture

```
Unity Editor Window (MCPChatWindow)
    │
    └─ System.Diagnostics.Process
        │
        └─ claude CLI (headless, stream-json mode)
            │
            └─ python -m unity_mcp.server
                │
                └─ TCP:9500 → Unity Editor Plugin
                    └─ ~142 MCP tools (create, set_property, screenshot, etc.)
```

### Spawn Invocation (v0.36.0)

```bash
claude -p \
  --output-format stream-json \
  --verbose \
  --include-partial-messages \
  --input-format stream-json \
  --mcp-config <config.json> \
  --permission-mode <plan|acceptEdits>
```

Key details:
- **`-p`** — headless streaming mode (no interactive terminal)
- **`--output-format stream-json`** — stream JSON events (partial message chunks)
- **`--include-partial-messages`** — emit tool cards + results as they arrive
- **`--input-format stream-json`** — accept JSON-encoded user turns on stdin
- **`--mcp-config`** — path to the MCP config file (defines `unity_mcp` server with optional env block)
- **`--permission-mode plan|acceptEdits`** — user-selected mode (tool calls require acknowledgment or auto-accept)
- **Auth:** Uses user's locally-installed `claude` CLI with cached subscription login. `ANTHROPIC_API_KEY` is explicitly stripped from child env to prevent API key leakage or double-billing.

**Subprocess Environment (v0.36.0, v0.55.0: scoped config delivery)** — CliBackendBase injects only:
- **UNITY_MCP_SESSION_TIMEOUT=300** — extended session deadline for reasoning models (Codex o3/o3-pro may think for 2–5 min)

**RULE (v0.55.0):** NEVER inject UNITY_MCP_PORT into process env — port comes via scoped config only. BuildSpawnEnv() returns only UNITY_MCP_SESSION_TIMEOUT. Each CLI backend delivers port via its --mcp-config JSON/TOML environment block.

**v0.55.0 Breaking Rule:** UNITY_MCP_PORT is **never** injected into process env. Instead, each backend delivers the port via scoped --mcp-config (JSON/TOML/env block per CLI):
- **Claude**: `--mcp-config <path>.json` with `"environment": { "UNITY_MCP_PORT": "<port>" }` block
- **Codex**: `--mcp-config <path>.json` with `"environment": { "UNITY_MCP_PORT": "<port>" }` block
- **OpenCode**: `--mcp-config <path>.json` with `"environment": { "UNITY_MCP_PORT": "<port>" }` block (v0.55.0: external MCP merge)
- **Other backends**: deliver UNITY_MCP_PORT in their scoped config env block (NEVER process env)

### Module Isolation

**C# asmdef:**
- `UnityMCP.Editor.Chat.asmdef` (references ONLY `UnityMCP.Editor`, autoReferenced=false)
- One-way dependency: Chat → Core (via assembly reference), not Core → Chat

**InternalsVisibleTo:**
- Core exposes internals: `[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat")]` in `AssemblyInfo.cs`
- Enables Chat to access internal core APIs (CommandRouter, RefManager, CommandRegistry, etc.)

**Settings Hook (Event-Driven):**
- Core fires `ChatSettingsHook.OnBuildToolsCatalog` event on MCPSettings build
- Chat subscribes: `ChatSettingsHook.OnBuildToolsCatalog += RefreshSettings`
- Preserves one-way dependency: core does not know Chat exists
- Removed the GUI code for Chat settings completely in core for clarity

## Multi-Backend Architecture (v0.14.0+)

Each CLI-based backend is a strategy over **4 variation axes:**

1. **BuildArgs** — spawn/resume argv construction (e.g., Claude uses `--resume <sessionId>`, Codex uses `exec resume <id>`)
2. **ParseLine** — NDJSON line → ChatEvent[] conversion (stream format differs per CLI)
3. **BinaryName** — CLI executable name for ChatBinaryResolver (e.g., `"claude"`, `"codex"`)
4. **IsPersistentProcess** — true = stdin loop (Claude), false = spawn-per-turn (Codex)

**CliBackendBase** (194-line abstract host, v0.55.0: port delivery via scoped config): Owns shared lifecycle (spawn, drain, accumulate, SessionId, Stop, Dispose). **CRITICAL RULE (v0.55.0):** `BuildSpawnEnv()` returns ONLY `UNITY_MCP_SESSION_TIMEOUT` — NEVER UNITY_MCP_PORT. Port must be delivered via each backend's scoped --mcp-config (JSON env block). Subclasses override only the 4 axes; all other logic (turn dispatch, tool accumulation, session management) is inherited.

**ClaudeBackend** (ported): Zero behavior change (−65 lines net). Now a thin wrapper over the base. Regression anchor proving the abstraction doesn't alter existing behavior.

**CodexAppServerBackend** (v0.14.0, simplified in v0.20.0 as only Codex option): Implements the 4 axes for OpenAI Codex via persistent `codex app-server` (JSON-RPC 2.0). One process per chat session (IsPersistentProcess=true), eliminates spawn-per-turn churn. Protocol: `initialize` → `thread/start` → repeated `turn/start` with `mcpToolCall` items + real token streaming via `item/agentMessage/delta`.

**AntigravityBackend** (v0.41.0, external LLM service): Implements the 4 axes for Antigravity with model selection and stream-json protocol.

**CodexArgBuilder** (v0.14.0): Constructs `codex app-server` argv + init args. Three `-c mcp_servers.unity*` flags passed at initialization. Format: `-c mcp_servers.{unity,unity_auth,unity_plugins}=<value>`.

**CodexAppServerParser** (v0.14.0, replaces CodexStreamParser, v0.30.5 silent abort fix): JSON-RPC 2.0 notification/response parser → ChatEvent. Emits agent_message (via delta tokens), mcp_tool_call, command_execution (aggregated_output or declined), file_change (changes array), and turn.completed (usage stats; CostUsd=0). **v0.30.5 fix:** Codex sets `status:"completed"` even on tool errors; real indicator is `result.isError:true` (no space). Parser now checks `!resultObj.Contains("\"isError\":true")` pattern-match. On error with empty text, appends `"[MCP tool error]"` placeholder. Emits `ChatEvent.Heartbeat()` on "reasoning" events (o3/o3-pro silent thinking). 15+ NUnit test cases cover all paths, +6 new error scenario tests.

**BackendRegistry** & **BackendKind** (simplified v0.20.0): Central enum + factory. User selects Claude (persistent stdin) or Codex (persistent JSON-RPC) from dropdown; MCPChatWindow.CreateBackend dispatches to the right subclass. BackendKind = {Claude, Codex} (removed spawn-per-turn CodexBackend entry).

**PendingTurnState v3** (upgraded): Now persists `BackendKind` to survive domain reload. Back-compatible with v1/v2 state; header includes version marker.

**Result:** Adding a new backend = 1 new CliBackendBase subclass + parser file. No changes to window, dispatcher, or lifecycle code.

### Codex Backend — Version-Specific Integration (v0.141.0+)

**Problem (OpenAI issue #11816, OPEN):** Codex 0.141.0 sends `mcp_elicitation` (approval-kind) events without timeout, causing indefinite blocking in headless stream-json mode. Unlike Claude which distinguishes request (top-level `id`) from notification (nested `id`), Codex doesn't signal request context cleanly.

**Layered Mitigation:**

1. **Layer 1 — Suppression + Sandbox:** `CodexArgBuilder` injects `--disallowedTools approval` (prevents Codex from emitting approval requests). Paired with `--permission-mode acceptEdits` for auto-accept on mutations (no approval needed). Sandbox: all tool calls pass immutable `args` dict to CommandRouter — no approval-mutation races.

2. **Layer 2 — Auto-Accept:** If approval leaks through Layer 1, `ControlResponseBuilder.CodexElicitationAccept()` auto-responds with status=accepted (never silent-drop). Prevents indefinite block but signals bug upstream.

3. **Layer 3 — Request/Notification Invariant:** `CodexAppServerParser.HasRpcId()` distinguishes top-level request `id` (field present, type string) from notification (field absent or null). Parser NEVER silent-drops: every incoming JSON-RPC frame must match this invariant or logs error + continues. Enables future version diffs.

**Files:**
- `CodexArgBuilder.cs` — line with `--disallowedTools approval`
- `CodexAppServerParser.cs` — HasRpcId check in frame dispatch
- `ControlResponseBuilder.cs` — CodexElicitationAccept entry point

**Details:** See `AI/mcp-server.md` § "Codex App-Server Elicitation Handling" for architectural explanation and code snippets.

## IChatBackend Abstraction

Single interface for pluggable chat backends:

```csharp
public interface IChatBackend
{
    event EventHandler<ChatEvent>? OnChatEvent;
    Task<bool> StartAsync(string modePermission, string userPrompt);
    Task StopAsync();
    Task SendUserTurnAsync(JsonObject turn);
    bool IsConnected { get; }
    string Status { get; }
}
```

**Implementations:** `ClaudeBackend` (Claude, persistent stdin), `CodexAppServerBackend` (Codex, persistent JSON-RPC). Future: add more via `CliBackendBase` subclasses.

**ChatEvent struct:**
- Normalized event type (ToolCard, ToolResult, UserMessage, Error, Status, Done)
- Humanized text output (e.g., "Editing /Enemies/Boss" not raw JSON)
- Raw event data preserved for debugging

## Features

### Annotation Tools & Scene Regions (Plugin v0.18.0+)

Scene annotations enable domain-specific markup via visual regions (points, polylines, measurements) that become selectable chip references in the chat. Implementation:

**RegionChipProvider.cs** — Implements `IChipKindProvider` for scene region objects:
- **Detects** region objects by component type (e.g., `PointMarker`, `RegionOutline`)
- **Renders** regions as graphical overlays in the Scene view (via OnSceneGUI)
- **Formats** annotations as `[region:path/to/annotation]` chips for send-time context
- **Navigate** — Click chip → selects region object in Inspector + flashes in Scene view
- **Ping** — Highlights region, moves focus to Scene view
- **Context Menu** (`AppendContextMenuItems`)** — Right-click options: "Delete Annotation", "Edit Properties"

**Annotation Types** (extensible via plugin registration):
- **Point:** Single world-space position (e.g., "place trap here at position X")
- **Polyline:** Connected line segments (e.g., "patrol path from A to B to C")
- **Measurement:** Distance/angle between two points (e.g., "gap is 5 units wide")

**Integration with Chat:**
- Regions created via `create_object component=PointMarker` (MCP tool)
- Chips render as colored pills matching the annotation kind
- Send-time context includes region properties (position, length, label)
- AI can modify via `set_property path=/region/name ...`

**Classes:**
- `RegionChipProvider.cs` — Chip kind provider for regions
- `AnnotationMarker.cs` — Base component for all annotation types
- `PointMarker.cs` — Single-point annotation
- `PolylineMarker.cs` — Multi-point path
- `MeasurementMarker.cs` — Distance/angle annotation
- `RegionRenderer.cs` — OnSceneGUI drawing (handles selection highlight, label rendering)

### Compile Auto-Fix Loop (F5, plugin 0.8.0)

`CompileAutoFix.cs` automatically retries after edits fail to compile. Lifecycle:

1. **On turn start:** Arm the retry loop (MAX_RETRIES = 3)
2. **On each compile finish:** Check if retries remain
3. **If compile succeeds:** Disarm immediately
4. **If retries exhausted:** Show a cap chip; final compile absorbed silently (no error spam)

**Provenance gating:** Only arms when the turn actually edited a `.cs` file (tracked by `_turnEditedCode` flag in MCPChatWindow.Drain.cs). Manual IDE edits never trigger auto-retries, preventing false positives.

### Editor State Snapshot Injection (F7, plugin 0.8.0)

`EditorStateSnapshot.cs` builds a lightweight context block and injects it early:

**Content:**
- Active scene name
- Compile status (OK, Compiling, Error)
- Console error count
- First 500 chars of scene hierarchy (with "…(truncated)" if longer)

**Injection:** Via `--append-system-prompt` on fresh chat sessions (ClaudeArgBuilder.cs sets the flag; ClaudeBackend.cs appends the block). On domain-reload resume, the snapshot is prepended to sent text via SentTextCache.

**Result:** Claude starts with full context, eliminating the 2–3 cold-start probe calls it used to make ("What scene are we in?", "Are there compile errors?", "Show me the hierarchy"). Immediate productivity boost; no extra token cost on subsequent turns.

### Tool Ping on Call Complete (F29, plugin 0.8.0)

`ToolPing.cs` flashes any GameObject a tool call touches. Behavior:

1. Tool call completes with args (e.g., `set_property path=/Enemies/Boss`)
2. `ToolPing` extracts the object path from the args
3. Resolves via `ComponentSerializer.FindObject(path)`
4. Calls `EditorGUIUtility.PingObject(instance)` (main thread, inside MCPChatWindow.Drain)
5. Object flashes briefly in the Hierarchy window

**Graceful:** If path missing or unresolvable, no-op (no error shown). Fires exactly once per tool call. Immediate visual feedback for the user on which object was just mutated.

### Plan/Act "Approve & Execute" Bridge (F11, plugin 0.10.0)

After a Plan-mode (Ask) turn finishes, `MCPChatWindow.Drain.cs` injects a one-shot "Approve & Execute" button into the transcript via `ApproveButtonFactory`. Clicking it:
1. Captures the current backend `SessionId`
2. Flips the window to Agent mode
3. Recreates the backend with `--resume <sessionId>` (preserves the just-produced plan)
4. Auto-dispatches the prompt "Execute the plan above."

Files: `MCPChatWindow.Approve.cs` (event handler), `ApproveHelper.cs` (session management), `ApproveButtonFactory.cs` (button builder), `ChatTranscript.Append(VisualElement)` made internal.

**Result:** Seamless bridge from planning to execution in a single workflow, plan never lost. 10 NUnit EditMode tests green.

### Slash-Command Templates (F12, plugin 0.10.0)

Typing `/` in the composer opens a UIToolkit popup of 5 builtin templates: `/fix-compile`, `/add-component`, `/playtest`, `/inspect`, `/screenshot`. Selecting one resolves to plain composer text BEFORE send — a pure input transform with NO MCP coupling.

Files: `SlashTemplate.cs` (`[Flags] ContextGather` enum + readonly struct), `SlashRegistry.cs` (Builtins/Match/Resolve), `SlashPopup.cs` (UIToolkit popup, MaxVisible=5), `MCPChatWindow.Slash.cs` (SetupSlash wires ChangeEvent + KeyDownEvent on parent `_inputArea` at TrickleDown).

**Optional context-gather** (compile errors / selection / scene state / console) with graceful "(context unavailable)" fallback on throw. KeyDown handler on parent at TrickleDown ensures deterministic trickle-down order: Enter resolves template BEFORE `EnterKeySend` fires.

**Result:** Speed up common workflows with one keystroke; templates provide context automatically. 16 NUnit EditMode tests green. +44 lines MCPChatWindow.uss.

### Per-Turn Undo Rollback (F6, plugin 0.11.0)

`TurnUndoTracker.cs` + `RestoreButton.cs` wrap each agent turn in a named Unity Undo group. An amber **Restore** button appears after each turn and reverts that turn's scene mutations in one click (native Unity Undo, scene-only). Only the last turn's button is active; older buttons disable when a new turn starts. Resumed-after-domain-reload turns also get a group.

Files: `TurnUndoTracker.cs` (group lifecycle), `RestoreButton.cs` (button UI + revert logic), `MCPChatWindow.Undo.cs` (partial, split from MCPChatWindow.cs), `.chat-btn--restore` in `MCPChatWindow.uss`.

**Reusable Primitive:** Built on a new public `UndoGroupHelper` core API (4 methods: `OpenNamedGroup`, `CloseNamedGroup`, `RevertToBeforeGroup`, `CanRevert`). Upcoming F27 (atomic batch rollback) will reuse this same system — one rollback mechanism, not two.

**Tests:** 11 NUnit EditMode tests green (TurnUndoTrackerTests 9/9, RestoreButtonTests 2/2). Core `UndoGroupHelper` has 6 NUnit EditMode tests.

**Result:** Agents can now safely mutate scene state with instant undo per turn. 9 EditMode tests in Chat, 6 EditMode tests in Core.

### Inactivity Watchdog for Reasoning Models (v0.30.5, v0.36.0 timeout messaging)

**MCPChatWindow.Drain.cs** now monitors event silence to handle Codex reasoning models (o3, o3-pro) that think silently for 2–5 minutes. **Implementation:**

1. **`_lastEventTime`** — timestamp of the most recent drained event
2. **`InactivityTimeoutSec`** property — returns 300s for Codex (long thinking), 90s for Claude/Gemini (normal responses)
3. **DrainAndRender() watchdog check** — If no events for longer than timeout while backend is running, emit failure card with context hint, finalize turn, call `OnTurnFailed()` (resets undo group, unlocks reload)
4. **Resets:** `_lastEventTime` updated on every OnSend (turn start) and every event drain

**v0.36.0: Timeout Context Hint** — Failure message now includes the last tool name executed (tracked via `_lastToolName` in EventHandlers.cs when ToolStart event fires). Format: `[Timed out: no response for 300s (last tool: set_property)]`. Helps debug which operation was in-flight when timeout occurred.

**Dead-Process Guard (v0.36.0)** — If backend process unexpectedly exits mid-turn (detected via `OnProcessDead()`), appends `[Process exited]` to transcript and finalizes. Surfaces unexpected connection loss (vs. timeout) as distinct error. Also clears turn flags to unlock reload guard.

**Why:** Old code assumed event silence = dead process and called `OnProcessDead()`, killing in-flight reasoning work. New approach: explicit timeout lets reasoning complete, fails gracefully if truly stuck. `ChatEvent.Heartbeat()` (emitted by CodexAppServerParser on reasoning events) resets watchdog without rendering anything.

**Tests:** 2 new inactivity timeout scenarios, 2 new dead-process guard scenarios.

### Chat Context Resolution via Chips (F2, plugin 0.9.0)

`ChipContextResolver.cs` resolves object-path chips to plain text at send-time. Three depth levels:

1. **PathOnly** — just the path (e.g., `/Enemies/Boss`)
2. **Summary** — path + top 3 non-Transform components (e.g., `/Enemies/Boss (Health, Animator, Collider)`)
3. **Full** — path + all components with serialized state

**Resolution logic:**
- **One chip** → Full depth (rich context for single object)
- **Many chips** → Summary depth (token budget)
- **Asset paths** → PathOnly (no components)
- **Budget cap** (2000 chars) → if Full exceeds cap, fall back to Summary

**Integration:** Wired into MCPChatWindow's send path via `OnSend` callback + `AttachScreenshot`. Before sending user message, `ChipContextResolver.ResolveAll()` translates each chip to plain text and inlines it. Reuses `SelectionSummary` + `ComponentSerializer` (DRY).

**Result:** Eliminates 1–3 `get_component` round-trips agents used to make on first turn with chipped objects. 12 NUnit EditMode tests green.

### Humanized Tool Card Rendering

Stream-json output from `claude -p` emits raw JSON tool cards. Chat parses and humanizes them to plain English:

**Raw:** `{"type":"tool_use","id":"t1","name":"set_property","input":{"path":"/Enemies/Boss","component":"Health","property":"value","value":"100"}}`

**Rendered:** `🔧 Editing /Enemies/Boss (Health.value = 100)`

Mapping in `ToolVerbMap.cs` (tool name → human action).

### Per-Backend Model Selector (v0.30.5)

**MCPChatWindow.Selector.cs** provides a dropdown menu for model selection with presets per backend. **Implementation:**

1. **Presets expanded (v0.30.5):** Per-backend model dropdown with hardcoded fallback presets per `BackendKind` (Claude, Codex, Gemini). Users can override via `Library/MCP_ChatBackendConfig.json` ModelPresets field. Custom model ID field always available.

2. **ModelPresets.cs (NEW)** — Extracted from BackendConfig.cs:
   - `ModelPresetEntry` (label, modelId)
   - `ModelPresetsConfig` (Claude[], Codex[], Gemini[])
   - `ModelPresetDefaults.All` — hardcoded fallback presets per BackendKind

3. **BackendConfigStore.GetPresetsForKind(BackendKind)** — Lookup presets in Library/MCP_ChatBackendConfig.json ModelPresets field; if not found, use hardcoded defaults. Allows users to override model lists without recompile.

4. **EditorPrefs persistence** — Selected model saved per backend (`MCPChat.SelectedModel.{Claude|Codex|Gemini}`). Rebuilt on backend switch.

5. **Custom field** — Typing an arbitrary model ID adds it to the dropdown (e.g., "claude-opus-4-8-123-custom").

**Why:** Codex reasoning (o3/o3-pro) requires explicit model selection (no default equivalents). Claude/Gemini update frequently; presets decouple model list from plugin version.

**Tests:** 44 BackendConfigStoreTests (preset lookup, fallback, config merge), 231 ModelSelectorTests (dropdown state, persistence, custom entry, backend switching).

### Drag-Drop GameObjects / Assets

- Drag a GameObject or asset into the chat input → creates a clickable "chip"
- Chip text: stable hierarchy path (e.g., `/Player/Sword`)
- Chip click: `PingObject(path)` + `SelectObject(path)` (Unity editor highlights the object)
- On scene change, chips invalidated (path refs are scene-relative)

### Auto-Include Selection Context (F4, plugin 0.7.0)

**SelectionSummary.cs** prepends the active GameObject's context to user messages. Format:

```
[Selection: /Path/To/GameObject (Component1, Component2, Component3)]

<user message>
```

Extracts top 3 non-Transform components; deduped against existing object-chip references. Result: Claude always knows what you're editing without explicit mention. Deferred rendering; chip paths persisted but not repainted after domain reload (UX-only; turn executes with correct context).

### Screenshot Attach

- Capture button → `MultiViewCapture` (4-panel: Front, Left, Top, Isometric)
- Attach screenshot to next user message
- Sends as base64-encoded binary in the stdin JSON turn

### Ask / Agent Mode Toggle

Two permission modes:
- **Ask** (`--permission-mode plan`) — tool calls require user acknowledgment before executing
- **Agent** (`--permission-mode acceptEdits`) — tool calls auto-execute with confirmation only on mutations

User can toggle mid-conversation via settings dropdown.

### Domain-Reload Safety & Turn Survival (F4, plugin 0.7.0)

### Reload Guard (ReloadGuard.cs)

When a turn is in-flight, prevents domain reload from interrupting by calling `EditorBuildSettingsScenes.LockReloadAssemblies()`. Lifecycle:

1. **On turn start:** Acquire lock via `LockReloadAssemblies()` (blocks Unity domain reload)
2. **Watchdog timer:** 120s countdown; if turn completes, unlock early. If timer fires, auto-unlock (fail-safe)
3. **On turn done:** Release lock immediately via `UnlockReloadAssemblies()`

Result: Domain reload queued during a turn waits until the turn finishes, so the chat session survives intact.

### Pending Turn State (PendingTurnState.cs)

Serializes in-flight turn state to `Library/MCP_ChatPendingTurn.txt` (plain-text pipe-delimited, base64-encoded payload). Format: `sessionId|turnId|requestJson_b64`. On `afterAssemblyReload`, the window's `OnEnable` reads the file and calls:

```csharp
ClaudeBackend.ResumeAsync(sessionId)  // via --resume <sessionId>
```

The CLI's `--resume` flag loads prior message history (via `load_session`) and continues the in-flight turn with the same context, picking up where it left off.

**Persistence:** Plain-text, survives recompilation and process restart. Cleaned up after resume or on window close.

### Sent Text Cache (SentTextCache.cs)

Tracks recently sent text (last 10 messages) to dedup against accumulated text during resume. Prevents duplicate context on reconnect.

### Orphan Process Cleanup

- Child `claude` process PID stored in `SessionState` (Editor-scoped serialization)
- On assembly reload (domain reload), cleanup task kills the PID via `Process.Kill()`
- Prevents zombie processes on recompilation or script reload

### Binary Resolution on macOS

**Problem:** Finder-launched Unity has a minimal PATH; `claude` binary may not be found.

**Solution:** Wrap the invocation in `/bin/zsh -lc`:

```csharp
var psi = new ProcessStartInfo
{
    FileName = "/bin/zsh",
    Arguments = "-lc 'claude -p --mcp-config ... > /tmp/claude.log 2>&1'",
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true
};
```

This ensures the child shell inherits the user's `.zshrc` PATH and finds `claude`.

## File Layout

```
unity-plugin/Editor/Chat/
├── CLI/                              # Backend logic (106 .cs files)
│   ├── IChatBackend.cs               # Backend interface
│   ├── CliBackendBase.cs             # Abstract host for CLI backends (4 axes)
│   ├── ClaudeBackend.cs, RelayBackend.cs, CodexAppServerBackend.cs, AntigravityBackend.cs
│   ├── BackendRegistry.cs            # Backend factory + enum
│   ├── ChatEvent.cs                  # Normalized event struct
│   ├── ChatBinaryResolver.cs         # Binary PATH resolution
│   ├── ClaudeArgBuilder.cs, CodexArgBuilder.cs  # Per-backend argv builders
│   ├── CodexAppServerParser.cs, RelayEventParser.cs  # Per-backend stream parsers
│   ├── ModelPresets.cs               # Per-backend model dropdown presets
│   ├── PendingTurnState.cs           # Domain-reload: persist in-flight turn state
│   ├── EditorStateSnapshot.cs        # Inject context block (scene, compile, errors)
│   ├── ChipContextResolver.cs        # Resolve object chips to plain text at 3 depths
│   ├── InlineChipModel.cs, InlineChipData.cs, ChipKindRegistry.cs  # Chip system
│   ├── BareNameNormalizer.cs, AtMentionNormalizer.cs  # Name normalization
│   ├── Mentions/                     # @mention system (7 files)
│   └── UnityMCP.Editor.Chat.CLI.asmdef
├── View/                             # UI rendering (50 .cs files)
│   ├── MCPChatWindow.cs + 19 partials (Drain, FlowBar, Send, Selector, Chips, etc.)
│   ├── MCPChatWindow.uss             # UIToolkit styling
│   ├── Annotation/                   # Screenshot annotation overlay (11 files)
│   ├── Markdown/                     # Markdown rendering + Mermaid (25 files)
│   ├── Preview/                      # Asset/object preview cards (14 files)
│   └── Viewers/                      # Specialized content viewers (11 files)
├── Tests/
│   ├── CLI/                          # Backend + chip logic tests (93 files)
│   │   ├── Helpers/                  # Test utilities (2 files)
│   │   └── Mentions/                 # @mention tests (7 files)
│   └── View/                         # UI rendering tests (112 files)
│       └── Helpers/                  # Test utilities (3 files)
└── [.meta files omitted]
```

## Enabling the Feature

### In MCPSettings Window

1. **Window > UnityMCP > Settings**
2. Scroll to **Agent Chat** section
3. Toggle **Enable Agent Chat** checkbox
4. Configure mode (Ask / Agent) and binary path (optional; auto-resolved on macOS)

## JSON-Only-at-Boundaries Principle

Internal models are C# **structs + plain text strings**. JSON appears ONLY at forced protocol boundaries:

- **stdin** — user turn envelope (JSON): `{"messages":[...], "attachments":[...]}`
- **stdout** — claude stream-json events (JSON): `{"type":"message_start",...}`
- **--mcp-config** — config file (JSON): defines MCP server
- **--permission-mode** — CLI arg (string): "plan" or "acceptEdits"

All intermediate parsing → plain C# objects (ChatEvent, ChatTranscript, ToolCard, etc.). Humanized output is plain text strings (`"🔧 Editing..."`), not re-encoded JSON.

**Token savings:**
- Omit JSON serialization inside Chat logic (→ no JsonConvert overhead)
- Humanize at parse time (→ one-pass JSON→text, not JSON→object→JSON)
- No intermediate JSON round-trips

## Testing

Chat module has 4 NUnit suites (EditMode only, no Live dependency):

- `ChatStreamParserTests` — Parse raw stream-json, emit ChatEvent structs
- `ClaudeArgBuilderTests` — Generate --mcp-config file + args
- `UserTurnBuilderTests` — Encode user messages → stdin JSON
- `ToolVerbMapTests` — Tool name → humanized text

Run via **Window > TextExecution > Test Runner** when `UNITY_INCLUDE_TESTS` is defined.

## Billing / Terms of Service

**Important:** Enabling MCP Chat spawns the **user's own** locally-installed `claude` CLI using **their own** logged-in Claude subscription. Usage, credits, and Anthropic Terms of Service are **between the user and Anthropic**. This feature does NOT proxy, cache, or share login credentials. Each user drives their own `claude` binary independently.

## Content Rendering

The Chat module includes an **extensible render subsystem** for displaying rich Markdown and Mermaid flowcharts in the transcript.

### Markdown Rendering

**Pipeline:** `string` (raw) → `MarkdownParser.Parse()` → `List<MdBlock>` → registry → `VisualElement` trees

- **MdBlock.cs** — Block model: enums `Heading`, `Paragraph`, `CodeFence`, `Mermaid`, `BulletList`, `OrderedList`, `BlockQuote`, `HorizontalRule`, `Table`, `Image` with metadata (Level, Lang, Lines, TableRows, Src/Alt).
- **MarkdownParser.cs + .Blocks.cs** — Single-pass string→blocks: fences parsed FIRST (lang==`mermaid` → Mermaid else CodeFence), `![alt](src)` standalone lines → Image blocks, table separator peek-ahead detection.
- **MarkdownInline.cs** — Rich-text escaping (angle-brackets FIRST, then inline markup): `**bold**`, `*italic*`, `` `code` ``, links `[text](url)` (renders text + dim URL), code-span protects inner stars.

**Renderers:**
- **MarkdownBlockRenderer** — dispatch 8 kinds (heading/paragraph/code/blockquote/rule/lists/table), partial files for table grid and bullet/ordered list layout
- **ImageBlockRenderer** — PNG/JPG paths/bytes → Texture2D, click opens via `EditorUtility.OpenWithDefaultApp`, textures freed on `DetachFromPanelEvent`

### Native Mermaid Flowchart Support

**Pure parse/layout stack (NO external library):**
- **MermaidGraph.cs** — POCO model: nodes (rect/round/diamond shapes), edges (with optional labels), direction (TD/LR/RL/BT)
- **MermaidParser.cs** — lines → graph or null (non-flowchart syntax → null); chained edges `A-->B-->C`, self-loops, labels non-greedy
- **MermaidLayout.cs + .Layers.cs** — Kahn topological sort + longest-path layering, pixel rects (float, no Vector2); cycle/self-loop guarded via visited-set cap; edge endpoints on node border not center. **Dynamic node sizing:** `MeasureNode(label)` calculates width from text lines + char-width estimate (fixes hardcoded 120px distortion). Bounds clamped (minW=60, maxW=280, minH=30, maxH=120) to prevent explosion on long text.
- **MermaidBlockRenderer** — `CanRender`= Mermaid kind; delegates to MermaidView; code-box fallback when TryBuild false
- **MermaidView.cs** — Absolute-positioned VE nodes + Label + edge overlay; **MANDATORY `edgeLayer.RegisterCallback<GeometryChangedEvent>(_ => edgeLayer.MarkDirtyRepaint())`** for edge redraws on resize
- **MermaidEdgePainter.cs** — Painter2D lines + arrowhead chevrons; no box-shadow, no transform (2021.3-safe)

### Extensible Registry Seam (Open/Closed Principle)

New content types = **1 new renderer file + 1 line in factory**, zero elsewhere edits.

- **IChatBlockRenderer.cs** — Interface: `bool CanRender(in MdBlock)`, `VisualElement Render(in MdBlock)`
- **ChatBlockRendererRegistry.cs** — Ordered, first-match-wins, Label fallback (never null)
- **ChatBlockRendererFactory.cs** — `CreateDefault()`: registers Mermaid + Image FIRST, MarkdownBlockRenderer LAST (catch-all)

**Future proof:** To add a 3D model preview renderer: (1) add `Model3D` to `MdBlockKind`, (2) parser maps fenced `lang=="unity-model"` → block, (3) new file `Model3DBlockRenderer : IChatBlockRenderer`, (4) one line in factory `reg.Register(new Model3DBlockRenderer())`. Done.

### Streaming → Finalize Strategy

Two-phase accumulation:
1. **Stream live** — plain text enters a Label (current behavior), accumulated into `_assistantRaw` StringBuilder
2. **Finalize on TurnDone** — `FinalizeAssistant()` clears live label, re-renders accumulated raw via `MarkdownParser.Parse()` + registry, replaces row children with rendered blocks

Called from `AppendUserBubble` + `AppendToolChip` so interrupted segments + text-between-tools each get their own bubble.

**Pinned invariant:** In `AppendOrExtendAssistant` null-branch: (1) `_assistantRaw.Clear()` FIRST, (2) create new row + label, (3) then (BOTH branches) append token. Raw is cleared exactly when a new live label begins.

### Texture Lifecycle

`ImageBlockRenderer`: `Texture2D` created from bytes → attached to `Image` VE → `DetachFromPanelEvent` callback destroys via `Object.DestroyImmediate()`. Eviction (first message dropped), finalize clears all children, OnDisable detaches all → callback fires for each texture.

### UX: Enter-to-Send + Removable Chips + Interactive Scene/Script Refs

- **EnterKeySend.cs** — Pure `Classify(KeyDownEvent)` → enum (Send/Newline/Ignore) + `InsertNewline(ref Caret)` logic (NUnit-testable); `Attach()` glue registers KeyDownEvent TrickleDown callback → Send calls `StopPropagation()` + `StopImmediatePropagation()` + `PreventDefault()` + onSend; Newline inserts `\n` at caret.
- **MCPChatWindow.Chips** partial — `AddObjChip(path)` + `CollectChipPaths()` → HashSet dedup; chip.userData=path; ✕ remove button = `_objChipStrip.Remove(chip)`. Ping moves to label on click.
- **Interactive Refs** — Chat messages can embed reference links via inline syntax `obj:/Path/To/Obj` or `script:Assets/MyScript.cs`. **ChatRefResolver** scans hierarchy at startup, **ChatRefAction** installs click/context-menu handlers (click=navigate+PingObject, Alt+click="Add to Context" → inject into input). LinkTag rendering (Unity rich-text `<link="obj:/...">`), hover tooltip, right-click menu with "Navigate" + "Add to context" options.
- **Tool-Call Grouping** — Multiple tool events from same tool call (e.g., 3 set_property on same object) group into 1 chip via ID tracking. Eliminates scatter when Claude chains mutations.
- **Copyable Text** — All transcript Labels have mouse selection enabled (drag select copies to clipboard). New CopyableText wrapper + CopyTextBuilder for multi-line copy blocks.

### Styling

**MCPChatWindow.uss** — ~156 lines appended: md-* classes (bubble, heading-1–6, code, code-fence, blockquote, hr, list-bullet, list-ordered, table, table-row, table-cell), mermaid-* (bubble, node-rect, node-round, node-diamond, edge-arrow), md-image + md-image-alt, obj-chip-remove. House palette: `#16161e/#1e1e2e/#2a2a44/#3a6aaa/#7aa2f7/#c0caf5/#d0d8ff`.

## Implementation Notes

### Why Spawn vs. Sidecar

- **No sidecar server needed** — reuses existing `unity_mcp.server` via the spawned CLI's MCP config
- **No API key exposure** — uses subscription auth from disk (logged-in CLI session)
- **Per-user isolation** — each Unity instance is independent
- **Natural upgrade path** — if user upgrades their `claude` CLI, MCP Chat auto-benefits

### macOS PATH Gotchas

- Finder-launched Unity has minimal PATH (e.g., `/usr/bin:/bin:/usr/sbin:/sbin`)
- `claude` binary typically installed in `/opt/homebrew/bin/claude` or user-local `~/.local/bin/claude`
- Solution: spawn via `/bin/zsh -lc 'claude ...'` to inherit user's shell config (`.zshrc`)
- Alternative: user can set `CLAUDE_PATH` env var in MCPSettings to override auto-resolution

### MCP Config Generation via ChatMcpConfigWriter (v0.36.0)

The `--mcp-config` file is auto-generated by `ChatMcpConfigWriter` at runtime, deriving the Python server path from the UPM package location. Resolution chain:

1. Probe `server/.venv/bin/python` (local venv)
2. Resolve absolute path via `uv` tool
3. Fall back to `python3` (system PATH)

**v0.36.0: Env block injection** — Config file now emits `"env":{"UNITY_MCP_PORT":"<port>"}` block (when chat port > 0). Python bridge reads this from the config env and uses it for initial connection, falling back to discovery files if needed.

**v0.55.0: External MCP Support** — OpenCode backend now merges 3rd-party MCP entries from global `~/.opencode/config.json` into the scoped config's "mcp" block. Non-Unity entries (detected by key filter) are injected additively; Unity entries are stripped to prevent conflicts. Future backends (Blender, etc.) follow the same pattern: scoped config + external merge.

Port discovery chain: no explicit port embedded in config itself (server self-discovers via `~/.unity-biome-mcp/ports/*.port`), but env var in config accelerates connection. The hardcoded `~/.claude/mcp.json` path is now a fallback only (used if config generation fails). This eliminates the need for manual setup in most cases.

### Prose-Fallback for Headless Chat (--disallowedTools AskUserQuestion)

**Problem:** The built-in `AskUserQuestion` tool auto-fails when Claude runs in headless stream-json mode (no stdin interactivity). The spawn writes JSON questions to the tool card, but Unity has no way to capture user input back through stdin within the stream. Response: timeout (~500ms), tool fails, context lost.

**Solution:** In `ClaudeArgBuilder`, add `--disallowedTools AskUserQuestion` to the CLI args. This tells Claude's built-in tool-use logic to skip the tool and instead respond with prose text describing what it would ask. Example:

```
Claude normally: [tool_use AskUserQuestion ("What color?")]
With disallowedTools: "What color would you like for the particle system? (I would ask you, but I can't do that in this mode.)"
```

**Result:** No tool-call failures, context-preserved prose question, user can paste answer into next input. Cost: ~200 tokens per question (prose vs. tool card), acceptable trade-off.

### Domain Reload Lifecycle

1. User edits a C# script in the Chat assembly or core
2. Unity detects domain reload, fires `[InitializeOnLoad]` finalizers
3. Chat's orphan-cleanup task reads PID from SessionState, calls `Process.Kill()`
4. Domain reload completes; Chat window re-initializes on next EditorApplication.update
5. User can start a new chat session

### Full-Path Chip Payload + "Show LLM payload" Inspector (Plugin v0.20.6)

Two paired changes guarantee the model receives full object/file paths and the UI can reveal the exact raw text of any sent turn.

**Full-path payload (`ChipTextInterleaver` + `AtMentionNormalizer`):**
- `ToLlmPayload`/`ToLlmText` emit each chip's `Path` (e.g. `@/Env/Player`) instead of its short `DisplayName` (`@Player`), falling back to `DisplayName` only for an orphan chip with an empty path. The display bubble still shows the short name via `ToDisplayText`.
- `AtMentionNormalizer` now builds match candidates from BOTH `DisplayName` and `Path`, sorted globally longest-first, so an echoed `@/UI Canvas/Main Camera` in the response wins over `@Main Camera` over `@Main`.
- **Reload-resume keeps the full path (task#10, plugin v0.20.7):** `DispatchTurn` caches the exact full-path `llmText` in `_sentLlmCache`; `SaveStateBeforeReload` persists it as `PendingTurnState.PendingLlmPayload` (v6 base64 header column) for in-flight saves only. `TryResumePendingTurn` re-sends `EditorStateSnapshot + PendingLlmPayload`, so a resumed turn carries the SAME full `@paths` + `[kind:path]` block as a fresh send. Pre-v6 blobs (no field) and idle saves fall back to `PendingText`; the serializer's `header.Length > 9` guard makes old blobs deserialize to `payload=""` with no crash. Idle-reload input restore is untouched.

**Always-raw inspector (`UserBubbleData` + `CopyableText`):**
- New `UserBubbleData { Display, Llm }` carries the bubble's short display text alongside the exact string sent to the model. User-bubble `userData` becomes a `UserBubbleData` whenever an `llmPayload` is threaded; it stays a bare `string` for the legacy null-payload path (assistant/tool bubbles are untouched).
- The sent-bubble right-click action is **"Show LLM payload"** (was "Show as text"); it logs `[MCP Chat] LLM payload:\n<raw>` reading `UserBubbleData.Llm`. **Copy** still returns `Display`.
- Payload is threaded for every turn type: fresh send / screenshot (`llmText`), compile-inject + approve (`displayText`, since sent == displayed), reload-resume (`sentText = EditorStateSnapshot + PendingLlmPayload`, the persisted full-path payload — see task#10 below — so the inspector reveals the state snapshot prefix AND the full @paths + `[kind:path]` block), and reload-restore (persisted `LlmPayload`). Backend-agnostic — identical for Claude and Codex.
- `TranscriptSerializer` persists a 4th base64 column `LlmPayload`; old 3-column blobs restore with `LlmPayload = null` (bare-string userData, no crash). Round-trip is idempotent.

## Known Limitations

- **ChipPath Repaint After Resume:** Object chips are persisted via `PendingTurnState` and restored after domain reload, but the chip strip UI is not repainted. The turn executes with correct context; the visual strip just shows stale paths until the next user message. This is a cosmetic UX issue; the actual turn data is correct.
- **MCPChatWindow Partials:** MCPChatWindow.cs (284 lines) plus 19 partial files (Drain, FlowBar, Send, Selector, Chips, etc.). Already well-split.

## Related

- **Core Architecture:** `AI/architecture.md` (CommandRouter, TCP bridge, tools catalog)
- **TCP Bridge:** `AI/tcp-bridge.md` (4-byte framing, heartbeat, SO_KEEPALIVE)
- **MCP Server:** `AI/mcp-server.md` (Python _UnstructuredMCP(FastMCP), structured_output=False on all tools, deferred schema loading, plugin system, tool gating)
- **Changelog:** `CHANGELOG.md` (feature timeline)
