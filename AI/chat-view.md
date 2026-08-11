# Chat View Architecture

In-Unity MCP Chat window: partial `MCPChatWindow`, Markdown-to-UIElements rendering, chip system for scene references, annotation overlays, and a unified relay backend.

## File Organization

### CLI Layer (Backend Abstraction)
- `Chat/CLI/IChatBackend.cs` — Backend interface used by the window
- `Chat/CLI/RelayBackend.cs` — Only C# backend implementation
- `Chat/CLI/RelayChatProcess.cs`, `RelaySpawner.cs`, `RelayEventParser.cs` — Sidecar connection, lifecycle, and pipe-event parsing
- `Chat/CLI/BackendRegistry.cs`, `BackendConfigStore.cs` — Backend selection and project-local settings
- `Chat/CLI/ChipKindRegistry.cs` — Plugin-extensible chip display registry (provider pattern)

### View Layer (UIElements Rendering)
- `Chat/View/MCPChatWindow.cs` — Main window lifecycle and shared state
- `Chat/View/MCPChatWindow.FlowBar.cs` — Activity animation bar + footer bar (BuildFooterBar, Send/Stop swap)
- `Chat/View/BackendSettingsForm.cs` — Per-backend settings forms (Claude/Codex/Antigravity/Kimi/OpenCode + chip display)
- `Chat/View/ChatSettingsSection.cs` — Settings panel builder (auto-scroll, timeout, per-backend foldouts, auth probe)
- `Chat/View/ChatTranscript.cs` — Message rendering + reload-survival serialization
- `Chat/View/ChatBlockRendererRegistry.cs` — Block type → renderer dispatcher
- `Chat/View/Markdown/MarkdownParser.cs` — Pure markdown→MdBlock parse (no side effects)
- `Chat/View/Markdown/MarkdownBlockRenderer.cs` — MdBlock → UIElements (heading, code, table, etc.)
- `Chat/View/Markdown/Mermaid/MermaidBlockRenderer.cs` — Mermaid block rendering
- `Chat/View/ChipPillFactory.cs` — Unified chip rendering (scene refs, toggles, buttons)
- `Chat/CLI/ChipTextInterleaver.cs` — Interleave chip displays with plain text
- `ChatHeaderAnim.cs` — Chat header connection-state animation (up/listen/down + ambient particles)

### Shared Models
- `Chat/CLI/ChatEvent.cs` — Normalized relay event
- `Chat/CLI/InlineChipData.cs`, `InlineChipModel.cs` — User input chip data
- `Chat/CLI/ToolCallRecord.cs` — MCP tool invocation and result

## MCPChatWindow (Main Window) — Partials

| Partial | Responsibility |
|---------|-----------------|
| MCPChatWindow.cs | Lifecycle, fields, shared state, transcript construction |
| MCPChatWindow.Send.cs / ChipInput.cs | Turn construction and send path |
| MCPChatWindow.Drain.cs / EventHandlers.cs | Relay event draining and state changes |
| MCPChatWindow.Selector.cs | Backend, model, and mode selection |
| MCPChatWindow.Session.cs | Session restore and pending-turn recovery |
| MCPChatWindow.Chips.cs / InlineChips.cs / Mention.cs | Context-chip and mention workflows |
| MCPChatWindow.Approve.cs | Ask-to-Agent approval flow |
| MCPChatWindow.FlowBar.cs | Activity animation and footer controls |

**Key Fields:**
- `_backend`: Current `RelayBackend` through the `IChatBackend` interface
- `_transcript`: ChatTranscript renderer
- `_agentMode`: Boolean for agent vs. one-shot mode
- `_sentLlmCache`: Full-path payload cache (reload-survival)
- `_permConfig`: Permission UI state
- `_resumeRetryCount`: Bounded retry for compile-clean gate (max 30)
- `_activity`: `ChatActivityState` — phase tracker (Idle / Sending / Receiving)
- `_sendBtn` / `_stopBtn`: Swapped via `OnActivityChanged` (F20: idle shows Send, active shows Stop)

**Reload-Survival (F21):**
- `SerializeForReload()` → JSON snapshot of _entries + _sentLlmCache
- `RestoreFromReload()` → Re-render from snapshot + re-send pending turns if compile clean
- Circuit breaker: _resumeRetryCount prevents infinite retry loop

## FlowBar (MCPChatWindow.FlowBar.cs)

Active-only activity animation bar. Driven by `ArcadeAnim.ControlledSmoothLoop`.

**Elements:**
- `_flowBar` — container (`flowbar` class), `PickingMode.Ignore`, `GroupTransform` hint
- `_flowFill` — animated fill pill (`flowbar__fill`) with translate + scale + opacity
- `_flowAura` — glow halo behind fill (`flowbar__aura`)
- `_flowParticles[7]` — pooled particles; alternating `flowbar__particle--hot` / `--soft` classes

**State Machine (OnActivityChanged):**
| Phase | CSS classes on _flowBar | Fill class |
|-------|------------------------|------------|
| Idle / askPending | none | removed |
| Sending | `flowbar--active flowbar--sending` | `flowbar__fill--sending` |
| Receiving | `flowbar--active flowbar--receiving` | `flowbar__fill--receiving` |

`OnActivityChanged` also swaps Send ↔ Stop button visibility (F20).

**Animation (AnimateFlowBar):**
- Fill: yoyo translate (cosine) + breathing scale + opacity pulse
- Aura: offset scale/opacity pulse (180° out of phase with fill)
- Particles: gaussian wake brightening around fill lead position; per-particle drift (sin/cos)

**Footer bar:** `BuildFooterBar` and `MakeModeBtn` live in this partial (moved from MCPChatWindow.cs to keep partials under 200 lines). Footer contains: model selector, Ask/Agent mode segment, plugin buttons, session menu, token readout, relay status label, context progress bar, Send/Stop buttons. (Agent selection is now done via `@mention` syntax in the input, not a footer dropdown.)

## ChatTranscript

**Responsibility:** Render turn messages (user + assistant) to UIElements.

**Architecture:**
- `_entries`: List<TranscriptEntry> (F21 reload-survival)
- `_registry`: ChatBlockRendererRegistry (dispatch MdBlock type → renderer)
- `_container`: VisualElement parent
- `_assistantBubble`: Live tail for streaming

**Key Methods:**
- `AppendUserBubble(UserMessage, llmPayload)` — Render user input with chips
- `AppendBlock(MdBlock)` — Dispatch to renderer based on block.kind
- `FinalizeAssistant()` — Close live tail; commit to transcript
- `SerializeForReload()` / `RestoreFromReload()` — Reload-survival

**Streaming:** Live assistant text appended to _assistantBubble; FinalizeAssistant() commits on LLM done.

**Tool Chips:** ToolChipGrouper batches tool-call UI (prevents duplicate displays).

## Markdown Pipeline

### Parse: MarkdownParser.Parse(text) → List<MdBlock>

**Single-pass, pure (no side effects).**

**Block Types:**
- Heading (H1-H6)
- CodeBlock (fenced or indented)
- Table (GFM pipe syntax)
- List (ordered/unordered)
- Image (standalone)
- Paragraph (default fallback)

**Key Regex:**
- Fenced code: `^```(language)?` → `^```$`
- Image: `^!\[...\]\(...\)$`
- Table: `^|...|...|$` (GFM 3-row header-sep-body)

**Fence Priority:** Checked first (code blocks take precedence over other syntax).

### Render: MarkdownRenderers.Render(block) → VisualElement

**Dispatcher pattern:** SwitchOnKind → Create block renderer (Label, Markdown UI, RichText, etc.).

**Mermaid Diagrams:**
- Detected: ````mermaid` fence
- Rendered: MermaidRenderer.CreateDiagram() → SVG texture → Image
- Fallback: Plain text if Mermaid unavailable

**Code Highlighting:** Syntax coloring per language (C#, Python, GLSL).

**Tables:** Rendered as VisualElement grid (no native UIElements Table; built from Rows/Columns).

## Chip System

### ChipPillFactory (Unified Rendering)

**Purpose:** Single source for chip display (scene refs, toggles, buttons, hyperlinks).

**Chip Types:**
- Scene object: /Path → clickable frame-to-object
- Asset ref: Guid → clickable open-in-inspector
- Toggle: checkbox state
- Button: action trigger
- Hyperlink: external URL

**Provider Model:**
```csharp
public interface IChipKindProvider
{
    string Key { get; }  // "scene", "asset", "toggle"
    VisualElement CreateChip(ChipData data);
    bool Navigate(string payload);
}
```

**Registration:**
```csharp
ChipKindRegistry.Register(new SceneChipProvider());  // 3rd party
```

**Markup Syntax:** `[kind:payload]text[/kind]` in LLM response (symmetric in/out).

### ChipTextInterleaver

**Purpose:** Interleave chip pill displays with plain text (no double-rendering).

**Input:** UserMessage with Segments (text + chip data)
**Output:** VisualElement with chips positioned inline

**Segments:** Each segment = (Text? or ChipData?) — builder pattern ensures no duplicates.

## Annotation System

### ChatAnnotation (F11)

**Purpose:** Persistent metadata on turns (edited, tool-calls, compile state).

**Fields:**
- `TurnEditedCode`: User hand-edited response (bypass LLM)
- `TurnHasToolCalls`: MCP tools were invoked
- `NeedsRefresh`: Transcript dirty (re-render on next frame)

**Persistence:** Stored in turn entry; survives reload via _entries list.

## Viewers & Overlays

### Viewer Pattern

**Purpose:** Specialized renderers for complex types (VFX previews, scene graph, 3D visualizations).

**Built-in Viewers:**
- Scene Graph viewer (hierarchy tree)
- Inspector viewer (component details)
- Diff viewer (code changes)
- Blueprint viewer (Mermaid diagrams)

**3rd-party Extension:** Plugins register viewers via Viewer registry (similar to ChipKind registry).

### SceneMcpOverlay

**Purpose:** In-scene visualization of MCP operations (regions, selections, annotations).

**Elements:**
- Region polygons (Lasso/Rectangle draw modes)
- Selected object highlights
- Gizmo handles for manipulation

**Integration:** Chat window can trigger overlay updates via static actions.

## Common Patterns

| Pattern | File | Why |
|---------|------|-----|
| Add new block type | Markdown/MarkdownParser.cs + MarkdownRenderers.cs | Single-pass parse + dispatch render |
| Add 3rd-party chip | ChipKindRegistry.Register(new MyProvider()) | No core edits; extensible |
| Persist chat state | ChatTranscript._entries + SerializeForReload() | Reload-survival; survives domain reload |
| Stream response | ChatTranscript._assistantBubble | Live tail; append + FinalizeAssistant() |
| Handle tool results | ChatTranscript.AppendBlock(ToolBlock) | ToolChipGrouper batches display |
| Add a CLI backend | Python backend registry + stream transformer, then existing settings/provider UI | Keeps CLI protocol knowledge out of C# |
| Animate activity state | MCPChatWindow.FlowBar.cs / OnActivityChanged | Drives CSS classes + particle animation via ChatActivityState.Phase |

## Reload-Survival (F21 Innovation)

**Problem:** Domain reload clears memory; chat window loses transcript.

**Solution:** SerializeForReload → JSON snapshot → RestoreFromReload + re-render.

**Circuit Breaker:** _resumeRetryCount prevents infinite loop (max 30 retries before giving up).

**Payload Cache:** _sentLlmCache stores full-path LLM input (not short display text) so re-send is identical.

## BackendSettingsForm

Pure UIToolkit form builder — no persistence logic. Static class.

**Forms per backend:**

| Method | Backend | Key fields |
|--------|---------|------------|
| `BuildClaudeForm` | Claude | Model, PermissionMode (plan/acceptEdits), ExtraArgs |
| `BuildCodexForm` | Codex | Binary path override, Model, PermissionMode, StartupTimeout (1–120 s) |
| `BuildAntigravityForm` | Antigravity (agy) | Binary path, Model, ApprovalMode (default/yolo), Sandbox toggle, ExtraArgs |
| `BuildKimiForm` | Kimi | Binary path, Model, ApprovalMode (default/yolo/plan), ExtraArgs |
| `BuildOpenCodeForm` | OpenCode | Binary path, Model format hint (`provider/modelId`), SkipPermissions toggle, ExtraArgs |
| `BuildChipDisplayForm` | (shared) | Allowed asset type toggles + per-kind depth/color overrides (registry-driven, P4) |

**Shared helper `BuildBinarySection`:** shows `ChatBinaryResolver.Resolve(binaryName)` auto-path hint (success/error class), optional install hint when not found, and an override `TextField` backed by EditorPrefs. Used by Antigravity, Kimi, OpenCode.

These forms persist UI values, but the current `RelayBackend` start payload
forwards only backend ID, mode, model, MCP port, resume ID, and system prompt.
Binary overrides, permission controls, startup timeouts, and extra arguments are
not active relay inputs. Python resolves backend binaries from the login-shell
`PATH`.

## ChatSettingsSection

Builds the connection settings panel content (called by `ChatConnectionSection`).

**Layout (top to bottom):**
1. Auto-scroll toggle (F22) — `EditorPrefs` `PrefKeys.ChatAutoScroll`, default true
2. Inactivity Timeout field — clamped 30–600 s, persisted via `BackendConfigStore`
3. Claude foldout (expanded by default) — binary path hint + override, auth status probe, ANTHROPIC_API_KEY warning, `BuildClaudeForm`
4. Codex foldout (collapsed)
5. Antigravity foldout (collapsed)
6. Kimi foldout (collapsed)
7. OpenCode foldout (collapsed)
8. Context Chips foldout — `BuildChipDisplayForm`; on save calls `RefreshColorResolver` + `RefreshChipDisplay` on all open chat windows
9. Plugin foldouts — one per `SettingsProviderRegistry.All` entry

**Auth probe (`ProbeAuthAsync`):** runs `claude auth status` on a ThreadPool thread (2 s timeout), updates label on main thread via `EditorApplication.delayCall`. Cancels cleanly on panel detach. Writes result to `EditorPrefs` `PrefKeys.ChatAuthStatus`.

## ChatHeaderAnim

Connection-state animation in the chat window header.

**Elements:** wave-root → lineL, hub (3 arcs + dot + orbit/orbitDot), lineR + ambient particles.

**States:** `up` (backend running) / `listen` (binary available, not logged in or auth unknown) / `down` (binary not found or auth failed). State polled every 600 ms via `root.schedule.Execute`.

**CSS class switching:** `BiomeUI.SetExclusiveClass` sets `wave--{state}` on arcs and lines, `wave-dot--{state}` on dot, `conn-{state}` on orbit. Particles state via `BiomeAmbientParticles.SetState(state)` (pattern: `BiomeParticlePattern.Chat`).

**Animation loop (SmoothLoop):** arc opacity + scale pulses (sin wave, staggered per arc), dot scale/opacity pulse, orbit angle via dual-frequency sin for organic feel, line L/R opacity breathe in antiphase.

## Error Handling

| Error | File | Fix |
|-------|------|-----|
| Markdown parse fails | MarkdownParser.Parse() | Null/empty → empty list (graceful) |
| Chip provider missing | ChipKindRegistry | Fallback to plain text |
| Reload serialization corrupt | ChatTranscript._entries | Discard; start fresh transcript |
| Mermaid render timeout | MermaidRenderer | Show SVG placeholder + text |

---

**Related:** `.claude/skills/encoding.md` (UTF-8 safety in markup), `AI/batch.md` (tool result formatting), `CLAUDE.md` § chat-features research.
