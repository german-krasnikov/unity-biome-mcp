# Feature: Optional In-Unity Agent Chat

## Overview

An optional Editor window that brings agentic chat directly into Unity. A single C# `RelayBackend` talks to the Python `chat_relay.py` sidecar, which owns the selected local CLI process and reuses the existing MCP tools.

**Isolation:** Chat is split into `UnityMCP.Editor.Chat.CLI` and
`UnityMCP.Editor.Chat.View`. Both reference Core; Core does not reference Chat.

## Architecture

```
Unity Editor Window (MCPChatWindow)
    │
    └─ RelayBackend / RelayChatProcess
        │
        └─ TCP relay protocol
            │
            └─ Python chat_relay.py
                │
                └─ selected local CLI + unity-biome-mcp server
```

### Relay Lifecycle

Unity sends semantic `start`, `send`, `events`, `set_mode`, and `kill` commands to the relay. `server/src/unity_mcp/backend_def.py` owns backend-specific argv, environment, authentication, resume, and MCP configuration. Claude keeps one stdin-driven process; Codex, Kimi, Antigravity, and OpenCode are started per turn.

The canonical implementation description is `AI/architecture.md` under **Chat Relay System**. Do not add CLI-specific process logic to the Unity window or create another C# backend implementation.

### Module Isolation

**C# asmdefs:**
- `UnityMCP.Editor.Chat.CLI` references `UnityMCP.Editor` and the Wizard assembly.
- `UnityMCP.Editor.Chat.View` references Core and Chat.CLI.
- Both are Editor-only and `autoReferenced=false`.

**InternalsVisibleTo:**
- Core exposes internals separately to `UnityMCP.Editor.Chat.CLI` and
  `UnityMCP.Editor.Chat.View` in `AssemblyInfo.cs`.

**Settings Hook (Event-Driven):**
- Core invokes `ChatSettingsHook.OnBuildConnection` when building Chat Settings.
- `ChatSettingsSection` subscribes and contributes the settings controls.
- Preserves one-way dependency: core does not know Chat exists
- Removed the GUI code for Chat settings completely in core for clarity

## Multi-Backend Architecture

`RelayBackend` is the only C# chat backend implementation. `BackendRegistry` selects a backend ID and configuration, while Python `BackendDef` implementations own binary resolution, arguments, environment, resume behavior, and output format. `stream_transform.py` normalizes backend output to the relay pipe protocol before C# converts it to `ChatEvent` values.

To add a backend, extend the Python backend registry and transformer coverage, then expose its configuration through the existing provider/registry UI. Do not reintroduce `CliBackendBase`, `ClaudeBackend`, `CodexAppServerBackend`, or backend-specific parsers in C#.

## IChatBackend Abstraction

Single interface for pluggable chat backends:

```csharp
public interface IChatBackend
{
    bool IsRunning { get; }
    string SessionId { get; }
    void Start();
    void SendTurn(string turnJson);
    void DrainEvents(List<ChatEvent> output, List<ToolCallRecord> toolOutput = null);
    void Stop();
    void SendControlResponse(string json);
}
```

**Implementation:** `RelayBackend`. Backend-specific lifecycle remains in the Python relay.

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

`EditorStateSnapshot.cs` builds a lightweight context block for recovery after a
domain reload:

**Content:**
- Active scene name
- Compile status (OK, Compiling, Error)
- Console error count
- First 500 chars of scene hierarchy (with "…(truncated)" if longer)

**Injection:** During pending-turn recovery, `MCPChatWindow.Drain` prepends the
fresh snapshot before resending and caches the exact payload through
`SentTextCache`. Normal new turns do not receive this snapshot automatically.

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
3. Keeps Claude's active relay session when available and switches the UI to
   Agent-mode approval behavior
4. Auto-dispatches the prompt "Execute the plan above."

Files: `MCPChatWindow.Approve.cs` (event handler), `ApproveHelper.cs` (session management), `ApproveButtonFactory.cs` (button builder), `ChatTranscript.Append(VisualElement)` made internal.

**Result:** Claude preserves the relay session across the planning-to-execution
bridge. Per-turn backends can start Agent mode, but do not currently retain the
selected resume ID through deferred startup.

### Slash-Command Templates (F12, plugin 0.10.0)

Typing `/` in the composer opens a UIToolkit popup of 5 builtin templates: `/fix-compile`, `/add-component`, `/playtest`, `/inspect`, `/screenshot`. Selecting one resolves to plain composer text BEFORE send — a pure input transform with NO MCP coupling.

Files: `SlashTemplate.cs` (`[Flags] ContextGather` enum + readonly struct), `SlashRegistry.cs` (Builtins/Match/Resolve), `SlashPopup.cs` (UIToolkit popup, MaxVisible=5), `MCPChatWindow.Slash.cs` (SetupSlash wires ChangeEvent + KeyDownEvent on parent `_inputArea` at TrickleDown).

**Optional context-gather** (compile errors / selection / scene state / console) with graceful "(context unavailable)" fallback on throw. KeyDown handler on parent at TrickleDown ensures deterministic trickle-down order: Enter resolves template BEFORE `EnterKeySend` fires.

**Result:** Speed up common workflows with one keystroke; templates provide context automatically. 16 NUnit EditMode tests green. +44 lines MCPChatWindow.uss.

### Per-Turn Undo Rollback (F6, plugin 0.11.0)

`TurnUndoTracker.cs` + `RestoreButton.cs` wrap each agent turn in a named Unity Undo group. An amber **Restore** button appears after each turn and reverts that turn's scene mutations in one click (native Unity Undo, scene-only). Only the last turn's button is active; older buttons disable when a new turn starts. Resumed-after-domain-reload turns also get a group.

Files: `TurnUndoTracker.cs` (group lifecycle), `RestoreButton.cs` (button UI + revert logic), `MCPChatWindow.Undo.cs` (partial, split from MCPChatWindow.cs), `.chat-btn--restore` in `MCPChatWindow.uss`.

**Reusable Primitive:** Built on the public `UndoGroupHelper` core API (`OpenNamedGroup`, `CloseNamedGroup`, `RevertToBeforeGroup`, `CanRevert`). Batch Undo rollback reuses the same mechanism for Undo-recorded Unity changes.

**Tests:** 11 NUnit EditMode tests green (TurnUndoTrackerTests 9/9, RestoreButtonTests 2/2). Core `UndoGroupHelper` has 6 NUnit EditMode tests.

**Result:** Agents can now safely mutate scene state with instant undo per turn. 9 EditMode tests in Chat, 6 EditMode tests in Core.

### Inactivity Watchdog for Reasoning Models (v0.30.5, v0.36.0 timeout messaging)

**MCPChatWindow.Drain.cs** now monitors event silence to handle Codex reasoning models (o3, o3-pro) that think silently for 2–5 minutes. **Implementation:**

1. **`_lastEventTime`** — timestamp of the most recent drained event
2. **`InactivityTimeoutSec`** property — applies the saved timeout with a
   300-second minimum for Codex and a 30-second minimum for other backends
3. **DrainAndRender() watchdog check** — If no events for longer than timeout while backend is running, emit failure card with context hint, finalize turn, call `OnTurnFailed()` (resets undo group, unlocks reload)
4. **Resets:** `_lastEventTime` updated on every OnSend (turn start) and every event drain

**v0.36.0: Timeout Context Hint** — Failure message now includes the last tool name executed (tracked via `_lastToolName` in EventHandlers.cs when ToolStart event fires). Format: `[Timed out: no response for 300s (last tool: set_property)]`. Helps debug which operation was in-flight when timeout occurred.

**Dead-Process Guard (v0.36.0)** — If backend process unexpectedly exits mid-turn (detected via `OnProcessDead()`), appends `[Process exited]` to transcript and finalizes. Surfaces unexpected connection loss (vs. timeout) as distinct error. Also clears turn flags to unlock reload guard.

**Why:** Event silence is not treated as an immediate process death. Relay heartbeat events reset the watchdog without rendering anything, while the configurable inactivity timeout handles a genuinely stalled turn.

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

### Tool Card Rendering System

Stream-json output from backend CLIs emits raw JSON tool calls. Chat parses and renders them as specialized **tool cards** via the pluggable `IToolCardRenderer` registry:

**Architecture:**
- `ToolCardRendererRegistry` — provider pattern for tool-specific renderers
- `IToolCardRenderer` interface — `OnStart()`, `OnUpdate()`, and render lifecycle hooks
- `ChatTranscript` — dispatch tool calls to registry, render via the matched renderer

**Built-in Card Renderers:**

1. **CodeEditDiffRenderer** (Edit / Write / MultiEdit)
   - Inline diff per changed file (Myers algorithm with intra-line highlights)
   - Shows added/removed/modified code with unified diff coloring
   - Click to navigate to file or view full diff

2. **MutationDiffCard** (scene mutations: set_property, set_active, create_object, delete_object, manage_component, wire_event, etc.)
   - Object path → click to ping in Hierarchy
   - Before/after value (e.g., `health = 50 (was 100)`)
   - Summaries operations across multiple objects

3. **TaskChecklistCard** (TaskCreate / TaskUpdate)
   - Accumulated task list with checkbox per task
   - Task ID, subject, description, status (open/closed)
   - Click to mark complete or reopen

4. **AgentCard** (Agent tool delegation)
   - Subagent name, type, and task description
   - Shows whether invocation succeeded or failed
   - Nested tool results from the delegated work

**Fallback Rendering:**
Simple tool calls without a registered renderer display as humanized text via `ToolVerbMap.cs` (e.g., `🔧 Editing /Enemies/Boss (Health.value = 100)`).

**Extension:**
Plugins register custom renderers via `ToolCardRendererRegistry.Register()` to handle domain-specific tools.

### Model Thinking Blocks (v1.29.0)

When backends emit thinking blocks (e.g., Claude with `extended_thinking`), Chat captures and displays them as collapsed blocks with a live thinking timer:

**Python Side (stream_transform.py):**
- Detects `content_block_type=thinking` events
- Emits `th|<text>` pipe-protocol messages
- Accumulates thinking text until block completes

**C# Side (MCPChatWindow.HandleEvent):**
- ACP `thinking` events → creates `ThinkingBlock` with accumulated text
- Renders as collapsible Foldout: `▶ Reasoning…` (user can expand to read full text)
- Default state: collapsed
- Ephemeral: not persisted in transcript after domain reload

**Feature:** Keeps chat concise by default while preserving reasoning visibility for debugging or inspection. Independent of tool invocation — thinking blocks are rendered separately from tool cards and the final response.

### Tool Result Passthrough (v1.29.0)

Backends that return tool results (Claude via stdin streaming) now propagate them through Chat instead of losing them in the relay:

**Python Side (stream_transform.py):**
- Detects `content_block_type=tool_result` events
- Stores result ID, success/error flag, and text
- Emits `tr|<tool_use_id>|<ok>|<text>` pipe-protocol messages upon block completion

**C# Side (ChatTranscript):**
- Parses `tr|` events → creates `ToolResultBlock`
- Renders as a labeled section under the corresponding tool call card
- Shows result text (truncated to ~500 chars to avoid spam)
- Color-coded: green for success, red for error

**Availability:** Only Claude currently emits tool results. OpenCode format is unknown; Codex, Kimi, and Antigravity do not expose result events. Tool cards remain fully functional without results as they already show mutation summaries.

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

### Agent Mentions via @-Mentions (v1.29.0)

Users now delegate work to subagents by mentioning them in the chat input, instead of selecting from a footer dropdown:

**Architecture:**
- `AgentMentionSource` — scans ancestor `.claude/agents/` directories (nearest-first via `AgentSearchPath.Resolve`), then `{homeDir}/.claude/agents/*.md`. Walk order: project folder → parent → parent → … → filesystem root, stopping at first match per agent name.
- `AgentChipProvider` — renders agent mentions as chip kind in chat input
- `AgentMissDetector` — warns if a mention was typed but the Agent tool was never invoked
- System prompt injects instruction: `[agent:name]` syntax invokes the `Agent` tool with `subagent_type=name`

**User Experience:**
1. Type `@` in the chat input → mention popup shows available agents (name, type, description from `.md` header)
2. Select an agent → inserts `[agent:agent-name]` as a chip in the message
3. Message is sent with the mention
4. Backend model reads the mention in the system prompt and decides whether to invoke `Agent` tool
5. If mention was typed but Agent tool never called → warning chip shows "Agent @name was mentioned but not delegated"

**Mention Sorting:** `MentionConfig` in `BackendConfigStore` controls sort order (Relevance, Name, Type, Recency) and popup size (3–20 rows, default 8).

**Why Removed from Footer:** Agents are now treated as context (like object references) rather than a backend selection. Cleaner UI; agents can be mixed in the same request without switching a dropdown.

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
- **Ask** — permission prompts are rendered for explicit user approval.
- **Agent** — permission prompts are auto-approved by the Unity-side policy.

The relay maps the selected mode to backend-specific CLI arguments. Users can toggle mode mid-conversation without adding backend-specific logic to the window.

### Domain-Reload Safety & Turn Survival (F4, plugin 0.7.0)

### Reload Guard (ReloadGuard.cs)

When a turn is in-flight, prevents domain reload from interrupting by calling `EditorBuildSettingsScenes.LockReloadAssemblies()`. Lifecycle:

1. **On turn start:** Acquire lock via `LockReloadAssemblies()` (blocks Unity domain reload)
2. **Watchdog timer:** 120s countdown; if turn completes, unlock early. If timer fires, auto-unlock (fail-safe)
3. **On turn done:** Release lock immediately via `UnlockReloadAssemblies()`

Result: Domain reload queued during a turn waits until the turn finishes, so the chat session survives intact.

### Pending Turn State (PendingTurnState.cs)

Serializes in-flight turn state to `Library/MCP_ChatPendingTurn.txt` (plain-text pipe-delimited, base64-encoded payload). On `afterAssemblyReload`, the window restores the pending state and starts `RelayBackend` with the persisted session ID.

```csharp
new RelayBackend(backendId, mode, model, mcpPort, resumeSessionId: sessionId)
```

The Python backend definition consumes the session ID for Claude. For
non-stdin backends, deferred startup currently drops that ID before spawning the
per-turn process; do not rely on in-Chat resume for those backends.

**Persistence:** Plain-text, survives recompilation and process restart. Cleaned up after resume or on window close.

### Sent Text Cache (SentTextCache.cs)

Tracks recently sent text (last 10 messages) to dedup against accumulated text during resume. Prevents duplicate context on reconnect.

### Relay Process Lifecycle

- Relay PID and TCP port are stored in `SessionState`.
- A domain reload leaves the relay alive and reattaches the C# process handle afterward.
- Unity quit terminates the relay. Backend stop or session replacement terminates the current CLI process while leaving the relay sidecar available for reuse.

### Binary Resolution on macOS

**Problem:** Finder-launched Unity has a minimal PATH; `claude` binary may not be found.

**Solution:** The Python backend resolver checks the inherited PATH, then queries the user's login shell when needed. C# does not construct a backend-specific shell command.

## File Layout

```
unity-plugin/Editor/Chat/
├── CLI/                              # Relay protocol, backend configuration, shared chat models
│   ├── IChatBackend.cs               # Backend interface
│   ├── RelayBackend.cs               # Only C# backend implementation
│   ├── RelayChatProcess.cs, RelaySpawner.cs
│   ├── BackendRegistry.cs            # Backend factory + enum
│   ├── ChatEvent.cs                  # Normalized event struct
│   ├── ChatBinaryResolver.cs         # Binary PATH resolution
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
│   └── View/                         # UI rendering tests
│       └── Helpers/                  # Test utilities
└── [.meta files omitted]
```

## Enabling the Feature

Open **MCP > Chat**. Chat is compiled and available without an enable toggle.
Use **MCP > Settings > Chat Settings** for the inactivity timeout, model
selection, context-chip display, and extension-provided settings. Backend
binaries must be available on the login-shell `PATH`; stored binary overrides
are not forwarded by the current relay start request.

## JSON-Only-at-Boundaries Principle

Internal C# models are structs and plain text strings. JSON is limited to protocol boundaries:

- **Unity → relay** — command envelopes for start, send, control response, and mode changes
- **Backend → relay** — backend-native JSON or text, normalized in Python
- **Relay → Unity** — ACP events dispatched directly to `MCPChatWindow.HandleEvent()` with no parser
- **MCP configuration** — backend-specific JSON or TOML generated in Python

All Unity-side rendering uses `ChatEvent`, transcript models, and plain text rather than backend-native protocol objects.

**Protocol overhead:**
- Omit JSON serialization inside Chat logic (→ no JsonConvert overhead)
- Humanize at parse time (→ one-pass JSON→text, not JSON→object→JSON)
- No intermediate JSON round-trips

## Testing

Chat tests are split between `unity-plugin/Editor/Chat/Tests/CLI/` for relay protocol and shared models, and `unity-plugin/Editor/Chat/Tests/View/` for UI behavior. Python backend definitions, relay lifecycle, and stream transformers are covered under `server/tests/`.

Run via **Window > TextExecution > Test Runner** when `UNITY_INCLUDE_TESTS` is defined.

## Billing / Terms of Service

**Important:** MCP Chat runs the user's selected locally installed CLI with that provider's local authentication. Usage, credits, and terms remain between the user and the selected provider. The relay does not proxy or share login credentials.

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

### Why the Relay Sidecar

- **Domain-reload survival** — the Python relay owns the CLI process outside the Unity AppDomain.
- **One C# protocol** — Unity handles semantic commands and normalized events, not backend-specific output formats.
- **Backend isolation** — argv, authentication, environment, and resume behavior live in `backend_def.py`.
- **Reconnect safety** — the relay buffer preserves sequenced events across C# reconnects.

### macOS PATH Gotchas

- Finder-launched Unity has minimal PATH (e.g., `/usr/bin:/bin:/usr/sbin:/sbin`)
- `claude` binary typically installed in `/opt/homebrew/bin/claude` or user-local `~/.local/bin/claude`
- `BackendDef.resolve_binary()` checks the current PATH and then uses a login-shell lookup on macOS/Linux.
- Successful login-shell PATH resolution is cached; failed lookups are retried after a short TTL.
- Windows uses registry PATH entries and known user-local install directories.

### MCP Config Generation

The relay delegates scoped MCP configuration to `server/src/unity_mcp/mcp_config_writer.py`. Each `BackendDef` chooses the format and how `UNITY_MCP_PORT` is delivered. The Unity layer passes only semantic backend settings and the MCP port.

Do not duplicate backend-specific JSON/TOML or environment rules in C#.

### Interactive Questions and Permissions

The relay normalizes permission prompts and user questions into pipe-protocol events. `ToolApprovalCard` and `AskUserCard` collect the response in Unity, and `RelayBackend.SendControlResponse()` sends it back through the active relay session.

### Domain Reload Lifecycle

1. User edits a C# script in the Chat assembly or core
2. Unity detects domain reload, fires `[InitializeOnLoad]` finalizers
3. The Python relay and CLI process remain outside the Unity AppDomain
4. Domain reload completes; the Chat window reconnects to the relay
5. Pending session and event sequence state are restored

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
