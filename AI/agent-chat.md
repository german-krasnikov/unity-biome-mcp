# Feature: Optional In-Unity Agent Chat

## Overview

An optional Editor window that brings agentic chat directly into Unity, spawning the user's local `claude` CLI as a child process. Zero new MCP tools — reuses all 126 existing tools via the spawn-the-CLI architecture.

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
                    └─ ~126 MCP tools (create, set_property, screenshot, etc.)
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

1. **Presets expanded (v0.30.5):**
   - **Claude:** Default, Fable 5, Opus 4.8/4.7/4.6, Sonnet 4.6, Haiku 4.5, Custom...
   - **Codex:** Default, GPT-5.5, GPT-5.4/5.4-Mini, o3-pro, o3, o4-mini, GPT-4.1/4.1-Mini, Custom...
   - **Gemini:** Default, 3.5 Flash, 3.1 Pro Preview, 3 Pro Preview, 3 Flash Preview, 2.5 Pro, 2.5 Flash, 2.5 Flash Lite, Custom...

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

## UX Sprint Features (v0.15.0)

### Feature F1 — Token Counter Reset on Backend/Model Switch

`TokenResetTests.cs` ensures token counters reset when user switches backend (Claude → Codex) or selects a different model. Implemented in `MCPChatWindow.Selector.cs`:

```csharp
void SelectModelDropdown_OnChange(ChangeEvent<string> evt) {
    ResetTokenCounters();
    CreateBackendWithSession();
}
```

Result: No stale token carry-over across model changes.

### Feature F2 — Cascade Restore (Undo Earlier Turns)

`RestoreButton.cs` + `TurnUndoTracker.RestoreFromIndex()`: User can restore any earlier turn (not just the last one). Clicking Restore on turn 3 reverts turns 3, 4, 5 in reverse order (cascade rollback via sequential `UndoGroupHelper.RevertToBeforeGroup()`).

**New method:** `RestoreFromIndex(int turnIndex)` iterates from tail backward, reverting each turn's Undo group. Verified in TurnUndoTrackerTests (9/9 green).

### Feature F3 — Approve Button Shows Only for Real Tool Calls

`MCPChatWindow.Drain.cs` + `ApproveButtonFactory.cs`: The "Approve & Execute" button is injected only when a turn has real tool calls (`_turnHasToolCalls = true`). Turns with pure prose responses never show the button, eliminating UI clutter.

**Verification:** ApproveFlowTests check flag gating.

### Feature F4 — Hierarchy Refs Carry #instanceID for Disambiguation

`SelectionSummary.Summarize()` + `ChipContextResolver.ResolveOne()`: When a scene has duplicate object names (e.g., two "Enemy" GameObjects), the chip path now includes the Unity instance ID: `/Enemy #12345`. Enables Claude to distinguish them.

**Format:** `path #<instanceID>` (appended by ChipContextResolver at send-time). Verified in SelectionSummaryTests (path-only scene objects gain #ID markers).

### Feature F5 — Inline Removable Chips + Drag-Drop + Context Menu

`InlineChipModel.cs` + `InlineChipField.cs` + `ChipPillFactory.cs`: Type objects directly into the composer via drag-drop. Chips appear as removable pills (✕) in a leading flex-row before the TextField (v0.16.0 refactored from overlay stack).

**Composed field** (`InlineChipField`): Pills are real layout children (flex-row), not overlays. No pixel-positioning bugs, no NBSP markers. Backspace-at-caret-0 removes last chip (atomic).

**Drag-drop routing** (`Chips.cs` OnDragPerform): Drops on field area add chips.

**Context menu** (`InlineChipField` + right-click on pill): "Add Selection to Context" inserts chip, "Show LLM Payload" reveals send-path format, "Remove" deletes chip.

**Model logic** (`InlineChipModel.Add/Remove/Clear`): Headless data layer. Pill rendering delegated to `ChipPillFactory` (shared with response pills, v0.16.0 P7).

Verified: InlineChipModelTests 11/11, InlineChipFieldTests 7/7, visual/interactive paths compile clean.

### Feature F6 — Auto-Scroll Toggle

`MCPChatWindow.Drain.cs`: EditorPref gate for auto-scroll behavior. Default ON. When OFF, streaming messages do not auto-scroll; user can read top of transcript while turn completes.

**Wired in:** `Drain()` loop checks `EditorPrefs.GetBool(PrefKey.AutoScroll, true)` before calling `ScrollViewMode.Scroll()`.

### Feature F7 — Status Panel Distinguishes CLI-Listening vs Chat-Active

`ChatBackendProbe.cs` (reflection-based, domain-reload safe): Detects if chat backend is running via reflection on `MCPChatWindow.s_instance`. `MCPStatusModel.GetState()` now returns 3-state enum:
- Down (no server)
- Listen (TCP running, no chat)
- ChatActive (Chat window running)

**Reflection:** `Type.GetProperty("IsRunning")` on Chat assembly (if loaded). Domain-reload safe: re-queried per call, no static cache.

Verified: MCPStatusModelTests include ChatActive state transitions.

### Feature F8 — Remove "(Beta)" Labels

`MCPSettingsUI.cs` + `ChatSettingsSection.cs`: Removed "(Beta)" from:
- Chat toggle button in MCPSettings
- Chat settings foldout header

Result: UI looks shipping-ready.

### Feature F9 — Per-Backend Settings Form → Own JSON → CLI Args

`BackendConfig.cs` + `BackendConfigStore.cs` + `BackendSettingsForm.cs`:

**Settings form** (UIToolkit dropdowns per backend):
- Claude: model (Opus, Sonnet, Haiku), permission mode (plan, acceptEdits), timeout, extra args
- Codex: same axes

**Persistence:** Writes to `Library/MCP_ChatBackendConfig.json` (project-local, NOT ~/.codex/config.toml or ~/.mcp.json). Format:
```json
{
  "claude": { "model": "opus-4-1", "permission_mode": "acceptEdits", "extra_args": "--verbose" },
  "codex": { "model": "default", "permission_mode": "plan" }
}
```

**Arg wiring:** `ClaudeArgBuilder.BuildArgs()` + `CodexArgBuilder.BuildArgs()` read from config and inject into argv (e.g., `--model=<model>`, extra args split on whitespace).

**DRY:** `ArgTokenizer.cs` (new, shell-style quote-aware split) centralizes whitespace+quote parsing for both builders. +11 tests.

Verified: BackendConfigStoreTests 10/10, BackendSettingsFormTests integration passes.

### Feature F10 — Typed Context Tags (Kind-Aware Chips)

`ChipKindDetector.cs` + `ChipData.Kind` + `ChipConfig.cs` + `ResponseTagInliner.cs`:

**Send-side (input):**
- Each chip carries a `ChipKind` (Hierarchy, Scene, Script, Prefab, Material, Texture, ScriptableObject, Asset)
- AI-facing format: `[hierarchy:/Player #123]`, `[script:PlayerController]`, `[scene:.../Main.unity]`
- Depth configurable per kind (none|path|summary|full, stored in BackendConfigStore.ChipConfig)
- Chips display left-side color-coded kind prefix (visual feedback)

**Receive-side (response):**
- `ResponseTagTokenizer.Tokenize()` parses `[kind:ref]`, `⟦kind:ref⟧` fences, and bare file paths registered via `IChipKindProvider.BarePathExtensions` (no hardcoded image regex)
- `MixedParagraphRenderer` renders text runs as Labels and tags/bare-paths as colored pills
- Pills support single-click inline preview and double-click navigation via the provider registry
- Wired into `MarkdownInline` between escape and bold/italic

**Classes:**
- `ChipKindDetector.Detect()` → ChipKind (pure, reflection-based hierarchy vs scene discrimination)
- `ChipContextResolver.EmitTyped()` + `ResolveAllTyped()` — send-time API
- `ResponseTagTokenizer` — response-time tokenizer
- `MixedParagraphRenderer` / `ChipClickRouter` / `ChipInlinePreviewPanel` — rendering + interaction

Verified: ChipKindDetector 13/13, ResponseTagInliner 17/17 (false-positive guards), EmitTyped 7/7, ChipConfig 3/3.

### Feature F11 — Inline Chips + Extensible Chip-Kind Registry (v0.15.8)

#### Extensibility: IChipKindProvider & ChipKindRegistry

**Core Innovation:** Third-party plugins (in separate asmdefs referencing `UnityMCP.Editor.Chat`) register custom chip kinds via the public `ChipKindRegistry` with ZERO core edits.

**Public Interface:**
```csharp
public interface IChipKindProvider
{
    string Key { get; }                    // Unique lowercase key, e.g. "hierarchy"
    int Priority { get; }                  // Lower = checked first
    bool CanHandle(Object obj, string assetPath);
    ChipData Create(Object obj, string assetPath);
    string IconName { get; }               // EditorGUIUtility.IconContent key
    string HexColor { get; }               // Pill + response tag color
    string FormatPayload(ChipData chip, ChipPayloadContext ctx);
    string DefaultDepth { get; }           // Fallback when no config entry
    string[] BarePathExtensions { get; }   // Extensions for bare-path recognition in responses (e.g., [".png"])
    void Navigate(string reference);       // Handle click on chip link (open, select)
    void Ping(string reference);           // Highlight/ping object (inline preview trigger)
    VisualElement BuildPreview(string path); // Build inline preview element (return null if none)
}
```

**Public Registry:**
```csharp
public static class ChipKindRegistry
{
    public static bool Register(IChipKindProvider p);
    public static bool Unregister(string key);
    public static IChipKindProvider Resolve(Object obj, string assetPath);
    public static IChipKindProvider ForKey(string key);
}
```

**Built-in Providers (8 total, Priority 100–800):**
- HierarchyChipProvider (100): GameObjects not in assets
- SceneChipProvider (200): .unity scene files
- ScriptChipProvider (300): MonoScript C# files
- PrefabChipProvider (400): .prefab files
- MaterialChipProvider (500): .mat material files
- TextureChipProvider (600): .png/.jpg image files
- ScriptableObjectChipProvider (700): .asset SO files
- AssetChipProvider (800): generic fallback for unlisted asset types

**New Methods (v0.36.0+):**
- **`BarePathExtensions`** — Array of file extensions recognized as bare-path references in assistant responses (e.g., `[".png", ".jpg"]`). Used by `ResponseTagTokenizer` to link bare file paths like "image.png" without markup.
- **`Ping(reference)`** — Highlight/ping the referenced object (flash in Hierarchy, select in Inspector). Called when inline preview panel first opens. Distinct from `Navigate` which may open a dedicated viewer.
- **`BuildPreview(string path)`** — Build a `VisualElement` for inline preview display. Return null if this kind has no visual preview. Called lazily when the preview panel opens; preview builders are wired via seam in `AssetChipProviderBase`.

**Priority Convention:**
- <100: Plugin providers override a built-in type
- 100–800: Built-ins (default)
- >800: Plugin providers extend (new kinds)

**Reload Survival (PendingTurnState v4):** Serializes `KindKeys[]` parallel to chip paths; on resume, re-binds by key. Falls back to re-detection if provider not yet registered.

#### Inline Rendering at Cursor

**Positioning (UitkCharRect.cs):** Uses PUBLIC `TextField.textSelection.GetCursorPositionFromStringIndex` API — confirmed working live on Unity 6000.3.0b7. H10 degradation: if API unavailable, falls back to row-layout strip (current behavior).

**Width Reservation (NbspReservation.cs):** Reserves pill width via U+FFFC marker + N×U+00A0 (non-breaking spaces), ensuring layout won't reflow when pill moves.

**Atomic Caret (TokenSpan.cs):** Caret skips whole chips (never lands mid-pill). Backspace on chip deletes entire chip (not character-by-character). Press arrow → moves caret before/after chip boundary.

**"Show LLM Payload" Context Menu:** Right-click on chip → reveals exact byte-for-byte payload sent to AI (symmetry test enforces match).

**Breaking Change — BUG B:** `ChipConfig` default depth `"summary"` → `"path"` (token-minimal). Restore via F9 settings form (per-kind dropdown). Marked in-code: `// BREAKING (H15)`.

#### Test Coverage

- **ChipKindRegistryTests:** Register, Unregister, Resolve, ForKey, priority ordering, version bumping
- **ChipKindRegistryPipelineTests:** End-to-end: detect → resolve → format → render
- **NbspReservationTests:** Width prediction, marker insertion/cleanup
- **TokenSpanTests:** Atomic caret boundaries, backspace/arrow behavior
- **UitkCharRectProbeTests:** Positioning API availability detection, H10 fallback
- **Wave4ChipInputTests:** Integration: drag-drop, context menu, serialization

All suites: 100% pass, zero new failures (5 pre-existing reds unrelated to F11).

### Review-Hardening Pass (v0.14.6)

**ArgTokenizer Quote-Awareness:** Fixes silent corruption of quoted multi-word ExtraArgs values (e.g., `--append-system-prompt "be terse"`). Shell-style: double+single quotes, unbalanced trailing tolerated. DRY across both Claude/CodexArgBuilder. +11 tests.

**ChatBackendProbe Reload-Safety:** Drops stale static MethodInfo cache; resolved per-call so status stays correct across domain reloads (was wrongly showing Listen when Chat was active).

**Dedup BackendConfigStore.Load():** MCPChatWindow.OnSend/AttachScreenshot now load store once and thread into AppendChipContext (lazy ??= fallback), avoiding double file-read+parse per turn.

### Feature F12 — Chip UX Overhaul (v0.16.0)

**Five production-ready pieces shipped together, resolving seven user problems (P1–P7):**

#### P1+P2: Composed Inline-Chip Field (Overlay Stack Deleted)

Replaced 466-line overlay architecture (InlineChipOverlay, NbspReservation, UitkCharRect, TokenSpan) with a simple **composed `InlineChipField`** — a flex-row VisualElement with pill children + trailing TextField.

**Why composed > overlay:**
- Pills are layout children, not overlays → never mis-position, never vanish on typing (P1+P2 solved by construction)
- No pixel-chasing, no NBSP markers, no coordinate-space drift
- Backspace-at-caret-0 removes last chip (atomic, standard tag-input UX)

**New classes:**
- `InlineChipModel` — Pure headless data (add/remove/clear/serialize/restore). Fully unit-testable. No Unity rendering dependency.
- `ChipPillFactory` — Static factory builds pills from registry. Shared by input field + response rendering (P7).
- `InlineChipField` — Composed VisualElement control. Flex-row of pill children + trailing TextField. Backspace-at-0 handler, context menu per pill.

#### P3+P5: Removed Legacy Auto-Selection

Deleted auto-prepend of `SelectionSummary` in send path. Context now flows **exclusively through explicit typed chips**. Prevents duplicate/verbose context. `SelectionSummary` class kept for depth="summary" resolution in chip context resolver.

#### P4: Per-Kind Chip Display Settings (Depth + Color)

Registry-driven settings form enumerates all registered kinds (built-in + 3rd-party plugins) dynamically. Each kind has:
- **Depth dropdown:** none/path/summary/full (LLM payload customization)
- **Color field:** Graphical pill color override

**Classes:**
- `ChipDisplayOverride` struct — Per-kind overrides (depth + hex color). Null fields = use provider default.
- `ChipConfig` — Extended with parallel arrays (`OverrideKeys[]`, `OverrideDepths[]`, `OverrideColors[]`) for 3rd-party kinds. Maintains backward compat with legacy explicit fields.
- `ChipPillFactory.ColorResolver` — Static `Func<string, string>` seam. Set once on window open, consulted by both input + response pills. Live-updated on settings save.

**Resolution order (per-kind):** Override > Legacy field > Provider default. No hardcoded kind lists, no switches.

#### P7: Response Scene-Object Pills (MixedParagraphRenderer)

Response-side `[kind:ref]` tags now render as graphical pills (leaf name, click→ping/select, tooltip=full ref) in paragraphs and lists — identical to input pills.

**Classes:**
- `ResponseTagTokenizer.Tokenize()` — Returns ordered tokens (text / `[kind:ref]` / bare paths). Uses `IChipKindProvider.BarePathExtensions` for extension detection; no hardcoded image regex.
- `RefParser` — Inverse of `ChipContextResolver.FormatChipRef`. Delegates hierarchy identity parsing to `HierarchyReference` (` #id`, ` @globalObjectId`, path).
- `HierarchyReference` + `HierarchyResolver` — Value object + resolver for scene object refs (survives reparent/rename via GlobalObjectId).
- `MixedParagraphRenderer.Render()` — Flex-row container with Labels (text runs via MarkdownInline.ToRichText) + ChipPillFactory pills (response mode: no remove button, single-click preview, double-click navigate). Tooltip = full ref.
- `ChipInlinePreviewPanel` + `PreviewBuilderRegistry` — Lazy inline previews per kind with cancellation.

**Side benefit:** Hierarchy refs now preserve ` #id` / ` @globalObjectId`; `HierarchyChipProvider.Navigate` resolves via `HierarchyResolver` instead of naive path matching.

#### P6: New-Session / Clear Button

Dropdown button with confirm dialog. Clicking "Clear" tears down the current chat:
1. Kill + restart the backend (fresh `CreateBackend()` with new `EditorStateSnapshot` + `SessionId=null` → next turn has no `--resume`)
2. Clear transcript + input + inline chips
3. Call `ReloadGuard.ClearPendingState()` so domain-reload can't resurrect old turn state
4. Reset per-session window state (sent-text cache, activity, token counters, turn flags)

**New class:** `MCPChatWindow.Session.cs` partial.

#### Test Coverage & Metrics

- **New test suites:** InlineChipModelTests, InlineChipFieldTests, ChipPillFactoryTests, ChipDisplayOverrideTests, ResponseTagInlinerTests, MixedParagraphRendererTests, NewSessionTests
- **Test count:** 1581/1586 EditMode pass (5 pre-existing reds, 0 CS errors)
- **Code delta:** −806 net lines (overlay stack deleted), +23 new tests
- **Breaking change:** `ChipConfig` default depth `"summary"` → `"path"` (token-minimal). Users restore via F9 settings form. Marked in-code: `// BREAKING (v0.16.0)`.

#### Package Version Change

`package.json` unity min bumped **2022.3 → 6000.0**. Rationale: The editor is already running 6000.3.0b7; the old minimum was a lie. Per META mandate: "If a limitation forces raising the Unity minimum to 6.0, DO IT." Migration cost: one line. Risk: Users on 2022.3 lose access — but they never had the full chip feature anyway (text APIs differ).

### Feature F13 — Inline Context Chips + Auto-Linking (v0.17.1)

**Three production-ready UX improvements shipped together (P1–P3):**

#### P1: Consolidated Chip Input (Removed _objChipStrip Dual-Path)

F12 left a legacy path (`_objChipStrip`) for backward-compat; F13 removes it entirely. All context chips now route **exclusively through `InlineChipField` + `InlineChipModel`**:

**Architecture:**
- `InlineChipModel` — Pure headless data (add/remove/clear/serialize/restore). No UI deps.
- `AppendChipContext()` — Single source of truth: calls `m.SerializePayload()` directly (removed legacy `AddChip` branch)
- `MCPChatWindow.cs` — Removed `_objChipStrip` field + `ClearChips()` call in Session cleanup

**Files modified:**
- `MCPChatWindow.Chips.cs` — Removed `AddChip()`, `CollectChipData()` methods (71 lines deleted). Drag-drop now adds directly to model.
- `MCPChatWindow.Send.cs` — Single `AppendChipContext()` call (−17 lines net)
- `MCPChatWindow.cs`, `MCPChatWindow.AutoHeight.cs`, `MCPChatWindow.Session.cs` — Wired to use `_chipField.Model.Count` instead of `_objChipStrip.Count`

**Tests:** ChipConsolidationTests (3 cases) — verify SerializePayload format matches send-path contract.

#### P2: Rich User Bubbles (User-Sent Chips as Pills)

User-sent messages now render `[kind:ref]` tags as clickable pills — identical visual/interactive style to AI-sent response pills.

**Mechanism:**
- `ChatTranscript.cs` — calls `MixedParagraphRenderer.InlineElement()` on user bubble text
- `MixedParagraphRenderer.InlineElement()` — splits text on `ResponseTagInliner` regex, builds mixed container (text labels + inline pill children)
- Pills have **no remove button** (read-only, user-approved), click → navigate via chip provider

**Symmetry:** User bubbles now render via the same code path as AI responses (P7 from F12).

**Tests:** UserBubblePillTests (4 cases) — plain text, single tag, mixed content, empty text.

#### P3: Unified Chat Reference Rendering (v0.20.0 Phase 1 consolidation)

AI responses can now mention scene object names (e.g., "see Player1 here") and bare file paths (e.g., "saved to img.png"). They route through ONE unified rendering path: bare name / bare path → normalization → `ResponseTagTokenizer` → `MixedParagraphRenderer` → `ChipPillFactory` pill.

**Legacy path deleted (v0.20.0):** Removed the secondary SceneNameLinker.Linkify path which was wrapping refs as `<link><u>Name</u></link>` at the static mutable `MarkdownInline.Linker` seam. This divergence caused dual rendering and inconsistent state. The unified path is now enforced:

1. **Normalization stage (BareNameNormalizer):** Scans LLM output text and converts bare scene object names to `[kind:ref]` bracket tags. Filters aggressively (length ≥3, skips generics, requires signature traits: digits/underscores/consecutive uppercase). Protects existing `[kind:ref]` tags and triple-backtick fenced code blocks. **Bare file paths are not normalized here** — they are recognized later by the tokenizer via `IChipKindProvider.BarePathExtensions`.

2. **Response rendering stage (ResponseTagTokenizer → MixedParagraph → ChipPillFactory):** Tokenizes text, `[kind:ref]` tags, `⟦kind:ref⟧` fences, and bare file paths into a shared model, then renders graphical pills with single-click preview, double-click navigate, tooltips, and colors per kind.

**Kill-switch:** `MCPChat.DisableSceneNameNorm` allows disabling normalization if needed (e.g., for custom name-linking logic in plugins).

**Integration:**
- `BareNameNormalizer.cs` — Converts bare scene object names to `[kind:ref]` format
- `ResponseTagTokenizer.cs` — Tokenizes `[kind:ref]`, `⟦kind:ref⟧` fences, and bare file paths
- `MixedParagraphRenderer.cs` — Renders tokens as labels/pills
- `ChatRefResolver.cs` — Exposed `Objects` property (read-only dict) for name lookup
- `MCPChatWindow.cs` — Calls RefreshResolver (renamed from RefreshLinker) before FinalizeAssistant in Drain TurnDone

**Tests:** BareNameNormalizerTests (fenced-block protection, edge cases), NormalizationPipelineTests (7 cases verifying unified path), ResponseTagInlinerTests (false-positive guards).

#### Test Coverage & Metrics

- **New test suites:** UserBubblePillTests (4), ChipConsolidationTests (3), NormalizationPipelineTests (7)
- **Test count:** 1600+/1605 EditMode pass (5 pre-existing reds, 0 new failures, 20 new tests)
- **Code delta:** −97 net lines (removed SceneNameLinker path), +120 insertions (unified path + tests)
- **plugin version:** 0.17.0 → 0.17.1 → 0.20.0

### Feature F14 — Inline @DisplayName Insertion + Chip Pill Strip in Bubbles (v0.17.2)

#### P1: Inline @DisplayName Insertion at Cursor

`MCPChatWindow.InlineChips.cs` — `InsertInlineChip()` now captures the cursor position in the TextField and inserts `@DisplayName` directly at that caret point. Chip references flow naturally inline with user text.

**Behavior:**
- "Add Selection to Context" (context menu) captures cursor index
- Inserts @DisplayName at caret → "analyze @Player and compare to @Enemy"
- Chips appear as pills in the `_pillRow` above text (visual affordance)
- Cursor clamped to prevent edge-case `ArgumentOutOfRangeException`

**Files modified:** `MCPChatWindow.InlineChips.cs` (cursor capture + insertion logic)

#### P2: Chip Pill Strip in Sent User Bubbles

`MCPChatWindow.Send.cs` + `ChatTranscript.cs` — Send path now splits the user's text into two representations:

- **rawText** — Display text with @names (e.g., "analyze @Player and compare to @Enemy")
- **llmText** — LLM text with [kind:ref] tags (e.g., "analyze [hierarchy:/Player] and compare to [hierarchy:/Enemy]")

Chip snapshot is copied before TextField clear, then passed to `AppendUserBubble()` which renders a `.user-chip-strip` row of visual pill elements above the message text. Pills are read-only (user-approved) and symmetric with AI-sent response pills.

**Files modified:**
- `MCPChatWindow.Send.cs` — Split rawText/llmText, snapshot chips, pass to bubble
- `ChatTranscript.cs` — Accept chips param, render .user-chip-strip row
- `MCPChatWindow.Drain.cs` — Restore path also passes chip snapshot (defensive copy)

**Restore path:** Resumed turns also get chip snapshot (defensive copy), preserving visual feedback on domain reload.

#### Test Coverage & Metrics

- **New test suites:** UserBubblePillTests +3 additional pill strip rendering tests (UserBubbleChipStripTests)
- **Test count:** 1562+/1567 EditMode pass (5 pre-existing reds, 0 new failures)
- **Code delta:** +107 lines (inline insertion + pill strip rendering)
- **plugin version:** 0.17.1 → 0.17.2

**Note:** This release also includes two bugfixes from earlier in the same version sprint:
- **BUG1:** InlineChipField layout changed flex-row → flex-column with dedicated _pillRow; USS align-items: center → stretch
- **BUG2:** SaveStateBeforeReload() no longer skips ActivityPhase.Idle; chips survive domain reload even in idle state

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
unity-plugin/Editor/
├── AssemblyInfo.cs                   # [assembly: InternalsVisibleTo("UnityMCP.Editor.Chat")]
├── Chat.meta                         # Meta for Chat/ folder
├── Chat/
│   ├── ChatEvent.cs                  # Normalized event struct
│   ├── ChatStreamParser.cs           # Parse stream-json from stdout
│   ├── ClaudeArgBuilder.cs           # Build --mcp-config JSON + --permission-mode
│   ├── UserTurnBuilder.cs            # Encode user message → stdin JSON
│   ├── ToolVerbMap.cs                # Tool name → humanized action
│   ├── IChatBackend.cs               # Backend interface
│   ├── ChatBinaryResolver.cs         # Binary PATH resolution
│   ├── ChatProcess.cs                # Process lifecycle manager
│   ├── ClaudeBackend.cs              # Implementation: spawns claude CLI
│   ├── ChatTranscript.cs             # In-memory message history
│   ├── MCPChatWindow.cs              # EditorWindow UI + interaction
│   ├── MCPChatWindow.Drain.cs        # Partial: message accumulation
│   ├── MCPChatWindow.FlowBar.cs      # Partial: activity indicator animation
│   ├── MCPChatWindow.uss             # UIToolkit styling
│   ├── ChatSettingsSection.cs        # Settings foldout in MCPSettings
│   ├── CliBackendBase.cs             # Abstract host for CLI backends (4 axes)
│   ├── CodexArgBuilder.cs            # Codex: argv + env-key-strip builder
│   ├── CodexAppServerParser.cs       # Codex: JSON-RPC 2.0 → ChatEvent
│   ├── CodexAppServerBackend.cs      # Codex: IChatBackend implementation (persistent JSON-RPC)
│   ├── BackendRegistry.cs            # Backend factory + enum
│   ├── ReloadGuard.cs                # Domain-reload: lock + unlock mechanism
│   ├── PendingTurnState.cs           # Domain-reload: persist in-flight turn state (v3: BackendKind)
│   ├── SelectionSummary.cs           # Auto-Selection: prepend active GameObject context
│   ├── SentTextCache.cs              # Domain-reload: track sent text for dedup
│   ├── CompileAutoFix.cs             # Auto-retry on compile failures (MAX_RETRIES=3)
│   ├── EditorStateSnapshot.cs        # Inject context block (scene, compile, errors)
│   ├── ToolPing.cs                   # Flash object on tool-call completion
│   ├── ChipContextResolver.cs        # Resolve object chips to plain text at 3 depths
│   ├── MCPChatWindow.Approve.cs      # Event handler: Approve & Execute button
│   ├── ApproveHelper.cs              # Session management: resume, mode flip
│   ├── ApproveButtonFactory.cs       # Button builder: humanized button UI
│   ├── SlashTemplate.cs              # Template model: enum ContextGather + struct
│   ├── SlashRegistry.cs              # Template registry: Builtins, Match, Resolve
│   ├── SlashPopup.cs                 # UIToolkit popup: 5 visible, arrow nav
│   ├── MCPChatWindow.Slash.cs        # Slash setup: KeyDown + ChangeEvent on parent
│   ├── BareNameNormalizer.cs         # Converts bare scene names in LLM output to [kind:ref] tags
│   ├── UnityMCP.Editor.Chat.asmdef   # Assembly definition (references Core)
│   └── Tests/
│       ├── ChatStreamParserTests.cs
│       ├── ClaudeArgBuilderTests.cs
│       ├── UserTurnBuilderTests.cs
│       ├── ToolVerbMapTests.cs
│       ├── CliBackendBaseTests.cs              # Tests base lifecycle + 4-axis dispatch
│       ├── CodexArgBuilderTests.cs             # Tests argv construction + env-key-strip
│       ├── CodexAppServerParserTests.cs        # Tests Codex JSON-RPC → ChatEvent (15+ cases)
│       ├── ReloadGuardTests.cs
│       ├── PendingTurnStateTests.cs            # Tests v3 header + BackendKind persistence
│       ├── SelectionSummaryTests.cs
│       ├── SentTextCacheTests.cs
│       ├── ApproveFlowTests.cs
│       ├── SlashRegistryTests.cs
│       ├── SlashPopupTests.cs
│       ├── UserBubblePillTests.cs             # User bubbles render [kind:ref] chips as pills (4 cases)
│       ├── ChipConsolidationTests.cs          # Verify chip serialization format (3 cases)
│       ├── NormalizationPipelineTests.cs      # Unified bare-name → [kind:ref] → pill pipeline (7 cases)
│       ├── BareNameNormalizerTests.cs         # Bare name detection + fenced-code protection
│       └── UnityMCP.Editor.Chat.Tests.asmdef
├── ChatSettingsHook.cs               # Event hook for settings updates
├── MCPSettingsUI.cs                  # Modified: fires ChatSettingsHook.Invoke
└── [other core files]
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
- **MCPChatWindow Line Count:** At 185 lines (approaching the 200-line ceiling), the file may need splitting into more partials if significant features are added.

## Related

- **Core Architecture:** `AI/architecture.md` (CommandRouter, TCP bridge, tools catalog)
- **TCP Bridge:** `AI/tcp-bridge.md` (4-byte framing, heartbeat, SO_KEEPALIVE)
- **MCP Server:** `AI/mcp-server.md` (Python _UnstructuredMCP(FastMCP), structured_output=False on all tools, deferred schema loading, plugin system, tool gating)
- **Changelog:** `CHANGELOG.md` (feature timeline)
