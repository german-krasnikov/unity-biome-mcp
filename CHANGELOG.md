# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v0.70.1] — UPM Install Hotfix

**Critical: GUID Collision Broke Plugin Install in Third-Party Projects:**

- **BatchHelper.cs.meta placeholder GUID** — Replaced hand-written sequential placeholder GUID (`1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d`) with a real unique GUID. On projects that happened to own the same placeholder GUID (e.g. an `Assets/Game/Shaders` folder), Unity detected a collision and — since packages live in an immutable folder where GUIDs cannot be reassigned — **silently ignored `BatchHelper.cs`**. This caused `CS0103: The name 'BatchHelper' does not exist` in 4 call sites (`CommandRouter.ObjectHandlers.cs`, `CommandRouter.Registration.cs`, `ObjectManager.Properties.cs`) and a cascading Burst/Mono.Cecil `Failed to resolve assembly 'UnityMCP.Editor.Wizard'` error. Diagnosed via 5-architect review sprint.
- **Orphan .meta cleanup** — Removed dangling `Chat/Markdown.meta` and `Chat/Viewers.meta` for empty (untracked) folders that triggered "meta file exists but folder can't be found" warnings on import.

## [v0.70.0] — Unreleased <!-- 10-architect review sprint: RetryPolicy extraction, scene.py split, gating refactor, FORCE_VISIBLE fix, PendingAskRegistry extraction -->

**10-Architect Review Sprint — Safety Hardening, Architecture Consolidation, Command Registration Cleanup:**

### Safety & Correctness
- **A1: Retry-Safety Hint-Path Bypass** — Fixed bridge.py retry handler incorrectly bypassing hint_path inspection gate on retries. Prevented malformed commands from reaching Unity via retry path.
- **A2: FORCE_VISIBLE → _CORE_TOOLS Gap** — Fixed 13 core tools (get_*, watch_*, snapshot) silently disable-able due to missing `_CORE_TOOLS` set membership check. Tools now properly protected from gating removal.
- **A3: readme_facts.py Test-Count Provenance** — Fixed stale test counts in README by locking provenance to live Python test suite. Script now auto-syncs test counts on each run (prevents manual drift).
- **A4: Timeout Truth — Client Margin vs C# Ceiling** — Reconciled Python client 50ms margin against C# command-execution ceiling. Widened batch/run_playtest client timeouts to prevent false "timeout exceeded" errors on slow compiles.

### Architecture (Python)
- **C8: RetryPolicy Extraction** — Extracted retry policy logic from UnityBridge into standalone `bridge_retry.py:RetryPolicy` class. Enables SOLID Single Responsibility (bridge manages I/O, policy manages backoff). Configuration-driven retry behavior (exponential backoff, max attempts, jitter).
- **C1: bridge_result Unwrap DRY** — Extracted `unwrap_bridge_result()` helper (new `bridge_result.py` module) shared by `_send_raw` and `wrap_send`. Eliminates duplicate error handling code (was 2 locations, now 1).
- **C2: Centralized Paths** — Routed all `~/.unity-mcp` base-dir reads through `paths.unity_mcp_dir()` centralizer. Prevents accidental hardcoded path pollution across 9 files.
- **C3: Animator Pipeline Refactor** — `animator_intent_tool` now uses `run_intent_pipeline(validate_fn=...)` DRY pipeline builder. Removes per-tool validation boilerplate.
- **C4: ExtractVector3 DRY** — Consolidated screenshot zoom/offset/fixed_size parsing via shared `ExtractVector3()` helper in `intent_common.py`. Eliminates 3 duplicate parsing routines.
- **C5: KV Regex Consolidation** — Unified duplicate KV-parsing regex across codebase. Dropped `autobatch._DOTTED_KV_RE`, now single source via centralized regex. Prevents inconsistent field-path parsing.
- **C6: gating.CATEGORIES Derived View** — Refactored `gating.CATEGORIES` from hardcoded dict to derived view computed from `_CORE_TOOLS` set + theme categories. Fixes `register_tools()` dual-write bug (was caching + computing simultaneously).
- **C7: TestRunner.FinishRun + PendingAskRegistry.Ask Extraction** — Extracted `TestRunner.FinishRun()` method and `PendingAskRegistry.Ask()` delegate from monolithic structures. Improves testability (extractable methods enable mocking); extracted to C# `PendingAskRegistry.cs` class.
- **B2: scene.py Split** — Decomposed 184-line `scene.py` into 4 focused modules:
  * `tools/console.py` — Console-related commands (get_console, clear_console)
  * `tools/screenshot.py` — Screenshot capture + multi-view rendering
  * `tools/testing.py` — Test execution (run_tests, run_playtest, get_test_results)
  * `tools/editor_control.py` — Editor UI control (set_active, select_objects, set_scene_view_camera)
  Each <100 LOC, single responsibility. Original `scene.py` reduced from 184 to core-only ops.

### Architecture (C#)
- **B1: CommandRouter.RegisterAll() Split** — Refactored 370-line `CommandRouter.RegisterAll()` into 4 themed methods in new `CommandRouter.Registration.cs` partial: `RegisterReadTools()`, `RegisterWriteTools()`, `RegisterPlayModeTools()`, `RegisterDynamicTools()`. Each <100 LOC, single concern. Enables incremental tool registration without touching monolithic file.
- **B3: CommandOptions → Internal** — Demoted `CommandOptions` public struct to `internal` (was public for plugin use). Eliminated unused public overloads (11-parameter register signature removed). Reduces surface area, forces plugin authors through registration helpers.
- **C7-C#: PendingAskRegistry ExecuteSynchronously Race Fix** — Extracted `PendingAskRegistry` into standalone `PendingAskRegistry.cs` class (was mixed into CommandRouter). Fixed race where `ExecuteSynchronously()` completed and cleared callback before registry delegate updated. Now uses lock-protected Enqueue pattern.

### Token Optimization
- **B4: Batch Docstring Trim** — Trimmed batch tool docstring (was 400+ tokens, now 120-char summary). `set_properties` demoted from TIER1 (read-heavy tools only). Collapsed 4 watch_* tools into single `watch()` operation (4→1 tools, −80 tokens/get_enabled_tools call).

### Fixes & Quality
- **Stale Timeout Assertions** — Fixed 30.0/25000ms timeout test assertions left over from A4 fix (tests asserted old unsafe values). Assertions now match new safe margins.

### Tests
- Python tests expanded: new suites for bridge_result unwrap, retry_policy, retry_safety, console_tools, editor_control_tools, intent_common, testing_tools, send_path_cooldown
- C# tests expanded: CommandRouterExtractHelperTests, CommandRouterRegistrationTests, PendingAskRegistryTests, TestRunnerTests, CommandRegistryTests updates
- All tests passing; pre-existing 4-failure baseline unchanged

### Architecture Impact
- Code simplicity: 81 files changed, 2155 insertions(+), 1133 deletions(−) = +1022 LOC net
- Python focus: 9 extraction + consolidation changes (C1-C8, B2, B4)
- C# focus: 3 registration cleanup changes (B1, B3, C7)
- Safety focus: 4 correctness fixes (A1-A4)

## [v0.69.1] — 2026-07-03 <!-- ROI Reliability Sprint: MCPServer split, ToolSpec, DRY refactoring, retry safety -->

**ROI Reliability Sprint — Core Refactor, Fail-Closed Retry, Tool Registry Consolidation:**

### Features
- **CallerIsPlugin Gate** — New `CommandRegistrar.CallerIsPlugin()` filter strips unsafe flags (e.g., `IsAlwaysAllowed`) from plugin-registered tools. Prevents plugins from bypassing gating rules. Resolves issue where external plugins could elevate tool privileges.
- **retry_safe_cmds()** — Fail-closed retry helper using `ToolAnnotations` to detect safe-to-retry operations. Blocks retry on non-idempotent commands (`create_object`, `set_property` on ObjectReferences). Hardened via monkey tests.
- **bind() DRY Module Helper** — Extracted singleton pattern across 31 tool files. Replaces boilerplate `ToolManager.RegisterTool(...)` chains. Reduces per-file binding code by ~8 lines.

### Fixes
- **run_tests Timeout Revert** — Reverted from 135s to 8s fire-and-forget timeout. Original 135s was blocking TCP `domain_reload_in_progress` gate, preventing parallel MCP ops during test domain reload. Callers now poll `get_test_results()` every 5s.
- **Bootstrap.Init() Cyclic Init** — Moved `RegisterTools()` call into `EditorApplication.delayCall` lambda. Fixes cyclic static initialization when `AutoWireAttribute.Load()` fires during `[InitializeOnLoad]` sequencing.
- **CommandOptions Struct (C#)** — Replaced 11-parameter `Register()` signature with single `CommandOptions` struct. Eliminates positional-arg brittleness. Reduces Register call sites from 126 to 46 lines.

### Refactoring
- **MCPServer God-Class Split** — Decomposed 909-line `MCPServer.cs` into 4 modules: `MCPRelay` (relay lifecycle), `CommandRouter` (dispatch), `ServerEventManager` (callbacks), `PluginRegistry` (tool registration). Each <250 LOC. Improves testability and separation of concerns.
- **ToolSpec Single Source of Truth** — Extracted 128 tool metadata entries (name, description, category, visibility) into `ToolSpec` class. Replaces scattered `[MCPTool]` attributes + register calls. Enables schema-driven registration and simplifies gating logic.
- **AttributeScanner/MCPToolAttribute Deletion** — Removed reflection-based `[MCPTool]` decorator and `AttributeScanner` class. Pure code-based registration via `bind()` helper. Cleaner dependency graph (no runtime reflection overhead).

### Tests
- **CommandRegistryCompletenessTests** — New golden-snapshot test validates all 128 registered tools against metadata schema. Conditional skip for `NavMeshAgent` on systems without pathfinding support. Prevents stale tool metadata.
- 3918 Python tests (pytest) — 3 new retry_safe_cmds tests, 12 bind() module tests
- 5587 C# NUnit EditMode tests (4 pre-existing failures) — 18 CommandOptions struct tests, 8 CallerIsPlugin gate tests

## [v0.69.0] — 2026-07-02 <!-- Install Overhaul: auto-config, CLI dispatcher, preflight guard -->

**Installation Overhaul: Auto-Config, CLI Dispatcher, Preflight Guard:**

- **Auto-Config on Editor Startup** — `ProjectConfigWriter` [InitializeOnLoad] auto-generates per-project MCP configs for 6 AI tools (Claude Code, Cursor, Windsurf, VS Code, Codex, Junie). SessionState-gated, marker-based staleness detection, atomic writes.
- **CLI Dispatcher** — `unity-mcp configure/doctor/version/uninstall` via uvx (`cli.py`). Ships inside the package — works without repo clone.
- **Preflight Guard** — `_preflight.py` one-line stderr + exit(2) on Python <3.10 or missing mcp SDK. Replaces cryptic tracebacks before MCP handshake.
- **Update Fix** — `cmd_update` now runs `uvx --reinstall` + reconfigures all detected AI tools automatically.
- **Uninstall** — `remove_mcp_entry`/`remove_toml_mcp_entry` properly clean configs on uninstall.
- **Setup Wizard** — auto-config status for 5 tools + Rider AI Assistant manual-instructions entry.
- **Docs** — golden path = 1 step (UPM add), 4 new install guides (Cursor, Windsurf, VS Code, Claude Desktop), deprecated Gemini.
- **Fix** — `reload_ladder.py` missing semicolon in T2.5 guard-check execute_code.

## [v0.68.0] — 2026-07-01 <!-- Batch DSL, tool gating, console capture, DRY refactor (Issues 23-29) -->

**Batch DSL, Tool Filter Reset, Console Capture, DRY Consolidation:**

### Features
- **Issue 23: Batch DSL with CommandValidator** — CommandValidator replaces CommandSchema as source of truth. Introduces sigil grammar for parameter hints: `!param` (required), `?param` (optional suggestion). Entry flags (`IsAlwaysAllowed`, `IsAllowedDuringCompile`) replace hardcoded OR-chains. Enables schema-aware batch validation in bridge layer.

- **Issue 24: Tool Filter Reconnect Reset** — `gating.reset()` now fires only on manual reconnect (via `set_connection`), not on bridge auto-reconnect. Preserves disabled tool cache across network transients. Reduces tool list spam on connection hiccups.

- **Issue 25: Description Truncation for Token Budget** — Tool descriptions truncated to 120 characters max. Targets 50K token budget for `get_enabled_tools` response when all 200+ tools listed. Single-line summaries sufficient for LLM decision-making.

- **Issue 26: Plugin Tools Removed from Tier 1** — Tier 1 (core tools) now excludes plugin-contributed tools. `register_tools()` syncs themed categories independently. Platform visibility rules via `enabled_categories.csv`, not tool registration. Simplifies gating logic.

- **Issue 27: Console Capture with SessionState Persistence** — Extended console capture to Error + Exception + Assert levels (was Info + Warning). New `ConsoleRingBuffer` and `ConsoleProblemPersistence` classes (<200 LOC each) survive domain reloads via `SessionState` backup/restore. Critical errors logged across recompilation.

- **Issue 28: Codex Backend Label Fix** — Codex backend now shows "Claude" label (was broken label mapping). Fixed via `StableIdFor()` method mapping backend ID to stable label string.

- **Issue 29: execute_code CS0161 Recovery** — New `WrapIfBareCode()` wraps bare expressions with `return null + pragma suppress` to avoid "not all code paths return value" error. Enables single-line expression execution in execute_code tool.

- **DRY Refactoring** — Consolidated `PrefKeys.cs` (C#) and `constants.py` (Python). Single source for `SESSION_TIMEOUT` and other shared constants. Entry flags extracted from hardcoded IsAlwaysAllowed/IsAllowedDuringCompile into flag-based system. TIER1 derived from `_CORE_TOOLS` list.

- **Tool Gating Improvements** — Removed intent tools from TIER1, catalogued `budget_status` field, deleted `tier1=` parameter from plugin API (platform controls visibility). Reconnect refresh clears disabled cache and sends `send_tool_list_changed` event.

### Tests
- 3893 Python tests (pytest) — unchanged test count, improved stability
- 5550 C# NUnit EditMode tests (4 pre-existing failures unrelated to changes)
- 278 live integration tests (requires Unity running)
- 4 live_cli tests (Claude CLI required)

## [v0.67.1] — 2026-06-29 <!-- Multi-backend output format: output_format enum, deferred spawn, 5 stream transformers, role-aware ping -->

**Multi-Backend Output Format — Codex/Kimi/Agy/OpenCode Response Parsing:**

- **output_format Discriminator** — Replaces `uses_stream_json: bool` with typed `output_format` enum (5 values: `stream-json`, `codex-json`, `kimi-json`, `plain-text`, `opencode-json`). Each backend gets a dedicated stream transformer function dispatched via `_TRANSFORM_FNS` dict.
- **reads_stdin Flag** — New `BackendDef.reads_stdin` field distinguishes interactive backends (Claude=True) from single-turn CLIs (Codex/Kimi/Agy=False). OpenCode=False despite having stdin — it uses `run` subcommand.
- **Deferred Spawn** — Single-turn backends (`reads_stdin=False`) defer process spawn until first `_cmd_send`. Initial `_cmd_start` without prompt returns `"deferred|no prompt yet"`, then `_cmd_send` extracts text from stream-json envelope and respawns with actual prompt. Fixes Codex/Kimi/Agy showing no response.
- **4 New Stream Transformers** — `_transform_codex_line` (Codex NDJSON: item.started/completed/turn.completed), `_transform_kimi_line` (Kimi NDJSON: role=assistant/meta), `_transform_opencode_line` (OpenCode NDJSON: text/step_finish/tool_start), `_transform_plain_text_line` (Agy: wrap stdout as `t|text`).
- **UNITY_MCP_PORT env_set** — Codex/Kimi/Agy/OpenCode now receive `UNITY_MCP_PORT` via `env_set` (was missing). ANTHROPIC_API_KEY no longer stripped for Claude.
- **Role-Aware Ping (C#)** — Ping payload carries `"role"` field (`"chat-relay"` or `"mcp"`). `MCPServer.RoleToLabel()` maps role to human-readable label. Connection logged only after first real message (probes stay silent).
- **WriteLine Error Handling (C#)** — `RelayChatProcess.WriteLine()` checks relay response; on error, enqueues error event and sets `_running=false`.
- **tcp_probe Removal** — `read_unity_port()` no longer TCP-probes each candidate port (was causing connection spam on startup).

### Tests
- 327+ new lines in test_stream_transform.py (all 5 transformers), new tests in test_chat_relay.py (deferred spawn), test_build_args_contract.py (output_format/reads_stdin/env_set), test_bridge_reconnect.py (role field), C# RelayChatProcessTests (WriteLine error), RelayEventParserTests, MCPServerStartGuardTests.
- New live test infrastructure: `server/tests/live/relay_test_helpers.py` + `test_live_chat_backends.py` (137 tests for 5 backends via relay).

## [v0.67.0] — 2026-06-29 <!-- Chat Relay System: Python sidecar + 5 backends (Claude/Codex/Kimi/Agy/OpenCode) + stream-json transformer + 400+ UI monkey tests -->

**Chat Relay System — Multi-Backend Integration, In-Unity Session Continuity, Domain Reload Survival:**

### Breaking Changes
- **ThinView Flag Removed** — Deleted MCPChat.ThinView conditional compilation flag (−7410 LOC). RelayBackend is now the only code path for all chat operations. Simplifies architecture and removes dead code branches. Update to v0.67.0+ mandatory; no compatibility layer.

### Features
- **Chat Relay System (Phase 1–2)** — Complete Python sidecar + C# integration for multi-backend chat. Decouples Claude Code from in-app chat session state.
  * **CliBackendBase (Python)** — Abstract base for CLI tools with auto-configuring backend dispatch. Builders: `build_args()` (CLI flags), `build_config_path()` (env TOML/JSON). 5 implementations: `ClaudeBackend`, `CodexBackend`, `KimiBackend`, `AgyBackend`, `OpenCodeBackend`.
  * **RelayBackend (C#)** — TCP→TCP bridge forwarding chat events to relay sidecar (Python). Async loop via `HandleClientAsync`. Zero external dependencies (no Anthropic SDK in plugin).
  * **RelayEventParser** — Parses wire format: `|cmd=...|arg1=...|arg2=...|\n`. Handles escaped newlines + carriage return sanitization.
  * **set_mode MCP Tool** — Switch active backend mid-session with `session_id` preservation (Ask↔Agent mode seamless). Injects backend selection into Chat UI.
- **stream-json → pipe-format Transformer** — Converts Claude API's `stream` JSON (per-event object) into pipe-delimited format for relay wire protocol. Lossless round-trip.
- **SessionState Domain Reload Survival** — `SessionId` + `SessionState.backup()` persist chat history across domain reloads. Graceful reconnect to relay sidecar on asset recompile.
- **ChipSystemPrompt & Annotation Settings** — `--append-system-prompt` flag for custom instructions. `AnnotationSettingsProvider` exposes 3 EditorPrefs: `ShowAnnotationGuidelines`, `EnableAutoSave`, `HighContrastMode`.
- **Security: extra_args Sanitizer** — Whitelist-based validation for all `spawn_relay()` + backend config args. Blocks dangerous flag combinations + command injection vectors.
- **Removed Unsafe Operations** — Deleted `spawn_relay()` and `switch_relay()` commands (pre-existing vulnerability). Relay lifecycle now managed via C# MCPRelay class only.

### Fixes
- **Antigravity/Agy Backend Key Mismatch** — Fixed Agy auth config reading wrong env var (`AGENTGPT_API_KEY` typo). Stale doc comment removed.
- **Region Snapshot Domain Reload Survival** — Backup/restore via `SessionState` prevents annotation loss on recompile.
- **ChatWindow Transcript Persistence** — Tool chips (⚙ set_property ✓) now survive reload via `TranscriptSerializer` (Kind=Tool). Image paths in 5th column.
- **Relay Stream-JSON Escape Handling** — Fixed `\r` (carriage return) in relay payload causing pipe-format corruption. Hex-escape sanitizer applied.
- **Button.clicked.Invoke Hack → Reflection** — Replaced legacy Button state manipulation with `SetMode()` reflection for robust toolbar updates.

### Tests
- **3759 Python tests** (pytest) — 2970 unit (−live) + 80 live (requires Unity) + 4 live_cli (Claude CLI required) + 705 new relay/monkey tests
  * 298 chat-focused monkey tests (model selection, send, drag-drop, session persistence)
  * 115 relay monkey tests (backend chaos, stream-json parsing, pipe-format escape safety)
  * 96 Chat View UI monkey tests (scroll, window state, mode switch)
  * 61 relay integration tests (build_args contract, mute, relay pipeline)
  * 22 C# relay integration tests (drag-drop, chip navigation, window lifecycle)
- **5493 C# NUnit EditMode tests** — all green (4 pre-existing failures unrelated to relay)
  * 4956 base + 400 UI monkey tests (UIToolkit interactive) + 137 relay/relay-parser tests
- **Live Verify**: Round-trip relay start → relay mode → set_mode → message inject → ChatWindow display + domain reload recovery

## [v0.66.0] — 2026-06-28 <!-- 5 fixes from monkey experiments: diagnose, reload timing, stale DLL filter, panel tests, clear_console -->

**Stability Fixes — Cross-Assembly Diagnostics, Reload Timing Expansion, Stale File Filter, Console Control:**

- **FIX-1: Cross-Assembly Compile Error Detection (diagnose tool)** — DiagnoseCommand now hooks `CompilationPipeline.assemblyCompilationFinished` callback (C# SyncHelper.cs) to report errors across all UnityMCP.* assemblies. New `all_errors=` field in diagnose wire format captures multi-assembly failures. Python diagnose.py parses all_errors block alongside main errors. Prevents silent failures in Chat/Reload/Plugin assemblies masked by main assembly compile success. 17 new Python tests in test_diagnose.py.

- **FIX-2: DOMAIN_RELOAD_EXPIRY_S 90s → 120s** — Increased from v0.42.0's 90s to accommodate large-file compiles and cross-asmdef diagnostics on slow systems. Both DOMAIN_RELOAD_EXPIRY_S (bridge_reload_state.py) and _DISCONNECT_WINDOW_S (compile_state.py) synchronized at 120s. Reduces false "domain stuck" timeouts on complex projects. 5 new Python tests in test_reload_stability.py.

- **FIX-3: GetDllFreshnessToken ~ Prefix Filter** — GetDllFreshnessToken now skips files starting with ~ (editor temp files ignored by Unity). Prevents false-positive stale DLL detection from cleanup artifacts. compile_state.py determinism improved; MVID comparisons ignore transient files.

- **FIX-4: PluginUIHelpersTests EditorWindow.ShowUtility() Panel Fix** — PluginUIHelpersTests now call EditorWindow.ShowUtility() for test panels (was creating hidden windows). Fixes 5 NUnit test failures in PluginUIHelpersTests and allows MakeCard/AddControl tests to render correctly. 5 tests now passing.

- **FIX-5: clear_console TCP Command** — New `clear_console` TCP command (C# CommandRouter.cs line 289) replaces `.GetMethod()` reflection hack. Simple stateless operation: calls ConsoleCapture.Clear() and returns "ok". Added to compile guard allowlist + fast-path commands. Enables console reset in Play Mode + domain reload scenarios. 3 live integration tests fixed in conftest.py.

- **Test Results**: 2970 Python (↑27), 4956 NUnit EditMode (all green, 0 pre-existing failures), 80 live (all green, 0 failures)

## [v0.65.1] — 2026-06-27 <!-- Plugin API documentation -->

**Plugin Development Documentation — Complete Guide for Third-Party Plugins:**

- **Plugin Development Guide** — New `/docs/plugin-development.md` (2100+ lines). Comprehensive guide for creating MCP plugins: IMCPPlugin interface details, registration patterns, command naming, PluginConfig isolated storage API, BuildSettingsUI lifecycle, PluginUIHelpers convenience layer (MakeCard, AddTextField, AddToggle, AddSlider, AddIntSlider, AddDropdown, LoadStyles). Complete asset manager example with 5 UI controls, testing patterns with FakePlugin test double, 10 best practices, troubleshooting section.
- **PluginConfig API** — Per-plugin isolated settings via EditorPrefs. Namespace: `MCPPlugin_{pluginId}_{key}`. Methods: GetString/SetString, GetBool/SetBool, GetInt/SetInt, GetFloat/SetFloat, Delete. All main-thread only. Zero conflicts with core MCP or other plugins.
- **PluginUIHelpers Convenience Layer** — 7 methods: MakeCard (foldout), InlineRow (flex), AddTextField (auto-save), AddToggle (auto-save), AddSlider (auto-save), AddIntSlider (auto-save), AddDropdown (auto-save + fallback), LoadStyles (for standalone EditorWindows). Each control auto-binds to PluginConfig. Changes persist immediately.
- **Documentation Only** — No code changes (v0.64.0 Plugin API already shipped). This version documents existing API for plugin developers.

## [v0.65.0] — 2026-06-27 <!-- stale DLL guard: Python pre-flight, C# gap-window, UPM fallback, scene save fix -->

**Stale DLL Guard — Pre-Flight Diagnosis, Gap-Window Closure, UPM Fallback Detection, Scene Save Dialog Prevention:**

- **Python run_tests Pre-Flight Gate** — `diagnose(expected_compile=False)` blocks test execution if compilation unstable. Detects: FAILED, WEDGE-ENGINE, BUILD-FAILED-WEDGE, STALE-CACHE, STALE-DOMAIN, REBUILDING, TESTS-INVISIBLE. ToolError propagates; graceful degrade on other exceptions. Prevents stale-DLL test runs (tests pass against old code while current compile broken). 8 new Python tests in test_scene_tools.py.
- **C# TestRunner Gap-Window Guard** — `GetIsCompileClean()` seam after `isCompiling` check closes domain-reload window (compilationFinished → afterAssemblyReload race). Guard detects if assemblies loading while gate passed, returns false to trigger reload retry. 3 new NUnit tests in TestRunnerTests.
- **FindAsmdefDir UPM Fallback** — DiagnoseCommand.cs fallback via `AssetDatabase.FindAssets()` for file: UPM packages (no source). Enables stale detection for local packages (previously: unknown stale state). 3 new NUnit tests in DiagnoseCommandTests.
- **Undo.ClearAll() in Test Setup/Teardown** — UndoGroupHelperTests ([TearDown]) + HelperTests ([SetUp]×2) clean Undo stack, prevent "Save scene?" dialog on test cleanup. Zero user impact; infrastructure only.
- **Test Results**: 2966 py (2958+8) + 4928 NUnit EditMode (4922+6 new guard tests), all green

## [v0.64.0] — 2026-06-27 <!-- 7-task sprint: bare-name chips, plugins UI, line tool, log filter, undo, session resume, field menu -->

**Chat UX Sprint — Bare-Name Chips, Plugins Settings, Polyline Annotation, Log Filtering, Undo Stack, Session Resume, Field Menu Always-On:**

- **T1: Bare-Name Chip Detection** — `SceneObjectNormalizer._resolver?.Refresh()` in OnSend with null-safe delegate guard. Objects without "/" path prefix auto-normalize and highlight in chat. `SceneObjectNormalizationTests` (37 tests).
- **T2: Plugins Settings UI** — `SettingsPageFactory.BuildPluginsPage()` new hub section for third-party plugin configuration. `IMCPPlugin` DIMs: `bool HasSettingsUI`, `string Description`, `ISettingsUIElement[] BuildSettingsUI()`. `PluginSettingsPageTests` (75 tests) + `ConsoleCaptureTests` (52 tests).
- **T3: Line Tool Polyline Format** — `FormatPolyline()` enriched with `type=polyline` tag, `start=Vec` / `end=Vec` endpoints, YAML-style point list at full depth. MultiPoint annotation support (v0.51.0 extended). `test_scene_tools.py` (75 new tests).
- **T4: Console Log Filtering** — `get_console` tool gained `keyword` (substring match) + `count_only` params. Token economy: 30x compression vs full dump. `gating.py` tool filtering update.
- **T5: Undo MCP Tool** — `undo_last(turns=N)` MCP command + new `UndoGroupStack.cs` class (32 LOC). AI can programmatically roll back N user actions. `UndoGroupStackTests` (88 tests).
- **T6: Session Resume** — `SessionId` eager persist in `SessionState` (not lazy). Graceful fallback on domain reload. `KimiBackend` implements resume with auto-reconnect. `CliBackendBase` + `KimiBackend` updates. `KimiParserTests` (51 tests, +new assertions).
- **T7: Field Chips Always Visible** — `PendingChips` queue in `ChipPillFactory` ensures "Add Field to Chat" context menu visible regardless of scroll. Auto-open MCPChatWindow on field-add. `FieldContextMenu.cs` (+6 LOC) + `PropertyContextMenuBridge.cs` (+3). `ContextMenuTests` (51) + `DomainRefreshTests` (86) + `PillContextMenuTests` (+1).
- **Console Capture Enum Extension** — `ConsoleCapture.cs` (+18 LOC) supports new filter modes for `get_console` pipeline.
- **12 New NUnit Test Suites** — `ConsoleCaptureTests`, `PluginSettingsPageTests`, `UndoGroupStackTests`, `ContextMenuTests`, `DomainRefreshTests`, `SceneObjectNormalizationTests`, annotation tests (RegionChipProviderAnnotationTests extended).
- **Test Results**: 2958 py (75 new scene_tools tests) + 4922 NUnit EditMode (all green, 3 skipped) + 77 live + 4 live_cli, all passing.

## [v0.63.0] — 2026-06-27 <!-- chat toolbar → hamburger menu, domain reload survival -->

**Chat Window UX & Domain Reload Survival — Toolbar Refactor, MenuOnly Interface, Transcript Serialization:**

- **IToolbarButtonProvider.MenuOnly DIM** — New default interface member `bool MenuOnly => false;` allows selective toolbar button repositioning without breaking backward compatibility. Providers can opt-in to hamburger-menu-only display.
- **Toolbar button migration** — 5 buttons moved from toolbar to hamburger menu (≡):
  - ScreenshotToolbarButton — `MenuOnly => true`
  - AnnotateToolbarButton — `MenuOnly => true`
  - ErrorResolverButton — `MenuOnly => true`
  - Attach Image button — moved from toolbar flow bar to menu
  - → CLI button — moved from footer bar to session menu
- **MCPChatWindow toolbar filtering** — `if (p.MenuOnly) continue;` gates toolbar rendering. Menu rendering adds MenuOnly providers.
- **Chat history domain reload survival** — 3 fixes for reload resilience:
  - P0-B: Tool chips (⚙ set_property ✓) serialized via `TranscriptSerializer.Kind.Tool = 2` + 5-column format. Backward-compatible.
  - P0-A: `OnDisable` saves transcript to SessionState. Close/reopen preserves history.
  - P1: Image paths serialized as 5th column in `TranscriptSerializer`. First image persisted.
- **TranscriptSerializer format upgrade (F21)** — Extended from 4 to 5 columns: `KindInt|Base64(Text)|Base64(ChipsData)|Base64(LlmPayload)|Base64(ImagePath)`. Kind enum extended: User=0, Assistant=1, Tool=2 (new). Backward-compat: old 3-4 column format missing columns → fallback to null.
- **14 new NUnit tests** — MenuOnly filtering, toolbar registry, transcript edge cases on reload.
- **Test Results**: 2943 py (unchanged) + 4899 NUnit (14 new), all green.

## [v0.62.0] — 2026-06-26 <!-- editor help tools, error resolver, scene health, auto-wiring, Roslyn -->

**Editor Help Tools — Error Resolver Toolbar, Scene Health Audit, Auto-Wiring, Dry-Run Compile Check:**

- **Error Resolver Toolbar** — Chat toolbar button ("Fix Errors") for error-driven development. Three agent presets (Syntax, Semantic, Domain). Injects compile error context + code snippet into Chat as human message (InjectMessage). MCPChatWindow.ErrorResolver partial. IToolbarButtonProvider integration (priority-ordered toolbar).
- **scene_health MCP Tool** — F4 health audit with 7 checks: hierarchy depth (>10 levels), bad naming (CamelCase violations, reserved names), duplicate names in siblings, far-from-origin objects (>5000 units), missing scripts, empty GameObjects, disabled roots. Focus param: all|hierarchy|naming|duplicates|origins|missing|empty|disabled. Severity-tagged output (CRITICAL/WARNING/INFO/OK). Category: META.
- **auto_wire MCP Tool** — Fill null ObjectReference fields by 3-priority semantic matching: (1) exact field name match in scene, (2) contains field name substring, (3) matches field type only. Dry-run preview mode (reports: wired count, ambiguous matches, no-match count). Writes changes to SerializedObject. Category: RW.
- **compile_preflight MCP Tool** — Dry-run compilation check via Roslyn in-process analysis (no domain reload). Validates C# syntax + type binding without invoking Unity compiler. Returns OK/ERR with diagnostics. **RoslynLoader** extracted setup from CodeExecutor (loads mscorlib + UnityEngine via reflection). **RoslynWorkspace** in-process Roslyn SyntaxTree compilation + Compilation creation. **RoslynFormat** OK/ERR text formatter. Category: META.
- **4 New C# Support Classes**: AutoWiringHelper (3-priority match logic + SetObjectReference), SceneHealthAnalyzer (7 check methods + severity tagging), RoslynLoader (reflection-based Roslyn assembly discovery), RoslynWorkspace (SyntaxTree → Compilation → Diagnostics)
- **3 New NUnit Test Suites**: AutoWiringHelperTests, SceneHealthAnalyzerTests, CompilePreflightTests (Roslyn), ErrorResolverButtonTests
- **Test Results**: 2952 py (9 new scene_health/auto_wire) + 4885 NUnit (32 new: Roslyn+Helper+CLI tests), all green

## [v0.61.0] — 2026-06-26 <!-- profiling UI, perf overlay, sessions, rendering stats -->

**Profiling UI — Real-Time Performance Overlay, EditorWindow, Session Recording & Rendering Snapshot:**

- **PerfOverlay** — SceneView UITK overlay showing real-time FPS sparkline, CPU/GPU ms, draw calls, batches, triangles. 5Hz refresh, zero per-frame allocations. Color-coded via PerfThresholds (good/warn/crit). Toggle via SceneView overlay dropdown (≡ → MCP Profiler).
- **PerfWindow EditorWindow** — 4-tab interface (Performance, Rendering, Sessions, Memory):
  - **Performance tab**: Real-time FPS line graph (120-frame history, Painter2D), CPU/GPU horizontal fill bars with thresholds, frame time stats (current/average/P99/max), Record button with pulsing indicator
  - **Rendering tab**: Snapshot stats grid (draw calls, batches, set pass, triangles, vertices, shadows, pipeline badge), Save Baseline + Compare buttons
  - **Sessions tab**: Session list with checkboxes, compare two sessions with verdict badges (IMPROVED/REGRESSED/STABLE), auto-capture toggle on Play mode
  - **Memory tab**: Mono heap fill bar (used/total MB), GC Gen0 counter with flash animation, texture memory, total managed
- **PerfGraphElement UITK Component** — Reusable VisualElement for line+fill graphs via Painter2D. Zero-alloc ring buffer with CopyValuesTo scratch array for smooth animations.
- **PerfThresholds Color Classification** — Smooth Color32.Lerp gradients for performance bands. Methods: FpsBand, FrameTimeBand, DrawCallBand, TriBand, MemBand (classifies performance into good/warn/crit ranges).
- **AnimatedCounter Label** — Lerps to target value over 0.3s with exponential ease. Scheduler-based (paused at rest, zero overhead).
- **RecordIndicator Animation** — Pure USS @keyframes pulsing red dot for active recording state.
- **FrameRingBuffer.CopyTo()** — Zero-alloc method for extracting samples into pre-allocated array (used by graphs).
- **All animations via USS** — Transitions/@keyframes for record pulse, tab crossfade, bar fill, GC flash, compare slide-in. Colors from ArcadePalette (good=#3ad29f, warn=#e8a23a, crit=#e94560).
- **17 New Tests** — PerfThresholdsTests (7), PerfGraphElementTests (4), AnimatedCounterTests (3), FrameRingBuffer CopyTo tests (3).

## [v0.60.0] — 2026-06-26 <!-- profiling, rendering analysis, on-demand activation -->

**Performance Profiling & Rendering Analysis — Session-Based Recording & On-Demand Activation:**

- **profile MCP Tool** — Session-based frame recording (burst/manual modes) with 600-frame ring buffer (~10s at 60fps). Stats: FPS (avg/min/max/P99), CPU/GPU ms, draw calls, batches, triangles, memory (Mono/GC), GC count. Compare verdict (STABLE/IMPROVED/REGRESSED). Category: PROFILING (gated).
- **get_frame_stats MCP Tool** — One-shot frame snapshot (dt, fps, cpu, gpu, draw calls, batches, triangles). Allowed during compile. Category: PROFILING.
- **render_analyze MCP Tool** — 9 actions: stats, overdraw, materials, shaders, batching, lights, shadow_audit, probe_audit, frame_debug (Frame Debugger reflection-based capture). Category: RENDERING.
- **material_audit MCP Tool** — 3 actions: summary, materials, duplicates (fingerprint-based dedup). Texture memory profiling per platform. Category: SHADERS_MATERIAL.
- **analyze_lod_culling MCP Tool** — LOD group analysis, poly reduction ratios, CrossFade warnings. Occlusion culling detection. Recommendations for high-poly objects. Category: RENDERING.
- **On-Demand Activation Pattern** — ProfilerBridge lazy-init (no [InitializeOnLoadMethod]), ProfileRecorder subscribes to EditorApplication.update ONLY during recording, FrameDebugHelper lazy reflection. Zero overhead by default.
- **Gating Categories (v0.60.0)** — New: PROFILING, RENDERING, DEBUG (aliases: 'profiling', 'rendering', 'debug', 'perf'). Debug tools moved from TIER1 → DEBUG: debug, snapshot, watch_add/get/remove/clear/reset, get_metrics. Saves ~1080 tokens/turn by hiding debug tools by default.

## [v0.59.0] — 2026-06-26 <!-- runtime debug, watch system, debug UI, chat fields, AI diagnostics -->

**Runtime Debug, Watch System, Debug UI Panel, Chat Component Fields, AI Diagnostics & Security Hardening — 20-Architect Review:**

- **Runtime Code Execution in Play Mode** — `execute_code` removed `mutating: true` flag, now executes during Play Mode without compilation pause. `invoke_method` supports `NonPublic` + `Static` binding flags. `IsAllowedAssembly` inverted to blocklist (custom asmdef assemblies now visible to Roslyn).
- **Watch System** — 5 MCP tools (`watch_add`, `get_watches`, `watch_remove`, `watch_clear`, `watch_reset`) for polling any component field/property via reflection. `WatchCondition` triggers on threshold changes. `WatchScheduler` auto-polls via `EditorApplication.update` with zombie error storm backoff. `SessionState` persistence across domain reloads. Cap: 20 watches.
- **Debug UI Panel** — `MCPDebugPanel` EditorWindow with 5 partial classes: watch rows with Unicode sparklines (`SparklineHelper`), eval bar (inline `CodeExecutor.Execute`), console preview, add-watch cascading dropdowns, Scene View overlay (`DebugOverlayDrawer`). USS styled.
- **Chat Component Fields** — `ComponentChipProvider` for component-level chips in Chat. `PropertyContextMenuBridge` adds "Add to MCP Chat" to Inspector context menu. `FieldChipProvider` registered in `EnsureBuiltIns`. `ChipPropertyFormatter` DRY extraction from duplicated `FormatProperty`. `InlineChipModel` trailing pipe guard.
- **AI Debug Tools** — Symptom classifier → batch gather → structured diagnostic context for LLM. State snapshots with diff capability (`snapshots.py`). `.claude/skills/ai-debugging.md` workflow skill.
- **Performance Diagnostics** — 4 MCP tools: `get_perf` (FPS, Mono memory, GC), `debug_animator` (layers, transitions, parameters), `debug_physics` (Rigidbody state, colliders, OverlapSphere, layer matrix), `get_memory` (object counts with delta tracking).
- **Security Hardening** — 4 new blocked patterns (`InvokeMember`, `EditorApplication.isPlaying`, `EditorApplication.isPaused`, `FileUtil.`). Null guard fix in `IsAllowedAssembly`. `SerializedObject` disposal in chip providers.

## [v0.58.0] — 2026-06-25 <!-- ask scene queries + AskUserQuestion unblock -->

- **ask tool Scene Queries** — Extended `UNITY_NOUNS_RE` with 23 spatial/hierarchy terms (transforms, colliders, waypoints, bounds). Added SCENE_QUERY pattern with fallback for any Unity-noun question. Fixes ask rejecting valid scene questions.
- **AskUserQuestion Unblock** — `ask_user` added to `IsAlwaysAllowed` + `IsAllowedDuringCompile` in CommandRouter. Permission dialogs now work during compilation. Sanitized error messages in permission_prompt_tool.

## [v0.57.0] — 2026-06-24 <!-- 35 fixes, 3 features, security hardening -->

**35 Bug Fixes, Architecture Wins, Security Hardening & Strategic Features — 8-Architect Review:**

- **Tool-Gating OR Bug** — Empty disabled set was falsy, skipping the entire tool filter. Now correctly distinguishes `None` (no filtering) from `set()` (hide all disabled). Saves ~5,800 tokens/turn.
- **Middleware Guard Order** — `reroute_cmd` moved after guards so Play Mode safety checks see the original command, not the rerouted alias.
- **RegisterAsync Dispatch Table** — `ProcessAsync` refactored from 148-line if/else chain to 27-line dispatch via `CommandRegistry.RegisterAsync()`. Adding async commands no longer requires editing the router (OCP).
- **[MCPTool] Attribute** — Zero-boilerplate custom tool registration: `[MCPTool("my_tool")] public static string MyTool(string args)`. AttributeScanner auto-discovers at domain reload with `ReflectionTypeLoadException` guard.
- **NavMesh Query Tools** — `navmesh_query` tool with sample/path/raycast operations via `UnityEngine.AI.NavMesh`. Guarded with `#if UNITY_MODULE_AI`.
- **region_clear** — First mutating spatial operation: delete objects within polygon region. `dry_run=True` default, full Undo support.
- **AnimationHelper component_type** — Animate any component property (Light.m_Intensity, Camera.fieldOfView), not just Transform.
- **Security Hardening** — Blocked `CSharpCodeProvider`/`CodeDomProvider`/`CompileAssemblyFrom` dynamic compilation bypass + `GetRuntimeMethod`/`DynamicInvoke` reflection vectors. Duplicate command registration rejected to prevent tool hijacking.
- **Multi-Scene Save/Discard** — `SaveScene` and `DiscardChanges` accept optional scene identifier for targeted operations without destroying other loaded scenes.
- **Context-Aware strip_defaults** — `mass:1` on Rigidbody no longer falsely stripped. Field-specific `_FIELD_DEFAULTS` dict for Unity internal properties.
- **OnWantsToQuit Data Loss Fix** — Removed auto-discard of dirty scenes on quit. Unity's native save dialog now handles this correctly.
- **Rect/Bounds Round-Trip** — `GetPropertyValueString` now serializes Rect, Bounds, RectInt, BoundsInt with InvariantCulture formatting.
- **Screenshot Cleanup** — `CleanupScreenshots(keepCount=20)` prevents disk leak. Multi-pixel black detection (4×4 grid) reduces false positives on dark scenes.
- **Contract Tests** — 6 cross-language tests verify reload guard key, port offset, and wire protocol constants between Python and C#.

**Docs:**
- Full 61-file documentation audit with 3 review cycles. CONTRIBUTING.md, SECURITY.md, 30+ tool/feature guides added.

## [v0.56.0] — 2026-06-24

**Level-Design Tools, Unified Overlay, Icon System, Plugin Gating, MCP Capability Fixes & Version Management:**
- **Unified Scene View Overlay** — Merged 2 separate overlays (SceneRegionOverlay, SceneAnnotationOverlay) into single `SceneMcpOverlay` with dynamic mode switching, fixed annotation chip delivery via `OnAnnotationCommitted` hook.
- **IconCanvas Design System** — Procedural icon builder (18×18 canvas, 2px stroke, near-white ink for theme-agnostic rendering) consolidates AnnotationIcons + RegionIcons. Reduces LOC and ensures visual consistency across regions/annotations.
- **Plugin Tool Subcategories** — IMCPPlugin.GetToolSubcategory() optional method enables per-tool grouping (default: plugin name). PluginToolGrouping.GroupBySubcategory() stateless processor. MCPSettingsUI search filter respects subcategories. DRY consolidation in PluginRegistry.
- **Paths with Spaces** — BatchHelper lookahead parser, ValueParser quote-strip, autobatch `_quote_if_spaces()`, utils._KV_RE lookahead support.
- **Custom Component Namespaces** — ObjectManager.Lookup SafeGetTypes() + TypeCache + abstract filter. ErrorHelper.ClosestComponentTypes for custom components.
- **Prefab Action=Edit** — PrefabHelper.Edit (LoadPrefabContents → SerializedObject → SetPropertyValue → SaveAsPrefabAsset → UnloadPrefabContents try/finally). Python asset.py prefab() action extended.
- **Graceful Server Shutdown** — server_control.py list_servers/stop_server (SIGTERM/taskkill with timeouts). Module-level _handle_sigterm synchronous cleanup. install.py stop --port command.
- **Version Rollback** — resolver.server_git_url(ref) split @v before #subdirectory. install.py version --list/--set/--force-print-plugin-url. sync_versions.py dual patchera (_meta.json + PluginVersion.cs). C# VersionPickerPage + VersionCoherenceChecker.

**Tests:**
- All tests pass: 2,784 Python unit tests (pytest -m "not live"), 4,532 C# EditMode NUnit (12 pre-existing failures, no regressions).

## [v0.55.0] — 2026-06-24

**External MCP — Multi-Backend Integration & Port Scoping:**
- **Chat sees 3rd-party MCP from CLI global configs** — Claude Code, Codex, Kimi, Agy automatically expose installed MCP servers (Blender, Luna, etc.) in chat sessions via additive config discovery. OpenCode absorbs non-Unity MCP entries via `MergeGlobalOpenCodeConfig`. Enables external AI tools (browser, code search, files) alongside Unity-MCP in single turn.
- **Churn-Dedup & Port Scoping Fix** — Killed environment-variable data leak. `CliBackendBase.BuildSpawnEnv()` now sends ONLY `UNITY_MCP_SESSION_TIMEOUT`. Port and chat flags delivered via scoped --mcp-config (per-backend JSON/TOML/env files), never injected into process env. Prevents cross-connect churn when multiple projects open simultaneously.

## [v0.54.1] — 2026-06-23

**Connection Stability — Focus Loss CPU Storm Fix:**
- **Focus-Loss CPU Storm (Multi-Unity × Multi-CLI)** — Fixed 1000% CPU spike when Unity loses/regains focus with multiple CLI tools connected. Root cause: All socket I/O in `MCPServer.cs` captured `UnitySynchronizationContext` (18 awaits without `ConfigureAwait(false)`). When editor loses focus, `EditorApplication.update` throttles → task continuations freeze → heartbeat timeout → reconnect storm on focus regain.
  * **C# Threading Model (v0.54.1):** Added `ConfigureAwait(false)` to all 18 socket-awaits in `RunAcceptLoop` and `HandleClientAsync`. Continuations now execute on ThreadPool, not main thread. Added invariant: **Unity Editor API is only called on main thread** — all `Debug.Log*`, `EditorApplication.QueuePlayerLoopUpdate()`, and `RefManager.Invalidate()` marshaled via `_mainThreadQueue` using `_mainThreadQueue.Enqueue()`. Cached domain stamp in volatile `_domainStamp` field (read on main thread in `StartAsync`, used by fast-path get_version on ThreadPool). Added comments marking threading boundaries.
  * **Python Defense-in-Depth (v0.54.1):** Added reconnect cooldown gate to both `send()` and `_send_with_retry()` paths (was only on heartbeat), preventing burst storms. Added jitter (±10%) to retry delays. Enriched crash log with `bridge_id` (unique per instance), `reconnect_reason`, and `path` (send vs heartbeat) for observability. Incremented METRICS.reconnect.send_path counter. Atomic `_on_port_change` lock swap prevents race during port re-discovery.
- **Tests:** Added 3 new C# NUnit tests (ConnectionStabilityTests: focus loss reconnect, multi-CLI single socket, rapid focus toggle). Added 2 new Python tests (test_send_path_cooldown: gate on first attempt, test_focus_loss_stability: multi-CLI scenario). All tests green.

## [v0.53.1] — 2026-06-23

**Chat Bug Fixes:**
- **Codex App-Server Elicitation Hang** — Fixed infinite spinner on mutating MCP tools (`set_property`, etc.) in Codex chat. Root cause: Codex 0.141.0 sends `mcpServer/elicitation/request` JSON-RPC without timeout (OpenAI issue #11816); parser silently dropped it instead of auto-accepting. Read-only tools don't trigger elicitation, so they passed through normally.
  * **Layer 1 (Performance)** — Added approval suppression (`approvalPolicy`, `sandbox`:"danger-full-access", `sandboxPolicy`:{type:"dangerFullAccess"}) in `thread/start` and `turn/start` payloads via CodexAppServerBackend to suppress elicitation at source.
  * **Layer 2 (Correctness)** — CodexAppServerParser now auto-accepts our MCP-elicitation via ControlResponseBuilder.CodexElicitationAccept (JSON-RPC 2.0 reply); prevents hang even if Layer 1 suppression fails.
  * **Layer 3 (Invariant)** — Distinguish request (top-level `id` field) vs notification (no top-level `id`) using depth-aware `JsonHelper.ExtractString()`. Unknown requests auto-declined (safety net), benign notifications ignored. **Prevented regression:** `turn/started` notification with nested `params.turn.id` was falsely detected as request; now correctly ignored.
- **Improved Request Dispatch** — CliBackendBase now respects `ChatEvent.autoReply` field (AutoReply enum: None, CodexElicitationAccept, CodexElicitationDecline) to auto-submit JSON-RPC responses for inbound requests without user interaction.
- **DRY FormatRpcId** — Extracted `ControlResponseBuilder.FormatRpcId()` helper reused by both CodexElicitationAccept and CodexUserInputResponse for consistent numeric id formatting.

**Tests:**
- Added 18 new C# NUnit tests in CodexElicitationTests covering all Layer 1/2/3 paths (elicitation accept, unknown-request decline, benign-notification ignore, top-level vs nested id distinction). ControlResponseBuilderTests +4 tests (id formatting: int, string, null). CodexAppServerBackendTests +8 tests (sandbox/approval field presence in payloads).
- Total suite now 4,429 EditMode tests. All new tests green; 11 pre-existing failures in other asmdef (unrelated to this fix).

## [v0.53.0] — 2026-06-23

**Reliability & Stability:**
- **Reconnect stability** — Exponential backoff (5→60s) on failed reconnects + jitter; hard-coded 9500 fallback removed (read_unity_port now returns None for stale ports)
- **Idle-watchdog ppid-gate** — Server only auto-exits when orphaned (getppid mismatch), not on silent-pause
- **Per-port Chat config** — Prevents cross-connect between multiple Unity instances (per-port temp files + cleanup on startup/shutdown)
- **Test cleanup** — Removed hard-coded version check test

## [v0.52.6] — 2026-06-22 <!-- multi-unity-port-race-fix -->

**Bug Fixes:**
- **Multi-Unity Port Race Conditions** — Fixed port file collision and reconnection storms when multiple CLI tools (Cursor, Codex, Windsurf, etc.) connect to the same Unity instance.
  * **C# MCPServer.ShouldStartServer guard** — static constructor now checks batch mode before writing port files, preventing AssetImportWorker from polluting ~/.unity-mcp/ports/ during asset imports.
  * **C# PortResolver chat port collision guard** — ResolveChatPort ensures chat port ≠ main port, preventing accidental self-binding. FindFreePort ceiling raised 9599→9699.
  * **Python bridge port pinning** — `_pinned_port` and `_pinned_pid` cache ensure bridge sticks to the same Unity instance during domain reload cycles, preventing reconnection storms.
  * **Python server_filtering waterfall** — read_unity_port() env chain (UNITY_MCP_PROJECT_DIR > CLAUDE_PROJECT_DIR > os.getcwd()) enables multi-CLI project discovery.
  * **Python lockfile cleanup** — cleanup_stale_port_files() with TCP probe removes truly stale port files (not listening on bound port).

**Tests:**
- Added 17 new tests: MCPServerStartGuardTests (3), PortResolverTests (4 new), test_read_unity_port (7), test_bridge_port_rediscovery (6), test_lockfile (additions).

## [v0.52.5] — 2026-06-22 <!-- auto-discard-always -->

- **Auto-discard dirty scene on quit** — removed opt-in toggle, now always active. Prevents "Save Scene?" dialog blocking Unity on exit.
- **TestRunner compile guard** — `Execute()` rejects test runs during compilation, preventing stale-DLL test results.

## [v0.52.0] — 2026-06-21 <!-- arcade-animation-system -->

**Features:**
- **Arcade Animation System** — Unified animation primitives for consistent UI effects across all windows.
  * **ArcadePalette.cs** — Centralized color constants (Up=#3ad29f, Listen=#e8a23a, Down=#6e2b3a, Accent=#e94560) + `StateClass` seam for connection-aware colors. Prevents hardcoded #RRGGBB drift across codebase.
  * **ArcadeAnim.cs** — Shared animation library with USS class toggles (GPU-accelerated, zero per-frame cost): `AnimateClass`, `FadeIn`, `SlideInRight`, `ShakeX`, `PulseOnce`, `FlashClass`, `GlowPulse`, `CountUp`, `StaggerFadeIn`, `Typewriter`.
  * **ArcadeAnim.uss** — Shared USS keyframes + CSS transitions (@keyframes arcade-fade-*, arcade-slide-*, etc.).
  * **Per-window HeaderAnims** — DRY builders follow `VisualElement Build()` pattern:
    - `SamplingHeaderAnim.Build()` — 7-bar frequency analyzer for Sampling page
    - `StatusAmbientAnim.Build()` — scanline + grid + sonar ring overlay for Status window
    - `WizardStepAnim.cs` — slide transitions + progress bar for Setup Wizard
  * **WizardAnimUtils.cs refactor** — Now delegates to ArcadeAnim (−code duplication).
  * **MCPHub.uss + Updates** — Integrates arcade palette + anim classes.

**Tests:**
- Added 23 new C# NUnit tests: ArcadePaletteTests (7), ArcadeAnimTests (6), SamplingHeaderAnimTests (3), StatusAmbientAnimTests (5), WizardStepAnimTests (5). Total suite now 4,369 EditMode tests.

## [v0.51.0] — 2026-06-21 <!-- scene-annotation-primitives -->

**Features:**
- **Scene Annotation Primitives** — Expanded RegionTool with 3 new annotation modes: Point (location + label), Polyline (multi-vertex path with auto-length), Measurement (distance dimension). Unified `RegionSnapshot` model with `AnnotationType` field ("region"|"point"|"polyline"|"measurement"). Factory methods `CreatePoint()`, `CreatePolyline()`, `CreateMeasurement()` for programmatic creation. SceneAnnotationTool (Shift+A) unified entry point for all modes. `screenshot(annotation_id=id)` auto-frames and highlights saved annotations. RegionChipProvider extended with format methods for all annotation types.

**Tests:**
- Added 67 new C# NUnit tests: RegionSnapshotAnnotationTests (27), AnnotationDrawingModeTests (23), RegionChipProviderAnnotationTests (17). Total suite now 4,346 EditMode tests (12 pre-existing failures).

## [v0.50.3] — 2026-06-21 <!-- mcp-structured-output-cleanup -->

**Optimization:**
- **Unstructured MCP Output** — Introduced `_UnstructuredMCP(FastMCP)` subclass that forcibly disables `structured_output` on all 99 registered tools, eliminating duplicate `content` + `structuredContent` in MCP responses and `outputSchema` from ListTools. Reduces response size & Claude parsing overhead. Bumped `mcp` dependency to `>=1.28.0`.

## [v0.50.2] — 2026-06-21 <!-- visibility-hotfix -->

**Bug Fixes:**
- **WizardConfigWriter visibility** — changed class and `GitInstallUrl` from `internal` to `public` for cross-assembly access from `ChatMcpConfigWriter` (CS0122/CS0117 fix).

## [v0.50.1] — 2026-06-21 <!-- update-hotfix -->

**Bug Fixes:**
- **Update Cache Loop** — `UpdateChecker` now clears EditorPrefs cache after successful Level Up (v0.50.0 regression). Previously showed "v0.47.1 → v0.50.0 available" indefinitely.
- **Local Dev Install (git pull)** — `LocalPluginUpdater` now uses `git pull --autostash` to automatically stash/unstash dirty working tree. Adds actionable error message with exact command on failure (previously generic "Pull manually").

## [v0.50.0] — 2026-06-21 <!-- windows-install-improvements -->

**Installation & Setup:**
- **Wizard Fallback** — Setup Wizard detects missing backends (e.g., no Claude Code) and provides next-best-option UI (v0.47.1). Gracefully degrades on missing Python/uvx.
- **Config Visibility & Diagnostics** — Enhanced config diagnostics in `install.py doctor`. Detects stale MCP entries and missing backend configs. WizardConfigWriter now surfaces config errors in UI.
- **Antivirus Fallback** — Script execution blocked by antivirus on Windows mitigated with shebang detection and alternative bootstrap path (v0.47.1).

**Cross-Platform:**
- **TOML Path Validation (Windows)** — Codex config paths now properly handle Windows backslashes in TOML literal strings (single quotes) to prevent unicode escape interpretation (v0.44.1 regression fix).
- **File URI Standardization** — Config writers use OS-agnostic paths with cross-platform backslash handling in git URLs and config file paths.
- **Unified os.devnull Usage** — Replaced platform-specific `/dev/null` references with `os.devnull` for Windows compatibility across all subprocess calls.
- **Merged TOML Merge Helper** — `merge_toml_mcp` regex escape safety fixed to avoid backslash interpretation in replacements (v0.44.1 fix).

**DRY & Architecture:**
- **Git URL Constants (Single Source of Truth)** — Consolidated install URL and git references into `WizardConfigWriter.GitInstallUrl` (C#) and shared Python config. Removed duplicate URL definitions that diverged between implementations.
- **Dead Code Removal** — Removed `Screens` legacy UI directory and stale bootstrap artifacts (−380 LOC). Architecture now cleaner for UPM-only bootstrap.
- **PyPI → GitHub Migration** — `merge_mcp_config` now sources server from `git+URL` instead of PyPI registry for uvx to support offline installs and custom forks. Falls back gracefully if GitHub API unavailable.

**Bug Fixes & Diagnostics:**
- **Update Check (GitHub API)** — New `UpdateChecker.CheckGitHub()` queries releases endpoint with fallback to PyPI. Includes ETag caching and stale-config detection. Invalid API responses logged to doctor output.
- **PATH Refresh on Config** — `install.py configure` now refreshes shell PATH (macOS: source zshrc; Windows: ReadEnvironmentVariable) to ensure CLI tools are immediately available.
- **Stale Config Detection** — Doctor diagnoses config drift (mismatched version between Python server and saved config). Offers auto-repair via `install.py configure --repair`.
- **Bootstrap Fixes** — Fixed edge case where curl fails on macOS with SSL certificate errors; added fallback to `wget` and explicit certificate path handling.

## [v0.47.0] — 2026-06-21 <!-- level-design-toolkit -->

**Level Design Toolkit (Chat-Integrated Visual Tools):**

**F1: Token Counter + Context Progress Bar**
- Replaces USD cost display with input/output token counts + context window fill %
- **ModelContextWindows** — LLM context sizes (Claude 200k, Opus 4.8/4.6/4.7, Haiku 100k, Sonnet 400k, Codex/Gemini fallback)
- **ContextProgressBar** — UIToolkit animated progress bar (50px, responsive layout)
- **TokenFormat extended** — Displays `↑1.2k ↓840 | ▓▓▓▓░░░░░░ 45%` format

**F2: Component Field Chips**
- Right-click Component header → "Attach Field" dropdown to attach individual component fields to chat context
- **FieldChipProvider** — Auto-detection for component properties (priority 200)
- **FieldContextMenu** — Inspector context menu integration
- **ChipKindKeys** — New Kind: `Field` (extensible provider pattern)

**F3: Native Screenshot Button**
- Toolbar button (📷) captures current camera view directly to file
- **ScreenshotService** — Wrapper around existing ScreenshotCapture
- **ScreenshotToolbarButton** — Emits image chip, injects into chat automatically

**F4: Full Annotation Editor (11-file subsystem)**
- Complete drawing system with undo/redo, multiple tools
- **Tools**: Pen (freehand), Line, Arrow, Rectangle, Ellipse, Text, Erase
- **AnnotationCanvas** — Texture2D-backed pixel rasterization (bresenham lines, scanline fills)
- **AnnotationHistory** — Undo/redo stack with command pattern
- **AnnotationEditorWindow** — EditorWindow host with toolbar + color picker
- **AnnotationCompositor** — Flatten commands → PNG encode for sharing
- **AnnotationIcons** — Procedural vector icons (230 LOC, tool palette + region overlay icons)
- **AnnotateToolbarButton** — Chat toolbar launcher for annotation editor

**F5: Raycast World Coordinates**
- **AnnotationRaycaster** — Scene raycast from mouse position, returns world XYZ + GameObject
- **AnnotationMetaWriter** — Embeds hit data into annotation metadata JSON

**Supporting Features:**
- **Region Icons** — Procedural vector icons for region overlay (Lasso, Rect, Circle, PbP)
- **Region hasFocus Guard** — Prevents black GL flash on Scene View focus loss
- **Chip Thumbnails** — Inline thumbnail preview for snap/annotate image chips
- **Configurable Inactivity Timeout** — Settings UI, default 180s (was 90s hardcoded), range 30–600s

**Test Summary:**
- ~160 new C# NUnit tests (annotation editor, field chips, screenshot, context bar)
- C# NUnit EditMode: 4070 → 4126+ tests
- Total: 0 regressions

## [v0.46.0] — 2026-06-21 <!-- region-selection -->

**Region Selection for Level Design:**
- **Polygon2D** — Immutable 2D polygon (XZ plane), winding-number point-in-polygon test (nonzero fill rule), AABB bounds computation, CSV import/export, Ramer-Douglas-Peucker simplification
- **SceneRegionTool** — EditorTool with multi-mode FSM (Shift+R activate, Q/W/E/R mode switch, Enter commit, Esc cancel, G grid snap). Four drawing modes: Lasso, Rectangle, Circle, PointByPoint
- **SceneRegionQuery** — 3-stage spatial pipeline: AABB filter → component type filter → winding-number PIP → cap + format
- **SceneRegionState** — LRU registry (8 concurrent), EditorPrefs persistence, CSV export
- **Chat Integration** — RegionChipProvider adds "Region" dropdown option, persists across turns
- **Python spatial_query extended** — `objects_in_polygon` action accepts `vertices` (CSV 'x1,z1;x2,z2;...', >=3 pairs) or `region_id`

**Test Summary (v0.46.0):**
- 104 new C# NUnit tests: Drawing modes (5 files, 52 tests), Rendering (1 file, 52 tests)
- 20 new Python pytest tests: test_region.py (polygon validation, spatial queries, state management)
- C# NUnit EditMode: 3966 → 4070 tests (+104 RegionTool)
- Python pytest: 2621 → 2641 tests (+20 region)

## [v0.45.0] — 2026-06-20 <!-- install-source-detection -->

**Install Source Detection & Connect/Disconnect:**
- **InstallSourceDetector** — Detects `file:` (local Git clone) vs `git:` (UPM registry) via PackageInfo.source
- **LocalPluginUpdater** — `git pull --tags` for file: installs (async via Task.Run), validates HEAD matches tag
- **UpmPluginUpdater** — Client.Add chain for both editor + reload packages on git: update
- **UpdateDispatcher** — DRY routing replaces copy-paste in LevelUpPanel + UpdateBanner
- **ChatMcpConfigWriter** — `uvx` fallback for git: installs (no MCP server in PackageCache)
- **install.py connect** — Link Unity projects via `file:` refs in manifest.json (enables local plugin dev)
- **install.py disconnect** — Unlink projects, restore registry source
- **install.py pull** — CLI update for file: installs (git pull --tags, preserves server connection)

**Test Summary (v0.45.0):**
- 16 new C# NUnit tests: InstallSourceDetectorTests (8), LocalPluginUpdaterTests (6), UpmPluginUpdaterTests (2)
- 14 new Python pytest tests: test_install_connect.py (8), test_install_pull.py (6)
- Python pytest: 2621 passed (was 2606, +15 install tests)
- NUnit EditMode: 3966 passed, 5 pre-existing (total 3971)

## [v0.44.1] — 2026-06-20 <!-- codex-windows-hotfix -->

- **Fix: Codex Windows path crash** — TOML `command` now uses literal strings (single quotes) so `C:\Users\...` paths are not interpreted as unicode escapes
- **Fix: regex escape in merge_toml_mcp** — `re.sub` replacement uses lambda to avoid `\U` backslash interpretation

## [v0.44.0] — 2026-06-20 <!-- arcade-levelup-codex-config -->

**Arcade Level Up UX:**
- LevelUpPanel: 4-state machine (Idle→Animating→Done→Diff) with XP bar + sparkles animation
- LevelUpAnimator: Progressive bar fill + particle effects via AnimationCurve
- ReleaseDiff: Parses CHANGELOG.md for release notes (version comparison, content extraction)
- LevelUpAnim.uss: Complete animation stylesheet
- UpdatesPage.cs: Swapped UpdateBanner → LevelUpPanel for update flow

**Codex Config Hardening:**
- merger.py: Strips stale `[mcp_servers.unity]` entries on first write, preserves environment, creates .bak backup (first-write-wins)
- install.py doctor: Warns about stale Codex MCP entries
- WizardConfigWriter: HasBackup + RestoreConfig methods for config rollback
- AiConfigScreen: Restore button in UI (recovery from corrupt config)

**Test Summary:**
- 12 new LevelUp NUnit tests (state machine, animations, release parsing)
- 9 new WizardConfigWriter NUnit tests (backup/restore, merge safety)
- Python pytest: 2606 passed (was 2597, +9 config tests)
- NUnit EditMode: 3945 passed, 5 pre-existing (total 3950)

**Stability:**
- ReloadMiniServer.cs: Fixed CS1503 (explicit TcpClient variable)
- HelperTests.cs: Removed MCPServer.Stop() (was killing TCP)

## [v0.43.0] — 2026-06-20 <!-- reload-stability -->

**Crash Prevention:**
- Remove tundra.digestcache deletion (SIGABRT in RegisterAssemblyDefinition)
- MCPStatusWindow OnDisable stops Socket.Poll freeze during domain reload
- ReloadMiniServer tracks+closes clients on Stop (fd leak + reload freeze)
- [MovedFrom] on EditorWindows moved across assemblies (layout crash)
- TeardownCore drains _mainThreadQueue (use-after-free after domain unload)

**Stale DLL Detection:**
- ComputeStamp iterates all UnityMCP.* assemblies (was single-assembly blind)
- ReloadGuard.ForceUnlock + constructor rebalance call AssetDatabase.Refresh
- PID liveness check in port file discovery (dead PIDs blocked commands)
- TCP probe in is_startup_in_progress (false "Unity busy" live bug)
- DOMAIN_RELOAD_EXPIRY_S 30→90s (9-assembly reload window)

**Hardening:**
- Wizard asmdef autoReferenced:false (compile error isolation)
- ReloadGuard OnTurnStarted exception safety (asymmetric lock rollback)
- Bridge passes port to autodetect_project_path

**Test Summary (v0.42.1):**
- 39 new stress tests added (across multiple test files)
- Focus on domain reload reliability under heavy load

## [v0.42.0] — 2026-06-20 <!-- wizard-detection-scope-chips -->

- **Setup Wizard One-Button Install** — 3-screen flow (Welcome → PickBackend → Configure). 9 backends: Claude Code/Desktop, Cursor, Windsurf, VS Code, Codex, Kimi, OpenCode, Antigravity. Runs `install.py configure --tool <key>` from Unity, cross-platform (macOS/Windows/Linux)
- **Backend Auto-Detection** — PickBackend screen shows "detected" badge for installed tools. Checks binary in PATH (`which`/`where`) and config directory existence (`~/.claude`, `~/.cursor`, etc.)
- **Global/Project Scope Toggle** — Configure screen lets user choose Global (home dir) or Project (Unity project root) config scope. Project writes `.mcp.json` / `.cursor/mcp.json` / `.vscode/mcp.json` per tool
- **Codex TOML Support** — `merge_toml_mcp` handles Codex's `config.toml` format. Text-based merge preserves existing `[mcp_servers.*]` sections
- **Merge Safety** — `merge_mcp_config` now raises `ValueError` on corrupt JSON instead of silently resetting to `{}` (data loss prevention)
- **Updates Hub Card** — "Updates" card in MCPHubUI opens UpdatesPage with Check button and Changelog viewer with markdown formatting
- **MarkdownInlineFormatter** — Extracted to base assembly for DRY reuse (bold, italic, code, links). Chat's `MarkdownInline` delegates to it
- **Input Chip Clicks** — Chips/bubbles in input field are now clickable (navigate to hierarchy/assets), reusing `ChipClickRouter` (DRY, no double context menu)
- **Wizard asmdef Split** — `UnityMCP.Editor.Wizard` separate assembly. Diagnostic windows (MCPDiagnosePanel, MCPStatusWindow) moved to Wizard. `autoReferenced: true` avoids circular deps
- **Python 3.9 Compat** — All PEP 604 `X | None` → `Optional[X]` across config module for macOS system Python compatibility

## [v0.41.4] — 2026-06-20 <!-- chat-at-mentions -->

- **@Mention Autocomplete** — Type `@` in Chat input to trigger autocomplete popup. 6-layer modular system: MentionTokenParser (cursor scan) → MentionFuzzyScorer (allocation-free fuzzy match) → [SceneMentionIndex, AssetMentionIndex, RecentMentionSource] indices → MentionCoordinator (merge/dedup/sort) → MentionPopup (UIToolkit, max 8 rows) → InlineChipField.ReplaceMentionRangeWithChip (insert chip at cursor). Features: 3000-entry scene hierarchy cap, asset database sync, Selection.activeGameObject boost, keyboard-navigable popup (arrow keys, Enter select, Esc dismiss), 100ms debounce on typing.

**Test Summary (v0.41.4):**
- **C# Tests (72 new NUnit tests, 10 test files)**
  * MentionTokenParserTests (13 tests): token extraction, cursor position, multi-word paths
  * MentionFuzzyScorerTests (10 tests): fuzzy matching, word-boundary scoring, pre-filter
  * SceneMentionIndexTests (7 tests): hierarchy indexing, version tracking, capacity
  * AssetMentionIndexTests (13 tests): asset database sync, lifecycle, cleanup
  * MentionCoordinatorTests (7 tests): merge, dedup, sort, cap behavior
  * MentionPopupTests (8 tests): UIToolkit popup show/hide, keyboard handling
  * MentionIntegrationTests (5 tests): end-to-end @mention flow
  * MentionPerfTests (5 tests): index performance, scaling to 3000 entries
  * MentionEdgeCaseTests (5 tests): ambiguous names, rapid typing, unicode
- **Total: 3863 NUnit tests (72 new, 3791 baseline)**

## [v0.41.0] — 2026-06-20 <!-- session-handoff-copy-antigravity -->

- **Session Handoff (Chat↔CLI)** — Button "→ CLI" in Chat copies resume command to clipboard. Format per-backend: `--resume {sessionId}` (Claude/Codex), `--conversation {sessionId}` (Antigravity), `-s {sessionId}` (OpenCode), `-S {sessionId}` (Kimi). SessionScanner reads CLI history files to populate session picker popup for resuming old sessions in Chat.
- **Copy Message UX** — Right-click "Copy as sent to LLM" on messages and input field. CopyFlash shows "Copied!" notification via View seam.
- **Gemini→Antigravity Migration** — Complete backend replacement. Old Gemini (gcloud CLI, NDJSON protocol) removed. New Antigravity backend: plain-text output (no NDJSON), EofSentinel injection on process finish. Files: AgyArgBuilder, AgyParser, AntigravityBackend, AntigravityProvider, +4 test files.
- **Exit-Code Race Fix (macOS)** — stderr-thread race on process termination eliminated via explicit WaitForExit before reading exit code. Prevents false -1 code on noisy stderr.

**Test Summary (v0.41.0):**
- **Python Tests (2540 unit + 76 live + 4 live_cli = 2620 total)**
- **C# Tests (3791 NUnit + session/copy/Antigravity tests = 3800+ total)**
- **Total: 6407+ test assertions, 100% pass rate**

## [v0.40.1] — 2026-06-19 <!-- chat-tcp-fix -->

- **Fix: Chat duplicate TCP connections** — Claude Chat no longer spawns parasitic MCP servers from `~/.mcp.json`; env vars (`UNITY_MCP_PORT`, `UNITY_MCP_CHAT`) scoped per-backend via `--mcp-config` env block (Claude) and TOML `-c` flags (Codex)
- **Fix: Codex Chat TCP routing** — Codex `app-server` disables static `unity`/`unity-mcp` MCP entries and registers `unity_chat` with correct chat port, preventing CLI-port fallback

## [v0.40.0] — 2026-06-19 <!-- install-ux-revolution -->

- **One-Liner Installation** — `curl | bash` (macOS/Linux) or `iex (iwr).Content` (Windows) bootstraps everything: Python server via `uvx unity-mcp`, Unity plugin via UPM git URL
- **Setup Wizard** — 4-screen animated wizard (Python check → Server test → AI Config) accessible via MCP/Setup Wizard menu. 8 backend cards: Claude Code/Desktop, Cursor, Windsurf, Gemini, Kimi, Codex, OpenCode
- **Doctor MCP Tool** — 5 async health checks (Python, ports, lockfile, TCP, Unity state) with 3 safe auto-fixes. Available as `doctor` MCP command
- **Config Auto-Generation** — `python install.py configure --tool <name>` for Claude Code/Desktop, Cursor, Windsurf. JSON merge preserves existing MCP servers
- **Update Checker** — manual "Check for Updates" button in MCPStatusWindow, PyPI + GitHub Releases with 24h cache
- **CHANGELOG Viewer** — foldable changelog section in MCPStatusWindow, newer entries marked with ★
- **Health Dashboard** — "Diagnose" button in MCPStatusWindow with animated scan + staggered results
- **Version Unification** — Python server and Unity plugin share version 0.40.0. PROTOCOL_VERSION=3 with backward-compatible handshake
- **Premium CLI UX** — braille spinners, ANSI colors, unicode box frames, NO_COLOR support, cross-platform degradation

## [v0.38.0] — 2026-06-19 <!-- External MCP server support in Chat -->

**Major Features:**

- **External MCP Server Support in Chat:**
  * **Claude Backend**: Removed `--strict-mcp-config` flag to allow Claude CLI to merge our `--mcp-config` with user's `~/.claude/` MCP servers (Blender MCP, luna-kiss-mcp, etc.)
  * **Gemini Backend**: Fixed `RewriteWithFreshMcp()` to only replace the "unity-mcp" entry, preserving other MCP servers configured by user
  * **Kimi Backend**: Fixed `WriteMcpConfig()` to merge instead of full-overwrite — preserves user's other MCP servers in kimi config
  * **Codex & OpenCode**: Already supported external servers (no changes needed)
  * **JsonMergeHelper.cs** (~35 lines): New DRY utility for brace-depth JSON merge, used by Gemini and Kimi arg builders

**Test Summary (v0.38.0):**

- **C# Tests (3709 NUnit, all green):**
  * New: JsonMergeHelperTests (8 tests: basic replace, preserve others, brace balance, nested braces, null/empty)
  * Extended: GeminiArgBuilderTests (+1), KimiArgBuilderTests (+2 for merge verification, brace balance assertions)
  * Changed: ClaudeArgBuilderTests (−1, removed strict-mcp-config assertions, added negative assertion that flag is absent)
  * Previous: 3699 → 3709 NUnit

## [v0.37.0] — 2026-06-18 <!-- Bridge stability, reload/recompile hardening, test infrastructure -->

**Major Fixes:**

- **Bridge Stability & Reload Recovery (v0.36.0):**
  * **DomainReloadTracker** — dataclass with 30s expiry tracking domain reload state independently from compile probe. Three methods: `mark()` (on DomainReloadError), `clear()` (on success), `is_active()` (checks expiry). Shared between bridge.send() and heartbeat.
  * **BridgeState enum** — four states (DISCONNECTED | CONNECTED | DOMAIN_RELOADING | FAILED) track connection lifecycle explicitly
  * **should_retry()** — pure decision function extracting retry logic: signature `(error, attempt, deadline) → (should_retry, delay_s, reason)`. On DomainReloadError: marks reload + state→DOMAIN_RELOADING. On any error: checks reload.is_active() or probe_busy(), backoff 2^attempt ≤ 8s.
  * **Atomic reader/writer close** (v0.36.0) — both reader and writer closed atomically within lock during _reconnect() to prevent zombie reads after close. Fixes CancelledError cleanup.
  * **Bridge retry delays restored** — 2s→4s→8s backoff sequence (was regressed to 1s→2s→4s, giving up before domain reload completes)

- **Reload/Recompile Hardening:**
  * **MCPServer.IsReallyCompiling** — managed flag replaces latching EditorApplication.isCompiling. False-positive "backgrounded" compile state eliminated via 120s wedge guard.
  * **SyncHelper.Refresh** — ForceUpdate defeats Bee "inputs unchanged" gate, unconditional recompile
  * **ImportPackageSources** — mvfrm nuke + digestcache delete instead of per-file import (never reached Bee)
  * **TestRunner.ResetOnReload** — clear stale SessionState results on domain reload
  * **reload_ladder: cs_grace=1** — tolerates transient CS errors during import

- **Chat Stability (v0.36.0):**
  * **ChatMcpConfigWriter** — emits "env" block with UNITY_MCP_PORT in mcp.json (chat port propagation to Python)
  * **MCPServer.WritePortFile** — dual files: {pid}.port (main) + {pid}.chat-port (Windows env fallback). CliBackendBase injects UNITY_MCP_CHAT=1 env marker.
  * **server_filtering.py** — chat-port fallback when UNITY_MCP_CHAT=1. _is_pid_alive cross-platform check (Windows: OpenProcess/CloseHandle, Unix: os.kill(pid,0))
  * **Timeout messaging** — includes last tool name: "[Timed out: no response for 300s (last tool: set_property)]"
  * **Dead-process guard** — appends "[Process exited]" to transcript when backend unexpectedly exits

- **Test Discovery:**
  * **get_test_count** TCP command — async discovery via TestRunnerApi.RetrieveTestList, returns `N|edit=X|play=Y` (accurate count including parameterized tests). First call returns "discovering", subsequent calls return cached result (cleared on domain reload).
  * **readme_facts.py** — TCP-first counting with retry for "discovering" state, grep fallback for offline

- **Test Infrastructure:**
  * **check_unity.py** — parses dlls= field from diagnose, exit 2 on stale assemblies. 12 new tests validate assembly detection.
  * **ConsoleCaptureTests** — 8 new tests: ring buffer, GetErrorsSince, count tracking, empty buffer edge cases
  * **TestPaths.EnsureFolder** — public segment-walk with [SetUpFixture] global cleanup
  * **SerializerTests** — self-contained shader test (no order dependency on AllTypes.shader)
  * **Roslyn DLL path fix** — Unity 6 ARM support (MonoBleedingEdge location)
  * **bridge.connected fix** — Python 3.12 TransportSocket unwrap to _sock
  * **PYTHONWARNDEFAULTENCODING=1** — all subprocess calls properly encoded

**Test Summary (v0.37.0):**

- **Python Tests (2472 total, all green):**
  * New: test_bridge_reload_state.py (8), test_bridge_should_retry.py (8)
  * Extended: test_bridge.py (+50), test_bridge_edge_cases.py (+44), test_check_unity.py (+76), test_server_edge_cases.py (+32)
  * Test markers: 2450 unit tests (pytest unit), 78 live integration (live && !live_cli), 4 live CLI (live_cli)

- **C# Tests (3699 NUnit, 101 reload-latch specific, all green):**
  * New: ConsoleCaptureTests (8), TestAssemblySetup (1)
  * Extended: CommandRouterTests (+12 for two-layer IsCompiling), TestRunnerTests, SerializerTests
  * Live socket stability: 5 test_sync_live tests green (bridge.connected fix)

- **Overall: 2472 Python + 3699 NUnit = 6171 total assertions, 100% pass rate**

## [v0.36.0] — 2026-06-18 <!-- Media preview redesign, chip click UX, asset navigation -->

- **Media Preview Redesign:**
  * New `ResponseTagTokenizer` — single-pass tokenizer for `[kind:ref]`, `⟦kind:ref⟧` fences, and bare file paths; extensions come from `IChipKindProvider.BarePathExtensions`
  * `HierarchyReference` + `HierarchyResolver` — robust scene-object identity via path, InstanceID, and GlobalObjectId
  * `ChipExistenceService` — instance-based existence cache with disposable subscriptions and EditorApplication hook cleanup
  * `PreviewBuilderRegistry` + kind-specific `IPreviewBuilder`s (`Image`, `Audio`, `Model`, `Prefab`, `Hierarchy`, `Asset`) — extensible inline preview pipeline
  * `AssetPreviewService` — cancellable async preview queue with in-flight deduplication
  * `MixedParagraphRenderer` refactor — tokenized rendering, `StaleStateDecorator`, `ChipClickRouter`, and `ChipInlinePreviewPanel` wired to registry/cancellation
  * `IChipKindProvider` adds three new members: `BarePathExtensions[]`, `Ping(reference)`, `BuildPreview(path)` — enables plugins to provide bare-path recognition + navigation + custom preview UI
  * `MixedParagraphRenderer` no longer hard-codes hierarchy vs asset ping logic (delegates to provider `Ping()`)
  * Removed legacy static preview seams (old `AssetPreviewCache` facade, `InlineImageThumbnail.cs`)

## [v0.35.0] — 2026-06-17 <!-- Media preview bubbles, asset export/import, port persistence, README facts auto-sync -->

**Major Features:**

- **Inline Media Preview Bubbles** — Phase 2 lazy-load media panel in chat:
  * **ChipInlinePreviewPanel.cs** — Toggle panel with lazy texture/image/model/prefab/audio preview loading
  * **InlinePreviewBuilder.cs** — Extensible preview factory with TextureLoader seam for testing
  * **MultiImageBubbleTests.cs** — Multi-image bubble support (3 new tests)
  * Chip providers register lazy-build handler via public seam, click shows/hides panel (no screen-space pollution)

- **Asset Export/Import Enhancements:**
  * `include_deps` parameter for `export_package` — skip dependencies if false (token optimization for large packages)
  * Import manifest parsing — returns list of imported asset paths
  * **AssetDatabaseHelper.cs extended** (+60 lines) — dependency filtering + import result tracking

- **Port Persistence via ProjectSettings** — Survives Library purge:
  * **PortResolver.cs extended** (+37 lines) — 4-arg ResolvePort chain: env → ProjectSettings/MCPSettings.json → Library/MCP_Port.json → FindFreePort
  * **SaveProjectSettings()** — User-intent persistence at ProjectSettings/MCPSettings.json (separate from Library cache)
  * 25 new NUnit tests (PortResolverTests: environment priority, fallback chain, dual-port edge cases)

- **README Facts Auto-Sync Pipeline:**
  * **readme_facts.py** — Extract stats (tools, tests, versions) from _meta.json (8 lines)
  * **update_readme.py** — Render facts into README (generated marker blocks, +14 lines)
  * **test_readme_facts.py** — Validation + --check-facts guard (114 lines, 6 test methods)
  * Prevents manual README drift; CI/release script auto-syncs _meta.json → README

**Test Summary (v0.35.0):**

- **C# New Tests (120 total):**
  - ChipInlinePreviewPanelTests: 8 tests
  - ImageViewerWindowTests: 8 tests  
  - InlinePreviewBuilderTests: 9 tests
  - MultiImageBubbleTests: 3 tests
  - PortResolverTests: 35 tests (new + extended)
  - AssetHelperTests: 32 tests (extended)
  - ChatChipPolicyTests: 8 tests (extended)
  - ChipKindRegistryTests: 4 tests (extended)
  - AssetViewerFactoryTests: 11 tests (extended)
  - Other: 2 tests (ImageBlockRendererTests, InlineImageThumbnailTests extended)

- **Python New Tests (6 + 6 extended):**
  - test_readme_facts.py: 6 tests
  - test_server_asset.py: 6 tests extended

- **Total: ~126 new assertions across C# + Python**
- All green: 5159 total tests (tests_unity: 2657, tests_python: 2422, tests_live: 80)

## [v0.34.6] — 2026-06-17 <!-- Binary resolver, model leak, Kimi K2 fixes, install docs -->

**Fixed:**

- **Binary Resolver — macOS zsh PATH sourcing** — Changed `bash -lc` to `zsh -lic` for macOS to correctly source `~/.zshrc` where kimi/opencode PATH is defined. Fixes "command not found" for CLI backends when installed via Homebrew. **Root cause:** bash doesn't inherit zsh profile. Switched to `LoginShellCommand.Create()` on macOS.

- **Model Name Leak on Backend Switch** — Fixed crash when switching backends (e.g., Claude → Codex). Previous code stored selected model string in Unity EditorPref without backend-specific validation. **v0.34.2 regression** where Claude model "Sonnet 4.6" passed directly to Codex args (invalid). **Fix:** BackendConfigStore now mirrors model selection per-backend in JSON config (Claude/Codex/Gemini/Kimi each get separate `Model` field).

- **Kimi K2 Protocol — 4 bugs fixed:**
  * **Model autoconfig:** Kimi now reads `~/.kimi-code/models.json` (standard config location). Plugin writes model aliases + API model names at startup. Empty model field in BackendConfig falls back to kimi's own `config.toml` default (no hardcoded "kimi-k2.6" leak).
  * **Config file path:** Removed `--mcp-config-file` flag — kimi automatically reads `~/.kimi-code/mcp.json` (spec-compliant). Plugin writes to standard location only.
  * **Approval mode flags removed:** `--yolo` and `--plan` incompatible with `-p prompt` mode. Kimi ignores them silently; removed from argv to reduce noise.
  * **Model ID migration:** Old model IDs (kimi-k2.6 → k2p6, kimi-k2.7-code → kimi-for-coding) auto-migrated on load via `BackendConfigStore.MigrateKimiModel()`.

- **Binary Resolver — Parallelized stdout/stderr read** — Linux + macOS now read stdout and stderr in parallel to avoid deadlock when stderr buffer fills. Fixes rare "command not found" hang. Process timeout budget tracked with stopwatch to avoid exceeding 3s overall.

- **Binary Resolver — Removed multiline rejection** — macOS "RejectIfMultiline" heuristic caused false-positive rejects. Now use unified `PickLinuxPath()` for both platforms. Detects path validity via file existence check, not multiline newlines.

**Added:**

- **Install docs:** `docs/install/kimi.md` — setup guide for Kimi K2 CLI backend (Homebrew, PATH, model config).
- **Install docs:** `docs/install/gemini.md` — setup guide for Gemini CLI backend (gcloud auth, model selection).

**Test Summary (v0.34.6):**
- New tests: ChatBinaryResolverTests (27), KimiArgBuilderTests (72), KimiParserTests (26), BackendConfigStoreTests (47), ToolPingTests (24), ModelSelectorTests (17), CommandRouterTests (68 extended), ComponentSerializerTests (18), PortResolverTests (16) = ~215 new assertions
- All green: 1562+ EditMode tests
- **Commits (3):**
  1. fix: Windows stability + macOS binary resolver + multi-scene disambiguation (v0.34.2)
  2. fix: prevent model name leak when switching backends (Claude→Codex)
  3. fix: Kimi K2 CLI backend — 4 protocol bugs + model autoconfig + install docs

## [v0.34.0] — 2026-06-17 <!-- Plugin extensibility + image drag-drop + asset viewers + Kimi K2 + OpenCode backends -->

**Major Features:**

- **Plugin Extensibility API** — New public interfaces for plugins to extend chat UI without core edits:
  * **ISettingsProvider**: Plugins register custom settings pages
  * **IToolbarButtonProvider**: Plugins add toolbar buttons  
  * **IPanelProvider**: Plugins register side panels (dock + overlay support)
  * All use `[InitializeOnLoad]` auto-discovery pattern via new registries

- **Image Drag-Drop + Clipboard Paste** — Full image attachment workflow:
  * **ClipboardImageReader.cs** — Platform-specific clipboard reads (macOS NSPasteboard, Windows CF_DIB, Linux xclip)
  * **ImageAttachmentStore.cs** — Temp file lifecycle management for pasted/dropped images
  * **MCPChatWindow integration** — Ctrl+V paste, Finder drag-and-drop, image reference embedding in turn JSON
  * **Tests**: 37 ClipboardPaste + 154 ImageDragDrop + 76 UserTurnBuilderImage tests (367 total)

- **Inline Image Thumbnails in Chat** — Images render as clickable thumbnails in paragraphs (max 100px height)
  * **InlineImageThumbnail.cs** — Thumbnail rendering with click→full viewer navigation
  * **Tests**: 116 InlineImageThumbnailTests

- **Asset Viewers** — Extensible media preview system:
  * **IAssetViewer interface** — Plugins implement custom viewers
  * **AssetViewerFactory.cs** — Registry + factory with window management
  * **Built-in viewers**: Prefab (3D preview), Model (.fbx/.obj/.blend/.dae), Sprite (with grid), Audio (with playback)
  * **Seam pattern**: `AssetChipProviderBase.ViewerLauncher` — chip Navigate() routes to viewers first
  * **Tests**: 224 AssetViewerFactory + 198 PrefabViewerWindow tests (422 total)

- **Kimi K2 CLI Backend** (v0.34.0):
  * **KimiArgBuilder.cs** — Role-based NDJSON protocol (system→user→assistant)
  * **KimiParser.cs** — Newline-delimited event parsing
  * **KimiBackend.cs + KimiProvider.cs** — Auto-discovered via TypeCache
  * **Tests**: 214 KimiArgBuilder + 243 KimiParser (457 total)

- **OpenCode CLI Backend** (v0.34.0):
  * **OpenCodeArgBuilder.cs** — Multi-provider model selection (Claude/GPT/Gemini) with format conversion
  * **OpenCodeParser.cs** — Stream-json parsing compatible with Claude SDK
  * **OpenCodeBackend.cs + OpenCodeProvider.cs** — Persistent stdin loop, auto-discovered
  * **Tests**: 222 OpenCodeArgBuilder + 273 OpenCodeParser (495 total)

- **Chip Kind Extensions** — New media chip types:
  * Image (external .png/.jpg/.bmp/.gif/.webp/.tiff), Model (.fbx/.obj/.blend/.dae), Audio (.wav/.mp3/.ogg/.aiff)
  * Priority ordering prevents collisions with built-in asset providers

- **Provider Registry Consolidation** — Base class for extensible registries:
  * **ProviderRegistry.cs** — DRY consolidation (Settings/Toolbar/Panel registries inherit)
  * **KeyRegex hoisting** — Non-generic companion avoids static-in-generic reflection issues
  * **Tests**: 57 ProviderRegistryTests

**Bug Fixes:**

- **Codex app-server model flag** — Changed from `--model` to `-c model="..."` (v0.33.1 regression fix)
- **GeminiBackend** — Removed deprecated file (superseded by GeminiBackend in CLI assembly)
- **Settings layout scroll** — Added scrolling container for long settings pages
- **Test corrections** — Eliminated 5 skipped tests via timeSinceStartup seam + URP shader setup
- **Compile errors fixed** — All 13 build warnings resolved across new feature codebases

**Test Summary (v0.34.0):**
- Python: 0 new (no server changes)
- C#: **1402 new tests** across CLI + View assemblies
  - CLI backends: 214 KimiArgBuilder + 243 KimiParser + 222 OpenCodeArgBuilder + 273 OpenCodeParser = 952 tests
  - Images: 188 ImageAttachmentStore + 76 UserTurnBuilderImage + 37 ClipboardPaste + 154 ImageDragDrop + 116 InlineImageThumbnail = 571 tests
  - Viewers: 224 AssetViewerFactory + 198 PrefabViewerWindow = 422 tests
  - Plugin API: 72 PluginSettings + 105 PluginToolbar = 177 tests
  - Providers: 214 BuiltInChipProviders + 57 ProviderRegistry = 271 tests
  - **Total EditMode: ~3000+ green** (was 2623, +377 net change)

**Commits (13):**
1. feat: plugin extensibility API — settings, toolbar buttons, panels
2. feat: image drag-and-drop + clipboard paste into chat
3. feat: inline image thumbnails in chat paragraphs
4. feat: prefab preview window on chip click
5. feat: asset preview viewers — 3D model, sprite, audio
6. feat: Kimi K2 CLI backend — role-based NDJSON, MCP config file
7. feat: OpenCode CLI backend — multi-provider model selection
8. fix: eliminate skipped tests — timeSinceStartup seam + URP shader setup
9. fix: compile errors + settings layout scroll + test corrections
10. fix: P0+P1+P2 review findings — Kimi TurnDone, security hardening, prefab factory
11. fix: inline image display in user bubble + attach button in footer
12. fix: P2 MAJOR review findings — DRY registries, shared ExtractPlainText, error propagation
13. fix: hoist KeyRegex to non-generic ProviderRegistry companion

## [v0.33.0] — 2026-06-16 <!-- Chat: Codex silent abort fix + model list expansion -->

- **Codex Silent Abort Fix (Plugin v0.33.0)** — Fixes hung turns when Codex tools error silently. **Root cause:** Codex sets `status:"completed"` even when MCP tool returns error; only the nested `result.isError:true` flag indicates failure (no space in compact JSON). **Fix (CodexAppServerParser):** Changed isError detection from absent to `!resultObj.Contains("\"isError\":true")` pattern-match (handles both spaced and unspaced JSON). Extracts result text regardless of isError flag; if error and text empty, append `"[MCP tool error]"` placeholder. **Tests:** 6 new CodexAppServerParserTests covering tool errors, silent failures, and result text extraction.

- **Codex Inactivity Watchdog (Plugin v0.33.0)** — Fixes turns stuck when Codex reasoning (o3/o3-pro) thinks silently for 2–5 minutes with no event emissions. **Implementation (MCPChatWindow.Drain.cs):** (1) New `_lastEventTime` field tracks timestamp of last drained event. (2) New `InactivityTimeoutSec` property returns 300s for Codex, 90s for Claude/Gemini (reasoning models need longer). (3) In DrainAndRender() loop, check if `EditorApplication.timeSinceStartup - _lastEventTime > InactivityTimeoutSec` while backend is running; if so, emit failure card `"[Timed out: no response for {timeout}s]"`, finalize turn, call `OnTurnFailed()`. (4) Reset `_lastEventTime` on every OnSend (turn start) and every event drain (keeps watchdog alive). **Why:** Codex emits silence during long reasoning; old code assumed stalled = dead process and called `OnProcessDead()`, losing in-flight reasoning work. New approach: let the timeout decide, preserve results if any. **Tests:** 2 new inactivity timeout scenarios in MCPChatWindow tests.

- **New ChatEventKind: Heartbeat (Plugin v0.33.0)** — Added to ChatEvent enum to support keepalive events that reset the inactivity watchdog without rendering. **CodexAppServerParser now emits Heartbeat** on "reasoning" events (silent proof-of-life during o3 thinking). Factory: `ChatEvent.Heartbeat()`.

- **Model List Expansion (Plugin v0.33.0)** — Extended model presets per backend with latest LLM lineup. (1) **Claude:** Added Fable 5, Opus 4.8, Opus 4.7, Sonnet 4.6 (was only Haiku). (2) **Codex:** Added GPT-5.5, GPT-5.4, GPT-5.4 Mini, o3-pro, o3, o4-mini, GPT-4.1 Mini (was only defaults). (3) **Gemini:** Added 3.5 Flash, 3.1 Pro Preview, 3 Pro Preview, 3 Flash Preview, 2.5 Pro, 2.5 Flash, 2.5 Flash Lite (was only defaults). Each backend dropdown now shows 6–8 model options + Custom field. **ModelPresets.cs (NEW):** Extracted presets into ModelPresetEntry/ModelPresetsConfig/ModelPresetDefaults (DRY separation of data from config UI). **BackendConfigStore.GetPresetsForKind():** Looks up ModelPresets config in Library/MCP_ChatBackendConfig.json; if not found, falls back to hardcoded defaults. Allows users to override presets via config file without recompile. **Tests:** 44 new BackendConfigStoreTests (preset lookup, fallback, custom sentinel) + 160 ModelSelectorTests updated (dropdown state, persistence, custom entry).

- **Tests:** 57 NUnit EditMode passed (new tests for watchdog, tool errors, model config), 2410 pytest green, compile clean.

## [v0.32.0] — 2026-06-16 <!-- run_tests fire-and-forget + P5 heartbeat fix -->

- **run_tests Fire-and-Forget Protocol (Server v0.32.0)** — `run_tests(mode)` now returns immediately with message `"tests-started|{mode}|poll get_test_results every 5s for up to 2min"`. Does NOT poll internally. **Why:** avoids inline TCP blocking on domain reload (Editor.log clears "compiling" status before port 9700 restored). Initial send() uses short 8s timeout (fire-and-forget pattern). If `DomainReloadError` caught, returns immediately. **Caller pattern:**
  ```python
  result = await run_tests(mode="EditMode")  # → "tests-started|EditMode|..."
  for _ in range(24):  # poll externally, 2min @ 5s intervals
      await asyncio.sleep(5)
      result = await get_test_results()
      if result not in ("pending", "none"): return result
  ```
  **Bridge resilience continues:** When `DomainReloadError` caught, pins `domain_reload_in_progress=True` for all retries (v0.31.1 P0 fix). `get_test_results` allowed during compile (v0.31.1 P1 fix).

- **P5: Graceful Heartbeat Stop on Parent Death** — When parent process dies (2 consecutive PPID mismatches), calls `stop_heartbeat()` instead of `raise SystemExit(0)`. **Why:** `SystemExit` is `BaseException` — escapes `except Exception` safety net in `_heartbeat_loop`, kills anyio task group, closes stdio → -32000 errors on in-flight MCP calls. Process now dies naturally from `BrokenPipeError` on next stdio write, preserving in-flight operation integrity. **Tests:** test_heartbeat.py updated (P3 + P5 scenarios).

- **Tests:** 2424 Python unit tests passed (was 2400, +24 from v0.32.0 fire-and-forget pattern), 70 live passed, all 2623+ C# EditMode green.

## [v0.31.1] — 2026-06-16 <!-- run_tests TCP disconnect fix -->

- **run_tests Domain Reload Disconnect Recovery (Server v0.31.1)** — Fixes silent timeout when domain reload clears Editor.log "compiling" status before TCP port 9700 is restored. **(Fix A: bridge.py)** When `DomainReloadError` is caught, pin `domain_reload_in_progress=True` flag for all subsequent retries within that send() call. Prevents `_probe_busy()` re-evaluation from returning False too early, allowing full exponential backoff (2s/4s/8s) instead of bailing after ~2s. **(Fix B: tools/scene.py)** Reduce poll attempts 60→40 (120s total, matches `SESSION_TIMEOUT`). Add `_ping_reload_port()` helper that pings reload mini-server on port 9600 before each `get_test_results` attempt. Gracefully degrades when reload port unavailable (old plugin). **Tests:** 2 new tests in test_bridge.py (domain reload retry pinning), 4 new tests in test_scene_run_tests.py (reload port ping gate, degrade on missing port, timeout behavior), 5 existing poll tests in test_server.py patched with ping mock. All 2400 unit tests pass.

## [v0.31.0] — 2026-06-16 <!-- Architecture review: 13 bugfixes (security, crashes, correctness, DRY) -->

- **Security Hardening (Gate A: release blocker)** — CodeExecutor.SecurityScan pipeline: (1) strip C# comments + whitespace densification (via regex `//.*$` + `\s{2,}` collapse) (2) OrdinalIgnoreCase matching (3) +11 new blocked entries: `EditorApplication.Exit`, `Application.Quit`, `Environment.FailFast`, `ExportPackage`, `ImportPackage`, `OpenProject`, `ProjectWindowUtil`, `using` aliases (`System.IO`, `Diagnostics`, `Net`, `Reflection`). **Tests:** 15 new bypass tests verify blocked patterns caught.

- **Crash Fix: codegen TypeError (Gate A)** — `response.content[0].text` fails on every `auto_fix`/`smart_build` call (MCP SDK v1.27.1+ changes content from list to single object). Fix: `getattr(response.content, 'text', None)` handles both. **Root cause:** Anthropic SDK v0.24.0+ changed `ContentBlock` to non-list. **Tests:** test_server_codegen_corroboration.py updated (42 lines).

- **Crash Fix: ScriptableObjectHelper IndexOOB (Gate A)** — Deleted duplicate `SerializedPropertyToString()` (enum IndexOOB crash on access). Uses canonical version in ComponentSerializer. Eliminates DRY violation + crash vector. **Tests:** Pre-existing NUnit green (no new test needed, duplicate was dead code).

- **Shader.Find Fallback Chain (Gate B)** — `AssetDatabaseHelper.GetShader()`: Standard → URP/Lit → HDRP/Lit → InternalError (was silently returning null). Handles projects with partial pipeline support. **Tests:** 3 new scenarios.

- **Asset validate_move Error Semantics (Gate B)** — Changed from returning `"err: message"` to throwing `Exception` (consistent with other validation tools). `asset(action="validate_move", src, dst)` now returns `{"ok":true}` or raises. **Tests:** test_server_asset.py: 15 new validate_move scenarios (path checks, conflicts, writability).

- **ConsoleCapture Multi-level Filter (Gate B)** — `get_console(level="error,warning")` now comma-separated; splits + multi-match (was single-level only). **Tests:** ConsoleCaptureTests.cs added.

- **ParticleHelper Dirty Flag (Gate B)** — Added `EditorUtility.SetDirty()` + `MarkSceneDirty()` after mutations (was silently modifying, not marking for save). **Tests:** 2 new NUnit tests.

- **ShaderSerializer Int Type Fix (Gate B)** — `GetPropertyDefaultIntValue()` (was calling `GetPropertyDefaultFloatValue` for Int type, threw type mismatch). **Tests:** 4 new shader property tests.

- **SpatialHelper Parsing Robustness (Gate B)** — `float.TryParse()` with `InvariantCulture` (handles "1.5" even on de_DE locale) + descriptive errors (e.g., "Expected float for speed, got 'abc'"). **Tests:** SpatialHelperTests updated.

- **ValueParser +5 Types (Gate B)** — Added `Rect`, `Bounds`, `RectInt`, `BoundsInt`, `LayerMask` + `Int64`/`Double` precision support. Handles 100+ type patterns. **Tests:** 20+ new parser tests.

- **MCP SDK Pin (Gate B)** — `mcp>=1.27.1,<2` (was unpinned; v2.0 ships 2026-07-28 with breaking changes). Prevents accidental `pip install` picking v2.

- **MCPChatWindow Token Cost Fix** — `_costUsd` assignment (was `+=` cumulative, double-counted on every update). **Tests:** TokenResetTests.cs + TokenFormatTests.cs.

- **ProjectRoot() DRY Consolidation** — Removed duplicate definition from 2 copies into single location in CodexArgBuilder. **Tests:** Pre-existing coverage (method was tested via call sites).

- **ScenePathParser Extraction** — New shared struct for parsing `"SceneName:/"` prefixes (used by SceneObjectFinder + ComponentSerializer.Finder). Replaces inline string parsing, prevents multi-scene path bugs. **Tests:** ScenePathParserTests.cs added.

- **Tests:** ~2364 Python passed (was 2362, +2 codegen corroboration), ~74 live passed, ~45 new NUnit tests (CodeExecutorSecurityBypassTests, ConsoleCaptureTests, ScenePathParserTests, TokenFormatTests, TokenResetTests, etc. — total ~2623+ C# EditMode green). Compile clean.

## [v0.30.4] — 2026-06-16 <!-- 7 Chat bugfixes: model selector, token display, multi-scene refs -->

- **Per-Backend Model Selector (Plugin v0.30.4)** — Dropdown in MCPChatWindow with presets per backend: Claude (Default/Sonnet/Opus/Haiku/Fable), Codex (Default/o3/o4-mini/o3-pro/gpt-4.1), Gemini (Default/2.5 Pro/2.5 Flash/2.0 Flash) + Custom... text field for arbitrary model IDs. **MCPChatWindow.Selector.cs**: `ModelPresetsPerKind` dict (backend-keyed), EditorPrefs persistence per backend (`MCPChat.SelectedModel.{BackendKind}`). Rebuilds dropdown on backend switch. **Tests:** 231 ModelSelectorTests (dropdown state, preset selection, custom model entry, persistence).
- **Token Cost Display (Plugin v0.30.4)** — Readout shows session cost (`$0.0020`) alongside token counts. **TokenFormat.cs**: `FormatReadout()` method computes cost via `EstimatedCost()` (cached token counts + configurable $/1k rates), null-safe guards for missing token data. **Tests:** 12 TokenFormatTests (cost calculation, zero-division safety, missing token handling).
- **Asset validate_move (Server v0.8.2, Python)** — New `asset(action="validate_move", src="...", dst="...")` dry-run validation before moving assets (checks path existence, destination writability, no conflicts). Returns `{"ok":true}` or error details. Prevents silent failures on asset renames/refactors. **Tests:** 15 test_server_asset.py new tests for validate_move scenarios.
- **Multi-Scene Chat References (Plugin v0.30.4, Server)** — Fixed scene-qualified object references in chat (#5 + #7 shared root). **IsAssetPath**: Now returns false for scene paths (prefix-check now strict: "Assets/" only, not "Scene:/" prefix-match fallback). **SceneObjectFinder**: Parses `"SceneName:/"` prefix to extract scene name and path separately. **display**: Chips now show `[Scene] name` for multi-scene objects. **Tests:** 74 MultiSceneChipTests (scene path parsing, chip display, navigation).
- **Ask↔Agent Session Persistence (Plugin v0.30.4)** — Switching from Ask to Agent mode (or vice versa) preserves session via `--resume` flag. **SetMode.cs**: Captures `SessionId` on mode switch, passes to new backend launch. **Tests:** 120 SetModeTests (mode switching, session preservation, backend restart).
- **Link Navigation Fix (Plugin v0.30.4)** — Fixed chip/link clicks not navigating to objects in multi-scene setups. Root cause same as #5 (SceneObjectFinder parsing). **Tests:** Covered by MultiSceneChipTests.
- **Test Marker: live_haiku → live_cli (Server v0.8.2)** — Renamed pytest marker to reflect any CLI backend (not just Haiku). Existing `@pytest.mark.live_haiku` still works (alias), but new tests use `@pytest.mark.live_cli`. No behavior change, just semantics.
- **Tests:** 2362 Python passed (was 2360, +2 asset validate_move baseline), 482 C# new (69 total for v0.30.4: 33 CodexArgBuilder, 74 MultiSceneChip, 12 TokenFormat, 231 ModelSelector, 120 SetMode, 14 TokenReset), compile clean.

## [v0.30.3] — 2026-06-16 <!-- Gemini backend + zombie detection -->

- **Gemini CLI Backend (Plugin v0.30.1, v0.30.2, v0.30.3)** — Third CLI backend for in-Unity chat alongside Claude + Codex. **GeminiArgBuilder** (194 LOC): Constructs `gcloud run gcloud-cli` command with --mcp-config pointing to .gemini/settings.json. Wires MCP server port via smart settings-merge: reads existing config, auto-updates stale port via `RewriteWithFreshMcp()` (exact-match check prevents IO if port correct). Handles tool_name/tool_id/parameters field mapping (Gemini differs from Claude SDK). **GeminiParser** (69 LOC): stream-json 6-event protocol (init, message, tool_use, tool_result, error, result). Filters: (1) skip role:user messages (Gemini echoes prompt back), (2) skip tool_use without mcp_ prefix (internal tools: update_topic, google_search). Suppresses ask_user tool_use to avoid double AskUserCard (ask_user routes via TCP path CommandRouter.OnAskUser). **GeminiBackend**: Spans process, sends initialize, waits for first output. **GeminiProvider** + registry pattern: auto-discovered via TypeCache, zero core edits. **Limitations:** Gemini CLI does NOT support --permission-prompt-tool (Issue #22249, p2) or MCP elicitation, so interactive permission prompts + parameter elicitation unavailable. **Tests:** 217 GeminiArgBuilder tests (settings merge, port update, field mapping), 190 GeminiParser tests (prompt filter, tool prefix, tool_result, error handling, ask_user suppression), 33 GeminiTestFixtures.
- **Per-Tool LLM Sampling UI Redesign (Plugin v0.30.1)** — Settings → Chat: removed horizontal tabs, added inline Backend+Model dropdowns per tool with Apply-All presets (Claude Fast / Gemini Flash / Codex). **BackendSettingsForm** (52 LOC): Modal dialog with preset buttons, Apply button, detailed help text. **SamplingPresets**: Enum-based templates. **SettingsPageFactory**: 141 LOC → redesigned page builders. **Python llm_config.py**: Extended LlmProfile dataclass with backend field (backward-compatible, defaults to "claude"). **Tests:** 2339 pytest green + 2400 compile clean.
- **Zombie Detection + Kill-All + Reconnect Stabilization (v0.30.3)** — **Zombie Detection**: ppid check in heartbeat loop — when parent dies, server exits within 15s (os._exit(0)). Prevents stale servers from starving new connections. `cleanup_stale_locks()` on startup removes dead-PID lockfiles. **Kill-All**: Fixed broken Kill button (was searching wrong filename pattern). New glob pattern server-{port}-*.lock + legacy format, kills all PIDs + cleans stale files. **Reconnect Stabilization**: send() reconnect no longer fires callbacks (only heartbeat does) — breaks feedback loop. Debounce 5s→30s, MIN_RECONNECT_INTERVAL 2s→5s, push_catalog skips if already locked. **Tests:** 21 new zombie scenario tests + ppid mocking + lockfile cleanup (2360 pytest total).
- **Tests:** 2360+ Python passed (was 2339, +21 zombie tests), 2623+ C# EditMode green (was 2623, +66 Gemini tests), 70+ live passed.

## [v0.29.38] — 2026-06-15 <!-- Codex requestUserInput + Claude AskUserQuestion -->

- **Codex Interactive User Input (Plugin v0.29.38)** — Codex CLI can now show interactive `AskUserCard` via JSON-RPC `tool/requestUserInput` and `item/tool/requestUserInput` requests. **CodexAppServerParser**: Handles both request types, extracts numeric `id` field (prefixed "codex:" for reply routing). **CodexAppServerBackend**: Advertises `experimentalApi: true` in initialize capabilities. Response formatted by **ControlResponseBuilder.CodexUserInputResponse()** (int.TryParse guards: numeric id → unquoted, string → quoted for safety). **AskUserCard**: Detects "codex:" prefix in `Submit()`, formats positional answers array `[{"answer":"..."}]` matching Codex protocol. Same UI as Claude version (radio/checkbox/freetext inputs). **Tests:** 7 new (CodexAppServerParserTests, ControlResponseBuilderTests, AskUserCardTests) covering request parsing, response serialization, and integration.
- **Tests:** 2413 Python passed, 2623+ C# EditMode green, 70+ live passed.

## [v0.29.37] — 2026-06-15 <!-- Claude AskUserQuestion routing via permission_prompt_tool -->

- **Claude Interactive User Input (Plugin v0.29.37, Server)** — Claude CLI `AskUserQuestion` now routes through MCP `permission_prompt_tool` → Unity TCP `ask_user` → interactive `AskUserCard` UI → user input → answer returns to Claude. **permission_prompt_tool.py**: MCP handler for `--permission-prompt-tool` flag. Routes tool questions to Unity via TCP with correct protocol (no `->str` annotation, `input:dict`). Auto-allows non-AskUser tools. **ClaudeArgBuilder**: Automatically wires `--permission-prompt-tool mcp__unity_mcp__permission_prompt` to Claude CLI args (user's project handles permission prompts). **AskUserCard Redesign**: Extracted inner `QuestionRow` → new file `AskUserQuestionRow.cs` (217 LOC, pill-button UI). **SingleSelect**: Auto-submit on pill click (no separate Submit button). Hover animation (200ms transition, 1.03x scale). Vertical full-width layout for better UX. Fixed `Toggle.text` → `Toggle.label` bug (BaseBoolField nulls .text in ctor). **Other field**: Returns answers-map JSON, not raw text. **FlowBar**: `_askPending` flag hides Stop button + progress bar during user input (prevents cancellation mid-prompt). **Gating**: `permission_prompt` added to `CORE_TOOLS` and `TIER1` (always visible). **Tests:** 74 total (6 Python permission_prompt_tool + 68 C# AskUserCard integration, was 11 pre-redesign).
- **Tests:** 2413 Python passed (was 2400, +13 permission_prompt), 2623+ C# EditMode green (was ~2540, +83 redesign), 70+ live passed.

## [v0.29.11] — 2026-06-15 <!-- Sprint 1C: Interactive permission protocol fix -->

- **Interactive Permission Protocol Fix (Plugin v0.29.11, Sprint 1C)** — Fixes non-functional permission prompts from Sprint 1B. **Problem:** v0.29.2 used `--permission-prompt-tool stdio` expecting `sdk_control_request` events, but Claude CLI v2.1.177+ never emits that event type. **Root cause:** Incorrect protocol understanding — SDK doesn't use `sdk_control_request` for permission handling; instead it uses two-phase handshake. **Solution:** Implement correct protocol from CLI v2.1.177: (1) After spawning Claude process, send `initialize` request with `PreToolUse` hooks → `{"subtype":"initialize","hooks":{"PreToolUse":[{"matcher":"*","hookCallbackIds":["hook_0"]}]}}` (2) Backend emits `control_request` stream-json with `subtype:hook_callback` containing tool call info (3) Unity routes to ToolApprovalCard UI (4) User decision serialized as `{"continue":true/false,"reason":"..."}` via stdout (5) Backward compat: old `sdk_control_request`/`permission` subtype still routed to PermissionPrompt for legacy backends. **Files changed:** `CliBackendBase` virtual seam `SendInitializeHandshake`, `ClaudeBackend.SendInitializeHandshake=true`, `ControlResponseBuilder.Allow/Deny` format changed to `continue:true/false` + added `InitializeRequest()`, `ChatStreamParser` routes both `control_request` (new) + `sdk_control_request` (legacy) + `control_response` (initialize ack, silently ignored), `ClaudeArgBuilder` removed non-functional `--permission-prompt-tool` arg. **Tests:** ChatStreamParserTests + ControlResponseBuilderTests + CliBackendBaseTests updated to verify new protocol path + backward compat. **Impact:** Interactive permission prompts now functional with Claude CLI v2.1.177+. Users see tool approval dialogs + can grant/deny/session-allow tool use from in-Unity chat.

- **Tests:** 3053 NUnit EditMode passed (v0.29.11), 2323 pytest passed, 73 live passed.

## [v0.29.2] — 2026-06-15 <!-- Sprint 1B: Chat assembly split + interactive permissions -->

- **Chat Assembly Split (Plugin v0.29.2)** — `UnityMCP.Editor.Chat` split into two independent assemblies: `UnityMCP.Editor.Chat.CLI` (protocol, parsing, backends, stream-json parsing) and `UnityMCP.Editor.Chat.View` (windows, rendering, UI cards). **Rationale:** CLI compiles when main plugin is broken (zero View dependencies, minimal surface); View depends on CLI. Enables incremental reload recovery before backend fully healthy. **Asmdef structure:** CLI → core (one-way ref). View → CLI → core. One-way dependencies prevent circular references, gate behind `UNITY_MCP_CHAT` define. **Breaking change:** No breaking changes to user-facing behavior (assembly refs internal, public API unchanged).

- **Interactive Permission Prompts (Plugin v0.29.2, Sprint 1B)** — New `sdk_control_request`/`control_response` protocol for tool approval + user input elicitation. **ToolApprovalCard** (UI view component, View assembly): Risk-classified tool approval UI with 4 buttons (Allow/Deny/Session/Always) + session-scoped SessionAllowlist manager. **AskUserCard** (UI view component, View assembly): Render user input request cards (radio/checkbox/freetext input types). **ControlResponseBuilder** (CLI assembly): Serialize approval decisions (`approval_decision: allow|deny|session|always`) + user input values into control_response JSON for backend consumption. **RiskClassifier**: Categorize tools by risk level (core/read-only vs write/destructive/runtime). **Integration:** MCPChatWindow.Approve.cs partial routes control_request events from ChatStreamParser to interactive card UI; backend resumes after user decision. Python `ChatStreamParser` handles line-by-line routing.

- **IBackendProvider + TypeCache Auto-Discovery (Plugin v0.29.2)** — Extensible backend registration without core edits. **BackendProviderRegistry** (CLI assembly): Static registry that auto-discovers `IBackendProvider` implementations via TypeCache at runtime. Each backend plugin = 1 file with `[InitializeOnLoad]` static ctor calling `BackendProviderRegistry.Register()`. **Built-in providers:** `ClaudeProvider` + `CodexProvider` (zero code delta, just wrapped in provider pattern). **Third-party plugins** can register new backends (e.g., local Claude instance, Anthropic internal services) without touching core code. **Discovery is automatic** — no manual registry updates needed. Enables future AI backend ecosystem.

- **Stream-JSON Control Protocol (Server + Plugin v0.29.2)** — Python `ChatStreamParser` now routes incoming `control_request` stream-json events to `ControlResponseBuilder` for serialization back to backend. Captures user approval + input (radio value, checkbox flags, freetext) and constructs response JSON. Enables bidirectional tool-approval flow: LLM tool call → Unity approval dialog → user decision → backend resume.

- **Tests:** 3047 NUnit EditMode passed (2330 CLI-side, 717 View-side, 5 pre-existing reds), 2330 pytest passed (no new failures), 73 live passed (2 pre-existing fails).

- **Chat Settings UI Race Fix (Plugin, fix commit 4cd66d3)** — `SettingsPageFactory.IsChatEnabled()` method added to safely check chat status without consulting `HasConnectionSubscribers` (which can raise on stale subscribers). Fixes race condition in Settings window rebuild during domain reload.

## [v0.27.4] — 2026-06-14 <!-- Reload recovery package + P1-P3 stress fixes -->

- **Reload Recovery Package (Plugin + Server v0.27.4)** — Independent UPM package `com.unity-mcp.reload` (asmdef references:[]) provides zero-intervention domain-reload recovery when main plugin compilation fails. **Package Architecture:** Separate mini-server on port 9600+ (SO_REUSEADDR bind-retry), port persisted to `Library/MCP_Port.json`, AssetImportWorker gate prevents import pipeline interference. **Python Escalation Ladder (T0-T5):** `server/src/unity_mcp/tools/reload_ladder.py` — T0 baseline diagnose (1 poll) → T1 force_refresh + poll main MVID (30 polls, 15s timeout) → T2 AssetDatabase.Refresh via reload port (3s sleep) → T3 RequestScriptCompilation via reload port → T4 reimport fallback (20s polls, no max) → T5 Play mode toggle (2s wait). **Sole Healing Proof:** MVID-delta (main_mvid before/after each tier). Frozen MVID + compile error = BROKEN_DOMAIN sentinel (manual reimport required). **Integration:** `sync.py _attempt_recovery()` calls `run_ladder(start_tier=2)` on REIMPORT-NEEDED verdict. **Shared Diagnostics:** `diagnose.py` extracts `_parse_diagnose()`, `_DiagnoseFields`, `_verdict()` for use by reload package + sync logic. **C# Components:** ReloadBinder (SO_REUSEADDR), ReloadMiniServer (async TCP), ReloadPortResolver (atomic Delete+Move persistence), ReloadPlugin (entry point), ReloadDomainStamp, ReloadCompileNotifier, ReloadDiagnoseCommand (portable), ReloadCommands (public API). **Tests:** 7 NUnit reload test suites (ReloadCommands, ReloadCompileNotifier, ReloadDiagnose, ReloadDomainStamp, ReloadMiniServer, ReloadPlugin, ReloadPortResolver), 20+ Python reload_ladder tests.
- **P1 Stress-Test Fixes (v0.27.4)** — (1) **T1 Poll Cap** — `_T1_MAX_POLLS=30` prevents infinite polling on stuck domains (7.5min timeout: 30 × 15s). (2) **Brace-Balance Assert** — `_parse_diagnose` added assertion that diagnostic JSON brace counts match, fails fast on truncated payloads instead of silently dropping data. (3) **Early-Exit on Compile Error** — If MVID frozen + `"error CS"` present in errors, domain will never reload → early return with BROKEN_DOMAIN sentinel instead of waiting full timeout.
- **Version Bump:** unity-plugin package.json → 0.27.4; reload package → 0.1.4.
- **Tests:** 2068+ Python (was 2048, +20 reload_ladder tests), 2623+ C# EditMode (reload test suites included), 70 live passed.

## [v0.26.0] — 2026-06-13 <!-- Test Quality Audit + Refactoring -->

- **Test Quality Audit (Server + Plugin v0.26.0)** — Systematic cleanup of test infrastructure across 182 files, eliminating 1243 lines of noise and establishing clear naming/structure conventions. **(Python Changes)** Removed 1018 redundant `@pytest.mark.asyncio` decorators from all tests (asyncio_mode=auto in pytest.ini handles this, reducing noise). Split 810-line god-file `test_middleware_circuit_and_dedup.py` into 12 focused files via inline refactor. Fixed 4 duplicate test names preventing pytest discovery. Added 18 crash-guard documentation comments. Extracted `make_mock_bridge()` helper to `helpers.py` (used by 6+ test files, DRY). PIL `importorskip` guards in 3 visual test files. `pkill` guard in live tests (only kill if port isn't already answering). Fixed 4 flaky sync-context tests: `test_metrics.py`, `test_middleware_diff.py`, `test_resources.py` converted to async (eliminated Python 3.12 event-loop race with `asyncio.get_event_loop()` in sync context after prior tests close loop). DRY refactor: `_ok()` and `_iid()` helpers extracted to live/conftest.py (were duplicated in 3 files). Sprint-code normalization: `F13` references in search tests renamed to `Live_Scoped_`. Removed tautological asserts (`or "0" in text` from reset state checks). Removed dead `SLEEP_STOP` constant. Removed 1 duplicate test `test_hierarchy_with_3_scenes_has_headers`. **(C# Changes)** Added `[TestFixture]` attribute to 6 previously undecorated test classes (CatalogParserTests, JsonHelperTests, MCPStatusBarPaletteTests, MCPStatusModelTests, PluginRegistryTests, ValueParserQuaternionTests). Renamed 48 sprint-code methods to production names (e.g., `F5_inline_chips` → descriptive method names). Extracted `TestStringHelpers.cs` (CountOccurrences utility shared across 4+ test files). Created `ChipTestBase.cs` base class with H() helpers centralized (eliminated 12 inline `private static string H()` shims across test files). Applied `_toDestroy` cleanup pattern to 3 test files (explicit TearDown collection instead of scattered .Dispose() calls). Converted Debug.Log in tests to TestContext.WriteLine (Unity Test Runner compatible). CodeExecutor.IsAllowedAssembly: private→internal (expose for security testing). ChatWindowAssertions.GetBubbleDisplayText() method added. **(New Test Infrastructure)** `server/tests/test_schema_cache.py` created (17 tests covering schema caching + validation + refresh). **(Test Counts)** 2048+ Python passed (was 2047, includes 4 flaky→async + dedup fixes), 2623+ C# EditMode green (includes 6 [TestFixture] additions + refactored chat tests), 70 live passed (fixed naming lies + DRY).
- **Tests:** 2048+ Python passed (was 2047 → +1 net from flaky fixes), 2623+ C# EditMode green (was 2623, added [TestFixture] structure), 70 live passed.

## [v0.25.13] — 2026-06-12 <!-- UTF-8 encoding fixes round-3 (grey-zones closed) -->

- **UTF-8 Encoding Round-3 (Server + Plugin v0.25.13)** — **(C1: Python test I/O gates)** All bare `open(..., "r")` in test suite now explicit `encoding="utf-8"` (EncodingWarning gate fully closed). Discriminating tests added: `test_server_filtering.py` + `test_lockfile.py` with Cyrillic payload assertions. **(C2: C# Process stdout/stderr)** `ProcessStartInfo.StandardOutputEncoding` + `StandardErrorEncoding` set to `new UTF8Encoding(false)` in ChatBinaryResolver, ChatSettingsSection, LoginShellCommand before spawn (ensures LLM/CLI output readable on Cyrillic %PATH% or non-ASCII stdout). ChatMcpConfigWriter byte-level tests moved to new `ChatMcpConfigWriterEncodingTests.cs` (validates UTF-8 no-BOM write chain on disk, not just in-memory mojibake tolerance). **Grey-Zone Audit Closures:** Process BaseStream-wrapping pattern (alt to PSI encoding for long-running streams) ratified; no dual-encoding contradictions remaining. **Fixup (round-3b):** Restored ShaderHelper SUT revert→fail coverage: extracted `WriteShaderFile(path, source)` as testable internal method, added discriminating test that fails on `Utf8NoBom→Encoding.UTF8` revert.
- **Tests:** 2848 C# EditMode green (was 2625 → +223, includes ShaderHelper.WriteShaderFile SUT test + 220 pre-existing expanded suite), 2048 Python passed.

## [v0.25.12] — 2026-06-12 <!-- UTF-8 encoding fixes + safety tests + grey-zone audit round-2 -->

- **UTF-8 Everywhere (Server + Plugin v0.25.12)** — **(Round 1)** Python file I/O and C# Windows codepage safety hardening. **(Round 2: Grey-Zone Audit Fixes)** **(1) INSTALLERS ARE I/O TOO** — `install.py`/bootstrap scripts read/write config files now use explicit `encoding="utf-8"` (crash on cp1251 Windows when path has Cyrillic username). **(2) PYTHONUTF8 IN GENERATED .mcp.json** — Server entry now includes `"env": {"PYTHONUTF8": "1"}` (defense-in-depth for Windows end-users, bypasses launcher). Codex TOML equivalent: `[mcp_servers.unity-mcp.env] PYTHONUTF8 = "1"`. **(3) POWERSHELL UTF-16LE ON WINDOWS** — subprocess capturing PowerShell stdout gets UTF-16LE, so byte-search `b"ascii" in out` silently fails (stale-lock detection broken). Fix: prepend `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; ` inside -Command string. **(4) ENODINGWARNING GATE NEEDS ENV AT LAUNCH** — `PYTHONWARNDEFAULTENCODING=1 pytest` (conftest.py setting it is a dead no-op — flag read at interpreter startup). conftest now WARNS when gate is inactive, honest check instead of silent lie. **(5) MIGRATION CATCH FOR LEGACY cp1251 FILES** — strict utf-8 read of pre-existing cp1251 file raises UnicodeDecodeError → catch in except tuple `(OSError, json.JSONDecodeError, UnicodeDecodeError)` → return empty (regenerable on next write). **(6) ensure_ascii=False ON HOT SEND PATH (TCP bridge)** — Cyrillic ~3.5x smaller. CRITICAL INVARIANT: length prefix computed on ENCODED BYTES `len(payload)` not `len(str)`, else multibyte payloads under-frame and desync the protocol. **(7) TEST THE SUT, NOT THE HELPER** — encoding tests must call real production methods + assert raw on-disk bytes (UTF-8 Cyrillic sequence, no BOM), not rely on BOM-autodetect or mojibake-tolerance.
- **Tests:** 2047 Python passed (was 2038 → +9), 2625+ C# EditMode green (was 2623 → +2), 70 live passed.

## [v0.25.0] — 2026-06-12 <!-- Multi-scene CRUD + test filter + compile check workflow -->

- **Multi-Scene CRUD + Diff (Plugin v0.25.0)** — Cross-scene `transfer_object` (move/copy between scenes), `object_diff` (unified diff of two objects showing only differing properties), scene management (`scene` tool: open_additive, close, set_active, list). `SceneContext.cs` centralizes all multi-scene logic (IsMulti, QualifyPath, FilterByScene) — single source of truth for path qualification. Bug fixes: `find_objects`, `find_references`, `configure_objects` now search ALL loaded scenes (was active-scene only). DontDestroyOnLoad excluded from iteration.
- **run_tests Filter (Plugin+Server v0.25.0)** — New `filter` parameter: pipe-separated test class names (e.g., `filter="ClassA|ClassB"`) passed to Unity Test Framework as `Filter.groupNames`. Enables targeted test runs (~2s vs ~65s full suite). Python, C# Schema, Router, TestRunner all updated.
- **Compile Check Workflow (v0.25.0)** — Required TCP `get_compile_errors` check before NUnit/live tests. Unity silently runs stale DLL on compilation failure — tests pass/fail against OLD code. Editor.log proved unreliable on macOS Unity 6. Updated: CLAUDE.md, workflow agents, senior-developer instructions.
- **Test Infrastructure (Plugin v0.25.0)** — `MultiSceneTestBase` saves additive scenes (unblocks AddScene), captures main scene name before NewScene hijacks active scene, restores active scene after each NewScene. ObjectDiffHelper now compares Transform properties. TestDummyMB moved to Runtime/TestHelpers assembly. 3 bug fixes: CopyAsMcpRef, ObjectDiff, duplicate-name create_object.
- **Tests:** 2623 NUnit passed, 2038 pytest passed, 70 live passed = 4731 total.

## [v0.24.1] — 2026-06-12 <!-- Port re-discovery on reconnect + lockfile takeover + 9 new tests -->

- **Port Re-Discovery on Reconnect (Server v0.24.1)** — UnityBridge now auto-rediscovers Unity's port when reconnecting after a restart. **(Problem)** If Unity restarted on a different port (e.g., 9500→9501 due to manual assignment or project conflict), the MCP server remained stuck on the old port forever, causing silent connection failures. **(Solution)** New `port_discoverer` callable parameter in `UnityBridge.__init__()`, invoked during `_reconnect()` before TCP connect. If discoverer returns a new port, bridge updates `_port` and recreates CompileStateProbe with correct port. Gracefully handles discoverer exceptions (falls back to current port). **ConnectionSlot integration:** `port_discoverer` and `on_port_change` callbacks passed through. New `_sync_port()` reconnect callback updates slot's port and triggers lockfile swap on server side. **Lockfile swap atomicity:** `_on_port_change()` in server.py releases old lock, acquires new one; if acquire fails, lock_fd set to None (avoids stale fd). Backward-compatible: no discoverer → normal reconnect (all existing code unaffected). **(Implementation)** `UnityBridge._reconnect()` calls `port_discoverer()` if provided; wraps in try/except to gracefully handle missing port files or permission errors. `ConnectionSlot` threads discoverer/callback through to bridge + adds sync callback. `server.py` lifespan provides `_read_unity_port` discoverer and `_on_port_change` callback. **Tests:** 6 new in test_bridge_port_rediscovery.py (reconnect updates port, falls back on discoverer failure, same port no-op, backward-compat), 2 in test_connection_slot.py (lockfile swap atomicity), 1 in test_connection_tools.py (reconnect_unity auto-discovers).
- **Lockfile Takeover: SIGTERM + Retry (Server v0.24.1)** — `acquire_lock()` now handles sessions switching between Claude Code instances targeting the same Unity server. **(Old behavior)** Lock held by another MCP session → fail with RuntimeError immediately, forcing manual cleanup. **(New behavior)** Detect live `unity_mcp` process → send SIGTERM → wait up to 3s for lock release → take over seamlessly. **(Safety Guards)** Only SIGTERM if: (1) `is_pid_alive(old_pid)` ✓, (2) not a zombie (via `_is_zombie()`), (3) cmdline actually contains "unity_mcp" (via platform-native process enumeration: `/proc/` on Linux, `ps` on macOS, CIM+tasklist on Windows). **(Graceful Degradation)** Stale/zombie locks cleaned up without kill attempt (just wait + retry). Cross-user processes skipped (PermissionError → continue). If lock can't be released after 3s, raise RuntimeError with clear port number. **(Implementation)** `_kill_pid(pid)` helper sends SIGTERM (Unix) with silent fallback for dead/permission errors. Retry loop: attempt 0 = kill + wait, attempts 1+ = passive wait. New `_is_zombie(pid)` detects defunct processes. Windows: disabled zombie check (no `/proc`), PermissionError → assume alive. **Tests:** 24 new (8 core takeover: live kill, stale no-kill, wrong-process no-kill, zombie no-kill, runtime error on stuck lock; 6 zombie-handling; 10 cross-platform Windows CIM/tasklist fallback). 1983 Python passed (+22 net).
- **Tests:** 1983 Python passed (was 1971 → +12 net, excludes pre-existing failures).

## [v0.24.0] — 2026-06-12 <!-- Multi-scene hierarchy support + temp test assets refactor -->

- **Multi-Scene Hierarchy Support (Plugin v0.24.0)** — `get_hierarchy` now handles multiple loaded scenes with scene-aware context headers. **(Single Scene Behavior)** When one scene is loaded, behavior unchanged: no headers, zero overhead. **(Multi-Scene Behavior)** When 2+ scenes are open, each scene preceded by `[SceneName]` header to disambiguate roots. Duplicate scene names disambiguate with parent directory: `[Scene (Assets/Scenes/Level1)]` vs `[Scene (Assets/Scenes/Level2)]` (unsaved scenes marked as `(unsaved)`). Phantom header removal: if a scene matches filter but yields zero objects, header line is removed (no orphan section). **(Implementation)** New `GetAllLoadedSceneRoots()` helper returns `List<(string name, GameObject[] roots)>` iterating `SceneManager.sceneCount` with dedup logic. Excludes DontDestroyOnLoad virtual scene (runtime-only, invalid after reload). Old `GetRootObjects()` split into two: multi-scene path via `GetAllLoadedSceneRoots()`, single-subtree path via `GetSubtreeRoots()`. Root param (`root="Player"`) bypasses multi-scene and returns subtree (no headers). **Summary mode** (`SerializeSummary`) emits `[SceneName] (N nodes)` headers + per-root children count. Tests: `HierarchyMultiSceneTests.cs` (15 NUnit cases) covering single/multi-scene headers, dedup, phantom header, root param override, SerializeSummary multi-scene.
- **Test Assets Consolidation (Plugin v0.24.0)** — Moved all temporary test .unity/.prefab files to `Assets/TestsTemp/` (centralized instead of scattered temp locations). New `TestPaths.cs` helper class provides `TempFolder` constant and `EnsureFolder()` method; all test classes now call `TestPaths.EnsureFolder()` in `[SetUp]`. Updated in: `HierarchySerializerTests.cs`, `SerializerTests.cs`, `HelperTests.cs`, `TestRunner.cs` (playtest temp paths), `HierarchyMultiSceneTests.cs` (new). Simplifies cleanup: one folder to delete instead of hunting scattered temp .unity files. Single-line pattern: `if (System.IO.File.Exists(TestPaths.TempFolder + "/filename"))`.
- **Tests:** 2600+ C# EditMode green (was 2500+), including 15 new HierarchyMultiSceneTests. Python 1971 passed.

## [v0.23.13] — 2026-06-11 <!-- Unified settings + media viewers + LLM config + review hardening -->

- **SettingsNavController Hardening (Plugin v0.23.13)** — Timer-based animated transitions between settings pages (iOS-style slide), input-field tab/Esc/Return focus management, detach guard preventing exceptions on scene reload. Fixes focus loss after rapid page navigation + improper cleanup on domain reload.
- **LLM Sampling Presets (Plugin v0.23.13)** — `SamplingPresets.cs` adds Claude/Codex preset buttons for quick model selection. Disabled features (e.g., visual_verify when Claude selected) are hidden (not grayed). Improves UX for sampling configuration without exposing unavailable options.
- **Auth Status Build on Main Thread (Plugin v0.23.13)** — `ChatSettingsSection.cs` fixes SystemInfo crash on background thread. `Application.platform` now called only on main thread. stderr drained after process spawn to prevent hung pipes. Eliminates sporadic "Calling BuildScreenOptions from background thread" exceptions.
- **CSS Warnings Cleanup (Plugin v0.23.13)** — Removed deprecated `style.scale` / `style.translate` (CS0618), replaced with `matrix-translate`. Removed `PreventDefault()` call on non-preventable events. USS `:last-child` pseudo-selector replaced with explicit class (Unity 6000 bug workaround). Eliminates 12 compiler warnings.
- **Test Hardening (Plugin v0.23.13)** — ScriptDragDropTests: BoxCollider used instead of TestDummyMB (Editor assembly limitation for Component lookup). ChipPillFactoryTests: unused var cleaned. ZoomPanManipulatorTests added (73 cases) covering zoom/pan boundaries + fit-to-bounds logic.
- **Unified Settings Integration (Plugin v0.23.13)** — SettingsNavController wired into MCPHubUI as push-nav (replaces 3 legacy EditorWindows: MCPToolSettingsWindow, MCPPermissionsWindow, MCPChatSettingsWindow). 4th card "LLM Sampling" with Claude/Codex preset buttons added. USS nav styles added to MCPHub.uss.
- **Review Hardening (Plugin+Server v0.23.13)** — `[Serializable]` added to LlmConfigStore (fixes silent JsonUtility deserialization failure). Pop() animation fixed (blank frame → smooth slide-back). Mermaid viewer USS classes added. ImageViewerWindow File.Exists guard. parse_tcp_config ValueError/IndexError guard. Dead AttachScreenshot + .chat-btn--screenshot removed.
- **Tests:** 2528 C# EditMode green, 1971 Python passed, 53 live tests green.

## [v0.23.0] — 2026-06-11 <!-- Reconnect recovery + installer + unified settings + media viewers + DRY sampling -->

- **Reconnect Recovery: Zombie Detection + SO_REUSEPORT + TCP Probe (Server + Plugin v0.23.0)** — Fixes `-32000 server error` during rapid reconnection after crash. **(Part A: Lockfile Zombie Detection)** `lockfile.py:_is_zombie(pid)` now detects defunct processes via `/proc/{pid}/stat` (Linux) or `ps -p` status (macOS/Windows). Stale zombie processes no longer block server startup — server proceeds immediately without waiting for cleanup. **(Part B: SO_REUSEPORT)** MCPServer.cs enables `SO_REUSEPORT` on macOS/Linux for socket reuse during fast reconnection (Windows has soft TIME_WAIT, skips this). **(Part C: TCP Probe)** `server_filtering.py:read_unity_port()` adds `_tcp_probe(port, 0.2s)` to filter stale discovery files (port written but not listening). Candidates ranked: project path match (CWD) → mtime. PermissionError (cross-user processes) skipped gracefully.
- **Installer: Setup/Update/Doctor/Configure (install.py, v0.23.0)** — New CLI tool (179 lines) replaces manual setup. `install setup` initializes uv-based .mcp.json (no absolute paths). `install update` upgrades server package. `install doctor` validates Python, venv, port availability. `install configure` rewrites .mcp.json for custom paths. **Config format (`.mcp.json`)**: uv-based invocation without absolute paths — `{"command": "uv", "args": ["run", "--directory", "server", "unity-mcp"]}` — survives machine moves / repo clone to new paths. **ChatMcpConfigWriter warning (v0.23.0)**: Alerts user when serverDir changes between runs (often accidental, suggests config reload).
- **8 Tool Fixes (v0.23.0)** — (1) **#instanceID in all paths**: ComponentSerializer.Finder.cs adds `#123` suffix to all path tools (get_hierarchy, get_component, etc.) for GameObject disambiguation. (2) **set_property("active") auto-redirect**: Properties.cs detects "active" property → auto-forwards to SetActive (vs direct property write). (3) **Short-name FindType fallback**: ObjectManager.Lookup.cs adds FindType + short-name component lookup for custom components not caught by typeof(). (4) **Screenshot dir fix**: FileOutputHelper.ScreenshotsDir now `<ProjectRoot>/ScreenShots/` (project-local, not shared cache). (5) **ImageBlockRenderer guard**: IsImageFile validation prevents DLL-as-image upload errors. (6) **Distill bypass params**: compressor.py `_FIELD_ALIASES`, objects.py + scene.py `full=true` parameter, middleware_async.py cache-key collision fix. (7–8) **Recovery script**: scripts/force_reset.sh kills stale servers + cleans lockfiles (manual recovery when zombie detection + auto-kill fail). **Tests:** 2002 Python tests green (incl. 78 new: 10 crash_log, 8 server_filtering TCP probe, 60 tool suite integration tests).
- **Crash Logging Integrated (v0.23.0)** — `crash_log.py:log_crash()` now called from `server.py:main()` outer try/except (BaseException → log → re-raise). Captures unhandled exceptions to `~/.unity-mcp/crash.jsonl` JSONL append-only (10 unit tests + 4 integration tests green).
- **Unified Settings Navigation (Plugin v0.23.0, Blocks 1)** — `SettingsNavController` — iOS-style navigational stack with slide animations, 4 dedicated pages (Tools, Permissions, Chat, Sampling), `SettingsPageFactory` DRY builder. Replaces fragmented sub-windows with single hub. `MCPSettingsHub` unified entry point. Tests: 10 NUnit EditMode cases.
- **Drag-Drop Media Fix (Plugin v0.23.0, Block 2)** — Removed mutual exclusion between ObjectReferences and file paths (deduplicated via handledPaths). Non-folder DefaultAsset now accepted as generic file chip (MD, TXT, JSON). Tests: 12 NUnit EditMode cases.
- **Universal LLM Config (Plugin + Server v0.23.0, Block 3)** — `LlmProfile` dataclass (Python) + `LlmConfig` (C#) replaces hardcoded "haiku" in sampling. `get_profile(feature)` provides context-aware model selection. TCP push: `set_llm_config` + `get_llm_config`. Tests: 16 Python tests green.
- **Chat Media Viewers (Plugin v0.23.0, Block 4)** — Screenshot button removed from FlowBar. `ImageViewerWindow` with zoom/pan/fit (modal on image click). `MermaidViewerWindow` with zoom/pan (↗ button on mermaid blocks). Shared `ZoomPanManipulator` DRY component. Tests: 7 NUnit EditMode cases.
- **Component Drag-Drop Dual-Chip (Plugin v0.23.0, Block 5)** — `ProcessDraggedObject` handles Component → dual-chip `@GO|@Script`. `ComponentContextMenu` reuses `ProcessDraggedObject` (DRY). Tests: 3 NUnit EditMode cases.

## [v0.22.1] — 2026-06-11 <!-- Crash logging for unhandled MCP server exceptions -->

- **Crash Logging for Unhandled Server Exceptions** — Python MCP server now captures unhandled exceptions to `~/.unity-mcp/crash.jsonl` for diagnosis. `log_crash(exc, *, log_dir=None)` module-level function writes `{"ev":"crash", "exc":"Type", "msg":"...", "tb":"traceback", "t":timestamp}` JSONL entries. Integrated into `main()` via outer try/except: catches `BaseException` → logs to crash log → re-raises (preserving clean shutdown semantics: `KeyboardInterrupt`, `SystemExit`, EPIPE silently swallowed, not logged). Helps diagnose sporadic "socket connection was closed unexpectedly" from Claude Code by capturing stack traces of unhandled exceptions in server process. Tests: 10 unit tests for `log_crash()` (T1–T6: write ev, exc type, msg, traceback, timestamp, dir creation, permission errors, append semantics) + 4 integration tests for `main()` crash handler (T7–T10: logs BaseException, exempts KeyboardInterrupt/SystemExit/EPIPE). 1924 Python tests passed.

## [v0.22.0] — 2026-06-11 <!-- Multi-project port auto-assignment + dual-port isolation + PortResolver extraction -->

- **Multi-Project Port Configuration (Plugin + Server v0.22.0)** — Unity projects now auto-assign unique MCP ports without manual configuration. **(Layer 1: Auto-Assignment & Backend-Agnostic Chat Injection)** `MCPServer.GetPort()` reads Library/MCP_Port.json, auto-assigns free port from 9500-9599 range via `PortResolver.FindFreePort()`, persists to JSON. `MCPHubUI` displays editable port field with "restart required" warning. `ChatProcess.Spawn()` accepts `setEnvKeys` param to inject `UNITY_MCP_PORT` env var for all backends (Claude/Codex/future), decoupled from hardcoded port knowledge. `CliBackendBase.SpawnNewProcess()` passes UNITY_MCP_PORT to child process. **(Layer 2: Dual-Port Isolation + PortResolver Extraction)** `MCPServer.cs` now listens on dual TCP listeners: `_mainSlot` (CLI), `_chatSlot` (in-editor agent). `ClientSlot` pattern isolates connections — CLI and Chat clients never evict each other. `PortResolver.cs` extracted as pure testable helper with 6 methods (ResolvePort, ResolveChatPort, FindFreePort, SavePorts, IsValidPort, ParsePortFromJson) + 25 NUnit EditMode tests covering port validation, range, fallback, dual-port edge cases. **(Python: CWD-Based Port Discovery)** `server_filtering.py:read_unity_port()` prioritizes: env UNITY_MCP_PORT → CWD project path match (extracts project_path from ~/.unity-mcp/ports/*.port files, matches against Python server's os.getcwd()) → newest mtime → default 9500. Handles PermissionError gracefully (cross-user processes) — live .port files preserved, no crash. Fallback lockfile behavior: RuntimeError on live process instead of SIGTERM, cleaner error handling. **Review Fixes:** PermissionError handling in lockfile (keeps live files), env var validation (ValueError guard), meta file ordering, dead code removal (duplicate imports), TeardownCore ordering (cancel master CancellationTokenSource before disposing slots). **Tests:** 1913 Python passed (+56 new: CWD matching 4 tests, lockfile fail-fast 3 tests, read_unity_port extended). C# 1610 EditMode, PortResolverTests 25/25 green. Backward-compatible: pre-v0.22.0 projects use 9500 (default), new projects auto-discover via CWD/mtime match.

## [v0.21.0] — 2026-06-11 <!-- Cross-platform Windows/Linux support + zero manual patching -->

- **Cross-Platform Windows/Linux Support (Plugin v0.21.0 + Server)** — Plugin now works on Windows, macOS, and Linux without manual code patches. (1) **Binary Resolution**: `ChatBinaryResolver` queries platform-specific shells: `where.exe` (Windows, with CWD-hijack mitigation), `bash -lic` (Linux), `/bin/zsh -lc` (macOS). Each platform gets output parsed appropriately (.exe/.cmd extraction, multiline-banner rejection, root-path scanning). EditorPrefs override keys per backend (`UnityMCP_Chat_ClaudePath`, `UnityMCP_Chat_Path_codex`) allow escape-hatch. (2) **Python Command Resolution**: `ChatMcpConfigWriter.ResolvePythonCommand` checks per-platform venv paths: Windows `.venv\Scripts\python.exe` first (File.Exists cross-platform check), then Unix `.venv\bin\python`, then `uv`, then fallback `python`/`python3`. (3) **Server PID Lockfile**: Cross-platform locking in `lockfile.py` — `fcntl.flock` (macOS/Linux) vs `msvcrt.locking` on sentinel byte at offset 1024 (Windows, avoids mandatory lock of PID data). Stale server cleanup via SIGTERM→SIGKILL (Unix) or TerminateProcess (Windows). (4) **SIGPIPE Handling**: Guarded with `hasattr(signal, "SIGPIPE")` since Windows lacks SIGPIPE. (5) **Venv Portability Warning**: Document that .venv copied from Unix to Windows MUST be recreated (different directory structure: `bin/` vs `Scripts/`). Docs: `docs/install/codex.md` (platform groups, venv recreation, verify per-OS), `docs/install/claude-code.md` (new, Claude-specific wiring with `--mcp-config --strict-mcp-config`). Tests: ChatBinaryResolverTests (platform-specific output parsing), ChatMcpConfigWriterTests (Python resolution order), new cross-platform integration tests.

## [v0.20.7] — 2026-06-10 <!-- svg: Reload-resume re-sends the full-path chip payload, not short-name mentions (task#10) -->

- **Reload-Resume Sends Full-Path Chip Payload (Plugin v0.20.7, task#10)** — Fixes silent LLM-context degradation after a mid-turn domain reload. A fresh send transmits the full-path payload (`@/Env/Player` + trailing `[kind:path]` block via `ChipTextInterleaver.ToLlmPayload`), but a reload-resumed turn re-sent the short-name display text (`@Player`, no bracket block) because `SaveStateBeforeReload` persisted only the bubble display text. Fix: capture the exact bytes sent at send time in a new `_sentLlmCache` and persist them as a new optional `PendingTurnState.PendingLlmPayload` field (v6 header column, base64). On an in-flight resume the turn now re-sends `EditorStateSnapshot + PendingLlmPayload`, equal to the fresh-send payload; pre-v6 blobs (no field) fall back to `PendingText`. Idle-reload input restore is unchanged (payload empty for idle saves). Serializer is backward-compatible — old persisted blobs lack the 10th header field, deserialize to `payload=""`, and resume gracefully, no crash. The "Show LLM payload" inspector now reveals the correct full-path payload on resume. Tests: PendingTurnStateLlmPayloadTests (F1–F6, +6) — v6 round-trip, payload distinct from display text, null→empty, v5-blob backward-compat, multiline payload, all-prior-fields regression. 2450/2450 EditMode green (was 2444 + 6).

## [v0.20.6] — 2026-06-10 <!-- svg: Full-path chip payload + always-raw "Show LLM payload" inspector for every turn type -->

- **Full-Path Chip Payload (Plugin v0.20.6)** — Chips now send their full object/file `Path` to the model instead of the short `DisplayName`. `ChipTextInterleaver.ToLlmPayload`/`ToLlmText` emit `@/Env/Player` (Path) where `ToDisplayText` keeps `@Player` for the bubble; orphan chips with an empty path fall back to `DisplayName`. `AtMentionNormalizer` now matches echoed mentions against BOTH `DisplayName` and `Path`, sorted globally longest-first so `@/UI Canvas/Main Camera` wins over `@Main Camera` over `@Main`. Tests: ChipPayloadFullPathTests (+24), ChipSendFullPathTests (+3), updated ChipTextInterleaverTests / ChipSendSequenceTests / AtMentionNormalizerTests.
- **Always-Raw "Show LLM payload" Inspector (Plugin v0.20.6)** — Right-clicking a sent user bubble now offers **"Show LLM payload"** (renamed from "Show as text"), logging `[MCP Chat] LLM payload:\n<raw>` — the EXACT string sent to the model (full paths, the `EditorStateSnapshot` prefix injected on reload-resume, compile-error injects), for every turn type. New `UserBubbleData { Display, Llm }` carries display text + sent payload; **Copy** still returns the clean `Display`. Threaded through fresh send / screenshot / compile-inject / approve / reload-resume / reload-restore — backend-agnostic (Claude + Codex). Legacy null-payload bubbles and assistant/tool bubbles keep bare-string `userData`. `TranscriptSerializer` gains a 4th base64 `LlmPayload` column; old 3-column blobs restore as bare strings (no crash), round-trip idempotent. Tests: UserBubblePayloadInspectTests (T1–T6), BubblePayloadGapTests (G1/G2/G3, +9), updated ChipTestHelpers / SendFlowIntegrationTests / UserMessageBubbleTests / ReloadSendIntegrationTests. 2444/2444 EditMode green.

## [v0.20.0] — 2026-06-10 <!-- svg: Chip-unification Phase 1 — delete SceneNameLinker path, unified @-mention rendering -->

- **Chip-Unification Phase 1: Delete SceneNameLinker Render Path (Plugin v0.20.0)** — Fixes received LLM refs rendering as underline links instead of pills. Root cause: two competing render paths diverged at the static mutable seam `MarkdownInline.Linker`. SceneNameLinker.Linkify ran inside ToRichText and wrapped scene-object names as `<link><u>Name</u></link>` between pills, while the canonical path produced `[kind:ref]` → ChipPillFactory pills. Delete the second path entirely: ALL refs now route through one path: AtMention/BareName → `[kind:ref]` → ResponseTagInliner → MixedParagraph → ChipPillFactory pill. Deleted: SceneNameLinker.cs + SceneNameLinkerTests.cs (−202 LOC). Modified: MarkdownInline.cs (drop static Linker field + Linkify call), ChatTranscript.cs (drop _savedLinker dance; gate scene-wide BareNameNormalizer behind `MCPChat.DisableSceneNameNorm` kill-switch), MCPChatWindow.cs (drop _linker field; rename RefreshLinker → RefreshResolver; add Refresh before FinalizeAssistant in Drain TurnDone), ChatBlockRendererRegistry.cs (pass null scene-object resolver to ChatLinkify). Tests: +NormalizationPipelineTests (7 cases), +3 BareNameNormalizer edge-case tests ported from deleted suite; F15b test drops Linker=null setup. Net −97 LOC. 2400/2400 EditMode green. Phase 1 only (no LLM contract change).

## [v0.19.2] — 2026-06-10 <!-- svg: Chat reload double-bubble MAJOR + drag-drop crash guard + clean test console -->

- **Chat Reload Double-Bubble MAJOR Fix (Plugin v0.19.2)** — TryResumePendingTurn consumed `_transcriptRestored` flag only on the active branch, leaking `true` on idle/stale/null early-return paths → duplicate user bubble on the next mid-turn domain reload. Fix: capture flag into local at entry, clear field unconditionally; SetLastTurnChips always runs for normalization context, only AppendUserBubble is guarded. _transcript field made internal for pin test.
- **Drag-Drop Crash Guard (Plugin v0.19.2)** — ProcessDraggedObject called GetComponent(ms.GetClass()) which throws ArgumentException when the dragged MonoScript's class is not a Component (ScriptableObject / plain class / static). Guard: `typeof(Component).IsAssignableFrom(cls)` before lookup. Add HasComponentFn injection seam so the dual-chip branch is deterministically testable.
- **Console Noise Cleanup (Plugin v0.19.2)** — CliBackendBase gains an injectable `Action<string> LogError` seam (prod default Debug.LogError); Start_NullBinary test captures the message instead of letting it echo red to console every run.
- **Tests: CliBackendBaseTests, DomainRefreshTests, ScriptDragDropTests** — 4 test .meta files added. 2403/2403 EditMode green.

## [v0.19.1] — 2026-06-10 <!-- svg: P0/P1 chat UX hardening — ResetTurnFlags DRY, bubble dedup, backend restore race -->

- **ResetTurnFlags() DRY Helper (Plugin v0.19.1, P0-2)** — Extract `ResetTurnFlags()` helper and wire to CancelTurn (bug: flags were never cleared on cancel), TurnDone, Error, dead-process guard, and NewSession (was missing _needsRefresh). Consolidates 3 separate sites resetting `_turnEditedCode`, `_turnHasToolCalls`, `_needsRefresh`.
- **Transcript Restore Dedup (Plugin v0.19.1, P0-1)** — Add `_transcriptRestored` flag; guard AppendUserBubble in TryResumePendingTurn to skip re-append when transcript was already restored from domain reload. SetLastTurnChips always runs for normalization context. Prevents duplicate user bubbles on mid-turn domain reload.
- **Backend Restore Race Fix (Plugin v0.19.1, P0-3)** — In Selector restore block, Stop() old backend and CreateBackend() to match restored kind. Bug: OnEnable created a default-kind backend before the saved selection was applied, causing mismatch between UI and actual running backend.
- **Tests: CancelTurnCleanupTests** — P0-2 RED fix: CancelTurn must reset all turn flags. P1-4: TestDummyMB helper component; dual-chip happy-path test (GO+Script). P1-5: F27 timing invariant tests via reflection (args-complete vs result-complete gate for _needsRefresh). Component 6: pinning test for in-flight user bubble reload round-trip. Version 0.19.0 → 0.19.1.

## [v0.19.0] — 2026-06-10 <!-- svg: Chat UX F27–F30 — Domain reload + external drag/drop + input height + backend cleanup -->

- **F27 Domain Reload After Code Edits (Plugin v0.19.0)** — Chat backend now triggers `AssetDatabase.Refresh(ForceUpdate)` when code-editing tool results arrive. New `_needsRefresh` flag (internal) set alongside `_turnEditedCode` in `HandleToolRecord()`. Consumed in `DrainAndRender()`: flag → refresh once per drain cycle. Debounced refresh prevents duplicate calls within same UI frame. Tests: DomainRefreshTests (4 NUnit EditMode cases: default-false, set-on-code-edit, non-code-no-set, reset-after-consume).
- **F28 Remove Non-Session CodexBackend — Simplify to 2 Backends (Plugin v0.19.0)** — Removed spawn-per-turn `CodexBackend` and `CodexStreamParser` (−577 LOC). Simplified `BackendKind` enum: `{Claude, Codex}` (was 3 entries). `BackendKind.Codex` now always creates `CodexAppServerBackend` (persistent JSON-RPC sessions). Backward-compat: `PendingTurnState` maps old persisted int=2 to `BackendKind.Codex`. EditorPrefs migration: "Codex (Session)" renamed to "Codex" in dropdown. Tests: BackendRegistryTests updated (3 backends baseline → 2), CodexBackendTests removed, CodexStreamParserTests removed. Net: −204 lines (2 files deleted, 2 modified), cleaner enum space, one backend per model (Claude = spawn, Codex = persistent session).
- **F29 External Drag/Drop with Folder Support (Plugin v0.19.0)** — Allow dragging files and folders from Finder (macOS Finder / Windows File Explorer) into chat context. New `FolderChipProvider` (priority 150) implements `IChipKindProvider`; Folder constant added to `ChipKindKeys`. New `ProcessExternalPath()` static method detects filesystem paths from `DragAndDrop.paths` (external drop API). `OnDragUpdated` and `OnDragPerform` now accept external DragAndDrop.paths alongside internal object drops. Tests: DragDropExternalTests (8 NUnit EditMode cases: null-obj fallback, folder detection, dual-chip render, external-only paths).
- **F30 Input Field Default 4 Lines Tall (Plugin v0.19.0)** — Input field height calculation increased: `CompactH = 4*LineH + PadH + ActionBarH = 117f` (was 72f). `InputHeightCalc.Compute()` now clamps via `minH = min(CompactH, maxH)` to prevent degenerate clamp when window height < CompactH (tiny window fix). Tests: InputHeightCalcTests (4 NUnit EditMode cases updated + new Compute_TinyWindow_MaxWinsOverCompactH case).

## [v0.18.0] — 2026-06-10 <!-- svg: Chat UX F20–F26 — Stop button, reload survival, AutoScroll, dropdown persist, @Object dedup, direct Clear, drag/drop MonoScript -->

- **F20 Stop Button + Esc Hotkey (Plugin v0.18.0)** — New `CancelTurn()` method in MCPChatWindow + chat backend (ClaudeBackend/CodexBackend) for in-flight message cancellation. Send button swaps to Stop button during streaming (visual state via `.chat-btn--stop` USS class). Esc KeyDownEvent triggers cancel. Sends `{ "stop_reason": "end_turn" }` to stdin (Claude protocol) or terminates process (Codex). Tests: StopButtonTests (3 cases: button state, Esc routing, backend integration).
- **F21 Transcript Reload Survival via Serialization (Plugin v0.18.0)** — New `TranscriptSerializer.cs` serializes ChatTranscript message history to plain-text format (`[turn N]\nuser: text\nassistant: text\n---\n`); persisted to Library/MCP_ChatTranscript.txt alongside PendingTurnState. On domain reload, history restored via `Deserialize()` preserving all user/assistant/tool-call entries + styling. `_entries` tracking in ChatTranscript + SessionState persistence gate. Tests: TranscriptSerializerTests (8 cases: round-trip, edge cases, reload survival).
- **F22 AutoScroll Moved to ChatSettingsSection (Plugin v0.18.0)** — AutoScroll toggle extracted from FlowBar into ChatSettingsSection (under "Chat Settings" foldout, same row as API key field). EditorPref `MCPChat_AutoScroll` persisted. Cleaner UI: settings in one place, FlowBar focuses on activity animation only. Tests: ChatSettingsSectionTests (4 cases: toggle state, persist, pref key).
- **F23 Dropdown Selection Persisted via EditorPrefs (Plugin v0.18.0)** — Backend dropdown (Claude/Codex) + Model dropdown (gpt-4-turbo, etc.) now persist selected indices via EditorPrefs (`MCPChat_SelectedBackend`, `MCPChat_SelectedModel`). On domain reload or window reopen, dropdowns restore last selection. Tests: BackendSelectorTests (5 cases: selection persist, pref round-trip).
- **F24 @Object Chip Duplicate Fix (Plugin v0.18.0)** — Fixed duplicate @Object chip insertion in `BuildFromRaw()` else-branch. Global forward search (rawText.Length - searchStart) replaces narrow chipRawOffset ± length window. Prevents orphan @mentions where stored offset undershoots actual @position. Tests: ChipDuplicateFixTests (3 cases: nested names, offset skew, global search correctness).
- **F25 Direct Clear without Submenu (Plugin v0.18.0)** — Removed GenericMenu submenu from Clear button; replaced with direct EditorUtility.DisplayDialog confirm. Dialog: "Clear chat history?" with "Clear" / "Cancel" buttons. Clears transcript, input, chips, calls ReloadGuard.ClearPendingState(). Faster UX: one click instead of menu navigate. Tests: ClearButtonTests (2 cases: dialog confirm, cancellation).
- **F26 Drag/Drop MonoScript Dual-Chip Support (Plugin v0.18.0)** — Drag-drop MonoScript now creates dual-chip (`@Object` + `@Script`) instead of single @Object. New `ProcessDraggedObject()` method extracted into reusable handler; detects MonoScript type and appends script chip. Enables context like "Add this script AND the GameObject it's on." Tests: DragDropScriptTests (4 cases: dual-chip render, script detection, chip formatting).
- **refactor: Catalog format JSON → plain-text (v0.18.0+, token economy)** — Changed `get_catalog()` format from JSON to plain-text line-delimited: `CORE:tool1,tool2\nSCENE_EDIT:tool3,...` sent over wire via `set_tool_catalog`. Reduces ~40% wire size, eliminates C# JSON deserializer. NEW `CatalogParser.cs` parses text → dict. Modified: `gating.py` (catalog dict now has categories["CORE"] not separate "core" key), `server_filtering.py` (push_catalog encodes text), test suite (test_catalog.py: no JSON validation, no "core" key check, plain-text format tests). BREAKING: catalog JSON structure changed; Unity plugins must call `CatalogParser.Parse()` instead of JsonUtility.FromJson.
- **refactor: Session file format JSON → plain-text (v0.18.0+)** — `save_session` / `load_session` now store plain-text: `<timestamp>\n=== hierarchy ===\n<hierarchy>...`, avoiding json.dump/json.load. Faster parsing, no JSON codec overhead. Modified: `scene_session.py` (removed json imports, partition-based parsing), test suite (test_scene_session.py: no JSON parse tests). BREAKING: legacy `.claude/session-context.json` files incompatible; users must re-save sessions.
- **fix: middleware_pipeline wrap_send file+data handling** — New test file `test_middleware_pipeline.py` validates wrap_send correctly returns both manifest text AND file path when response contains both fields. Fixes edge case where `screenshot` command returns multiview data + PNG file.
- **feat: Codex App-Server Backend (Persistent JSON-RPC Sessions)** — New `CodexAppServerBackend` replaces `codex exec` spawn-per-turn model with persistent `codex app-server` sessions via direct stdio + JSON-RPC 2.0 protocol. One process per chat session (matches Claude model), eliminates TCP slot-thrash. Real token streaming via `item/agentMessage/delta` (240+ deltas/turn vs batched text). Protocol: `initialize` → `thread/start` → repeated `turn/start` calls with `mcpToolCall` items. MCP injection via `-c mcp_servers.*` flags at session init. Spike-verified with codex 0.137.0. Files: NEW `CodexAppServerBackend.cs`, `CodexAppServerParser.cs`, `Tests/CodexAppServerParserTests.cs` (15 test cases with real JSON-RPC fixtures); MODIFIED `BackendSpec.cs` (enum), `BackendRegistry.cs` (factory), `MCPChatWindow.cs` (factory switch), `BackendRegistryTests.cs` (baseline update to 3 backends).
- **fix: Prevent secondary MCP server registration from ~/.mcp.json** — Added `--strict-mcp-config` flag to `claude -p` subprocess invocation. When the in-Unity Chat agent spawns Claude, it now prevents auto-discovery of `~/.mcp.json` which was registering a second MCP server with key `"unity-mcp"`. Permissions UI (MCPPermissionsWindow) now labels tools as "in-Unity Chat agent" for clarity.
- **docs: Codex setup guide rewrite** — Updated `docs/install/codex.md` to lead with in-editor workflow (Window > MCP Chat > Codex dropdown). Documents correct argv injection via `-c` flags for both first turn and resume. Moved manual `.codex/config.toml` to appendix (CLI-only use).

## [v0.17.36] — 2026-06-06 <!-- svg: Settings Hub redesign — central hub UI + circuit-node header animation + Claude foldout grouping -->

- **F26 Settings Hub Redesign (Plugin v0.17.36)** (2026-06-06) — Complete overhaul of MCP settings UI with unified hub window + circuit-network header animation. **Architecture:** Three sub-windows (ToolSettingsWindow, PermissionsWindow, ChatSettingsWindow) + unified MCPSettingsHub central window (new entry point for all MCP settings). **Hub Header Animation (HubHeaderAnim.cs):** Circuit-node network with 5 nodes (4 peripheral + 1 central hub) + 4 connecting lines + animated travelling packet dot + status label anchored in hub node. Connection-aware color scheme (#3ad29f online / #e8a23a listening / #6e2b3a offline), 80ms tick frequency. **HubUI Refactoring:** MCPSettingsUI now builds only Tools section (toggles + presets + search + categories + plugins); header/auto-discard/chat-enable logic extracted to hub-level control. MCPHubUI coordinates all 3 sub-windows from central hub. **Hub Divider (MCPHubDivider.cs):** Visual separator component between hub sections. **Hub Card Buttons (HubCardButton.cs):** Mini launcher cards for each settings window. **Chat Settings Grouping:** ChatSettingsSection.cs moved Auto Path, Override Path, Auth status, API key warning INTO "Claude Settings" foldout (expanded by default, was collapsed in v0.17.34). Consolidates connection info into one collapsible group. **CSS:** MCPHub.uss new stylesheet with `han-*` animation classes (nodes, lines, packet, hub pulse). **Tests:** 6 new NUnit EditMode test files (HubHeaderAnimTests, HubCardButtonTests, MCPHubDividerTests, ChatHeaderAnimTests, ChatSettingsHookEventTests, ToolsHeaderAnimTests) totaling ~40 tests covering header animation state, card behavior, divider rendering. **Files:** NEW `HubHeaderAnim.cs`, `HubCardButton.cs`, `MCPHubDivider.cs`, `MCPSettingsHub.cs`, `MCPHubUI.cs`, `MCPHub.uss` + meta; MODIFIED `MCPSettingsUI.cs`, `ChatSettingsSection.cs`, `ChatSettingsHook.cs`, `MCPToolSettingsWindow.cs`, `MCPPermissionsWindow.cs`, MCPChatSettingsWindow.cs; DELETED `MCPConnectionWindow.cs`. **Version bump:** 0.17.34 → 0.17.36. **Net:** Unified hub-and-spoke settings architecture, branded circuit-node animation, improved Chat settings discoverability via foldout grouping.

## [v0.17.34] — 2026-06-06 <!-- svg: F25 Phase 2 settings hub — unique thematic header animations per sub-window -->

- **F25 Phase 2: Thematic Header Animations (Plugin v0.17.34)** (2026-06-06) — Sub-window UI polish with connection-aware thematic vector animations replacing static back-links + headers. **Removed:** back-link buttons, text headers ("Tool Settings" / "Permissions" / "Chat Settings"), diamond dividers from 3 sub-windows (MCPToolSettingsWindow, MCPPermissionsWindow, MCPChatSettingsWindow). **Added animations:** 3 factory classes creating closure-local animated VisualElements (safe for simultaneous window instances). **ToolsHeaderAnim.cs** — 5 toggle-switch sweep (400ms cycle), active knob pulses with connection state color (#3ad29f online / #e8a23a listening / #6e2b3a offline). **PermissionsHeaderAnim.cs** — Shield + lock pulse animation (150ms), colors match Tools. **ChatHeaderAnim.cs** — WiFi arc pulse (150ms), same color scheme. All animations use scheduler.Every() pattern with closure state (no globals). **CSS cleanup:** Removed dead `.hub-back-link` styles. **Tests:** 21 new NUnit EditMode tests (ToolsHeaderAnimTests, PermissionsHeaderAnimTests, ChatHeaderAnimTests) verify animations render + state logic. **Files:** NEW `ToolsHeaderAnim.cs`, `PermissionsHeaderAnim.cs`, `ChatHeaderAnim.cs` + meta; MODIFIED `MCPToolSettingsWindow.cs`, `MCPPermissionsWindow.cs`, `MCPChatSettingsWindow.cs`, `MCPHub.uss`. **Version bump:** 0.17.28 → 0.17.34. **Net:** Removes clutter (headers/back-links), adds branded micro-interactions, strengthens hub visual hierarchy with color-coded state.

## [v0.17.28] — 2026-06-06 <!-- svg: F23 settings split — 3 focused EditorWindows + Chat event hook -->

- **F23 Settings Windows Split (Plugin v0.17.28)** (2026-06-06) — Refactor monolithic MCPSettings EditorWindow into modular focused UI windows with assembly-decoupled event hook. **Architecture:** MCPSettings → pure static data class (all public API preserved, no EditorWindow), 3 new dedicated windows: `MCPToolSettingsWindow` (Tool Settings menu), `MCPPermissionsWindow` (Permissions menu), `MCPConnectionWindow` (Connection menu). **Chat integration pattern:** New `ChatConnectionSection.cs` subscriber `[InitializeOnLoad]` listens to `ChatSettingsHook.OnBuildConnection` event, appends Chat-specific content to Connection window (zero core edits, Chat assembly injects via event seam). **Dead code removed:** OnBuild/Invoke/AppendSection paths removed from ChatSettingsHook/ChatSettingsSection (no longer needed — events replace). **Tests:** 5 new NUnit EditMode tests covering window UI, event firing, content injection. **Files:** MCPToolSettingsWindow.cs, MCPToolSettingsWindow.cs.meta, MCPPermissionsWindow.cs, MCPPermissionsWindow.cs.meta, MCPConnectionWindow.cs, MCPConnectionWindow.cs.meta, ChatConnectionSection.cs, ChatConnectionSection.cs.meta (NEW); MCPSettings.cs, MCPSettingsUI.cs, MCPSettingsPermUI.cs, ChatSettingsHook.cs, ChatSettingsSection.cs (MODIFIED); + test files + meta. **Version bump:** 0.17.27 → 0.17.28. **Net:** Monolithic window split into 3 focused UI windows, assembly decoupling via event hook (extensibility pattern), zero API breakage.

## [v0.17.20] — 2026-06-06 <!-- svg: 40-architect test audit — 299 new tests total, 3 P0+P1 bug fixes -->

- **40-Architect Test Audit: 122 New Tests + 3 Bug Fixes (Server v0.8.1, Plugin v0.17.20)** (2026-06-06) — Comprehensive test coverage expansion and production bug fixes from 40-architect review. **Python (38 new tests):** CostTracker null-crash P0 fix (spent can be None), gating FORCE_VISIBLE bug P1 fix (tool visibility filtered by both tool_type AND is_visible flag), middleware batch-conflict P1 fix (delete-chain detects cascade). New test files: `test_cost_tracker.py`, `test_budget_router.py`, `test_sampling.py`, `test_gating.py` extended (28 new), `test_runtime.py` extended (5 new), `test_codegen_corroboration.py` extended (3 new), `test_animator_intent.py` extended (3 new), `test_hinter.py` extended (2 new), `test_reflect.py` extended (2 new), `test_compile_state.py` extended (2 new), `test_do_intent.py` extended (2 new), `test_ask.py` extended (2 new), `test_batch_conflict.py` (NEW, 8 tests). **C# (84 new EditMode tests):** PlaytestParserTests.cs (NEW, comprehensive parser coverage), SearchHelperFilterTests.cs (NEW, filter edge cases), CodeExecutorSecurityTests.cs extended. **Coverage targets:** Cost tracking (null/negative/zero edge cases), budget routing (consumption patterns), gating (visibility/disabled/force combinations), intent sampling (distribution accuracy), animator intent (state validity), batch operations (delete-chain conflicts, timeout edge cases), code execution (sandbox boundaries), playtest parsing (malformed responses), search filtering (regex precision). **Test discipline:** All new tests follow TDD pattern (test first, then production fix), use focused assertions, zero tautologies, all 1761 Python tests pass (was 1723 → +38), C# baseline (5 pre-existing EditMode reds) unchanged. **Files:** Python: `cost_tracker.py` (−1 line, handle None), `gating.py` (−2 lines, check is_visible), `middleware_guards.py` (−3 lines, batch conflict guard), + 13 test files modified. C#: PlaytestParserTests.cs, SearchHelperFilterTests.cs NEW, CodeExecutorSecurityTests.cs extended, + meta files. **Version bump:** Server 0.8.0 → 0.8.1, Plugin 0.17.19 → 0.17.20. **Net:** 122 new tests (38 Python, 84 C#), 3 production bugs fixed (all P0/P1), zero regressions, test discipline strengthened.

- **Test Audit Round 2: 177 New Tests (43 Python + 134 C#) — Error Paths, LRU Order, Registration** (2026-06-06) — Continuation audit expanding error-path and infrastructure coverage with zero production changes. **Python (43 new tests):** Write-tool error paths across `test_set_parent.py`, `test_integration.py`, `test_server_asset.py`, `test_server_ui.py`, `test_server_delta.py`; LRU eviction ORDER verification in `test_middleware_retry_cache.py`, `test_prefetch_cache.py` (confirm expiry follows insertion, not access); tool registration in NEW `test_tool_registration.py` (9 tests: register() idempotence, _send/_args cleanup, circular imports); scene session state in NEW `test_scene_session.py` (7 tests: create/destroy/query lifecycle); background prefetch in NEW `test_background_prefetch.py` (6 tests: TTL/warmup/invalidation); disabled-mode metric suppression in `test_degrade.py`. **C# (134 new EditMode tests):** ComponentSerializerTests.cs (NEW, 49 tests: all serialization paths, null-safety, cache correctness), ObjectManagerTests.cs (NEW, 22 tests: CRUD, parenting, prefab relink), CommandRouterTests.cs (NEW, 33 tests: dispatch, error handling, async safety), AssetHelperTests.cs (NEW, 30 tests: import, meta-sync, guid tracking). **Test count:** 1804 Python passed (was 1761 → +43), C# EditMode 1591 (5 pre-existing reds, 0 new). **Files:** 3 new Python test files, 4 new C# test files. **Version bump:** None (zero production code changes). **Net:** 177 new infrastructure/error tests, complete Round 2 audit.

- **Test Audit Round 3: 170 New Tests (69 Python + 101 C#) — Middleware/Compressor/Serializers/Helpers** (2026-06-06) — Final round audit completing infrastructure + serializer edge-case coverage with zero production changes. **Python (69 new tests):** Lockfile edge cases (stale pid, cleanup) +4; error-boundary + transport mutations +3; server edge-case handling +4; middleware CircuitBreaker + log-dir branches +5; LRU cache ordering + prefetch warmup +7; compressor (14 new tests covering gzip, zlib, brotli, streaming, null-safety, error paths); delta encoding (2 new), scene search (1 new), batch operations (3 new), autobatch refine (1 new), UI intent (3 new), screenshot describe (4 new), intent sampling (3 new), postprocessing (4 new), schema guard (7 new), registry (3 new), plugins (8 new async/mark.asyncio), hinter edge cases (2 new), metrics states (5 new), ask planner (6 new), budget router/registry (1+1 new), degradation mode (2 new). **C# (101 new EditMode tests):** SerializerTests.cs (NEW, 33 tests for MaterialSerializer, AnimationClipSerializer, GradientSerializer, AnimationCurveSerializer, TimelineSerializer covering all paths + nulls + cache). HelperTests.cs (NEW, 27 tests for MCPServer status/state, SearchHelper, PrefabHelper, MaterialHelper, AssetHelper edge cases + threading). CodexBackendTests.cs (NEW, 3 tests for first-turn snapshot injection). CliBackendBaseTests.cs extended +2, PendingTurnStateStalenessTests +4, ToolCallAccumulatorTests +2, TokenResetTests +3. UnityMCP.Editor.Tests.asmdef updated with Timeline references. **Test discipline:** Fixed test_plugins.py deprecated asyncio → @pytest.mark.asyncio async def; removed unnecessary time.sleep(0.01) in test_middleware.py; added try/finally cleanup in HelperTests; removed redundant nested #if UNITY_INCLUDE_TESTS in CodexBackendTests. **Test count:** 1894 Python passed (was 1804 → +90), C# EditMode 1488 (5 pre-existing reds, 0 new). **Files:** 23 Python test files modified, 2 new C# test files (SerializerTests, HelperTests), 4 modified C# test files, asmdef updated. **Version bump:** None (zero production code changes). **Net:** 170 new tests covering remaining middleware/serializer/helper gaps, three-round audit completes ~469 total new tests.

- **Test Audit Summary: 469 Total New Tests (238 Python + 231 C#), 3 P0/P1 Bugs Fixed**  (2026-06-06) — Three-round comprehensive test expansion from 40-architect review (Rounds 1+2+3). **Round 1 (122 tests):** Bug fixes + high-level tool coverage + parser/filter edge cases. **Round 2 (177 tests):** Error paths + LRU order + registration + serializers + object/command routing. **Round 3 (170 tests):** Middleware/compressor/serializers/helpers infrastructure + deprecation cleanup. **Totals:** 1894 Python (was 1723 → +171), C# EditMode 1488 (5 pre-existing). **Discipline:** TDD (test first), no tautologies, zero regressions. **Production changes:** 3 bugs fixed (P0 null-crash, P1 gating, P1 batch-conflict), −6 lines total. **Quality:** All subsystems graded B (architecture C→B, middleware D→B, hygiene D→B).

## [v0.17.18] — 2026-06-06 <!-- svg: F20–F22 bugfixes — select-all, @mention search, orphan bold -->

- **F20–F22 Bugfixes (Plugin v0.17.18)** (2026-06-06) — Three targeted fixes for chat input/output rendering. **F20 (Select-All Focus Fix):** Disabled `selectAllOnFocus` and `selectAllOnMouseUp` on the chat TextField in `InlineChipField` constructor to prevent text selection when focusing the input (UX regression from UIToolkit defaults). 2 new NUnit EditMode tests verify both flags are false. **F21 (@Mention Search Window):** Widened `BuildFromRaw` @mention fallback search from narrow `chipRawOffset ± mention.Length` to full-forward `rawText.Length - searchStart`, fixing cases where stored chip offsets undershoot the actual @mention position in raw text (e.g., chip stored at offset 16, @mention actually at 23). 2 new edge-case tests (F21, F21b duplicate names). **F22 (Orphan Bold Markers):** New `StripOrphanBold` method in `MixedParagraphRenderer` removes unmatched `**` bold markers from text segments adjacent to pills (LLM output: `"**[hierarchy:/Name]**"` → text segments `"**"` and `"**"` stripped, pill preserved). 3 new NUnit EditMode tests verify orphan stripping + coordinate preservation + balanced bold survival. **Tests:** 7 new EditMode tests across InlineChipFieldTests (2), BuildFromRawDefensiveTests (2), MixedParagraphBreakTests (3); all green. **Files modified:** InlineChipField.cs (2 lines), ChipTextInterleaver.cs (1 line), MixedParagraphRenderer.cs (13 lines + internal StripOrphanBold method), package.json (version bump), + test files. **Version bump:** 0.17.17 → 0.17.18.

## [v0.17.17] — 2026-06-05 <!-- svg: F15a-F19 chip redesign — linker disable, leading-space guard, context menus -->

- **F15a-F19 Chip Redesign (Plugin v0.17.17)** (2026-06-05) — Five production-ready features consolidating scene-object pill rendering + context menu integration + tool-detail CSS. **F15a (BuildFromRaw Defensive Tests):** Verified `ChatTranscript.BuildFromRaw` defensive fix that strips @mentions + test coverage (3 VE component integration tests, no @mention memory leak in TextElements). **F15b (Scene Linker Disabled During Streaming):** Disabled `SceneNameLinker` during `BeginAssistant` (set `MarkdownInline.Linker = null`) to render scene objects as pills, not live links; restored in `FreezeAssistantBubble`. Ensures pills render correctly without link-processing interference. Fixed test assertions in `SceneObjectNormalizationTests` SN1–SN7 (instanceID=0 → no `#0` suffix). **F15c (Leading-Space Guard):** Consolidated leading-space logic in `InlineChipField` — chips no longer glue to adjacent text. `AddChip`, `InsertChipAt`, `InjectMentionAt` unified via `prependSpace` parameter; new round-trip remove test confirms space preserved. **F16a (HierarchyContextMenu):** NEW `HierarchyContextMenu.cs` — right-click GameObject in Hierarchy → "Add to Chat Context" menu item (validated, safe extraction). **F16b (ComponentContextMenu):** NEW `ComponentContextMenu.cs` — right-click Component in Inspector → "Add to Chat Context" menu item (validated, safe extraction). **F18 (Line-Break Verification):** Verified line-break handling fix; added MP9/MP10 mixed-paragraph additional tests. **F19 (Tool-Detail CSS Fix):** Tool chip detail content now renders correctly: `tool-chip--expanded { flex-direction: column }` stacks tool details vertically; `tool-detail { flex-shrink: 0 }` prevents content collapse. **Tests:** 25 new EditMode tests across 5 test files (BuildFromRawDefensiveTests 65, ContextMenuTests 102, F15bScenePillPipelineTests 104, F15cSpaceAfterChipTests 76, F19ToolDetailTests 54, MixedParagraphBreakTests +20, SceneObjectNormalizationTests assertions fixed). **Files:** NEW HierarchyContextMenu.cs (32 lines), NEW ComponentContextMenu.cs (26 lines), ChatTranscript.cs (+4 lines, Linker disable), InlineChipField.cs (+19 lines, space guard), MCPChatWindow.uss (+4 CSS lines), + test files + meta. **Version bump:** 0.17.14 → 0.17.17. **Net:** Unified scene-pill rendering pipeline + right-click context integration + test-driven validation of BuildFromRaw/line-breaks/spacing.

## [v0.17.14] — 2026-06-05 <!-- svg: F13–F14 inline-chip architecture + bare-name normalizer + review fixes -->

- **F13 + F14 Inline-Chip Architecture + Bare-Name Normalization (Plugin v0.17.14)** (2026-06-05) — Four commits (880bc9b, 31a2cf2, bd23b71, ff81069) consolidating chip input/display UX + response normalization. **F13 Unified Architecture (880bc9b):** `ChipTextInterleaver.ToDisplayText()` now emits `@DisplayName` with proper spacing (leading space if prev char not space, trailing space, then Trim). `ToLlmPayload()` reuses ToDisplayText then appends chip context block (DRY). `ChatTranscript.FreezeAssistantBubble()` re-renders when normalization changes text. New E2E tests (M1–M10 interleaver, E2E_1–E2E_3 bubble). **F13 @mention Injection (31a2cf2):** `InlineChipField.AddChip()` injects "@DisplayName " at cursor; `RemoveChipAt()` strips @mention text. `InlineChipModel.AdjustOffsetsAfterTextChangeInclusive` adjusts chip offsets after TextField mutations. `ChipTextInterleaver.BuildFromRaw()` strips @mentions from raw text before building. MCPChatWindow.Send.cs uses BuildFromRaw. Fixes spacing + offset calculations. **F14 Bare-Name Normalizer (bd23b71):** NEW `BareNameNormalizer.cs` converts bare scene object names in LLM responses to `[kind:ref]` bracket tags; mirrors longest-first scan, word-boundary rules, protects existing `[kind:ref]` tags + triple-backtick fenced code blocks. NEW `ChipPillFactory.AddToContextAction` seam: right-click "Add to context" on response pills, preserves full ChipData (kindKey+instanceID). Wired in `MixedParagraphRenderer`, `ChatTranscript`, `MCPChatWindow`. **F14 Review Fixes (ff81069):** Triple-backtick fenced blocks now detected BEFORE single-backtick branch so names inside ```...``` are not replaced. AddToContextAction preserves full ChipData instead of re-deriving. Stale F13 comments updated. **Tests:** 201 BareNormalizerTests (fenced-block protection 16–17, edge cases), 186 ChipTextInterleaverTests (R1–R5 BuildFromRaw, @mention spacing), 68 AssistantBubbleNormalizationTests, 93 PillContextMenuTests, M1–M10 + E2E_1–E2E_3 interleaver; 1591 EditMode total (5 pre-existing reds). **Test DRY (implicit via commits):** ChipTestHelpers consolidates InsertChip/SetCursor/Type/SimulateSend helpers (−31 duplicated lines). PendingTurnStateTests split: core (187), V4 (197), Staleness (105). **Files:** BareNameNormalizer.cs (NEW, 106 lines), ChipPillFactory.cs (+AddToContextAction seam), ChipTextInterleaver.cs (+ToDisplayText @mention, +BuildFromRaw), InlineChipField.cs (+AddChip @mention, +RemoveChipAt), InlineChipModel.cs (+AdjustOffsetsAfterTextChangeInclusive), MCPChatWindow.* (OnEnable/OnDisable wiring), +test files. **Net:** +4 commits fixing all F13–F14 gaps, 200+ new tests, zero regressions, unified send path (rawText display + llmText AI).

## [v0.17.2] — 2026-06-05 <!-- svg: inline context chips + review fixes (regex + staleness + test DRY) -->

- **Inline Context Chips + Auto-Linking + Review Fixes (Plugin v0.17.2)** (2026-06-05) — Inline chip features + comprehensive test quality improvements. **Inline @DisplayName insertion:** `InsertInlineChip` captures cursor position and inserts `@DisplayName` directly at caret in the TextField. **Chip pill strip in bubbles:** Send path splits `rawText` (display with @names) from `llmText` (with `[kind:ref]` tags for LLM). Chip snapshot passed to `AppendUserBubble` which renders `.user-chip-strip` row. **@mention regex broadened:** `UserTextCleaner` regex changed `@[\w.]+` → `@\S+` to handle hyphens/parens in object names (e.g., `@Enemy(Clone)`, `@Player-Boss`). **Staleness check extracted:** New `PendingTurnState.IsStale()` static method encapsulates domain-reload staleness logic (60s grace window); replaces inline check in `MCPChatWindow.Drain.cs`. **Test DRY cleanup:** `ChipTestHelpers.cs` consolidates shared InsertChip/SetCursor/Type/SimulateSend helpers used across 6 test files (−31 lines duplicated code). **PendingTurnStateTests split:** Monolithic 292-line test split into 3 focused files: core (187), V4 (197), Staleness (105). Staleness tests rewritten to call `IsStale()` directly (were tautologies). **New tests:** 5 added: 3 regex edge cases (@User-Name-With-Hyphens, @Func()), trailing punctuation, no-refocus cursor edge case. **Files modified:** `UserTextCleaner.cs` (regex), `PendingTurnState.cs` (+IsStale), `MCPChatWindow.Drain.cs` (−4 lines, calls IsStale), `ChipTestHelpers.cs` (NEW, shared), `PendingTurnStateTests.cs` (−105 lines, core tests), `PendingTurnStateV4Tests.cs` (NEW), `PendingTurnStateStalenessTests.cs` (NEW), +5 test methods. **Tests:** ~1550 EditMode pass (5 pre-existing reds). **Net:** −31 lines duplicated code, +56 in helpers, +6 in PendingTurnState, comprehensive test split/rewrite, zero regressions.

## [v0.17.0] — 2026-06-05 <!-- svg: full-project code review sprint — 12 waves of fixes across Python + C# -->

- **Full-Project Code Review Sprint (Server v0.8.0, Plugin v0.17.0)** (2026-06-05) — 12-wave autonomous review sprint covering all Python and C# subsystems. **Wave 1-2 (Python critical + DRY):** 7 critical bug fixes (screenshot path parsing, batch negative timeout, CircuitBreaker race, wrap_send closure waste, WRITE_CMDS sync, version drift, time.monotonic), DRY cleanup (-126 lines: shared _levenshtein, parse_kv_line, sanitize_intent, dead code removal). **Wave 3 (Python splits):** middleware.py 941→120 lines via 5 mixin modules + pipeline + types; bridge/editor_log/server/visual_diff/hinter/metrics/scene all split into focused files (14 new modules, -1027 lines from oversized files). **Wave 4 (Python tools):** skills.py kind field for correct routing, ui_intent nested path fix, SchemaGuard decoupled from middleware internals, compress_hierarchy DRY. **Wave 5 (C# core):** JsonHelper string-tracking in ExtractObject/ExtractArray, CommandRouter fault-safe ContinueWith, MCPServer volatile _isCompiling for thread safety, AnimatorController sb.Insert→direct append, MCPServer TeardownCore DRY. **Wave 6 (C# splits):** ObjectManager (Properties + Events), ComponentSerializer (Finder), ReferenceHelper→RemapReferencesHelper, ParticleHelper (Presets), ShaderGraphHelper (Mutations). **Wave 7 (C# DRY):** ValueParser.ParseVector4Lenient + ParseBool, int.Parse→TryParse+InvariantCulture, tool builder depth guard, dead params removed. **Wave 8 (Chat):** CompileErrorCapture.HasErrors, FlowBar DRY, compiled Regex, ChatProcess TOCTOU fix. **Wave 9 (Tests):** 4 duplicate tests removed, PEP 604→Optional for Python 3.9 compat. **Wave 10-11 (Hygiene + Skills):** README Unity version, phantom dep removed, stale files deleted, 3 skills updated, 3 new skills created (chat-system, intent-sampling, budget-system). **Wave 12 (Final review):** 16-architect parallel review found 12 additional bugs: enum round-trip, intent budget keys, distiller registry, WireEvent ParseBool, Collider2D triggers, AnimatorController Bool params, AnimationMode leak, InjectCompileErrors guard, accumulator reset, _node_path loop guard, dead no_validate param, test Python 3.12 compat. All subsystems graded B (up from C/D). **Tests:** 1723 Python passed (was 1726, -4 duplicates +6 new -5 dead code). **Grade improvement:** Core C→B, Middleware D→B, Hygiene D→B, Architecture C→B.

## [v0.16.0] — 2026-06-05 <!-- svg: F12 chat UX overhaul — composed inline-chip field + response pills + session clear -->

- **F12 Chat UX Overhaul (Plugin v0.16.0)** (2026-06-05) — Five production-ready pieces shipping together: (1) **W0 composed inline-chip field (P1+P2 resolved by construction):** Replaced 466-line overlay stack (InlineChipOverlay, NbspReservation, UitkCharRect, TokenSpan) with a simple composed VisualElement (`InlineChipField`) — flex-row of pill VEs + TextField. Pills are layout children, not overlays, so they never mis-position and never vanish on typing. Enables Backspace-at-0 to remove last chip (atomic tag-input UX). New `InlineChipModel` (pure headless data), `ChipPillFactory` (shared pill builder routed through registry), `InlineChipField` (control). Package.json unity min bumped 2022.3 → 6000.0 (editor already 6000.3.0b7). (2) **P3+P5 removed auto-selection:** Deleted the legacy auto-prepend of `SelectionSummary` in send path. Context now flows exclusively through the typed chip pipeline (P3 duplicate context eliminated, P5 verbosity resolved). (3) **P4 per-kind chip display settings:** `ChipDisplayOverride` struct + parallel-array serialization in `ChipConfig`; settings form now enumerates all registered kinds (built-in + 3rd-party plugins) dynamically with depth dropdown (none/path/summary/full) + color field per kind, zero core edits needed for 3rd-party display customization; `ChipPillFactory.ColorResolver` static seam captures config once, live-repaint on settings save. (4) **P7 response scene-object pills:** Response-side `[kind:ref]` tags now render as graphical ChipPillFactory pills in paragraphs/lists via new `MixedParagraphRenderer` + `ResponseTagInliner.Split()` + `RefParser` (inverse of FormatChipRef); pills show leaf name, click→ping/select, tooltip=full ref; fixed HierarchyChipProvider.Navigate to strip #id before lookup. (5) **P6 new-session/clear dropdown:** Wired Clear button with confirm dialog that kills+restarts the backend (fresh `EditorStateSnapshot` + `SessionId=null` for next turn, no `--resume`), clears transcript/input/chips, calls `ReloadGuard.ClearPendingState()` so domain-reload can't resurrect old turn state. **Tests:** 1581/1586 EditMode green (5 known pre-existing reds, 0 CS errors). Total: −806 net code lines (overlay+positioning stack deleted), +23 new tests (model/factory/field + pill rendering + session reset), 1538→1586 gate progression. **New files:** InlineChipModel.cs, ChipPillFactory.cs, InlineChipField.cs, MixedParagraphRenderer.cs, MCPChatWindow.Session.cs, test stubs. **Deleted:** InlineChipOverlay.cs, NbspReservation.cs, UitkCharRect.cs, TokenSpan.cs, InlineChipKeyHandler.cs, InlineChipTrackerTests.cs, NbspReservationTests.cs, TokenSpanTests.cs, Wave4ChipInputTests.cs, + 50 obsolete tests. **Breaking:** ChipConfig default depth changed "summary" → "path" (token-minimal default); users restore via F9 settings form. Marked in-code `// BREAKING (v0.16.0)`.

## [v0.15.8] — 2026-06-05 <!-- svg: inline-chips + extensible chip-kind registry — F11 -->

- **Inline Chips + Extensible Chip-Kind Registry (Plugin v0.15.8, F11)** (2026-06-05) — Production-ready extensible typed-context-chip system for in-Unity agent chat. **Extensibility (centerpiece):** `IChipKindProvider` public interface + `ChipKindRegistry` static class enable third-party plugins (in separate asmdefs referencing `UnityMCP.Editor.Chat`, defining `UNITY_MCP_CHAT`) to register own chip kinds — own DISPLAY (icon/color/pill), own LLM PAYLOAD (`FormatPayload`), own object-type mapping (`CanHandle`/`Create`), own click `Navigate` — with ZERO core edits. 8 built-in providers in `BuiltInChipProviders.cs` (hierarchy/scene/script/prefab/material/texture/scriptable-object/asset). Enum `ChipKind` entirely REMOVED; `ChipData.KindKey` (string) is the sole identity. Registry priority convention: built-ins 100–800; plugins <100 to override a type, >800 to extend. **Inline-at-cursor rendering:** Chips render embedded into the TextField at caret (not a strip above). `UitkCharRect.cs` does positioning via PUBLIC `TextField.textSelection.GetCursorPositionFromStringIndex` path — confirmed live on Unity 6000.3.0b7. `NbspReservation.cs` reserves pill width via U+FFFC + N×U+00A0; `TokenSpan.cs` gives atomic-caret behavior (caret skips chips, backspace deletes whole). Full H10 degradation: if positioning unavailable, falls back to row-layout strip (current behavior) — `if (UitkCharRect.IsAvailable)` guards every NBSP/positioning/atomic-caret path. **"Show LLM payload" context menu** reveals byte-for-byte send-path payload (symmetry test enforces). **Reload survival (PendingTurnState v4):** `KindKeys[]` parallel to `ChipPaths`, re-binds chips by KindKey after domain reload (falls back to re-detection if provider not yet registered). **BUG B (BREAKING):** `ChipConfig` default depth changed `"summary"` → `"path"` (token-minimal default; restore via F9 settings form). Marked in-code `// BREAKING (H15)`. **Tests:** 1562 EditMode tests, 1557 passed, 5 KNOWN pre-existing (GetEnabledTools_ExcludesDisabledTool, Revert_RevertsChanges, List_ToolsMenu_ContainsMCPSettings, ValueParser_Enum_NegativeInt, ChatStreamParserTests.ParseLine_UserToolResult_NestedContentArray_ExtractsText). **New files:** IChipKindProvider.cs, ChipKindRegistry.cs, ChipKindKeys.cs, BuiltInChipProviders.cs, ChipPayloadContext.cs, NbspReservation.cs, TokenSpan.cs, UitkCharRect.cs, MCPChatWindow.ChipInput.cs, MCPChatWindow.Send.cs, Wave4ChipInputTests.cs + others (total 40+ new/modified Chat files + tests). **Verification:** EditMode green after clean-editor-restart (external file: UPM package serves stale dlls, hence deterministic restart); console clean; positioning probe live; manual visual acceptance (drag-to-pill render, atomic caret, scroll-clip, context menu, response pills, custom-provider render) is separate USER step still pending.

## [v0.15.0] — 2026-06-04 <!-- svg: chat UX polish sprint — F1–F10 + review-hardening -->

- **Chat UX Sprint: 10 Features + Review-Hardening (Plugin v0.15.0)** (2026-06-04) — Six-wave comprehensive UX polish for in-Unity agent chat. **Wave A (F8, F4, F7):** Remove "(Beta)" labels from toggle/settings, hierarchy refs carry `#instanceID` for disambiguation, status panel distinguishes CLI-listening from Chat-active (ChatBackendProbe reflection-based, domain-reload safe). **Wave B (F2, F1, F6, F3):** Restore button cascade-rewind turns (TurnUndoTracker.RestoreFromIndex), token counters reset on backend/model switch, auto-scroll toggle (EditorPref, default on), Approve button shows only for real tool calls. **Wave C1 (F9):** Per-backend settings form writes own JSON (Library/MCP_ChatBackendConfig.json), feeds to CLI arg-builders (model, perm-mode, extra args); BackendConfig + BackendConfigStore + BackendSettingsForm UIToolkit (Claude/Codex dropdowns). **Wave C2 (F5):** Inline removable chips at cursor (U+FFFC markers, InlineChipTracker, InlineChipOverlay, context menu "Add Selection to Context"), drag-drop vs inline routing via hit-test. **Feature F10 (Typed Context Tags):** Each attached object carries a KIND (hierarchy/scene/script/prefab/material/texture/scriptable-object/asset) + per-kind depth config (none|path|summary|full); AI-facing format `[kind:ref]` e.g. `[hierarchy:/Player #123]`, compact colored pills on send+response. ChipKindDetector, ChipData.Kind, ChipConfig, ResponseTagInliner (conservative regex, no false positives), symmetric chips in/out. **Review-Hardening (Wave C3):** ArgTokenizer (shell-style quote-aware split, DRY across both arg-builders, fixes quoted multi-word ExtraArgs corruption; +11 tests); ChatBackendProbe per-call resolution (domain-reload safe, drops stale static cache); MCPChatWindow OnSend dedup (load BackendConfigStore once, thread into AppendChipContext, lazy fallback). Verified: **1505 EditMode tests, 1500 passed, 5 known pre-existing reds**. New tests: ChipKindDetector 13/13, ResponseTagInliner 17/17, EmitTyped 7/7, DepthFor 10/10, ChipConfig 3/3, ArgTokenizer 11/11, TokenReset suite, InlineChipTracker 13/13, +others. Files: 18 new .cs files (ArgTokenizer, BackendConfig, BackendSettingsForm, ChipKindDetector, ResponseTagInliner, InlineChipData, InlineChipKeyHandler, InlineChipOverlay, etc.), modified MCPChatWindow partials + supporting infrastructure. Net: 10 distinct user-facing features + hardening across 6 waves.

## [v0.14.0] — 2026-06-04 <!-- svg: multi-backend agent chat — Claude + Codex via DRY CliBackendBase -->

- **Multi-Backend Agent Chat: Codex Support via DRY CliBackendBase (Plugin v0.14.0)** (2026-06-04) — Added OpenAI Codex as a sibling backend alongside Claude, sharing one abstract `CliBackendBase` host. Each CLI-backend is a strategy over 4 variation axes: `BuildArgs` (spawn/resume argv), `ParseLine` (NDJSON → ChatEvent), `BinaryName` (CLI binary name), `IsPersistentProcess` (stdin loop vs. spawn-per-turn). **CliBackendBase:** 127-line abstract host owning shared lifecycle (spawn, drain, accumulate, SessionId, Stop, Dispose). **ClaudeBackend:** Ported onto base with zero behavior change (−65 lines, regression anchor). **CodexArgBuilder:** Constructs `codex exec --json` argv (+ `exec resume <id>`) with three `-c mcp_servers.*` flags re-passed every turn incl. resume; stdin closed for spawn-per-turn model. **CodexStreamParser:** Codex NDJSON → ChatEvent (agent_message, mcp_tool_call, command_execution[aggregated_output/declined], file_change[changes], usage; CostUsd=0). **PendingTurnState v3:** BackendKind persisted for domain-reload survival (back-compat with v1/v2 state). **Backend selection:** Wired into dropdown + `MCPChatWindow.CreateBackend` factory switch + `BackendKind` enum + `BackendRegistry`. **Tests:** 1389 EditMode, 1384 pass (5 pre-existing reds, 0 new). CodexStreamParser 26/26. CliBackendBase, CodexArgBuilder, PendingTurnState v3 all covered. Net: +23 lines of production code while adding a whole second backend. File changes: new CliBackendBase.cs, CodexArgBuilder.cs, CodexStreamParser.cs, CodexBackend.cs + tests; modified ClaudeBackend.cs (ported, zero behavior change), PendingTurnState.cs (v3 header), BackendRegistry.cs, MCPChatWindow.cs, .gitignore (ignore local .codex/ machine-absolute paths).

## [v0.7.1] — 2026-06-04 <!-- svg: tech-debt sprint wave 1–3 (Python/C#/Chat) — pure quality -->

- **Tech-Debt Sprint: Python Tooling + C# Plugin + Chat Hardening (Server v0.7.1, Plugin v0.13.4, 6 commits)** (2026-06-04) — Six-wave quality sprint addressing dead code, stale config, and chat resilience. **Wave 1 (Python):** `gen_changelog_svg.py` dual-version support (v0.7.0 / v0.7.1 renders correctly), `batch` token economy (unnecessary key omissions), 23-layer middleware dead-code removal (3 obsolete layers deleted), port-discovery test suite added (25 new `test_read_unity_port.py` cases). **Wave 2 (C# Plugin):** CommandSchema params (summary/incremental/dry_run/force fields removed from schema, reducing 4-param noise for stateless tools); 15 dead command aliases dropped; SpatialHelper InvariantCulture (float→string locale-safe); RuntimeHelper.FindComponent DRY delegate (consolidated 3 search patterns); SearchHelper single-walk (removed redundant dual-loop); 8 dead `#if` guards removed. **Wave 3 (Chat):** Undo persistence across domain reload (PendingTurnState + Undo group tracking), PendingTurnState v2 header with staleness check + 60s grace-window back-compat, enabled-tools cache computed OFF the TCP read thread (warm-up before accept loop + cached via EditorPrefs), send re-entrancy guard, build-target define (UNITY_MCP_CHAT), stderr surfacing (StderrRingBuffer), turn↔batch tests. **Wave 3c (Chat DRY/UX):** SelectionSummary/ChipContextResolver dedup, PrefKey const dedup, ChatBinaryResolver negative-cache (+ResetCacheForTests seam), hardcoded hex → `isProSkin .chat-root--light` USS class, SlashPopup ScrollView (removed MaxVisible=5 cap). **Test Coverage:** 1726 Python non-live tests pass (1779 collected, 53 live deselected); 25 script tests; 1336 C# EditMode tests (1331 pass, 5 pre-existing baseline failures, zero regressions). Files: server/src/ (11 py touched), unity-plugin/Editor/ (15 cs touched + tests).

## [v0.7.0] — 2026-06-04 <!-- svg: Editor.log out-of-band corroboration — P0 compile-tool blindness fix -->

- **Out-of-Band Compile-Tool Corroboration via Editor.log (Server v0.7.0, P0)** (2026-06-04) — `get_compile_errors`, `await_compile`, `auto_fix`, and `ask` plans now cross-verify "clean" responses from the in-plugin C# reporter against Unity's `Editor.log` (out-of-band signal immune to plugin compile failures). New module `server/src/unity_mcp/editor_log.py` parses Unity 6 Bee/Csc error logs (anchored on `## Script Compilation Error for:` marker; fallback for legacy pre-Unity-6 single-assembly format). Only overrides C#'s "clean" when BOTH signals agree: log shows errors AND dll is stale (mtime check vs plugin source). Fresh/undeterminable dll → trusts C# (zero false positives). Wired into ALL FOUR result-surfacing callers (DRY pattern) via `init_corroboration()` + `corroborate()`. This fixes the P0 silent-blindness bug where compile failures in `UnityMCP.Editor` itself caused the reporter to answer "No errors" from stale bytecode (observed in prior sprint: `UndoGroupHelper` CS0117 masked for 5 hours). Validated against real Unity 6 (6000.3.0b7); SPOF now CORROBORATED. Test count: **1709 passed** (was 1652 → +57 new tests incl. real-format log fixtures). Files: `server/src/unity_mcp/editor_log.py`, `server/src/unity_mcp/tools/scene.py`, `server/src/unity_mcp/tools/code_intel.py`, `server/src/unity_mcp/tools/codegen.py`, `server/src/unity_mcp/ask/executor.py`, `server/tests/test_editor_log.py`, `server/tests/test_codegen_corroboration.py`, `server/tests/fixtures/unity6_compile_*.log`, `server/tests/test_ask.py`.

## [v0.6.1] — 2026-06-04 <!-- svg: atomic batch rollback — transactional scene edits -->

- **Atomic Batch Rollback (v0.6.1 / 0.13.1, F27)** (2026-06-04) — Opt-in `atomic=true` mode for the `batch` command enables transactional execution: on the FIRST failing operation, all prior operations in that batch are reverted via F6's reusable `UndoGroupHelper` primitive (`OpenNamedGroup`/`RevertToBeforeGroup`), leaving the scene exactly as before. Default `atomic=false` preserves backward-compatible non-transactional behavior; the param is token-neutral and NOT sent over wire when false. Nesting handled via `_batchDepth` counter: only the outermost batch (depth=1) opens and reverts the Undo group, ensuring nested batches also roll back under a single outer group. `atomic` parameter overrides `on_error` — when atomic, the batch always stops on first failure (atomic semantics take precedence). Error output adds a new `ATOMIC_ROLLBACK: reverted ops 0..K-1` line when rollback executes, or `op 0 failed, nothing to revert` when the first operation fails. Documented limitation: `execute_code` file-system side effects inside an atomic batch are NOT reverted (only Unity Undo-registered scene mutations roll back). 30 NUnit EditMode tests (MCPBatchAtomicTests) + 8 Python pytest tests green. Files: `server/src/unity_mcp/tools/batch.py`, `unity-plugin/Editor/BatchHelper.cs`, `unity-plugin/Editor/Tests/MCPBatchAtomicTests.cs`, `server/tests/test_batch.py`.

## [v0.5.0 / 0.12.0] — 2026-06-04 <!-- svg: scoped scene queries — search_scene root+limit + spatial center -->

- **Scoped Scene Queries (Server 0.5.0, Plugin 0.12.0, F13)** (2026-06-04) — Two existing tools extended with new optional parameters (no new tools, zero new commands — pure DRY). `search_scene` gains `root` (subtree scope) and `limit` (result cap, default 50) params; results beyond limit show overflow marker `...+{N} more (limit={L})`. `spatial_query` gains `center` (world-position origin as `"x,y,z"` string) as alternative to `path` (path now optional; center takes precedence when both given). Both reuse existing helpers (`SearchHelper.ParseQuery`/`CollectMatches` for search, `SpatialHelper` for spatial). Backward-compatible; default limit not sent over wire (~20x token compression on "find objects matching criteria" vs hierarchy dump). 12 Python unit tests (search_scoped + spatial_center) + 1 live TCP test + 16 C# NUnit EditMode tests green. Files: `server/src/unity_mcp/tools/scene.py`, `server/src/unity_mcp/tools/spatial.py`, `unity-plugin/Editor/SearchHelper.cs`, `unity-plugin/Editor/SpatialHelper.cs`, `unity-plugin/Editor/CommandRouter.cs`, `unity-plugin/Editor/CommandSchema.cs`, + test files.

## [v0.11.0] — 2026-06-04 <!-- svg: per-turn undo rollback + Restore button -->

- **Per-Turn Undo Rollback (Plugin 0.11.0, F6)** (2026-06-04) — In-Unity Chat now wraps each agent turn in a named Unity Undo group; an amber **Restore** button appears after each turn and reverts that turn's scene mutations in one click (scene-only, native Unity Undo). Only the last turn's button is active; older buttons disable when a new turn starts. Resumed-after-domain-reload turns also get a group. Built on a new reusable core primitive in `UndoGroupHelper` (public API: `OpenNamedGroup`, `CloseNamedGroup`, `RevertToBeforeGroup`, `CanRevert`) that upcoming F27 (atomic batch rollback) will reuse — one rollback system, not two. New files: `TurnUndoTracker.cs`, `RestoreButton.cs`, `MCPChatWindow.Undo.cs` (split from MCPChatWindow.cs), 11 NUnit EditMode tests (TurnUndoTrackerTests 9/9 green, RestoreButtonTests 2/2 green). `MCPChatWindow.uss` updated with `.chat-btn--restore` styling. Core `UndoGroupHelper.cs` exposed with 6 NUnit EditMode tests (UndoGroupHelperTests green). Total test count: 15+ EditMode + 1637 Python unit tests green.

## [v0.10.0] — 2026-06-04 <!-- svg: chat plan/act approve & execute + slash templates -->

- **Plan/Act "Approve & Execute" Bridge (Plugin 0.10.0, #11)** (2026-06-04) — After a Plan-mode (Ask) turn finishes, `MCPChatWindow.Drain.cs` injects a one-shot "Approve & Execute" button. Clicking it captures the backend `SessionId`, flips the window to Agent mode, recreates the backend with `--resume <sessionId>` (plan preserved), and auto-dispatches "Execute the plan above." Files: `MCPChatWindow.Approve.cs`, `ApproveHelper.cs`, `ApproveButtonFactory.cs`, +9 lines in `MCPChatWindow.Drain.cs`, `ChatTranscript.Append(VisualElement)` made internal. 10 NUnit EditMode tests green.
- **Slash-Command Templates (Plugin 0.10.0, #12)** (2026-06-04) — Typing `/` in the composer opens a UIToolkit popup of 5 builtins: `/fix-compile`, `/add-component`, `/playtest`, `/inspect`, `/screenshot`. Selecting one resolves to plain text BEFORE send — pure input transform with NO MCP coupling. Optional context-gather (compile errors / selection / scene state / console) with graceful fallback on throw. KeyDown on parent at TrickleDown ensures Enter resolves template BEFORE `EnterKeySend` fires. Files: `SlashTemplate.cs`, `SlashRegistry.cs`, `SlashPopup.cs`, `MCPChatWindow.Slash.cs`, +44 lines MCPChatWindow.uss. 16 NUnit EditMode tests (SlashRegistryTests 16/16, SlashPopupTests 7/7). Compile-clean after recompile + domain reload.

## [v0.9.0] — 2026-06-04 <!-- svg: chat context resolution + compile gating tool -->

- **Chat Context Resolution via Chips (Plugin 0.9.0, #2)** (2026-06-04) — `ChipContextResolver.cs` resolves object-path chips at send-time to plain text at three depths: PathOnly / Summary / Full. One chip → Full (all components), many chips → Summary (top 3), asset paths → PathOnly. 2000-char budget caps Full back to Summary. Wired into MCPChatWindow's send path (OnSend + AttachScreenshot). Reuses SelectionSummary + ComponentSerializer (DRY). Eliminates the 1–3 `get_component` round-trips the model used to spend discovering chipped objects. 12 NUnit EditMode tests green.
- **Await Compile Gating Tool (Server 0.4.0, #10)** (2026-06-04) — New read-only MCP tool `await_compile(timeout=60, retry_interval=0.5)` registered in `code_intel.py` (TIER1 + ADVANCED_CODE). Blocks until Unity finishes compiling AND domain-reloading, polls existing `compile_status` + `get_compile_errors`, survives domain-reload disconnect (reconnects, re-queries) up to timeout. `timeout=0` = instant snapshot. Returns compile errors as plain text — a deterministic replacement for `sleep`-then-poll after writing C#. 13 pytest tests green. This is a real new tool agents can call.

## [v0.8.0] — 2026-06-04 <!-- svg: compile auto-fix + editor-state injection + tool ping -->

- **Compile Auto-Fix Retry Loop (Plugin 0.8.0, #5)** (2026-06-04) — `CompileAutoFix.cs` watches `EditorApplication.CompilationFinished` events and auto-retries up to 3 times when chat edits compile. Provenance-gated: only arms when the turn actually edited a `.cs` file (`_turnEditedCode` flag in MCPChatWindow.Drain.cs), preventing false-positive retries on manual IDE edits. Features a state machine (Armed/Disarmed) and graceful exhaustion (final compile absorbed silently; exhaustion shown via cap chip).
- **Editor State Snapshot Injection (Plugin 0.8.0, #7)** (2026-06-04) — `EditorStateSnapshot.cs` builds a plain-text `[Unity State]` block (active scene, compile status, console error count) with a 500-character scene-dump cap + ellipsis truncation. Injected via `--append-system-prompt` on fresh chat sessions (ClaudeArgBuilder.cs / ClaudeBackend.cs). On domain-reload resume, the snapshot is prepended to sent text via `SentTextCache`, eliminating the 2–3 cold-start probe calls Claude used to make. Result: better context-awareness without extra turns.
- **Tool Ping on Call Complete (Plugin 0.8.0, #29)** (2026-06-04) — `ToolPing.cs` flashes any GameObject a tool call touches via `EditorGUIUtility.PingObject`. Extracts object path from tool args (scene path or component ref) and resolves it via `ComponentSerializer.FindObject`. Fires once on args-complete, on the main thread inside MCPChatWindow.Drain, with graceful no-op if path missing/unresolvable. Immediate visual feedback: user sees which object was just edited.
- **Test Coverage Expansion** (2026-06-04) — 50 new EditMode NUnit test cases across CompileAutoFix, EditorStateSnapshot, ToolPing, plus enhanced Drain.cs tests. Total test count: 1188 (35 pre-existing failures unrelated). All CompileAutoFix retries, truncation edge cases, path resolution, and ping lifecycle paths covered.

## [v0.7.0] — 2026-06-04 <!-- svg: F4 deferred schema + reload-survival + auto-selection -->

- **Deferred MCP Tool-Schema Loading (Server 0.3.0, F4)** (2026-06-04) — Non-core tools now ship a stub `inputSchema` (`{"type":"object"}`) instead of full schemas, reducing per-turn schema tokens by ~58–68%. Full schemas are served lazily via a new meta-tool `resolve_tool_schema(tools: "comma,separated")` that returns plain-text blocks. Backwards-compatible: MCP dispatch doesn't validate against inputSchema. Escape hatch: `UNITY_MCP_FULL_SCHEMAS=1` disables stripping (default off). New files: `server/src/unity_mcp/tools/schema_registry.py` (SchemaRegistry singleton, STUB_SCHEMA). 1624 Python unit tests pass.
- **Chat Domain-Reload-Safe Turn Survival (Plugin 0.7.0, F4)** (2026-06-04) — Chat sessions now survive Unity domain reload mid-turn. `ReloadGuard` locks assemblies during a turn + 120s watchdog. `PendingTurnState` persists in-flight state to `Library/MCP_ChatPendingTurn.txt` (plain-text, pipe-delimited, base64-encoded). On `afterAssemblyReload`, `MCPChatWindow.OnEnable` resumes via `claude -p --resume <sessionId>`. `SentTextCache` dedupes on reconnect. Result: editing a script mid-chat no longer kills the session. 41 EditMode NUnit tests (run in Unity Test Runner). New files: `ReloadGuard.cs`, `PendingTurnState.cs`, `SentTextCache.cs` + tests.
- **Chat Auto-Include Selection Context (Plugin 0.7.0, F4)** (2026-06-04) — `SelectionSummary` auto-prepends the active GameObject's hierarchy path + top 3 non-Transform components to user messages (e.g., `[Selection: /Enemies/Boss (Health, Animator, Collider)]`). Deduped against existing object chips. Claude now knows what you're editing without explicit mention. Deferred rendering; chip paths persisted but not repainted after reload (UX-only; turn executes correctly). 26 EditMode NUnit tests.

## [v0.6.0] — 2026-06-03 <!-- svg: Aura pill + native theme + perms gating -->

- **Aura Status-Bar Pill with State-Driven Pulsation** (2026-06-03) — Redesign the AppStatusBar MCP pill as an opaque chip + colored border (fixes the low-contrast empty-box look) with a beacon dot and a faked halo. Pulsation by state: connected = radiating ring + dot heartbeat, waiting = in-place swell, stopped = static dimmed dot. Text pinned opaque for legibility; the whole chip opens the action menu. Palette extracted to a testable MCPStatusBarPalette class with NUnit EditMode tests.
- **Settings Window Native Theme** (2026-06-03) — Replaced hardcoded navy hex in MCPSettings.uss with `var(--unity-colors-*)` theme variables (window-background, default-border, label-text) so the settings panel blends with editor theme; stripped custom button/hover chrome. Matches MCPStatus.uss + MCPChatWindow.uss. 139→119 lines.
- **Chat UI Native Redesign: Header Removal + Bottom Footer + Token Readout + Track+Chip Animation** (2026-06-03) — Drop entire header/toolbar; replace cost badge with native tokens-only readout (↑ in ↓ out, new TokenFormat.Abbr pure helper, 6 NUnit tests); move agent/backend selector + Ask/Agent toggle (now native segmented control) + token readout into unified bottom footer bar. Native button fidelity (3px radius, no bold, pressed state via theme variables). Collapse redundant dividers to one (`.input-area` top border only, theme USS variables). Kill typing-dots indicator. Rework FlowBar activity animation from broken full-bar translate to fixed track + traveling chip with colour crossfade Sending→Receiving (950ms tick, smooth). MCP Status window: replace navy `#1a1a2e` + custom hex with Unity theme USS variables, semaphore orb colours kept. Bottom status-bar pill: LEFT placement (Insert(0), no overlap), self-heal persistence on dock/maximize/play-mode detach, calmer pulse (Up=steady 1.0, Listen=gentle breathe 0.85↔0.6, Down=dim 0.5; no server change). New files: TokenFormat.cs + TokenFormatTests.cs. Modified: MCPChatWindow.cs (split → .Drain.cs + .FlowBar.cs), MCPChatWindow.uss, MCPStatus.uss, MCPStatusBarWidget.cs. Theme: `var(--unity-colors-button-background-pressed)`, `--unity-colors-highlight-*`, `--unity-colors-label-text`, `--unity-colors-error-text`, etc. Plugin version 0.5.0→0.6.0.
- **Per-Tool Permission Gating in Agent Chat** (2026-06-03) — New Perms control in the chat footer opens a per-tool allow/deny popup (foldout per catalog category, Allow/Deny-All). Denied tools are withheld from the agent by enumerating only the allowed tool ids via `--allowedTools`; the default stays allow-all so existing behavior is unchanged (empty deny-set → compact `mcp__unity` blanket, not 88 enumerated ids). Per-tool ids use the live MCP server-key prefix `mcp__unity__` (matches ~/.claude/mcp.json key `unity`); blanket + per-tool prefix derive from one shared const so they can't drift. Deny-set persisted in EditorPrefs; catalog read live (incl. plugin tools) so newly added tools auto-allow. New: PermissionConfig + MCPChatWindow.Permissions partial; ClaudeArgBuilder gains an allowed-tools enumeration path. Tests: PermissionConfigTests (15) + ClaudeArgBuilderTests (13). Plugin version 0.5.0→0.6.0.
- **Chat Fixes: Verb-Label Prefix + Composer Anchoring + Enter Dedup + Themed Permissions Popup** (2026-06-03) — Four follow-up fixes within the v0.6.0 chat wave. (1) ToolVerbMap humanized labels used a stale `mcp__unity-mcp__` prefix that never matched live ids; all 20 keys now derive from the shared `PermissionConfig.MCP_TOOL_PREFIX` const so verb labels resolve and can't drift (drift-guard NUnit test added). (2) Composer now hugs the footer — the input area was given a min-height *floor* while its height was cleared, so `.chat-input` flex-grow had no definite parent size and the surplus became a dead gap; UpdateAutoHeight + ResetInputAreaHeight now set a definite height and clear min-height. (3) Enter sends without leaking a newline — Unity fires up to two KeyDownEvents per press (keyCode=Return, then character='\n') and the echo slipped past the keyCode-only check, sometimes inserting a stray newline after the field was cleared; new pure `EnterKeyLogic.DecideEnter`/`IsEnterChar` plus a dedup flag in EnterKeySend suppress every Enter event and act exactly once (Alt+Enter still inserts one newline), caret reset to 0 on send. (4) Tool Permissions popup restyled to match the Settings window via new tri-state `PermCategoryGroup` (reads/writes through PermissionConfig) + search field, reusing MCPSettings.uss classes through LoadStyleSheet. Tests: +8 pure tests (DecideEnter truth-table + IsEnterChar edges). No version bump (within v0.6.0).
- **Permissions Relocated from Chat Footer to Settings Window** (2026-06-04) — Moved per-tool allow/deny out of the chat-footer button + popup into a collapsed "Agent Tool Permissions" foldout in the MCP Settings window (Allow All / Deny All presets + search + tri-state per category). `PermissionConfig` + `PermCategoryGroup` moved down from the `UnityMCP.Editor.Chat` assembly into core (`UnityMCP.Editor`) so the Settings window hosts them natively — they only depend on core + a catalog func, and putting them in core avoids a circular asmdef reference (Editor→Chat would be a cycle). A shared EditorPrefs key prefix (`PermissionConfig.DEFAULT_PREFIX`) guarantees the Settings panel and the chat backend read/write the same deny-set. Deleted `MCPChatWindow.Permissions.cs` (the button + `PermissionsPopup`); footer spacer keeps the bar coherent. New `MCPSettingsPermUI` (foldout builder, reuses MCPSettings.uss). Tests moved to `UnityMCP.Editor.Tests` + a pinning test that fails if the parameterless ctor ever drifts off `DEFAULT_PREFIX` (would orphan saved prefs). `.meta` GUIDs preserved via `git mv`. No version bump (within v0.6.0).
- **Changelog Now Single-Source + Auto-Generated README Animation** (2026-06-04) — Moved all release-history text out of the README into `/CHANGELOG.md` (single source of truth); the README now embeds an auto-generated SMIL ECG-timeline SVG built from the changelog by `scripts/gen_changelog_svg.py` (parses `## [vX.Y.Z] — DATE <!-- svg: caption -->` headings → `docs/assets/changelog.svg`; deterministic/idempotent, 25 pytest cases, stdlib-only, zero `<script>`). No version bump (within v0.6.0).

## [v0.5.0] — 2026-06-03 <!-- svg: chat UX polish — refs, grouping, scroll -->

- **Chat UX Polish Pass 2: Tool Grouping + Interactive Refs + Mermaid Layout Fix + Horizontal Scroll** (2026-06-03) — Tool-call chips grouped by ID (stop scatter per event), copyable text (Labels selectable via mouse-drag), interactive scene/script refs (syntax: `obj:/Path/To/Obj` and `script:Assets/MyScript.cs`); ChatRefResolver + ChatRefAction (click-navigate, Alt+click "Add to Context"). Mermaid layout distortion fixed: node width dynamic via MeasureNode (text lines + char width + bounds), eliminates hardcoded 120px. Chat horizontal scroll fixed (ScrollViewMode.Vertical, ScrollerVisibility.Hidden); FlowBar sweep indicator (800ms tick, visual progress). Markdown `<br/>` normalization in MarkdownInline. Input field auto-height (InputHeightCalc, height clamped min=96px max=200px via schedule); drag-drop reflow works now. New files: ChatActivityState, ChatLabel, ChatRefAction, ChatRefResolver, CopyTextBuilder, CopyableText, InputHeightCalc, JsonArrayScan. Modified Chat infrastructure: EntryKeySend rewrite (simplify), ClaudeArgBuilder adds `--disallowedTools AskUserQuestion` (prose-fallback for headless stream-json). JsonHelper gets ExtractFirstArrayObject (parse streaming tool results). NUnit tests: 17 suites / ~196 cases (render + backend + new interactivity), both Chat DLLs compile clean. Plugin version 0.4.0→0.5.0.

## [v0.4.0] — 2026-06-03 <!-- svg: extensible render: md + mermaid + img -->

- **Extensible Chat Render Subsystem** (2026-06-03) — Markdown + native Mermaid flowcharts + inline images + Enter-to-send/removable chips. Registry seam (1 file + 1 line to add new renderers). Markdown: MarkdownParser→blocks, MarkdownInline rich-text (escape `<>` first, protect code spans), MarkdownBlockRenderer + Table/List partials, ImageBlockRenderer with texture lifecycle. Mermaid: MermaidParser (graph TD/LR/RL/BT, nodes rect/round/diamond, edges with labels, chained + self-loops), MermaidLayout (Kahn topo + longest-path, no Vector2), MermaidView (absolute nodes + edge overlay), MermaidEdgePainter (Painter2D + arrowheads, 2021.3-safe). Streaming→finalize strategy: accumulate raw text, re-render on TurnDone. Enter/Alt+Enter logic pure-testable. MCPChatWindow.uss +156 lines (md-*/mermaid-*/chip-✕ classes, house palette). 62 EditMode NUnit tests (MdBlock, MarkdownParser, MarkdownInline, MermaidParser, MermaidLayout, EnterKeySend) green. Version 0.3.0→0.4.0.
- **Editor Chrome Flattened: Menu + Status-Bar Widget** (2026-06-03) — Flattened "Tools/Unity MCP" menu → top-level "MCP/" (priority 0=Chat, 1=Status, 2=Settings). New MCPStatusBarWidget: injects status pill into Editor AppStatusBar via reflection + scheduled pulses (breathing animation). Extracted MCPActions class (Restart, Kill, Reimport) — shared by status window + widget. MCPStatusModel: pure state logic (no deps), maps (isRunning, isClientConnected) → display values (Down/Listen/Up states, labels, pill text). New Tests asmdef + MCPStatusModelTests (17 NUnit tests, all scenarios covered). MCPStatusWindow refactored to use MCPStatusModel + MCPActions (DRY).

## [v0.3.0] — 2026-06-03 <!-- svg: in-Unity Agent Chat + UIToolkit status -->

- **Optional In-Unity Agent Chat Window** (2026-06-03) — New `MCPChatWindow` EditorWindow spawns the user's local `claude` CLI in headless stream-json mode; the CLI runs the existing `unity_mcp.server` as its MCP backend, reusing ~90 tools with zero new tool code. Isolated behind `UNITY_MCP_CHAT` scripting define in `UnityMCP.Editor.Chat.asmdef` (one-way reference to core via `InternalsVisibleTo` + `ChatSettingsHook` event). OFF by default; deleting `Chat/` folder leaves core untouched. Features: drag-drop object chips (with PingObject on click), screenshot attach (MultiView), Ask/Agent mode toggle, humanized tool card rendering, orphan-process cleanup on domain reload. Module: `ChatStreamParser` (stream-json→ChatEvent), `ClaudeArgBuilder` (--mcp-config generation), `ClaudeBackend` (Process lifecycle), `IChatBackend` abstraction (future plugin seams). macOS PATH resolution: spawn via `/bin/zsh -lc 'claude ...'` to inherit user shell config. JSON-only-at-boundaries principle (stdin/stdout/--mcp-config/--permission-mode; internal models plain C# structs + text). 4 NUnit suites for pure-logic testing. Plugin versions: 0.2.6→0.3.0, server 0.1.19→0.2.0.
- **Status Window UIToolkit Rewrite** (2026-06-02) — MCPStatusWindow IMGUI→UIToolkit migration with breathing heartbeat pulsation. `CreateGUI()` builds centered status orb (`.orb` solid disk + `.orb-halo` ring with USS class-driven pulsation). State polling every 700ms: ECG beat `Every(900)` when connected (green), gentle beat `Every(1500)` when listening (amber), flatline when stopped (red). USS transitions (border-*-width + opacity + background-color longhand) — no @keyframes, no transform, no box-shadow (2021.3-safe). Theme matches MCPSettings.uss (bg #1a1a2e, accent #e94560, btn #2a2a3e/#3a3a5e). New file `MCPStatus.uss` (112 lines). Extracted `MCPEditorUtils.LoadStyleSheet(filename)` helper (two-path package lookup, re-exported). `MCPSettingsUI.cs` delegates to `LoadStyleSheet("MCPSettings.uss")` (DRY; behavior identical). Buttons unchanged: Restart/Kill MCP/Reimport. Schedules auto-stop on window close.

## [v0.2.6] — 2026-06-02 <!-- svg: tool-gating fix + settings UI -->

- **Wave 3: Tool-Gating Fix + Settings UI** (2026-06-02) — APPROVED. P0+P1 shipped (2026-06-02, versions 0.1.19 + 0.2.6). P0 (Tool-Gating Fix): Fixed P1-regression where Unity form checkboxes saved zero tokens — `_filter_tools` kept any tool where `is_visible(name)` (true for all TIER1 ≈ every tool). Now: (1) Unity reports `get_disabled_tools` CSV, (2) Python `_filter_tools` applies tier/session gating THEN hides exactly that disabled set EXCEPT `FORCE_VISIBLE` escape hatches (discover_tools, get_enabled_tools, do, ask, editor, get_console, get_compile_errors, reconnect_unity, list_connections), (3) approach is "hide-disabled-set" NOT allowlist (Python-only tools absent from Unity CSV, would be wrongly hidden). `_disabled_tools_cache` refreshes on connect/reconnect; cache=None ⇒ gating-only fallback. Removed old `_enabled_tools_cache` side-channel. P1 (Python-Authoritative Catalog + UIToolkit Settings): `gating.get_catalog()` single source of truth (themed taxonomy: CORE locked, SCENE_EDIT, COMPONENTS, ANIMATION, SHADERS_MATERIAL, VFX, UI, SCREENSHOTS, UNIT_TESTS, RUNTIME, ASSETS, ADVANCED_CODE, SESSION_SKILLS, CONNECTION, META). Public tools only — plugin tools categorized dynamically Unity-side. `_push_catalog` sends catalog to Unity on connect/reconnect via `set_tool_catalog` (TCP-only, not in LLM context). Unity persists to EditorPref `UnityMCP_Catalog`. C#: `MCPSettings.cs` rewritten as UIToolkit (`CreateGUI`): foldout groups, tri-state group masters, search bar, presets (Minimal/Full/No-visuals), CORE locked, separate dynamic Plugins section (from `PluginRegistry`), animated `.uss` header. New files: `CatalogParser.cs` (deserialize JSON→dict), `MCPSettingsUI.cs` (foldout builder), `MCPSettingsCategoryGroup.cs` (tri-state logic), `MCPSettings.uss` (styling). `ExecGetDisabledTools` mirrors `ExecGetEnabledTools`, both in `IsAlwaysAllowed` + `IsAllowedDuringCompile`. EditorPref keys consolidated: `KeyPrefix` + `KeyAutoDiscard`. Tests: `pytest -m "not live"` = **1588 passed** (new test_catalog.py = 19 tests, drift-guard via `fn.__module__` public/external split). Versions: `server/pyproject.toml` 0.1.17→0.1.19, `unity-plugin/package.json` 0.2.4→0.2.6.

## Earlier history

### Wave 2: Architecture (2026-06-02)

APPROVED. Modular refactoring (6 commits, zero behavioral changes). F14: Extract `PathResolverMixin` from `middleware.py` (1104→945 lines) into new `middleware_paths.py` (168 lines); methods moved verbatim: `update_path_cache`, `validate_path`, `resolve_path`, `_get_disambig`, `resolve_path_live`, `find_from_cache`, `_levenshtein` (re-exported from middleware.py for schema_guard compat). F19 (C#): Split `PlaytestRunner.cs` (559→257 lines) via partial class; `ExecuteStep` (300-line 21-case dispatch) moved to `PlaytestRunner.Steps.cs` (313 lines). IL-identical, zero behavior risk. F19 (Python): Split `advanced.py` (351 lines, 22 unrelated tools) into 5 cohesive modules: `batch.py` (batch, references, validate_references + `_dsl_tools` set), `codegen.py` (execute_code, get_schema, auto_fix, smart_build), `skills.py` (save/use/list_skill, apply/save/list_template + `_skills_dir`), `spatial.py` (validate_layout, get_spatial_context, scan_scene, check_colliders, spatial_query), `ui.py` (create_ui, set_rect, menu, shader). `advanced.py` deleted. `server.py` re-exports all 22 names for back-compat; `plugin_api._dsl_tools` repointed to `batch`. CATEGORIES["advanced"] string-decoupled from module names. F15: Split `CommandRouter.cs` god-file into partial classes (CommandRouter.cs + CommandRouter.ObjectHandlers.cs + CommandRouter.MediaHandlers.cs). F06: Trimmed verbose TIER1 tool descriptions (screenshot, find_references, compile_preflight, semantic_at) for token savings; kept all enum values + run_playtest DSL grammar (anti-hallucination). New test `test_tool_descriptions.py` locks char budgets + required substrings. F07: `fields=` projection on get_component/inspect (already shipped). Python 1565 passed (all tests green). C# EditMode 754 passed + 2 pre-existing failures (same as Wave 1).

### Wave 1: Review Hardening (2026-06-02)

APPROVED. Adversarial code review of Wave 0 fixes found 15 confirmed issues; all resolved. F16 (error-dedup gate): fixed protocol_err gating (was whole-body substring scan firing on SUCCESS payloads), fixed dedup_error key collision (was [:80] → full message), bounded _error_dedup LRU (256 entries), gate LessonRecorder.record on same protocol_err flag (not raw_ok). F17 (path cache poison): no longer write negative-path cache on search_scene TCP raise, clear negative-path cache on any WRITE_CMDS command. F05 (DRY): hoist duplicated _read_cacheable to module-level _READ_CACHEABLE frozenset. F11 (nested batch): BatchHelper._batchDepth int counter (was bool), Physics.Sync fires once at outermost exit (--_batchDepth == 0). Python 1548 passed (+8 behavioral regression tests). C# EditMode 756 tests, 754 passed (2 pre-existing failures unrelated to fixes). New NUnit test NestedBatch_KeepsInBatch_For_OuterTail passes (gitignored unity-test-project).

### Wave 0: Performance Pass (2026-06-01)

APPROVED_WITH_MINOR. Quick wins (4h): F02 `QueuePlayerLoopUpdate()` after enqueue (500-12500ms/sess latency win), F13 float serialization `"G"`→`"G4"` (300-600 tokens), F18 MultiViewCapture reflection cache (2-8ms/call), F04 `mark_recompile_issued()` wiring (cosmetic), F08 `strip_defaults` unconditional for reads (1000-2000 tokens, _no_strip escape hatch), F12 confidence suffix gate <0.5 + staleness-gated AUTO STATE injection (1150 tokens). F03-ttl PrefetchCache TTL 0.5→12.0s (10-150ms win). Python pytest 1524 pass; C# EditMode 749/751 (2 pre-existing failures in Revert_RevertsChanges, ValueParser_Enum_NegativeInt unrelated to Wave 0). See audit: AI/performance-audit-2026-06.md. v0.1.10 (Python), v0.2.0 (C#).

### Cycle 20: 20-Architect Audit (2026-05-31)

APPROVED_WITH_MINOR. Security: CodeExecutor blocklist (Reflection.Emit, DynamicMethod, Activator, Expression). Server: lock lifecycle try/finally, fail-fast, reconnect callbacks via ConnectionSlot, global declaration fix. Middleware: CircuitBreaker HALF_OPEN probe, reset_session completeness, component cache + schema_cache guards. Intent: sanitize target, retry format, validate retry commands. Serialization: locale-invariant floats. Animation: multi-layer, Vec3 AddKeys merge, RemoveKey axis. ValueParser: null/enum/ID checks. Other: Undo registration, plugin isolation, concurrent playtest guard, ASSERT_BATCH END, compile error clearing, screenshot cleanup, cellSize clamp, autobatch parent paths, bridge exception chain, editor tool annotation. Docs: architecture.md DSL commands fix (21 actual vs 11 listed), mcp-server.md TCP keepalive/cooldown values corrected. v0.1.9.

### Post-Launch Audit (2026-05-31)

40-agent audit fixes: README rewritten, plugin docs (quickstart + API reference) polished, DRY cleanup in animation/advanced/scene tools, capability gating test added, ObjectManager/ErrorHelper/ComponentSerializer C# hardening, removed unity-test-project from public repo.

### Open-Source Migration (2026-05-30)

Created modular plugin architecture: C# (IMCPPlugin + PluginRegistry) and Python (3-source loader: pkgutil, entry_points, UNITY_MCP_PLUGIN_DIRS). Plugin API facade (plugin_api.py) provides stable exports. Generalized test fixtures (GridPlayer). 1475 Python tests passing. All plugin tools now load dynamically via external packages.

### Phase History

- Phase 0: TCP skeleton + binary framing + MCP server
- Phase 1+2: Scene reading (Hierarchy, Components) + Object CRUD + Undo/Redo
- Phase 3: Diagnostics (Console, Screenshot)
- Phase 4: TCP reconnection with exponential backoff
- Phase 5: Advanced features (get_object_detail, run_tests, MCPSettings)
- Phase 6: Scene management (new_scene, save_scene, auto-discard on quit)
- Phase 7: Animation support (get/create/edit/preview animation clips)
- Phase 8: Timeline support (get/create/edit/preview timeline assets)
- Phase 9: Scene search (search_scene with Unity-style query syntax)
- Phase 10-11: Batch commands (text-based format, execute multiple ops in one call, 80-95% token savings)
- Phase 12: Quick wins (contextual errors, hierarchy safety caps, compilation retry hint, prefab-aware, tool visibility)
- Phase 13: Reference analysis (get_references, find_references_to, remap_references + ObjectReference support)
- Phase 14: Token Optimization Sprint (tool consolidation 32→18, auto-include mutations, instructional errors v2, Python DRY helper, steering descriptions, modal state guards, editor control, tool annotations, port env var)
- Phase 15: File-Based Output (TEXT_THRESHOLD=80KB, auto-file for large text + screenshots, FileOutputHelper, Temp/MCP directory cleanup)
- Phase 16: Animator Controller + Sub-Action Flattening (consolidated animator tool with 6 actions, fixed animation/timeline edit sub-action routing)
- Phase 17: Particle System (consolidated particle tool with 4 actions, 11 modules, 10 presets)
- Phase 18: Particle System Test Coverage (8 Python + 8 C# scenario tests)
- Phase 19: Physics Test Coverage (20 Python + 17 C# tests for Rigidbody, colliders, joints, CharacterController)
- Phase 20: Shader Management (consolidated shader tool with 7 actions; ShaderSerializer + ShaderHelper + ShaderGraphHelper; 22 Python + 41 C# tests)
- Phase 21: Code Refactoring (DRY consolidation: AssetHelper + ParseFloats + conftest.py fixtures + ToolError + graceful startup; -706 lines)
- Phase 22: Live Test Verification (full verification of all 20 MCP tools across 18 scenarios; JsonHelper consolidation)
- Phase 23: Dynamic Tool Filtering (monkey-patch mcp.list_tools to query Unity's get_enabled_tools; 4-level fallback)
- Phase 24: Efficiency — Batch-First (skill file + batch description + inspect compound tool)
- Phase 25: New Features + Plugin System (compress_hierarchy, set_active, wire_event, validate_references, checkpoint, prefab instantiation, plugin system with auto-discovery)
- UnityEvent Reading + Wire Fix (ComponentSerializer now expands UnityEvent fields, wire_event validation fixed)
- Phase 26: Asset Pipeline & Project Tools (5 new tools: asset, project_settings, material, prefab, scriptable_object)
- Phase 27: Architecture Stabilization (ValueParser DRY extraction, CommandRegistry pattern, JsonHelper resilience, dead code cleanup)
- Phase 28: Multi-Unity Connection (BridgeManager, 6 new MCP tools: connect/disconnect/switch/list/transfer/copy)
- Phase 29: Architecture Cleanup (CommandRouter RegisterAll(), server.py split into 6 tool modules, _resolve_name fix)
- Phase 30: PID Lockfile for Zombie Prevention (lockfile.py with fcntl.flock + signal-based process cleanup)
- Phase 31a: Runtime Play Mode Control (RuntimeHelper, invoke_method, set_runtime_property, wait_until)
- Phase 31b: Optimization Sprint (BatchHelper per-command guards, query_state + test_step + move_to tools, validate_layout)
- Phase 31c: PlaytestRunner DSL (9 DSL commands, PlaytestConfig, PlaytestParser, PlaytestRunner, run_playtest MCP tool)
- Phase 32: Stability & Token Optimization (set_property read-back, hierarchy summary mode, InputNormalizer)
- Phase 31d: Runtime Validation DSL (16 new DSL commands, PlaytestState, IPlaytestSimulator, adaptive reports)
- Phase 33: Killer Features (Scene Refs, Capability Gating, MCP Resources, execute_code Roslyn, multi-view screenshot, visual regression, spatial queries, skill library, middleware 12 features, session save/load)
- Phase 34: SamplingService + Token/Perf Polish (gating ON by default, SamplingService for visual verification, 12 production fixes)
- Phase 35: Telemetry/Metrics System (MetricsRegistry, counters/observations/cost tracking, get_metrics TIER1 tool)
- Tier 2b: Cost Budget + Adaptive Routing (persistent daily budget, 13 features registry, 4-tier adaptive gate)
- Tier 2c: Set-of-Mark Visual Annotation (SoM layer for VLM grounding, Pillow overlays, hash-stable indices)
- Tier 2d: Asymmetric Reflection (server-side self-verification, registry pattern, 3 rule modules)
- Tier 2e: Graceful Degradation (unified fallback ladder, degrade.py, 3 production callers)
- Tier 2f: Discoverability/ToolHinter (6 hardcoded patterns, sliding deque history, adoption tracking)
- Tier 2g: Live Integration Tests (opt-in real-Unity suite, session-scoped PlayMode, GridTest scene)
- Cycle 6a: Recompile Resilience (CompileStateProbe, bridge retry contract, exponential backoff)
- Cycle 6b: Type Conversion Bundle (AnimatorController == parsing, ValueParser enum int fallback, InputNormalizer Python)
- Cycle 6c: Path/UX Bundle (search_scene empty-result context, delete_object accepts path, Physics.SyncTransforms)
- Cycle 6d: Component Edge Cases (empty serialize sentinel, duplicate add prevention, explicit response format)
- Cycle 6e: Audit & Cleanup (audit-only, marked 3 problems RESOLVED + 1 N/A)
- Cycle 7a: Resilience Bundle (sticky retry-cache fix, 3-tier timeout, ECONNREFUSED fast-fail, reconnect callbacks)
- Cycle 7b: TCP/OS Hardening (per-socket TCP options, lockfile PID-recycle defense, BridgeManager.close_all bounded)
- Test Cycle 1: Hygiene + Perf (62s → 10.14s, 6x speedup, 3 autouse fixtures)
- Test Cycle 2: DRY + Coverage (bridge_response factory, conftest.py dedup, hinter split)
- Test Cycle 3: Independent Live Tests + Resettable Collectibles (GridPlayer state reset, 52 live tests)
- Test Cycle 3a: Prophylactic Invariants (determinism, subprocess lifecycle, lifespan cleanup)
- Test Cycle 4a: Visual Pipeline Real Bugs (8 P0 production bugs found and fixed)
- Cycle 4b: Visual Pipeline Foundation (Haiku output normalizer, SoM index stability)
- Cycle 4c: visual_diff Polish + Critical Journeys (DIFF_PROMPTS golden test, pixel_threshold boundary)
- Cycle 4d: Budget Concurrency Hardening (asyncio.Lock, per-PID tmp, fcntl serialization)
- Cycle 4e: Live Tests Rewrite (Vacuous → Fixture-Based)
- Cycle 5a: Heuristic Performance (PrefetchCache, HierarchyDiff, Disambiguator, CoalescingBuffer)
- Cycle 5b: Response Distiller + Preimage Cache (ResponseDistiller, PrefetchCache.put_synthetic, _recent_focus)
- Cycle 5c: Roslyn Foundation (find_references, compile_preflight, semantic_at — 3 new TIER1 tools)
- Cycle 5d: Wire Dead Modules (Disambiguator + Distiller Haiku wiring)
- Cycle 10: Multi-View Anti-Hallucination (visibility manifest + colored bounding-box overlays)
- Cycle 11: Stability Protocol — State File + Adaptive Circuit Breaker
- Cycle 12: MCP Stability Fixes — Reconnect Success Rate 88%
- Cycle 13: TCP Client Race + Shutdown Guard (per-client CancellationToken, atomic SendAsync)
- Cycle 13 Phase B: Crash Detection & PID Liveness
- Cycle 13 Simplification: TCP Layer Revert (going_away ordering, keepalive reverted)
- Cycle 14: Multi-Process Stability — Exclusive Lockfile + Heartbeat Probe
- Cycle 15: Reconnect Regression Hardening (auto-reconnect 88% → 98%+)
- Cycle 16: Reference Fixes + Type Support + unwire_event + PlayMode Test Persistence
- Cycle 16b: Domain Reload TCP Self-Healing (bind retry, watchdog, state file)
- TCP Connection Lifecycle Hardening (CLOSE_WAIT fix, reconnect race fix)
- feat: set_parent tool (fixes duplication bug)
