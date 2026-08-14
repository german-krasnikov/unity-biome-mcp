# Feature: Architecture Overview

<!-- Overview doc — max 500 lines, single entry to all AI/ files -->

## Overview

MCP server for controlling Unity Editor from MCP-compatible AI clients with
compact, tool-oriented responses.

## Installation & Distribution

**Golden-path install flow (v0.68.0+):**

1. **Python Server**: Runs on-demand via `uvx --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp` (GitHub-direct git+URL install, GIT_INSTALL_URL constant in resolver.py + C# WizardConfigWriter.cs)
2. **Unity Plugin**: Add via Package Manager → **Add package from git URL** → `https://github.com/german-krasnikov/unity-biome-mcp.git?path=unity-plugin` (UPM git URL, only 1 step needed)
3. **Auto-Config** (`ProjectConfigWriter.cs`): On Editor startup (SessionState-gated, per-session), discovers the resolved port and installed package version, then writes project-local configurations for Claude Code, Cursor, VS Code, Windsurf, Codex, and Junie. Configs are written to client-specific project paths such as `<project>/.mcp.json` and `<project>/.codex/config.toml`.
4. **CLI Dispatcher** (v0.68.0, cli.py + _preflight.py): `unity-biome-mcp configure/doctor/version/uninstall` subcommands via uvx. Preflight guard turns crashes before handshake into one-line stderr (no traceback).
   - **Screen 1 (Welcome)**: Product introduction
   - **Screen 2 (Pick Backend)**: 10 backend cards with available binary/config detection
   - **Screen 3 (Configure)**: Auto-generated project config, Chat-auto setup, manual instructions, or client-specific configuration
   - **Screen 4 (Install AI Skills)**: Optional bundled skill and agent installation
5. **10 Wizard Backends**: Claude Code, Claude Desktop, Cursor, Windsurf, VS Code, Codex, Kimi, OpenCode, Antigravity, and Rider AI Assistant.
6. **Config CLI**: `python install.py configure --tool <client-key>` uses the current `CLIENT_REGISTRY`; run `python install.py configure --help` for the accepted keys. `--project-dir` selects project-local output where that client supports it.
7. **Doctor Tool**: `python install.py doctor` diagnostic checks (Python, imports, TCP connectivity, config validity). **v0.47.1**: validates git+URL presence in configs, warns on stale PyPI entries, checks uvx + git in PATH
8. **Version Sync**: `server/pyproject.toml` is canonical.
   `scripts/sync_versions.py X.Y.Z` bumps it and all generated copies, including
   the local package entry in `server/uv.lock`;
   `scripts/sync_versions.py --sync` repairs copies from the canonical value,
   and `--check` is the no-write drift gate.
9. **GitHub-Direct Install** (v0.47.1): DRY consolidation — `GIT_INSTALL_URL` constant shared between Python resolver.py and C# WizardConfigWriter.cs, consumed by all backends for consistent versioning. Update banner includes `--reinstall` flag for recovery

**Architecture changes:**
- **install.py** (`install/` module): Multi-command CLI (setup, update, doctor, configure, uninstall) with lazy config module imports. `--project-dir` flag for scope toggle (v0.42.0). **doctor warns about stale Codex entries (v0.44.0)**. **v0.45.0**: Added `connect` (link projects via file: in manifest.json), `disconnect` (restore registry source), `pull` (git pull --tags for file: installs). **v0.47.1**: doctor validates git+URL presence in configs, warns on stale PyPI entries, checks uvx/git in PATH. **v0.96.1**: `setup` and `update` subcommands auto-generate `.mcp.json` pointing at the local venv (`_write_mcp_json()` calls `resolve_server_cmd()` + `merge_mcp_config()`).
- **Config system** (`server/src/unity_mcp/config/`): CLIENT_REGISTRY (Claude Code/Desktop/Cursor/Windsurf), config path detection, MCP JSON merger, backup/restore. **Codex TOML merger (v0.42.0)**: `merge_toml_mcp()` support. **Stale entry cleanup (v0.44.0)**: strips `[mcp_servers.unity]` on first write, creates .bak backup (first-write-wins). **v0.47.1**: `resolver.GIT_INSTALL_URL` as single source of truth, validator.py skips json.loads for TOML clients (Codex), respects per-client root_key (mcpServers vs mcp_servers). **v0.96.1 resolver changes**: `find_python()` is now venv-first — checks `server/.venv/bin/python` (or `Scripts/python.exe` on Windows) before falling back to uvx or sys.executable. `find_server_command()` priority: venv → uvx → sys.executable. `uvx` commands now include `--quiet` flag to suppress stderr in MCP hosts. `find_port()` uses `iter_port_files()` from `paths.py` for legacy `~/.unity-mcp/ports/` fallback.
- **Update checker & LevelUp UX** (v0.42.0+v0.44.0): GitHub API polling (v0.47.1: switched from PyPI to GitHub releases API via api.github.com/repos/.../releases/latest) + UpdatesPage changelog viewer. **LevelUp arcade-style animation (v0.44.0)**: 4-state panel (Idle→Animating→Done→Diff), XP bar + sparkles via LevelUpAnimator, release notes diff via ReleaseDiff. **v0.45.0**: InstallSourceDetector (file: vs git: detection via PackageInfo.source), LocalPluginUpdater (git pull --tags async), UpmPluginUpdater (Client.Add chain), UpdateDispatcher (DRY routing), ChatMcpConfigWriter uvx fallback. **v0.47.1**: `_update_check.py` uses GitHub releases API with importlib.metadata for version read, 24h cache TTL, banner includes --reinstall flag. **v0.50.0+**: UpdateChecker validates git+URL in configs, ClearCache on Level Up callback chain (v0.50.1). **v0.50.2**: WizardConfigWriter GitInstallUrl made public for cross-assembly access. **v0.53.0 Chat config lifecycle**: Per-port temp configs (unity-biome-mcp-config-{port}.json) written by ChatMcpConfigWriter.GetOrCreateConfigPath(). Python lifespan cleanup: `cleanup_stale_port_files()` deletes configs >2h old. C# OnDisable: DeleteOwnConfig() removes MCPServer.ServerChatPort config on shutdown (ChatMcpConfigWriter.DeleteConfig). ConfigFileName(port) → port>0 ? "{port}.json" : legacy bare name (backward compat).
- **ClientSkills**: `unity-plugin/ClientSkills/` ships 11 folder skills, 4
  focused agents, supporting resources, and `claude_to_codex.py` with the UPM
  package. The **Install AI Skills** screen installs canonical Claude artifacts
  and can run Codex sync. `SkillsInstaller.cs` preflights conflicts and legacy
  fingerprints, stages writes, rolls back failed transactions, and writes the
  version marker last. The converter owns generated Codex files through a
  SHA-256 manifest and fails closed on unowned targets, unsafe paths, or
  malformed ownership data. `SkillsInstallerTests.cs` contains 26 focused
  installer tests.
- **Plugin side** (C#, v0.68.0: ProjectConfigWriter, v0.47.1-v0.67.x: SetupWizard). **Auto-Config**: `ProjectConfigWriter` [InitializeOnLoad] → SessionState gated (once per session) → Run(projectRoot, port, version) → auto-writes per-project configs for each target in `ProjectConfigTargets.All` (Claude Code, Cursor, VS Code, Windsurf, Codex, Junie). Config formats abstracted via `ProjectConfigFormats` (JSON, TOML, custom). TOML writing via `ProjectConfigToml.cs`. Gitignore patching via `GitignorePatcher.cs` (append project config paths, idempotent). **v0.44.0-v0.67.x legacy**: SetupWizard 3-screen flow, BackendDescriptor with 9 backends + IsDetected, PickBackendScreen + ConfigureScreen, scope toggle, Wizard asmdef split. **Config recovery (v0.44.0)**: WizardConfigWriter.HasBackup + RestoreConfig, AiConfigScreen Restore button. **v0.45.0**: Async local plugin updates (LocalPluginUpdater.UpdateAsync), UPM registry updates (UpmPluginUpdater.UpdateAsync). **v0.47.1**: `WizardConfigWriter.GitInstallUrl` constant (shared with Python resolver.py), AiConfigScreen with fallback copyable JSON on UPM installs, `AiToolCardFactory` platform-aware path methods for Windows (ConfigDir detection, .as_posix() for TOML paths, BackendDescriptor platform-specific root_key). **v1.0.1: Port baking removed** — `WizardConfigWriter.Entry()` no longer emits `UNITY_MCP_PORT` env block in permanent AI tool configs; Python uses `~/.unity-biome-mcp/ports/{pid}.port` discovery instead (updated on every bind including fallbacks). Mirrored in `mcp_config_writer.py` — `UNITY_MCP_PORT` only written when `mcp_port != 0`. Prevents connection failures after Windows port drift and multi-project desync.

## Architecture (for Architect)

```
Claude Code ←──stdio──→ Python MCP Server ←──TCP:PORT[+CHAT]──→ Unity Editor Plugin
     │                        │                                  │
     │  MCP Protocol          │  Binary protocol                │  Unity API
     │  (JSON-RPC 2.0)        │  [4B len BE][JSON]              │  (main thread)
     │                        │                                  │
     │                        ├─ ConnectionSlot (single)          ├─ CommandRouter (async, v0.57.0: switch dispatch)
     │                        ├─ Capability Gating (TIER1+cat)   ├─ CommandRegistry (sync+async handlers, v0.69.0: CommandOptions)
     │                        ├─ Plugin system (auto-discovery)  ├─ MCPServer.StartAsync (registration before bind, v0.73.1)
     │                        │  - opt-in disable: env UNITY_MCP_SKIP_PLUGINS=prefix ├─ PluginRegistry (IMCPPlugin)
     │                        ├─ Deferred Schema Loading         ├─ CommandValidator (validation)
     │                        │  (stub schemas + lazy resolve)   ├─ ValueParser
     │                        ├─ 23-layer Middleware (opt-in)    ├─ 7 Serializers
     │                        ├─ CompileStateProbe               ├─ RefManager (& base62, v1.31.0)
     │                        ├─ PID presence lock (per session) ├─ PlaytestRunner + DSL
     │                        ├─ Port discovery (CWD-based)      ├─ RuntimeHelper (Play Mode)
     │                        ├─ Config module (client detection) ├─ MultiViewCapture (4-panel)
     │                        ├─ Update checker (GitHub API, v0.47.1) ├─ CodeExecutor (Roslyn)
     │                        ├─ Config TOML merger (v0.42.0)    ├─ PortResolver (dual-port)
     │                        ├─ GIT_INSTALL_URL constant        ├─ SetupWizard (4 screens, 10 backends)
     │                        └─ Heartbeat (15s, reconnect)      ├─ UpdatesPage (changelog viewer)
     │                                                           ├─ AiToolCardFactory (platform paths)
     │                                                           ├─ Guards (compile/play/runtime/tool)
     │                                                           └─ Python resolver (venv/uv/system)
```

### Why This Architecture

- **Python MCP**: Claude Code launches via stdio, mature SDK
- **TCP socket**: Explicit `going_away` signaling and bridge reconnection recover across Unity domain reloads
- **Binary framing**: 4-byte BE length prefix + JSON, minimal overhead
- **Bounded caching**: Eligible reads may use the middleware's short-lived,
  invalidated prefetch cache; uncached calls go through `bridge.send`

### Components

1. **MCP Server** (Python: 90+ modules total, including `server.py`, 23+ tools modules + support, v0.42.0: +25 config/TOML tests, v0.47.1: +73+78 config validation tests, ROI sprint v0.69.0: +4 files for metadata DRY, review sprint v0.70.0: +4 tool files split + bridge_retry + bridge_result, tools gap sprint v0.77.0: +8 domain test files, v0.78.8: +test_bridge_reload_gate + test_bridge_role + test_middleware_alias_lifecycle, v0.78.9: +alias_status tool + compress param + validate_aliases + _warm_alias_cache — 4300+ Python unit tests green, v0.78.10: +test_connection_status + readonly-batch blast-radius bypass + alias_status tier1 + semantic connection status, v0.78.11: +test_middleware_read_cmds (59 READ_CMDS tests) + test_tool_schema_coverage FastMCP contract tests — 4380+ Python unit tests green, v0.79.1: -scenarios.py -scene_session.py -fuzzer.py, +test_playtest_path.py, run_playtest path= param — 4375 Python unit tests green, v0.80.0: SOLID/DRY/KISS/OCP/SRP sprint — middleware_hooks.py POST_HOOKS registry, editor_log.py split into _freshness + _wedge, bridge_socket.py frame helpers, GetCapabilities mutating_cmds + runtime_cmds, −669 LOC net, v0.80.1: _DiagnoseFields.is_really_compiling + WEDGE-ENGINE stale-latch detection, reload_ladder T5 → force_play_stop, _orphan_guard autouse live fixture; playtests ROI sprint: +transaction.py (scene_change_plan+apply_scene_change), +verify.py (verify_after_change 5-gate pipeline), +console_mark/get_console_since watermarks, +run_tests_wait blocking poller, +run_playtest_file/run_playtest_suite multi-runner — 17 new ToolSpec entries, 11 new Python test files; v0.83.0: ToolSpec v2 mutability+runtime_only fields, 18→8 categories with alias layer, CORE 24→15, is_write(cmd,args) action-aware classification, diagnose FAILED:→FAIL:, AUTO STATE gated on writes only — 4544+ Python unit tests green; v0.84.0: CORE 15→11 (delete_object/set_parent/scene/search_scene demoted to TIER1 SCENE), TIER1 total now 46, release_smoke new TIER1 tool, get_perf deprecated→get_frame_stats, BREAKING param renames create_ui/set_rect/object_diff, run_playtest_suite auto_play=False, lint severity WARN→ERROR, delete_object blast_radius 3→4, _no_distill auto-set on fields=, REFLECT excluded from distillation — 4571+ Python unit tests green; v0.85.1: -get_perf removed (use get_frame_stats), -run_playtest_file removed (use run_playtest path=), batch blast_radius now dynamic (scans inner cmds), resolve_scene_refs mutability fixed to 'read', RenderAnalyzer non-readable mesh guard, PlaytestLinter no-evidence=ERROR, +test_registration_parity.py (3 tests, zero-drift guard), SceneTestBase Undo.ClearAll() TearDown fix — ~4572+ Python unit tests green; v0.86.0: test quality review — 83 Python tests deleted (vacuous/self-testing/duplicate), -test_schema_cache.py (17 tests), ~45 assertions hardened (exact values over truthy), _cleanup_orphans + _make_scenes retry+logging in live conftest — 4630 Python unit tests green; v0.87.0: release stabilization — 5 P0 + 12 P1 fixes: `do` demoted CORE→SYSTEM (CORE 11→10), `direct_only: bool` field on ToolSpec (21 tools marked), batch pre-flight rejects direct_only tools with line remapping in on_error=continue, get_compile_errors fetches compile_status for corroborate(), diagnose stale-dll gate requires compile != "idle", C# fixes: UIHelper SetTMPText try/catch TargetInvocationException, TimelineHelper Undo.RecordObject+SetDirty, ObjectManager.Events m_/PascalCase normalization for wire_event+unwire_event, AnimationHelper clipName null guard, AnimatorControllerHelper states null guard, MaterialHelper returns "ok: prop=value", SceneHealthAnalyzer ItemCap=20 on 3 checks, GameStateHelper error truncated 200 chars, CommandRouter._hiddenFromCatalog filters internal commands, screenshot path= param honored with traversal validation — 4630 Python unit tests green; v0.90.0: +test_bridge_retry.py (RetryPolicy), +test_console.py (watermark), +test_objects.py (find_type), +181 lines in test_runtime.py — 4681 Python unit tests green; v0.91.0: real-project audit fixes — +test_surface_parity.py (surface parity: direct_only excluded from TCP catalog), +test_tools_verify.py (verify.py ratio extraction), +test_tool_schema_coverage.py additions; DEPRECATED category added: get_perf+run_playtest_file stubs raise ToolError with migration hint, excluded from _ALL_KNOWN + catalog; 7 more tools marked direct_only (console_mark, discover_tools, get_console_since, mcp_status, release_smoke, resolve_tool_schema, run_tests_wait); _SCHEMA_KEEP_FULL_EXTRA: run_playtest/run_tests/run_tests_wait/resolve_tool_schema keep full schemas — 4703 Python unit tests green; v0.92.0: API pragmatic review — +test_result_envelope.py (isSuccess predicates: move_to+ask_user), +test_compile_workflow.py (STALE-DOMAIN+MANUAL-REQUIRED gates); move_to+ask_user gain isSuccess predicates; discover_tools include_legacy=False default + structured=True mode; screenshot output_path alias; sync_unity added to _SCHEMA_KEEP_FULL_EXTRA; serialized_field_rename_audit new VERIFY tool; ~5088 Python unit tests green)
   - **Tool Metadata DRY (v0.69.0)**: `tools/tool_specs.py` single source of truth — 154 ToolSpec entries (148 user-visible + 6 _INTERNAL) with category, core, tier1, timeout_s, mutability, runtime_only, direct_only. (v0.77.0: `shader` added to BATCHABLE set; **v0.83.0: ToolSpec v2** — added `mutability: Literal['read','write']` and `runtime_only: bool` fields; WRITE_CMDS/READ_CMDS/_RUNTIME_ONLY_CMDS now derived from _SPECS at import, −67 LOC hardcoded sets; `is_write(cmd, args)` in `middleware_types.py` for per-call action-aware classification via `ACTION_READS` dict mapping 12 action-based tools to their read actions; **v0.84.0: CORE 15→11** — `delete_object`, `set_parent`, `scene`, `search_scene` demoted to TIER1 SCENE; TIER1 total 46; 15 old uppercase aliases (`SCENE_EDIT`, `ANIMATION`, etc.) emit `DeprecationWarning`; new `release_smoke` tool; `get_perf` deprecated; BREAKING: `create_ui`/`set_rect` (`fontSize`→`font_size`, `offsetMin`→`offset_min`, `offsetMax`→`offset_max`), `object_diff` (`pathA`→`path_a`, `pathB`→`path_b`); **v0.87.0: CORE 11→10** — `do` demoted from CORE to SYSTEM (direct_only=True); added `direct_only: bool = False` field — 21 tools marked direct_only, batch pre-flight rejects them with friendly message + line remapping in on_error=continue mode; **v0.91.0**: 7 more tools marked direct_only (console_mark, discover_tools, get_console_since, mcp_status, release_smoke, resolve_tool_schema, run_tests_wait) — ~28 direct_only total; DEPRECATED category added for removed tools (get_perf, run_playtest_file) with ToolError migration stubs, excluded from _ALL_KNOWN and catalog). Eliminates drift across 4 collections: gating._CORE_TOOLS, gating._THEMED_CATEGORIES, gating.TIER1, gating._ALL_KNOWN (now derived at import time). `timeout_categories.py` generates TIMEOUT_CATEGORIES dict + get_timeout(cmd) from _SPECS at import. `tools/_common.py` provides bind(module_globals, send, args) helper — uniform binding across all 23 tools/*.py register(mcp, send, args) functions.
   - **Scene Tools Split (review sprint v0.70.0, B2)**: `tools/scene.py` SPLIT into 4 focused modules:
     * **console.py**: get_console (keyword + count_only filters), clear_console
     * **screenshot.py**: screenshot (camera modes, annotated frames, Haiku describe)
     * **testing.py**: recompile (force reimport), await_compile (polling), compile health checks
     * **editor_control.py**: editor (state/play/stop/pause/select/project_path)
     * **scene.py** (residual): get_hierarchy, set_parent, search_scene, get_spatial_context
     * **Rationale**: Reduces scene.py to <150 LOC (was 300+); each module serves distinct domain (console I/O vs. visual capture vs. build control vs. hierarchy). All 5 modules follow unified `register()` pattern via `bind()`.
   - **Meta Tools Refactored (v0.69.0, v0.78.9: +alias_status)**: `tools/meta.py` extracts discover_tools, doctor, resolve_tool_schema, set_llm_config from server.py composition root into standard register() pattern — same signature as other tools modules, uses bind(). **v0.78.9**: `alias_status()` MCP tool added — sends `alias_status` command (no args) to C# and returns alias table health: loaded/empty/stale state, count, and source. Annotated read-only (`RO`). Improves composability and testability.
   - **RetryPolicy Extraction (review sprint v0.70.0, C8)**: NEW `bridge_retry.py` module extracts retry decision logic from UnityBridge.should_retry() into unified **RetryPolicy** class. Consolidates exception-path retries + hint-path retries (Unity 'retry' JSON sentinel) behind single `is_retry_safe` gate, preventing bypass bugs.
   - **BridgeResult Unwrapping (review sprint v0.70.0, C1)**: NEW `bridge_result.py` with `unwrap_bridge_result(dict) → (ok, data, err, file)` pure function. Eliminates inline unpacking & error handling scattered across 50+ call sites.
   - **148 MCP tools registered** (v0.70.0: scene split → +1 deduplicated register call, net +0 tools; tool count 124→121 via watch consolidation B4; v0.79.1: -5 tools: run_scenario/save_scenario/load_scenario/list_scenarios + fuzz_playtest removed with scenarios.py + fuzzer.py; playtests ROI sprint: +17 tools; pipeline-gap sprint: +3 tools: bake, build, package). Gating: TIER1 + themed categories (derived from tool_specs._SPECS). External plugins can add more tools dynamically. `_UnstructuredMCP(FastMCP)` subclass forces `structured_output=False` on all tools. Ungated (always visible): `get_test_results`, `budget_status`, `diagnose`.
   - **Config Module (v0.42.0+v0.44.0, v0.71.0: shared SERVER_NAME constant)**: `server/src/unity_mcp/config/` extended with TOML merger for Codex backend. `merge_toml_mcp(path, section)` merges MCP config into TOML with diff-based updates (preserves user settings). Python 3.9 compat: `Optional[X]` instead of `X | None`. ValueError raised on corrupt JSON. **Stale entry cleanup (v0.44.0)**: strips `[mcp_servers.unity]` duplicates on first write, creates .bak backup. **v0.47.1**: `validator.py` skips json.loads for TOML clients, checks string presence in configs. Adds 25 new tests (v0.42.0) + 9 new tests (v0.44.0) + 151 new tests (v0.47.1: 73 Python + 78 C# in test_config_gaps.py). **v0.71.0: Shared SERVER_NAME Constant (config/merger.py)**: `SERVER_NAME = "unity-biome-mcp"`. `_OLD_NAMES = ("unity-mcp",)` migration tuple strips stale keys on every write (orphaned duplicates cannot persist). All config paths now use `SERVER_NAME` key (JSON `mcpServers[unity-biome-mcp]`, TOML `[mcp_servers.unity-biome-mcp]`). MCP_BLANKET derived in backend_def.py as `f"mcp__{SERVER_NAME}"` = `"mcp__unity-biome-mcp"`
   - **CodeExecutor.SecurityScan (v0.31.0, v0.89.0: SecurityLevel)**: Hardened pipeline — (1) strip C# comments via regex (2) whitespace densification (3) OrdinalIgnoreCase matching (4) 11 new blocked patterns (EditorApplication.Exit, Application.Quit, Environment.FailFast, ExportPackage, ImportPackage, OpenProject, ProjectWindowUtil, using-aliases for System.IO/Diagnostics/Net/Reflection). **v0.89.0**: `SecurityLevel` enum (`Standard`/`AllowAll`/`Strict`) — three pre-computed pattern sets; `AllowAll` skips ALL security scans (no pattern matching, no regex — all C# APIs available); `Standard` blocks unsafe namespaces + `.GetValue(/.SetValue(/.Invoke(`; `Strict` additionally blocks `GetField(/GetProperty(/GetFields(/GetProperties(`; `AllowAll` is the default. Security error messages include actionable `Suggestion:` hints. `using Object = UnityEngine.Object` added to auto-usings. User `using` directives hoisted above wrapper. **v1.4.0**: `ReplaceTopLevelReturns` uses brace-depth-aware regex to rewrite bare `return;` statements only at top level (not inside local functions). Using-hoisting regex accepts lowercase namespace names and underscores (e.g., `using system.text;`, `using _helpers;`). `return;` auto-replaced with `return null;` only for bare top-level returns.
   - **Quality Pipeline Infrastructure (v1.17.0+)**: Automated validation and metrics tracking for tool schemas and AI skills. Three scripts:
     * **export_tools.py** — exports 148 non-_INTERNAL MCP tool definitions to JSON (toolsmith or mcplint format, both still supported) for schema linting. No external deps; runs on repo root. **v1.22.0**: `--format toolsmith` output now includes `title` and `annotations` fields from tool metadata.
     * **quality_delta.py** — parses mcp-toolsmith (errors/warnings/avg_score) reports only. **v1.22.0**: Removed `mcp-lint` tool support; now single-linter pipeline (mcp-tool-card-linter only).
     * **check_skills_freshness.py** — 7 static checks on `.claude/skills/` and `.claude/agents/` frontmatter: version parity, stale tool refs, model usage, field consistency, ownership tags. Runs with `--strict` flag in CI/CD; emits ERROR or WARNING.
     * **CI Integration** — `ci-python.yml` job `tool-quality` runs export_tools + linters → quality_delta → uploads metrics. `release-preflight.yml` enforces quality gate (exit nonzero on critical issues). **v1.19.0**: CodeCov integration with per-ecosystem flags (Python: `server/coverage.xml` → Codecov `flag=python`; C#: OpenCover XML from Linux EditMode → Codecov `flag=csharp`). SonarCloud C# quality scan (`ci-csharp-quality.yml`) performs source-only analysis on Editor/Reload assemblies (configured via `sonar-project.properties`). OpenSSF Scorecard (`scorecard.yml`) publishes security posture to api.securityscorecards.dev with SARIF upload to GitHub Security tab.
   - **In-Unity Chat System (v0.66.6+)**: Unified RelayBackend on C# side communicates with Python chat_relay.py sidecar. Five backends are managed server-side (Claude, Codex, Kimi, Agy, OpenCode) via `backend_def.py` with CLI argument builders and stream transforms. C# dispatches semantic commands; the relay owns CLI protocols, binary resolution, model selection, and event normalization. See the Chat Relay System section below.
   - `_UnstructuredMCP(FastMCP)` subclass: overrides `add_tool()` to force `structured_output=False` on all registered tools, eliminating duplicate `content` + `structuredContent` in responses + `outputSchema` from ListTools (v0.50.3). Reduces MCP response size & Claude parsing overhead. **FastMCP Contract Tests (v0.78.11, `test_tool_schema_coverage.py`)**: 7 new tests verify the JSON Schema FastMCP actually emits to MCP clients (`mcp._tool_manager._tools[name].parameters["properties"]`). Guards against FastMCP silently dropping params (e.g., `compress` from `get_component`/`inspect`, `validate_aliases` from `batch`). Also verifies `alias_status` registration and that all core tools with params have non-empty properties.
   - **Lifespan** (v0.36.0+, T6: idle watchdog + dormant): auto-discover Unity port from `~/.unity-biome-mcp/ports/*.port`, acquire exclusive PID lockfile, create ConnectionSlot, connect bridge, fetch disabled tools cache (`get_disabled_tools`), push Python-authoritative catalog (`_push_catalog`), start heartbeat, register reconnect callbacks, load_plugins(), start idle watchdog thread. **Idle Watchdog (T6)**: Background thread monitors two idle timers: (1) `_last_useful_activity` — timestamp of tool dispatch/response (reset on every send/receive); (2) `_transport_activity` — timestamp of heartbeat ping (touched by bridge._on_transport_activity callback). Three paths: (A) subagent timeout — exits if idle > subagent_timeout (user-configured); (B) orphan timeout + parent alive — if idle > timeout and parent PID unchanged, schedules `_schedule_dormant()` (CONNECTED→DORMANT) via asyncio.run_coroutine_threadsafe(), then stays alive as orphan-reaper; (C) orphan timeout + parent dead — exits if parent PID changed (parent died). Guard: in_flight_count > 0 prevents counting idle time during active tool execution. Dormant scheduling uses TOCTOU guard (rechecks idle time under asyncio lock before state transition). Dormant mode is low-power: TCP closed, heartbeat stopped, process waits for next user request to wake.
   - **MCP SDK Version (v0.31.0, v0.50.3)**: Pinned `mcp>=1.28.0,<2` — v2.0 ships 2026-07-28 with breaking changes (e.g., `response.content` structure). Upper bound prevents silent breakage. v0.50.3: bumped to 1.28.0+ for structured_output support.
   - Plugin system (3-source discovery: pkgutil built-in, entry_points, UNITY_MCP_PLUGIN_DIRS env): each plugin has `register(mcp, send_fn, args_fn)`. UNITY_MCP_SKIP_PLUGINS env (comma-separated prefixes) skips matching plugins.
   - _send() helper: sends to bridge via slot, raises ToolError on !ok
   - File-based output: checks `file` field in response → returns path string
   - Tool annotations: readOnlyHint, destructiveHint for MCP compliance
   - Dynamic tool filtering: patches `mcp._mcp_server.request_handlers[ListToolsRequest]` with gating + disabled-set subtraction (hide-disabled-set model, not allowlist)

2. **TCP Bridge** (Python: `bridge.py` + `bridge_heartbeat.py` + `bridge_reload_state.py` + `bridge_retry.py` + `bridge_result.py` + `bridge_socket.py` + `connection_slot.py` + `lockfile.py` + `compile_state.py` + `server_filtering.py`)
   - **ConnectionSlot**: single per-project connection with project-based discovery
   - **Port Discovery** (`server_filtering.py:read_unity_port`): `UNITY_MCP_PORT` takes precedence. Otherwise, live `~/.unity-biome-mcp/ports/*.port` candidates are ranked by project context (`UNITY_MCP_PROJECT_DIR`, `CLAUDE_PROJECT_DIR`, or current working directory) and then modification time; the non-probing cold-start fallback is `9500`. Legacy `~/.unity-mcp/ports/` files remain a compatibility fallback.
   - **Port Persistence (v0.35.0)** — PortResolver discovery chain: env UNITY_MCP_PORT → ProjectSettings/MCPSettings.json (user intent, survives Library purge) → Library/MCP_Port.json (cache) → FindFreePort. MCPServer.cs calls SaveProjectSettings() to persist both main + chat port assignments at startup. `MCPServer.WritePortFile()` writes `{pid}.port`. DeletePortFile() cleans it. Backward compatible: nil ProjectSettings falls through to Library cache. **v1.0.1: SaveRuntimePorts** — new PortFileManager method: updates `MCP_Port.json` + `{pid}.port` but NOT `MCPSettings.json`. Called by MCPServer when bind falls back to an alternate port — preserves user intent so next reload retries the configured port, no cascade drift (9514→9516→9518). **CleanStalePeerPortFiles** — called at startup, removes `.port` files from dead PIDs before writing our own.
   - **TCP Frame Helpers (`bridge_socket.py`, v0.80.0 M67)**: `frame_write(writer, payload)`, `frame_read(reader)`, `frame_read_with_timeout(reader, timeout)` — 4-byte BE length-prefix framing extracted to single module. Used by bridge, heartbeat, chat_relay, reload_ladder, and doctor. Eliminates inline struct.pack/unpack at every call site.
   - **UnityBridge (v0.36.0)**: AsyncIO TCP client, 4-byte BE length prefix JSON
     * **BridgeState enum**: DISCONNECTED | CONNECTED | DOMAIN_RELOADING | FAILED (startup grace expired) | DORMANT (intentional TCP close, heartbeat stopped) | WAKING (reconnect triggered by incoming request)
     * **DomainReloadTracker** (`bridge_reload_state.py`, v0.36.0): Tracks domain reload state independently from compile probe (30s expiry). Shared between bridge.send() and heartbeat via `_reload` instance. Three methods: `mark()` (on DomainReloadError), `clear()` (on success), `is_active()` (checks expiry). Decouples reload window from compile heuristics.
     * **should_retry()** (`bridge.py`, v0.36.0): Pure decision function invoked by _send_with_retry on error. Returns (should_retry: bool, delay_s: float, reason: str). Logic: (1) check attempt count/deadline, (2) on DomainReloadError mark reload + state→DOMAIN_RELOADING, (3) on any error check reload.is_active() or probe_busy(), backoff 2^attempt ≤ 8s. Extracted from inline retry logic for testability + clarity.
     * **Suspend & Dormant Mode (T6)**: `suspend() → bool` method transitions CONNECTED→DORMANT. Aborts if queue non-empty or state != CONNECTED (returns False). On success (returns True): stops heartbeat, closes TCP reader/writer, resets cooldown/backoff. Dormant is an intentional low-power state — TCP is closed but process is alive. Next `send()` from DORMANT triggers DORMANT→WAKING transition (reconnects). Heartbeat skips reconnect attempts when state==DORMANT, preventing reconnect spam. Idle watchdog can schedule dormant via `_schedule_dormant()` when parent process is alive + idle time exceeds threshold (Path B: orphan mode). See idle watchdog section below.
     * **Atomic reader/writer swap** (v0.36.0): In _reconnect(), both reader and writer closed atomically within lock to prevent zombie reads after close. Fixed CancelledError cleanup.
   - Socket: TCP_NODELAY, SO_KEEPALIVE (idle=60s, interval=10s, count=3 on macOS/Linux)
   - **Heartbeat (v0.78.5 lifecycle fix, v0.78.8 exception split, T4 callback, T6 DORMANT guard)**: 15s interval, raw ping. Task created ONLY in `_reconnect()`, destroyed ONLY in `close()`. `_ensure_heartbeat()` deleted — heartbeat no longer restarted on every `send()` call. Self-cancel guard in `close()`: `asyncio.current_task() is not self._heartbeat_task` prevents heartbeat from cancelling itself (avoids swallowing its own close path as CancelledError). **Ping exception split (v0.78.8)**: `asyncio.TimeoutError` (Unity alive, App Nap/heavy compile) → apply `_ping_stall_failures` counter; `Exception` (ConnectionReset, IncompleteRead, OSError = dead TCP) → close immediately and reconnect. This prevents 6-min zombie bridges caused by TCP errors being treated as transient stalls. **Stall logic**: 3 consecutive `TimeoutError`s + process alive → increment `_ping_stall_failures` and reset failure window. After 6 stall windows (~6 min of sustained stall), force-close. `TimeoutError` + process dead → close immediately. 2s polling when disconnected (5s when busy). **Transport activity callback (T4)**: After successful ping, `_raw_ping()` invokes `_on_transport_activity()` callback if set. Wired to `server._touch_transport_activity()` in lifespan after `start_heartbeat()` — updates the idle watchdog's transport-layer activity timer. Distinguishes transport-level keepalive (heartbeat) from user work (tool dispatch). **DORMANT guard (T6)**: When state==DORMANT, `_tick()` returns early (skips all reconnect logic). Prevents heartbeat from waking the bridge prematurely during intentional dormant suspend.
   - **connected property (v0.78.5)**: Simplified to `self._writer is not None and not self._writer.is_closing()`. Removed `select + MSG_PEEK` path — it caused false negatives on Python 3.12+ `TransportSocket` wrapper.
   - **Reload gate (v0.78.5, v0.78.8 guard removed)**: `_reload_gate: asyncio.Event` (open by default; `wait()` returns immediately). On `domain_reload` retry: gate is always `clear()`d (v0.78.8 removed the `if not self.connected:` guard — gate must clear regardless of connection state to prevent retries sleeping through reconnect). On `_reconnect()` success, gate is `set()` and `_ping_stall_failures` reset. `send()` retry path uses `asyncio.wait_for(_reload_gate.wait(), timeout=delay+jitter)` — wakes immediately on reconnect instead of sleeping full backoff.
   - **Retry Policy (v0.70.0, C8, A1)**: NEW `RetryPolicy` class in `bridge_retry.py` consolidates retry decisions:
     * Exception-path: `decide(error, attempt, deadline, cmd) → (should_retry, delay_s, reason)`
     * Hint-path: `allow_hint_retry(cmd) → bool` (Unity 'retry' JSON sentinel)
     * Unified gate: `is_retry_safe` callback checks both surfaces, preventing bypass bugs (A1)
     * Supports: DomainReloadError (immediate mark + 2^attempt backoff), TimeoutError (check is_retry_safe gate), busy (reload active or probe busy)
   - **Port Re-Discovery on Reconnect (v0.24.1, v0.52.6 pinning)** — `UnityBridge` accepts optional `port_discoverer` callable (typically `read_unity_port`), invoked during `_reconnect()` before TCP connect to detect if Unity moved to a new port. If discoverer returns different port, bridge updates `_port` and recreates CompileStateProbe. **v0.52.6: Reconnect pinning** — bridge caches `_pinned_port` and `_pinned_pid` to stick to the same Unity instance during domain reload cycles, preventing reconnection storms when multiple ports are available. Falls back to discovery if pinned instance dies. Gracefully handles discoverer exceptions (falls back to current port). ConnectionSlot threads discoverer through and adds `_sync_port()` callback to sync port back to slot + trigger server-side lockfile swap (`_on_port_change`). Backward-compatible: no discoverer → normal reconnect.
   - **CompileStateProbe**: heuristic compile/domain-reload detector (state file, PID check)
   - **Stale DLL Guards (v0.65.0)**:
     * **Python run_tests Pre-Flight** — `diagnose(expected_compile=False)` executed before test launch. Blocks on: FAILED, WEDGE-ENGINE, BUILD-FAILED-WEDGE, STALE-CACHE, STALE-DOMAIN, REBUILDING, TESTS-INVISIBLE. Prevents tests passing against stale DLLs. ToolError propagates; other exceptions gracefully degrade.
     * **C# TestRunner Gap-Window Guard** — `GetIsCompileClean()` seam checks if assemblies are loading between `isCompiling=false` and afterAssemblyReload gate. Detects race window and retries. Closes domain-reload timing gap.
     * **UPM Fallback Detection** — DiagnoseCommand.FindAsmdefDir() uses `AssetDatabase.FindAssets()` for file: UPM packages (source unavailable). Enables stale detection for local packages (previously: unknown state).
     * **Cross-Assembly Compile Errors (v0.66.0)** — DiagnoseCommand now reports `all_errors=` field via `CompilationPipeline.assemblyCompilationFinished` callback (C# SyncHelper.cs), capturing compile errors across all UnityMCP.* assemblies. Python diagnose.py parses and validates all_errors block alongside main errors field. Enables detection of silent failures in plugin/Chat/Reload assemblies that may be broken while main assembly compiles clean.
   - **DomainReloadError**: on Unity `going_away` event → immediate close + busy flag. Heartbeat calls `_reload.mark()` on DomainReloadError (v0.36.0). **v0.90.0**: `send()` catch path also calls `_reload.mark()` on `DomainReloadError` — tracks reload from TCP connect failures, not only heartbeat. Heartbeat suppresses reconnect when `_reload.is_active()` — prevents reconnect storm during domain reload window.
   - **BridgeResult Unwrapping (v0.70.0, C1)**: NEW `bridge_result.py` module provides `unwrap_bridge_result(dict) → (ok, data, err, file)` pure function. Eliminates inline unpacking across 50+ call sites:
     * Handles both text-result and structured-response formats
     * Extracts file path when present (screenshots, exports)
     * Centralizes error handling logic (reduces duplication)
   - **PID Lockfile**: each session owns `~/.unity-biome-mcp/server-{port}-{pid}.lock`; multiple sessions may coexist:
     * **macOS/Linux**: `fcntl.flock` (advisory, whole-file lock)
     * **Windows**: `msvcrt.locking` on sentinel byte at offset 1024 (non-blocking, avoids mandatory lock of PID data at bytes 0-31)
     * Cleanup removes files whose PIDs are no longer alive; it never terminates another process
   - **SIGPIPE handling**: guarded with `hasattr(signal, "SIGPIPE")` since Windows lacks SIGPIPE. Suppressed on Unix to prevent server crash on client disconnect.
   - **Reconnect (v0.30.3, v0.52.7)**: exponential backoff throttling (MIN=5s → MAX=60s, reset on success, jitter ±10%). v0.52.7: cooldown re-armed on every attempt (not only success), preventing retry spam when port unavailable. Heartbeat debounce=30s. send() reconnect no longer fires callbacks (only heartbeat does) — breaks reconnect feedback loop. push_catalog skips if already locked.
   - **Client Identification (v0.78.5)**: `UNITY_MCP_CLIENT` env var sent as `role` field in ping JSON (`"codex"`, `"cursor"`, `"windsurf"`, `"claude-desktop"`; falls back to `"mcp"` when unset). `install_initialized_hook(mcp, get_slot)` in `server_filtering.py` registers `InitializedNotification` handler — on MCP handshake, reads `session.client_params.clientInfo.name`, skips `"Claude Code"` (default), fire-and-forgets `set_client_label` command to Unity (3s timeout, DEBUG log on failure). C# `RoleToLabel()` in `ClientConnectionHandler.cs` expanded: codex→"Codex session", cursor→"Cursor session", windsurf→"Windsurf session", claude-desktop→"Claude Desktop session". `ClientSlot.Label` (new `volatile string` field): cleared on new session connect, updated by `set_client_label` command, used in disconnect log.
   - **Client Hello Handshake (T5, new→new 1 RT, new→old fallback 3 RT)**: Combined reconnect sequence replaces 3-roundtrip ping + project_path + get_version protocol with single-frame client_hello exchange. **Python UnityBridge changes**: `ClientHelloPayload` dataclass contains full session identity (role, session_id, lock_token, bridge_pid, agent_id, display_name, cwd, started_at_utc). `UnityBridge.__init__` sets `_session_id`, `_lock_token`, `_started_at_utc` once per bridge instance (stable across reconnects). `session_id` and `lock_token` are read-only properties. `_build_hello(msg_id)` constructs the client_hello JSON frame with all payload fields. `_open_reconnect_candidate` now sends client_hello first; delegates to `_check_version_from_hello` (fast path, new C#) or falls back to `_verify_candidate_project + _fetch_and_check_version` (legacy, old C#). **C# ClientConnectionHandler changes**: `IsSlowPath(cmd)` adds `client_hello` to fast-path list (never dispatches to main thread). On receiving client_hello, extracts cmd/id early (before first-message block) to handle first-message bookkeeping itself (no double-logging). `client_hello` response combines pong + helloVersion:2 + version string + projectPath in single frame. Calls `SetEntrySession(sessionId, lockToken, agentId)` from payload. **MCPServer._cachedDataPath**: `volatile string` cached from `Application.dataPath` on main thread in StartAsync(); used by ThreadPool client_hello fast-path handler (Application.dataPath is main-thread-only, cannot call from TCP handler). **lockfile.py metadata**: `write_lock_metadata(path, sessionId, lockToken)` and `read_lock_metadata(path)` store/retrieve session identity in lockfile line 2 (JSON). `acquire_lock()` gains optional metadata kwarg. Server lifespan writes lockToken + sessionId to lockfile after successful connect. **Backward compatibility**: new→new (v0.76.0+) sends client_hello, receives combined response (1 RT). new→old (v0.75.x or earlier C#) sends client_hello, gets ok:false or missing helloVersion field, falls back to legacy project check + get_version (3 RT). old→new: Python 3-roundtrip unchanged, C# accepts and responds to ping/get_version as before. old→old: unchanged 3-roundtrip protocol.
   - Max message: 10MB. Timeout ownership is layered across wrappers, the
     retry session, Unity request deadlines, and operation arguments; use the
     canonical table in [`AI/tcp-bridge.md`](tcp-bridge.md#timeout-layers).

3. **Unity Plugin** (C#: 165+ files, ~17800 LOC, v0.42.0: Wizard asmdef split, Updates folder, MarkdownInlineFormatter extraction, v0.44.0: LevelUp UX, v0.45.0: InstallSourceDetector + async updaters, v0.55.10: unified SceneMcpOverlay + IconCanvas + PluginToolGrouping, v0.59.0: Runtime Debug + Watch System + Chat field chips + Debug UI panel, ROI sprint v0.69.0: CommandOptions struct, CallerIsPlugin gate, v0.73.1: command registration race fix — Bootstrap.cs deleted, registration moved to MCPServer.StartAsync, tools gap sprint v0.77.0: +ShaderGraphHelper.Mutations.cs +961 LOC across TimelineHelper/AnimatorControllerHelper/AnimationHelper/ParticleHelper/MaterialHelper/CommandRouter — 6126 C# NUnit green, v0.78.5: MaxClients 4→8, ClientSlot.Label, set_client_label command, RoleToLabel expansion, v0.78.8: AliasExpander.cs (C#-side $alias expansion in batch DSL + direct MCP tools), SceneTestBase.cs (abstract TearDown base for 36 test classes) — 6426 C# NUnit green, v0.78.9: AliasStatusTests.cs (alias_status command + IsStale tracking), PlaytestGlobalAliasTests.cs (PlaytestConfig alias auto-injection into run_playtest) — 6550+ C# NUnit green, v0.78.10: AliasStatusTests.cs count assertion relaxed to existence-only check, v0.78.11: AliasExpander BuildPipePath (pipe-preserving ValPath resolution), TestRunner DeleteTempScene cleanup, +13 C# alias pipe tests + 6 PlaytestGlobalAlias tests — 6633+ C# NUnit green, v0.79.1: +PlaytestPathTests.cs (run_playtest path= file execution, traversal guard), check_colliders path optional fix — 6452 C# NUnit green, v0.80.1: +SceneCleanTestBase.cs (root-object leak-detection base), +force_play_stop command (allowedDuringCompile, T5 ladder), force_refresh enhanced (ReloadGuard unlock + RequestScriptReload + RepaintAllViews), DiagnoseCommand isReallyCompiling field — 6455+ C# NUnit green; playtests ROI sprint: +PlaytestLinter.cs, +PlaytestRunner.Snapshot.cs, +SceneRefResolver.cs, +SceneRefLinter.cs, +WAIT_CAPTURED/SWEEP_PATH/provenance in PlaytestParser, +9 new test files (~1200 new NUnit tests); v0.86.0: test quality review — 25 C# tests deleted (self-testing/duplicate), ~10 assertions hardened, 8 test renames (snake_case→PascalCase), TearDown/SetUp added for SetParentTests + UndoGroupHelperTests + EnabledToolsCacheTests + GetAliasesTypedTests + ColliderFitHelperTests, RenderAnalyzer.cs MissingComponentException crash fix (try/catch moved to cover GetComponent call) — 6537 C# NUnit green; v0.90.0: +PlaytestRunner.FrameCapture.cs (CAPTURE_FRAMES step execution), +PlaytestLaunchWindow.cs (MCP/Playtest Launcher EditorWindow), +6 new test files (PlaytestForLoopTests, PlaytestFrameCaptureTests, PlaytestPathPrefixTests, PlaytestCaptureStringTests, Sprint3FrictionTests, RuntimeHelperInvokeTests), SyncHelper._pumpActive singleton guard + isCompiling early-exit, TestRunner dirty-scene save before NewScene, SceneCleanTestBase leaked-name error report — 6687 C# NUnit green (1 pre-existing failure); v0.91.0: real-project audit — CommandRouter VALIDATION errors → Debug.LogWarning (was LogError); mutating commands now call UndoGroupStack.Push + ChangeWatcher.RecordMutation inline (get_changes + undo_last now track MCP commands); lint_playtest/render_analyze/compile_preflight throw InvalidOperationException on error → ok:false; run_playtest returns ok:false when result contains FAIL; CompleteFromInner gains isSuccess predicate; ObjectManager set_property+set_property_delta call MarkSceneDirty in Edit Mode; transfer_object cross-scene parent guard (validates parent is in target scene); ErrorClassifier.Classify+FormatError unwrap TargetInvocationException before type-switch; SceneHelper.SaveScene throws IOException on failure (was silent); ChangeWatcher.RecordMutation public static inline hook; +MultiSceneOperationsTests.cs (29) +ObjectManagerTests.cs (25) +ResultEnvelopeTests.cs (32) — 6699 C# NUnit green (1 pre-existing failure); v0.92.0: API pragmatic review — +SerializedFieldRenameAudit.cs (YAML scan for stale field data after rename), +Roslyn/UnityPreflightHints.cs (static analyzer: serialized Dictionary/interface/abstract/rename checks), MaterialHelper target=shared|instance|asset, ScriptableObjectHelper Set echoes old→new + missing field list, PrefabHelper Save mode=new|overwrite + GetOverrides format=structured + Revert scope=children, AnimationHelper CreateClip try/catch+rollback, BatchHelper.HasErrors promotes inner ok:false to batch envelope, UIHelper atomic create rollback — 6700+ C# NUnit green (1 pre-existing failure); v1.15.0: +ShaderGraphHelper.Layout.cs (graph_get_layout, graph_set_layout, graph_auto_layout; topological sort with overlap detection), +ShaderGraphLayoutTests.cs (18 tests) — 7230+ C# NUnit green (1 pre-existing failure))
   - **SyncHelper.cs (v0.90.0 reload stability)**: `_pumpActive` singleton guard — `StartTickPump` returns immediately if already active, preventing N×300 concurrent pump accumulation on rapid reconnects. Pump exits when `EditorApplication.isCompiling` becomes true (early-exit). `RequestScriptReload()` gated on `!isCompiling`. `Refresh()` called after `AllowAutoRefresh` in `force_refresh` handler. TestRunner.cs: dirty temp scene saved silently before `NewScene` in `RunFinished` to suppress "Save modified scenes?" dialog.
   - **MCPServer.cs**: Dual TCP listeners (main port 9500-9599 + chat port auto-assigned, separate), 4-byte BE framing, 10MB max, SO_KEEPALIVE, **v0.23.0: SO_REUSEPORT** (macOS/Linux) for rapid reconnect recovery, auto-assigns free ports via `PortResolver.FindFreePort()`, persists to Library/MCP_Port.json, state file (`ready`/`compiling`/`reloading`), `going_away` event before domain reload, ClientSlot pattern isolates CLI and Chat connections (**v0.78.5: MaxClients raised 4→8; `volatile string Label` field added — cleared on new session connect, updated by `set_client_label` command, used in disconnect log**; **T3: Per-Entry Metadata** — `ClientActivityState` enum (Active/Idle/Dormant/Closing), `ConnectionSnapshot` readonly struct (Index, Generation, Label, RemoteEndpoint, ConnectedAt, LastUsefulAt, LastCommand, InFlightCount, State, SessionId, DisplayName), per-entry metadata fields (ConnectedAtTicks, RemoteEndpoint, Label, LastCommand, SessionId, LockToken, AgentId, DisplayName, LastUsefulActivityTicks, InFlightCount with atomic sync primitives), methods: SetEntryEndpoint, SetEntryLabel, BeginCommand, EndCommand, GetEntryLabel, SetEntrySession, TakeSnapshot, DisconnectEntry, SetLastUsefulTicksForTest — enables connection state snapshots for diagnostics and connection lifecycle tracking). **T7: DormantBridgeScanner** — NEW static class in `ConnectionSnapshot.cs` with `Scan(port, activePids)` method that detects bridge processes holding lock files but not in active TCP slots. Returns `IReadOnlyList<DormantInfo>` (lightweight descriptors: BridgePid, Kind="Unknown", Cwd=null). Test seam `OverrideLockDir` for lock file path isolation. `GetActiveSnapshots()` alias on ClientSlot returns per-connection snapshots for integration with MCPStatusWindow.). **v0.37.0: IsReallyCompiling** — managed flag replaces EditorApplication.isCompiling latching, 120s wedge guard prevents false-positive "backgrounded" state. **v0.36.0: WritePortFile** writes `{pid}.port` (main) + `{pid}.chat-port` (chat listener discovery, managed by PortFileManager.cs). **v0.52.6: ShouldStartServer guard** — static ctor checks `ShouldStartServer(isBatchMode)` to prevent AssetImportWorker from creating conflicting port files during batch asset reimports. Detects batch mode via `EditorApplication.isBatchMode` OR `-nographics` args. **v1.0.1: Windows port fixes** — bind fallback calls `SaveRuntimePorts` (not `SavePorts`) to avoid MCPSettings.json drift; `CleanStalePeerPortFiles()` called at startup; `ClientSlot.cs` sets `LingerOption(true, 0)` on all close paths (DisconnectAll/KillPhantoms/eviction) to force RST instead of FIN — eliminates TIME_WAIT on Windows so port is freed immediately after domain reload. **T5: _cachedDataPath** — `volatile string` field caches `Application.dataPath` on main thread in StartAsync(); used by ThreadPool client_hello fast-path handler (Application.dataPath is main-thread-only and cannot be called from TCP async handlers).
   - **PortResolver.cs**: Pure testable helpers (ResolvePort, ResolveChatPort, FindFreePort, SavePorts, IsValidPort, ParsePortFromJson) with 25 NUnit tests. Validates 1024–65535 range, skips reserved ports, fallback to OS-assigned via port 0. **v0.52.6: Chat port collision guard** — ResolveChatPort ensures chat port ≠ main port (prevents accidental self-binding). FindFreePort ceiling raised 9599→9699 to accommodate dual-port scanning. **WI-7 (centralized 3-port allocation)**: NEW atomic `BindFreePort(exclude: List<int>)` method eliminates TOCTOU race (scan+bind in single operation); `ResolveReloadPort()` for reload port discovery; `FindFreePortExcluding(exclude: List<int>)` skips multiple ports; `TrySaveAllPorts(main, reload, chat)` writes all 3 ports atomically. PortFileManager.ReloadPort property + EnsureAllPorts() orchestrates full 3-port resolution chain on startup.
   - **CommandRouter.cs** (v0.57.0 refactor, v0.70.0 B1: Registration split): RegisterAll() → calls core commands + PluginRegistry.RegisterAllPlugins() for external plugins, data-driven IsMutating/IsRuntime. **v0.37.0: DefaultIsCompiling** — two-layer check (IsReallyCompiling + 120s wedge guard) prevents false-positive compile blocks. **v0.57.0: ProcessAsync simplified** — switch-based dispatch table via `CommandRegistry.HasAsyncHandler()` replaced inline if/else chains (148→27 lines, Open/Closed Principle). Extracted 6 async handlers: AsyncRunTests, AsyncWaitUntil, AsyncMoveTo, AsyncTestStep, AsyncRunPlaytest, AsyncAskUser. **B1 (v0.70.0): Registration Split** — NEW `CommandRouter.Registration.cs` partial class splits ~340-line RegisterAll() into 4 themed methods matching guard-flag precedence. **WI-2 (ReadOnly MCP mode)**: NEW `IsReadOnly` property (reads `MCPSettings.json`); `CheckGuards()` blocks all write commands when IsReadOnly=true via early return with error message. PortFileManager.ReadOnly exposes setting. BatchHelper integration: read-only batches bypass verification gates. Tests: BatchHelperReadOnlyTests, CommandRouterReadOnlyTests, PortResolverReadOnlyTests verify blocking behavior and read-command passthrough.
     * `RegisterMetaCommands()` — always-allowed + compile-safe (ping, get_enabled_tools, set_tool_catalog, diagnose, set_client_label, etc.)
     * `RegisterReadCommands()` — non-mutating, read-only (get_hierarchy, get_component, get_console, search_scene, etc.)
     * `RegisterWriteCommands()` — mutating, write-heavy (create_object, set_property, manage_component, batch, etc.)
     * `RegisterRuntimeCommands()` — play-mode-only (invoke_method, query_state, move_to, run_playtest, etc.)
     * Snapshot-tested by CommandRegistryCompletenessTests; per-bucket coverage in CommandRouterRegistrationTests.
   - **CommandRegistry.cs** (v0.57.0 refactor, v0.69.0: CommandOptions struct, v0.70.0 B3: demoted to internal): Func<string,string> handlers + `Action<string,string,TaskCompletionSource<string>>` AsyncHandler field. **v0.69.0 CommandOptions**: Groups rarely-changing trailing params (Mutating, Runtime, Required, Optional, SpecialDispatch, AlwaysAllowed, AllowedDuringCompile, Description, MaxResponseChars) into plain mutable struct. New preferred overloads: `Register(cmd, handler, CommandOptions)` + `RegisterAsync/RegisterAction` equivalents. Legacy bool-param overloads converted to forward to CommandOptions for backward compatibility. Duplicate registration guarded (warning log, skips). `HasAsyncHandler(cmd, out handler)` returns dispatch table entry. `Execute()` enforces Handler ≠ null, throws on async-only entries. **B3 (v0.70.0): Demoted to internal** — Zero production call sites outside CommandRegistry.cs itself (grep-verified); every real registration uses legacy bool-params, which build CommandOptions internally. Now purely plumbing detail.
   - **PendingAskRegistry.cs** (v0.70.0, C7 extraction): NEW class extracts Ask() method from CommandRouter. Manages GUID-keyed TaskCompletionSource registry for ask_user command responses. Isolated state machine: Register(requestId) → GetTcs(requestId) → await completion. Unidirectional: Plugin asks user → chat UI responds → TCS signals AI. Testable in isolation via PendingAskRegistryTests.
   - **PermissionConfig.cs** (v0.71.0: shared SERVER_NAME constant, cross-language drift guard): Per-session MCP tool permission config — deny-set backed by EditorPrefs. **v0.71.0 Shared Constants**: `SERVER_NAME = "unity-biome-mcp"` (matches Python config/merger.py), `MCP_BLANKET = "mcp__" + SERVER_NAME` (derived formula, NOT manual string). `MCP_TOOL_PREFIX = MCP_BLANKET + "__"`. Comment explicitly notes: "NOT SERVER_NAME.Replace('-', '_')" — hyphens are legal in MCP tool names and never touched by Claude's sanitizer. Cross-language drift preventable via `test_server_name_consistency.py` (regex-based text assertion matching Python/C# constants, runs in unit suite, $0 cost). **Prior Incident (v0.70.7)**: Two independent config writers used different server-name literals, silently registering two MCP servers on the same port. This guard prevents regression.
   - **CommandRouter.ExtractVector3 (v0.70.0, C4)**: NEW static helper method parses "x,y,z" string to Vector3. Reduces scattered inline parsing across ~8 transform/movement/spatial commands. Pure function, no side effects.
   - **Command registration (v0.73.1, replaces Bootstrap.cs deleted in v0.73.1):** Registration moved from `[InitializeOnLoadMethod] Bootstrap.Init()` into `MCPServer.StartAsync()`, called before TCP bind. Guarantees commands are registered before any connection is accepted. `CommandRegistry.Ready` (`volatile bool`) resets in `Clear()`, sets at end of `RegisterAll()`. `CommandRouter.CheckGuards()` returns `retry-2000` when `!Ready` — safe guard against rare startup races. `EnsureEnabledToolsCacheWarm()` dead method removed.
   - **PluginRegistry.cs** (v0.69.0: CallerIsPlugin gate): Static registry for IMCPPlugin implementations. Plugins register via `[InitializeOnLoad]`. One-way asmdef dependency: external → public. **v0.69.0 CallerIsPlugin**: `RegisterAllPlugins()` strips AlwaysAllowed/AllowedDuringCompile from plugin-provided handlers (CallerIsPlugin=true) — plugins can't self-gate bypasses. Core tools keep flags for internal permission logic.
   - **IMCPPlugin.cs** (v0.64.0: DIMs for settings UI; v0.65.1: documentation): Interface — Name, CommandPrefix, RegisterCommands(), OnDomainReload(), AdditionalCommands (DIMs). **v0.64.0 DIMs**: `string Description` (plugin purpose), `bool HasSettingsUI` (default false), `VisualElement BuildSettingsUI()` (configurable settings). Enables plugin UI registration without breaking backward compatibility. **v0.65.1 complete guide**: `docs/plugins/index.md` (Quick Start) and `docs/plugins/api-reference.md` document IMCPPlugin contract, registration patterns, PluginConfig storage, UI building, PluginUIHelpers layer, testing patterns, best practices, troubleshooting.
   - **PluginConfig.cs** (v0.65.1 new): Isolated EditorPrefs storage for plugins. Namespace: `MCPPlugin_{pluginId}_{key}`. Methods: GetString/SetString, GetBool/SetBool, GetInt/SetInt, GetFloat/SetFloat, Delete. All main-thread only. Zero conflicts with core MCP or other plugins.
   - **PluginUIHelpers.cs** (v0.65.1 new): Convenience layer for plugin settings UI. 7 methods: MakeCard (bordered foldout), InlineRow (flex row), AddTextField (auto-persist), AddToggle (auto-persist), AddSlider (float, auto-persist), AddIntSlider (int, auto-persist), AddDropdown (with fallback, auto-persist), LoadStyles (standalone EditorWindows). Each control binds to PluginConfig automatically.
   - **SettingsPageFactory.cs** (v0.64.0): `BuildPluginsPage()` method generates MCP Hub settings section for all registered plugins. Lists descriptions, renders `BuildSettingsUI()` per plugin. Integrated into MCPHubUI. 52 tests in PluginSettingsPageTests.
   - **CommandValidator.cs**: parameter validation via `CommandRegistry.TryGetContract()`, fuzzy did-you-mean via `StringDistance.ClosestMatch`, contract declared at `Register()` call site
   - **ValueParser.cs**: vectors, quaternions, colors, arrays, 100+ types (Rect/Bounds/RectInt/BoundsInt/LayerMask + Int64/Double precision), type-aware SetPropertyValue
   - **InputNormalizer.cs**: component/property/value normalization
   - **BatchHelper.cs**: multi-command text parser + executor (on_error=continue/stop). **v0.37.0:** testable IsCompiling seam via CommandRouter (supports reload-latch testing). **v0.78.8:** calls `AliasExpander.ExpandText(rest)` on each DSL line before key=value parsing.
   - **AliasExpander.cs** (v0.78.8, NEW; v0.78.9: IsStale + alias_status; v0.78.11: BuildPipePath): C#-side $sigil expansion for batch DSL and direct MCP tool calls. Lazily loads alias table from all `PlaytestConfig` assets in project (skips `VarRuntime` aliases). Cache invalidated by `AliasConfigPostprocessor` on any `.asset` reimport or domain reload. Two entry points: `ExpandJson(argsJson)` (JSON-escapes values, called from `CommandRouter.Process/ProcessAsync` L90/L144) and `ExpandText(text)` (plain text, called from `BatchHelper.ParseLine`). Unknown sigils left intact (no throw). `_tableOverride` seam for unit tests (bypasses AssetDatabase). **v0.78.9**: `IsStale` property (`bool`): true when `_hasLoaded=true` but `_table=null` (cache evicted after load). `alias_status` C# command returns `loaded: <state>\ncount: N\nstale: <bool>` health summary. **v0.78.11 `BuildPipePath(QueryAlias)`**: private static helper in `GetTable()` — preserves full `path|component|field` pipe format for `ValPath` aliases (previously dropped component/field, silently resolving to path-only). Now `$alias` correctly expands to `path|Comp|field` for tools like `get_component` and `inspect`.
   - **PlaytestParser DSL (v0.90.0 additions)**: `PATH_PREFIX /path` — applies path prefix to all VAL path aliases (Phase 0.7.1, first occurrence wins, post-INCLUDE). `FOR $var IN start..end` / `END_FOR` — integer loop unrolling at parse time, max 10000 iterations, nesting supported. `CAPTURE_FRAMES n INTERVAL s [CAMERA name] [MODE strip|list] [LABEL name]` — screenshot sequence (n≥2). `ASSERT_FRAMES_DIFFER label` / `ASSERT_FRAMES_STATIC label` — pixel-hash frame comparison. `ASSERT_CHANGED $name` — asserts captured value changed since `CAPTURE $name /path|Comp|field`.
   - **7 Serializers**: HierarchySerializer (tree, MAX_NODES=3000, incremental, summary), ComponentSerializer (key-value, UnityEvent expansion, PrefabStage-aware, **v0.23.0: #instanceID in all path tools**, **v1.4.0: backslash escaping + bracket protection for special characters in names**), AnimationSerializer, TimelineSerializer, AnimatorControllerSerializer, ParticleSerializer, ShaderSerializer
     * **ComponentSerializer.Finder (path resolution, v1.4.0 updates)**: `FindObject(path)` resolves paths with special character support. `SplitPathSegments(path)` tokenizes paths handling (1) backslash escapes (`\/` → `/`, `\\` → `\`), (2) bracket-protected segments (`[Zone A/Zone B]` stays intact), (3) round-trip guarantee via `GetPath(go)` → parse → `FindObject()`. Bracket and quote depth tracking in path/tokenizer operations prevents false splits.
   - **ScenePathParser (v0.31.0)**: Shared struct for multi-scene path parsing (`"SceneName:/"` prefix extraction). Used by SceneObjectFinder + ComponentSerializer.Finder. Replaces inline string parsing, prevents multi-scene reference bugs. **v1.4.0**: Updated to handle bracket-protected and escaped segments in multi-scene paths.
   - **ObjectManager (v0.23.0 fixes, v0.55.10 custom namespace support, v0.75.4: RenameObject, v0.77.0: CloneObject)**: Properties.cs auto-redirects `set_property("active")` to SetActive. Lookup.cs adds FindType + short-name fallback for custom components. **v0.55.10**: SafeGetTypes() preserves partial ReflectionTypeLoadException, TypeCache + abstract/generic filter for AddComponent safety. **v0.75.4**: `RenameObject(path, name)` renames a GameObject with Undo support, marks scene dirty in Edit Mode, and returns the new scene path. **v0.77.0**: `CloneObject(path, offsetX, offsetY, offsetZ)` duplicates a GameObject with positional offset, returns new scene path.
   - **FileOutputHelper (v0.23.0)**: ScreenshotsDir now `<ProjectRoot>/ScreenShots/` (project-local, not shared cache)
   - **RefManager (v1.28.0: $HEX format, v1.31.0: & prefix + base62)**: **v1.31.0** outputs scene object IDs with `&` prefix + base62 encoding (e.g. `&aB3`, alphanumeric base62 chars). **Backward-compatibility**: InputParser and C# code still parse legacy `$HEX` format and `#decimal` instanceIDs for transition. **WirePrefix.cs constants**: `&` (Ref) and `$` (Alias) separate the wire protocols. **Hierarchy caching**: persistent RefManager refs in paths instead of transient hex IDs. **Python middleware**: detects `&` prefix for hierarchy nodes. Used in: ObjectReference parameters (set_property), tool output, chat chips, scene diffs.
   - **ErrorHelper**: contextual errors with did-you-mean hints
   - **RegionTool (v0.46.0+v0.51.0)**: Interactive Scene View annotation tool for level design
     * **Polygon2D**: Immutable 2D polygon (XZ plane), winding-number PIP test, AABB bounds, CSV import/export, RDP simplification
     * **SceneRegionTool**: EditorTool with multi-mode FSM (Lasso/Rectangle/Circle/PointByPoint), keyboard shortcuts (Shift+R activate, Q/W/E/R mode switch, G grid snap, Enter commit, Esc cancel)
     * **SceneRegionQuery**: 3-stage spatial pipeline (AABB pre-filter → component filter → PIP test → cap+format), GameObject[] array result
     * **SceneRegionState**: LRU registry (8 slots) + EditorPrefs persistence, CSV export for later use
     * **Drawing Modes (v0.51.0: expanded)** (IDrawingMode + IAnnotationMode): LassoMode, RectangleMode, CircleMode, PointByPointMode (region selection); PointMode (single point + label), PolylineMode (polyline with auto-length), MeasurementMode (distance measurement). Each mode tracks active state, completion, grid snap tolerance. DrawingUtils shared snapping logic.
     * **RegionSnapshot (v0.51.0: expanded)**: Unified model with `AnnotationType` field ("region"|"point"|"polyline"|"measurement", null=legacy). Factory methods: `CreatePoint()`, `CreatePolyline()`, `CreateMeasurement()`. Labels + LengthOrDistance + Direction support per type.
     * **Rendering (v0.51.0, v0.55.10 unified overlay)**: RegionRenderer (GL wireframe + fill + annotation overlays), RenderState (+3 annotation fields), UIToolkit SceneMcpOverlay (merged SceneRegionOverlay + SceneAnnotationOverlay with mode-dynamic rendering). Multi-layer display: regions (wireframe+fill), points (radius marker), polylines (vertices+length), measurements (dimension line). Annotation chip delivery via OnAnnotationCommitted hook.
     * **SceneAnnotationTool (v0.51.0)**: Unified entry point (Shift+A) for all annotation modes. Mode switching via menu. SceneAnnotationShortcut wires hotkeys. SceneAnnotationUtils for common validation/snapping.
     * **Chat Integration**: RegionChipProvider for region+annotation selection in chat (format methods: FormatRegion, FormatPoint, FormatPolyline, FormatMeasurement). **T3 (v0.64.0): FormatPolyline Enrichment** — Extended with `type=polyline` tag, `start=` / `end=` Vec3 endpoints, YAML-style point list at full depth. MultiPoint annotation support (extends v0.51.0 capabilities). 75 tests in test_scene_tools.py.
     * **Tests**: 104 C# NUnit tests (v0.46.0) + 67 new annotation tests (v0.51.0): RegionSnapshotAnnotationTests (27), AnnotationDrawingModeTests (23), RegionChipProviderAnnotationTests (17)
     * **GdSnapshotSerializer.cs (v0.74.0, updated v0.92.x)**: Converts `RegionSnapshot` annotations to VAL $label DSL lines for playtest scripts (ALIAS format removed in v0.92.x). `ToAliasLines(snap)` → `VAL $label x,y,z` (point/region), numbered vertex aliases (polyline), `$label_start`/`$label_end` (measurement). `ToPlaytestPreamble(snapshots)` renders all snapshots as a block preamble. Labels sanitized (lowercase, underscores, alphanum only). Internal to `UnityMCP.Editor.RegionTool` namespace.

### Runtime Debug Subsystem (v0.59.0)

**Play Mode-Only Debugging & Monitoring:**

   - **execute_code Play Mode Support**: Removed `mutating=true` restriction — code now executes in Play Mode (read-only scripting, no Edit Mode asset changes). Blocked patterns (Reflection, File I/O, Process control) unchanged.
   - **invoke_method Private/Static Support**: Roslyn reflection now inspects private + static methods. IsAllowedAssembly check inverted to blocklist model (Roslyn visibility spans custom asmdef, Unity Code domain hidden by default).
   - **Security Hardening (4 new blocked patterns v0.59.0)**:
     * `InvokeMember(` — reflection-by-name dispatch bypass
     * `EditorApplication.isPlaying`, `EditorApplication.isPaused` — Play mode kill-switch queries
     * `FileUtil.` — UnityEditor file API bypass (System.IO already blocked)
     * Null guard fix in IsAllowedAssembly (handles edge case custom asmdef with zero types)
   - **Watch System**: 6 C# files + Python watch.py with 5 MCP tools:
     * **WatchEntry**: id, path, component, field, condition, action, intervalMs (serializable state)
     * **WatchCondition**: parses `< 10`, `> 0`, `== null` comparison expressions
     * **WatchEvaluator**: Roslyn-based field value evaluation + condition triggering
     * **WatchRegistry**: SessionState persistence of active watches
     * **WatchScheduler**: EditorApplication.update polling (~500ms default interval)
     * **WatchCommandHandler**: Maps 5 watch MCP tools to C# commands
     * **Python tools**: watch_add (path, component, field, condition, action, interval_ms) → watch ID; get_watches (list + logs); watch_remove (by ID); watch_clear (all); watch_reset (re-arm triggered)
     * **Conditions**: Optional comparison (e.g., `field < 10`) triggers action
     * **Actions**: 'log' (default) or 'pause' (editor pause on trigger)
   - **Chat Component Fields (v0.59.0, v0.64.0: always-visible)**:
     * **PropertyContextMenuBridge**: Right-click Inspector property → "Add to MCP Chat" context menu entry
     * **ComponentChipProvider**: Programmatic chip kind for component-level chip (path format: `goPath|CompType`). Priority 125, summary depth shows ~10 fields
     * **FieldChipProvider**: Programmatic chip kind for single field chip (path format: `goPath|CompType|fieldName`). Priority 130, summary shows field value only. Registered via ChipKindRegistry.EnsureBuiltIns().
     * **ChipPropertyFormatter DRY extraction**: Shared serialized property rendering used by both ComponentChipProvider + FieldChipProvider (handles UnityEvent expansion, ObjectReference disambiguation)
     * **SerializedObject disposal fix**: Using statements ensure SO cleanup (prevents memory leak during watch polling)
     * **T7 (v0.64.0): Field Chips Always Visible** — `PendingChips` queue in `ChipPillFactory` ensures menu remains visible regardless of scroll state. Auto-open `MCPChatWindow` on field-add. `FieldContextMenu.cs` enhanced with pending chip queue
   - **T5 (v0.64.0): Undo MCP Tool & UndoGroupStack**:
     * **UndoGroupStack.cs** (32 LOC): Manages nested undo/redo groups for atomic multi-step operations. `PushUndoGroup()` → stack operations → `PopUndoGroup()` on exit. Thread-safe registration tracking.
     * **undo_last MCP Tool**: Reverts N user actions via `EditorApplication.Undo.PerformUndo()` loop. AI-callable action rollback (mutating, RW category).
     * **TurnUndoTracker**: Integrates undo state into CLI backend turn tracking (v0.64.0: +2 LOC for undo awareness)
   - **Debug UI Panel (EditorWindow, v0.59.0)**:
     * **MCPDebugPanel**: Menu item `MCP/Debug Panel`, hosts MCPDebugUI builder
     * **MCPDebugUI (5 partial files)**: Watch rows, eval bar, add-watch dialog, console preview, sparklines
     * **DebugOverlayDrawer**: Scene View labels for watched field values (read-only GL overlay)
     * **SparklineHelper**: Compact sparkline graphs for numeric field trends (32×16px, min/max/current tracking)
     * **MCPDebug.uss**: Stylesheet for panels + sparklines
   - **AI Debug Tools (Python v0.59.0)**:
     * **debug_tool.py**: Symptom classifier → batch command generator. Maps keywords (move, collide, anim, spawn, etc.) → relevant component types + MCP tools. Returns structured context for Haiku reasoning.
     * **snapshots.py**: State capture + diff. Compares two snapshots (values before/after mutation).
     * **4 new MCP tools**: debug_tool (symptom → batch plan), get_perf (profiler metrics; removed v0.85.1 → use get_frame_stats), debug_animator (animator state dump), debug_physics (physics query results), get_memory (memory usage per system)
   - **Profiler Helpers (C#)**:
     * **ProfilerHelper**: QueryAsyncFrameTime, GetMemorySummary, GetAllocationRate
     * **MemoryHelper**: GetTotalMemory, GetGCStats, GetSystemMemory
     * **AnimatorHelper**: GetAnimatorState (current state, parameters, clip info)
     * **PhysicsHelper**: RaycastResults, PhysicsSceneQuery, ColliderOverlapCheck

### Profiling & Rendering Analysis Subsystem (v0.60.0)

**On-Demand Performance Profiling & Rendering Optimization:**

- **profile MCP Tool (5 modes)**: Session-based frame recording with 600-frame ring buffer (~10s at 60fps)
  - Modes: `start` (manual record), `stop`, `status`, `analyze` (compute stats), `compare` (before/after verdict)
  - Stats: FPS (avg/min/max/P99), CPU/GPU time (ms), draw calls, batches, triangles, memory (Mono/GC), GC count
  - Ring buffer zero-copy iteration, compare verdict (STABLE/IMPROVED/REGRESSED)
  - Category: PROFILING (gated, v0.60.0+)

- **get_frame_stats MCP Tool**: Instant one-shot snapshot (dt, fps, cpu, gpu, draw calls, batches, triangles)
  - No parameters, fast path (allowed during compile)
  - Category: PROFILING

- **bake MCP Tool (pipeline-gap sprint)**:
  - Lighting bake operations: `start` (async fire-and-forget BakeAsync), `status` (poll completion), `cancel`, `clear`, `settings`
  - Occlusion bake operations: `start` (async via MainThreadDispatcher), `status`, `clear`
  - Category: ASSETS (write)

- **render_analyze MCP Tool (9 actions)**:
  - `stats` — draw calls, batches, triangles, setpass, shadow counts
  - `overdraw` — opaque/transparent/particles/UI counts per viewport
  - `materials` — active material count, dedup candidates (shader+keywords fingerprint)
  - `shaders` — shader list, variant count per permutation
  - `batching` — SRP batcher compatibility, static/dynamic/GPU instancing candidates
  - `lights` — categorized by type/bake status, shadow map count, probe audit
  - `shadow_audit` — shadow cascade count, max distance, split recommendations
  - `probe_audit` — probe count, placement density, missing coverage detection
  - `frame_debug` — reflection-based Frame Debugger batch-break-cause analysis
  - Category: RENDERING

- **material_audit MCP Tool (3 actions)**:
  - `summary` — total material count, memory usage, compression stats per platform
  - `materials` — per-material breakdown (name, shader, property count, instance count)
  - `duplicates` — fingerprint-based dedup (shader+keywords+properties, excluding textures)
  - Category: ASSETS

- **analyze_lod_culling MCP Tool**:
  - LOD group analysis: coverage (% of high-poly objects with LOD), poly reduction ratio per level
  - CrossFade warnings, poly density heatmap
  - Occlusion culling detection (per-scene)
  - Recommendations for high-poly objects missing LOD
  - Category: RENDERING

- **build MCP Tool (pipeline-gap sprint, 300s timeout)**:
  - BuildPipeline player builder: `build` action (async via MainThreadDispatcher)
  - Parameters: target (StandaloneWindows64|StandaloneOSX|Android|iOS|WebGL, default active), scenes (comma-sep asset paths, default Build Settings), path (output, default Builds/<target>), dev (development build flag)
  - Category: SYSTEM

- **package MCP Tool (pipeline-gap sprint, 60s timeout)**:
  - PackageManager async operations via EditorApplication.update pump
  - Actions: `list` (installed packages), `search` (requires query), `add` (requires name, version optional), `remove` (requires name)
  - Category: ASSETS

- **Tool Extensions (pipeline-gap sprint)**:
  - **asset**: +`read_text` (path), +`write_text` (path+content), +`reimport` (path), +`create` AnimatorController/ScriptableObject (class= required for SO)
  - **editor**: +`paths` param for multi-select via comma-sep list (e.g., "/Player,/Enemy,/NPC")
  - **navmesh_query**: +`get_settings` (list all NavMesh agent type settings), +`set_settings` (update NavMeshSurface agent params: agentRadius, agentHeight, agentClimb, agentSlope)
  - **project_settings**: +`graphics`, `audio`, `input` targets (extending tags|layers|sorting_layers|quality|physics|time|player); +`build_target` param for ScriptingBackend (Standalone|iOS|Android|etc) value setting; +tag removal via prop=remove; +quality SetQualityLevel() via prop=currentLevel

- **On-Demand Activation Pattern**:
  * ProfilerBridge lazy-init (no [InitializeOnLoadMethod] overhead)
  * ProfileRecorder subscribes to EditorApplication.update ONLY during active recording
  * FrameDebugHelper lazy reflection (instantiated only on render_analyze frame_debug action)
  * Zero cost by default — no profiler handles, no per-frame tick until explicitly called

- **Gating Categories (v0.60.0)**:
  * New: PROFILING, RENDERING, DEBUG (aliases: 'profiling', 'rendering', 'debug', 'perf')
  * Debug tools moved from TIER1 → DEBUG category: debug, snapshot, watch_add/get/remove/clear/reset, get_metrics
  * Hides debug schemas by default and reveals them on demand

### Profiling UI Subsystem (v0.61.0)

**Real-Time Performance Visualization & Recording:**

- **PerfOverlay** — SceneView UITK overlay (5Hz refresh, zero-alloc):
  * FPS sparkline (60-sample history)
  * CPU/GPU time (ms) with color-coded bands
  * Draw calls, batches, triangles counters
  * Threshold-based coloring (good/warn/crit)
  * Toggled via SceneView overlay dropdown (≡ → MCP Profiler)

- **PerfWindow EditorWindow** (opened via MCP → Performance menu):
  * **Performance tab**: 120-frame FPS line graph (Painter2D), CPU/GPU fill bars, frame stats (current/avg/P99/max), Record button
  * **Rendering tab**: Snapshot stats grid (draw calls, batches, setpass, triangles, vertices, shadows, pipeline badge), Save Baseline + Compare buttons, verdict badges
  * **Sessions tab**: Session list with checkboxes, two-session comparison (IMPROVED/REGRESSED/STABLE), auto-capture on Play mode toggle
  * **Memory tab**: Mono heap bar (used/total MB), GC Gen0 counter with flash animation, texture memory, total managed

- **PerfGraphElement** — Reusable UITK VisualElement:
  * Line + fill graph rendering via Painter2D.generateVisualContent
  * Zero-alloc: ring buffer with CopyValuesTo(FrameSample[] dest) scratch array
  * Animator callbacks for smooth updates

- **PerfThresholds** — Color band classification:
  * Methods: FpsBand, FrameTimeBand, DrawCallBand, TriBand, MemBand (→ color enum)
  * Smooth Color32.Lerp gradients between bands
  * ColorForBand(band) → Color, FpsColor(fps) → Color, etc.

- **AnimatedCounter Label** — Exponential ease lerp (0.3s):
  * Updates on scheduler tick (paused when stable)
  * Zero allocation at rest

- **RecordIndicator** — UI Toolkit transition:
  * Scheduled USS state toggle for the red recording dot (#e94560)
  * Triggered when PerfWindow.Record = true and paused with the panel lifecycle

- **FrameRingBuffer Enhancement** (v0.61.0):
  * Added `CopyTo(FrameSample[] dest)` zero-alloc bulk export
  * Used by PerfGraphElement to extract samples for rendering

- **Styling**:
  * Animations use supported USS transitions plus short scheduled state changes
  * Colors from ArcadePalette: good=#3ad29f, warn=#e8a23a, crit=#e94560
  * UITK only (no IMGUI, Unity 6 Overlay API)

### Editor Help Tools Subsystem (v0.62.0)

**Error-Driven Development, Scene Health Audit, Auto-Wiring, Dry-Run Compilation:**

- **Error Resolver Toolbar Button** — Chat integration for compile error fixing:
  * **ErrorResolverButton.cs** (IToolbarButtonProvider): Adds "Fix Errors" button to MCPChatWindow toolbar
  * **MCPChatWindow.ErrorResolver.cs** (partial): InjectMessage(prompt) routes user-facing agent presets (Syntax, Semantic, Domain) as message contexts. Captures compile error context + code snippet, injects into chat history as human message
  * Enables error-driven development: compile, fix errors in Chat immediately

- **scene_health MCP Tool** — 7-check scene hierarchy audit:
  * **Focus modes**: all | hierarchy (>10 depth) | naming (CamelCase/reserved) | duplicates (sibling names) | origins (>5000 units away) | missing (scripts) | empty (GameObjects) | disabled (roots)
  * **Severity tags**: CRITICAL (blocking) | WARNING (performance) | INFO (conventions) | OK (clean)
  * **SceneHealthAnalyzer.cs** (C# helper): 7 static check methods — CheckMissingScripts, CheckDeepHierarchy, CheckBadNaming, CheckDuplicateSiblings, CheckEmptyObjects, CheckWorldOrigin, CheckDisabledRoots. Returns formatted output with path + issue description per check
  * Category: VERIFY (gated)

- **auto_wire MCP Tool** — Semantic ObjectReference field filling:
  * **3-Priority Matching Logic**: (1) exact field name match in scene, (2) contains field name (substring), (3) type-only match
  * **Dry-run Mode**: preview changes without applying (returns: wired count, ambiguous matches, no-match count)
  * **AutoWiringHelper.cs** (C# helper): FindMatchingObjects(field, priority) returns candidates, SetObjectReference(obj, field, value) writes to SerializedObject
  * Category: RW (mutating)

- **compile_preflight MCP Tool** — Dry-run C# validation:
  * **No Domain Reload**: validates via Roslyn in-process analysis (vs. Editor compiler)
  * **Syntax + Type Binding**: checks C# grammar + type resolution without invoking Unity's full compile pipeline
  * **Returns OK/ERR** with diagnostic details (line, column, message)
  * **RoslynLoader.cs** (C# helper): Extracted Roslyn assembly loading from CodeExecutor. Reflects mscorlib + UnityEngine via System.Runtime.InteropServices.RuntimeEnvironment
  * **RoslynWorkspace.cs** (C# helper): SyntaxTree → Compilation → Diagnostics pipeline. Creates Compilation with references, filters to CS errors/warnings
  * **RoslynFormat.cs** (C# helper): OK/ERR formatter — returns summary + line:col:message per diagnostic
  * Category: VERIFY (allowed during compile, zero side effects)

4. **Guards (C#)**
   - **Compile guard**: blocks all except ping, get_version, get_console, clear_console, screenshot, get_enabled_tools, compile_status
   - **Play Mode guard**: blocks mutating commands (changes would be lost)
   - **Runtime guard**: runtime commands blocked outside Play Mode
   - **Tool enable guard**: MCPSettings per-tool toggle (ping/get_version/get_enabled_tools always allowed)
   - **Fast-path commands** (bypass main thread): ping, get_version, status, get_enabled_tools, clear_console (v0.66.0)

5. **Per-command timeouts (C#)**: run_tests=130s, run_playtest=130s, batch=65s, wait_until/move_to/test_step=30s, default=25s

**Durable test runs** — consumer agents normally call `run_tests_wait`, which owns exact-run correlation and recovery. Unity Biome MCP repository and disposable-worker runs use `run_unity_tests.py`. Direct `run_tests(mode, filter, request_id)` is the low-level nonblocking API and returns `request_id`, `run_id`, `utf_guid`, and `state`; only explicit protocol clients should drive it:
```python
ack = await run_tests(mode="EditMode", request_id=stable_request_id)
# tests-started|request_id=...|run_id=...|utf_guid=...|state=dispatched
snapshot = await get_test_run(run_id=run_id)
```
The low-level caller resolves `START-UNKNOWN` with the same request identity,
never redispatches a dispatched run, and accepts only a reconciled terminal
snapshot. Uncorrelated `get_test_results` and `get_test_progress` are legacy
diagnostics, not verdicts.

6. **Post-mutation features**: console error capture, SuggestNext (recommends verification tool), auto-return parent subtree after create/delete

7. **In-Unity Chat Session Control (v0.19.0, F20–F30, v0.36.0 timeout messaging)**
   - **Stop button (F20)**: `CancelTurn()` stops the current relay-owned CLI session. Esc hotkey also triggers cancel. Button UI swaps from Send→Stop during streaming.
   - **Timeout Context Hints (v0.36.0)**: When a turn exceeds the configured inactivity timeout, the failure message includes the last tool name. Codex has a 300s minimum; other backends have a 30s minimum. Tracked via `_lastToolName` in EventHandlers.cs.
   - **Dead-Process Guard Message (v0.36.0)**: When backend process unexpectedly exits mid-turn, appends `[Process exited]` to transcript before finalizing. Surfaces connection loss (vs. timeout) as distinct error state. Clears turn flags to unlock reload.
   - **Transcript reload survival (F21, v0.63.0)**: TranscriptSerializer.cs persists chat history to SessionState at Library/MCP_ChatTranscript.txt. Format: 5-column line-delimited `KindInt|Base64(Text)|Base64(ChipsData)|Base64(LlmPayload)|Base64(ImagePath)` with Kind enum (User=0, Assistant=1, Tool=2). Tool-call entries (Kind.Tool) serialize with tool name + args in Text column. Image paths (P1) stored as ImagePath column (first image captured). Backward-compat: old 3-4 column format missing ImagePath/LlmPayload columns fallback to null. On domain reload, `MCPChatWindow.OnDisable()` saves transcript to SessionState. History restored on reopen via `AppendToolChip()` from `_entries` list, preserving all entry types + styling.
   - **Settings persistence (F22–F24)**: AutoScroll toggle persisted in EditorPrefs, dropdown selections (Backend, Model) cached, all restore on domain reload / window reopen.
   - **Chip correctness (F24–F26)**: @Object duplicate fix via global forward search instead of narrow offset window. Direct Clear dialog (no submenu). Drag-drop MonoScript creates dual-chip (@Object + @Script).
   - **Domain reload trigger (F27)**: `_needsRefresh` flag set when code-editing tool result arrives; consumed in DrainAndRender to call `AssetDatabase.Refresh(ForceUpdate)` once per drain cycle. Ensures .cs edits via chat backend trigger recompilation.
   - **Current Chat relay**: C# uses only `RelayBackend`; Python `chat_relay.py`, `backend_def.py`, and `stream_transform.py` own backend processes and event normalization. See [Chat Relay System](#chat-relay-system-v0666-replaces-clibackendbase--5-backend-variants-with-unified-relaybackend).
   - **External drag/drop (F29)**: FolderChipProvider accepts files/folders from Finder. ProcessExternalPath() static method routes DragAndDrop.paths into chip context.
   - **Input height (F30)**: Default input field height 4 lines (CompactH=117f). Compute() clamps via minH=min(CompactH, maxH) to prevent degenerate clamp in tiny windows.
   - **Chat Component Fields (v0.59.0, extended v1.28.0)**: Right-click Inspector properties or components → "Add to MCP Chat" menu entries. **PropertyContextMenuBridge**: wires EditorApplication.contextualPropertyMenu hook (v1.28.0: expanded to support ScriptableObject targets in addition to scene GameObjects; all property context menu items consolidated under single submenu with raised priority). **ComponentChipProvider** (priority 125, key "component"): chip for entire component summary (format: `goPath|CompType`). **FieldChipProvider** (priority 130, key "field"): chip for single field (format: `goPath|CompType|fieldName`; v1.28.0: now resolves asset-path chips for material, texture, and ScriptableObject fields in addition to scene paths). Both registered via ChipKindRegistry.EnsureBuiltIns(). **ChipPropertyFormatter DRY**: unified serialized property rendering (UnityEvent expansion, ObjectReference disambiguation, value display). **Copy Reference Action (v1.28.0)**: "Copy Ref" menu item on property/field context menu copies resolved reference (path or asset ID) to clipboard. **SerializedObject disposal**: Using statements prevent memory leaks during repeated inspector interactions.

9. **Per-Backend Model Selection + Token Cost Display + Multi-Scene Chat Refs (v0.30.4, expanded v0.30.5)**
   - **Model Selector (Plugin v0.30.5)**: **MCPChatWindow.Selector.cs** with expanded presets per backend: **Claude** (Default, Fable 5, Opus 4.8/4.7/4.6, Sonnet 4.6, Haiku 4.5, Custom), **Codex** (Default, GPT-5.5, GPT-5.4/5.4-Mini, o3-pro, o3, o4-mini, GPT-4.1/4.1-Mini, Custom), **Gemini** (Default, 3.5 Flash, 3.1/3 Pro Preview, 3 Flash Preview, 2.5 Pro/Flash/Flash Lite, Custom). **ModelPresets.cs (NEW, v0.30.5)** extracted from BackendConfig: ModelPresetEntry, ModelPresetsConfig, ModelPresetDefaults.All (hardcoded fallbacks per BackendKind). **BackendConfigStore.GetPresetsForKind()** looks up Library/MCP_ChatBackendConfig.json ModelPresets field; not found → falls back to hardcoded defaults. **Result**: users can override model lists via config file without recompile. Dropdown rebuilt on backend switch. EditorPrefs persistence: `MCPChat.SelectedModel.{BackendKind}` (per-backend state). Custom model entry via text field. **Tests**: 44 new BackendConfigStoreTests (preset lookup, fallback, merge), 231 ModelSelectorTests (dropdown state, preset selection, custom entry, persistence across domain reload).
   - **Token Cost Display (Plugin)**: **TokenFormat.cs** extended with `FormatReadout()` → displays session cost (`$0.0020`) alongside token counts. Computes cost via `EstimatedCost(input_tokens, output_tokens)` with configurable $/1k rates (per backend). **Null-safe**: guards missing token data, avoids division-by-zero. **Tests**: 12 TokenFormatTests verify cost calculation, zero-token safety, missing data handling.
   - **Asset validate_move (Server v0.8.2)**: New `asset(action="validate_move", src="...", dst="...")` dry-run validation before asset move operations. Checks path existence, destination writability, conflict detection. Returns `{"ok":true}` or error details. Prevents silent failures on renames/refactors. **Tests**: 15 test_server_asset.py new scenarios.
   - **Asset Export/Import Enhancements (Server v0.8.2, Plugin v0.35.0)**: `export_package` gains `include_deps` parameter (default true) — skip dependencies if false for token optimization on large packages. `import_package` now returns manifest: list of imported asset paths. **AssetDatabaseHelper.cs extended** with dependency filtering + import result tracking. **Tests**: 6 new test_server_asset.py scenarios.
   - **Multi-Scene Chat Reference Fix (Plugin + Server v0.8.2)**: Fixed scene-qualified object paths in chat. **IsAssetPath** now strict: returns false for "Scene:/" prefix (asset paths only "Assets/" prefix). **SceneObjectFinder** parses `"SceneName:/"` to extract scene name + path separately. Chips display `[Scene] name` for multi-scene objects. **Tests**: 74 MultiSceneChipTests (parsing, display, navigation).

12. **Plugin Extensibility API + Image Drag-Drop + Asset Viewers (v0.34.0, v0.63.0: MenuOnly DIM)**
   - **Plugin Extensibility (Settings/Toolbar/Panels, CLI v0.34.0)**: New public seam interfaces for plugins to extend chat UI without core edits:
     * **ISettingsProvider**: Plugins register custom settings UI pages (e.g., `OnBuildUI()` returns VisualElement foldout, `SectionName`/`Priority`)
     * **IToolbarButtonProvider (v0.63.0 MenuOnly)**: Plugins add toolbar buttons with click handlers and icon. New default interface member `bool MenuOnly => false;` allows selective button repositioning without backward-compat breaks. Providers override to `MenuOnly => true` to show only in hamburger menu (≡). MCPChatWindow.Plugins.cs filters: `if (p.MenuOnly) continue;` gates toolbar rendering; menu separately adds MenuOnly providers.
     * **IPanelProvider**: Plugins register side panels (dock + overlay support)
     * **Registry classes**: `SettingsProviderRegistry`, `ToolbarButtonRegistry`, `PanelProviderRegistry` — all use `Register()` + discovery via `[InitializeOnLoad]` pattern
     * **MCPChatWindow hook points**: Settings foldout + toolbar + left/right panels all query registries on window open, render provider content dynamically
     * **Tests**: 72 PluginSettingsInjectionTests, 105 PluginToolbarButtonTests (button state, click handlers, lifecycle)

   - **Image Drag-Drop + Clipboard Paste (CLI v0.34.0)**:
     * **ClipboardImageReader.cs** (142 LOC): Platform-specific clipboard image read (macOS: NSPasteboard Foundation PInvoke, Windows: CF_DIB check stub, Linux: xclip subprocess). Returns PNG bytes or null, never throws.
     * **ImageAttachmentStore.cs** (96 LOC): Stores pasted/dropped images with temp file lifecycle. `AttachImage(bytes)` → saves to Library/.unitymcp_images/, returns relative path. `GetAttachedPaths()` → list of stored images. `Cleanup()` → removes stale files on session end.
     * **MCPChatWindow.ClipPaste.cs** (partial): Wires clipboard paste via Ctrl+V in input field. Detects image mime-type, attaches, emits chat event with image reference.
     * **MCPChatWindow.Chips.cs** (partial): DragAndDrop.paths routing — external files/folders (Finder drag) detected, filtered for images, attached same as paste.
     * **UserTurnBuilder.cs** extended: Embeds image references in user turn JSON as `image_url` blocks (Claude SDK protocol).
     * **Tests**: 37 ClipboardPasteTests (platform detection, mime-check, file write), 154 ImageDragDropTests (path filtering, attachment, multiple images), 76 UserTurnBuilderImageTests (turn JSON serialization with images)

   - **Inline Image Thumbnails in Chat (View v0.34.0)**:
     * **InlineImageThumbnail.cs** (70 LOC): Renders thumbnail strips in chat paragraphs (max 100px height, click→full viewer)
     * **MixedParagraphRenderer** extended: Detects `[img src="..."]` markdown, calls InlineImageThumbnail for rendering
     * **Tests**: 116 InlineImageThumbnailTests (sizing, fallback on missing image, click navigation)

   - **Prefab Preview Window (View v0.34.0)**:
     * **PrefabViewerWindow.cs** (151 LOC): EditorWindow displaying prefab 3D preview (camera orbit, zoom controls)
     * **PrefabPreviewLoader.cs** (82 LOC): Instantiates prefab in temporary scene, loads preview scene, destroys on close
     * **Wired**: Asset chip right-click "View" or MCPChatWindow chip click (via BuiltInChipProviders.ViewerLauncher seam) routes to PrefabViewerWindow.Open()
     * **Tests**: 198 PrefabViewerWindowTests (window lifecycle, prefab loading, camera controls, cleanup)

   - **3D Asset Viewers (View v0.34.0)**:
     * **AssetViewerFactory.cs** (83 LOC): Registry + factory for extensible media viewers. Wires WindowType → `IAssetViewer` implementations
     * **ModelViewerWindow.cs** (151 LOC): Displays .fbx/.obj/.blend/.dae models (instant load via import settings, camera orbit/zoom)
     * **SpriteViewerWindow.cs** (78 LOC): Displays sprite textures with grid overlay (100% zoom default, fit-to-window toggle)
     * **AudioViewerWindow.cs** (142 LOC): Plays audio clips (play/pause/loop, duration display, waveform placeholder)
     * **AudioUtilProxy.cs** (66 LOC): Wrapper for `AudioUtil.GetDurationInSamples()` (Editor-only API, reflection-based fallback for older Unity)
     * **IAssetViewer interface**: Plugins implement to add custom viewers (e.g., video player, shader preview)
     * **BuiltInChipProviders extended**: `AssetChipProviderBase.ViewerLauncher` seam — wired by AssetViewerFactory [InitializeOnLoad]. Chip Navigate() checks `ViewerLauncher?.Invoke(path)` first; if true, viewer handled; else falls back to ping
     * **Tests**: 224 AssetViewerFactoryTests (factory dispatch, plugin registration, viewer lifecycle), 198 PrefabViewerWindowTests (see above)

   - **Chip Kind Extensions (View v0.34.0)**:
     * **ChipKindKeys extended**: Added Image, Model, Audio (beyond existing Hierarchy/Scene/Script/Prefab/Material/Texture/ScriptableObject/Asset/Folder)
     * **BuiltInChipProviders extended**: `ModelChipProvider` (priority 450, handles .fbx/.obj/.blend/.dae), `AudioChipProvider` (priority 550, handles .wav/.mp3/.ogg/.aiff), `ImageChipProvider` (priority 50, handles external .png/.jpg/.bmp/.gif/.webp/.tiff — obj==null only)
     * **Tests**: 84 new tests for new providers (MdBlock rendering, chip detection)

   - **ProviderRegistry Consolidation (CLI v0.34.0)**:
     * **ProviderRegistry.cs** (82 LOC, new): Base class for extensible provider registries (DRY consolidation across Settings/Toolbar/Panel registries). Single `Register()` + `Resolve()` pattern, optional priority ordering
     * **KeyRegex hoisting**: Moved `_KeyRegex` to non-generic companion to avoid static-in-generic reflection issues (C# generic type safety)
     * **Tests**: 57 ProviderRegistryTests (concurrent registration, key uniqueness, priority ordering)

   - **Tests Summary (v0.34.0)**:
     * Python: No new tests (0 changes to server/)
     * C#: 1402 new tests across CLI + View assemblies
       - CLI: 214 KimiArgBuilder + 243 KimiParser + 222 OpenCodeArgBuilder + 273 OpenCodeParser + 214 BuiltInChipProviders + 57 ProviderRegistry + 188 ImageAttachmentStore = 1411 tests
       - View: 224 AssetViewerFactory + 198 PrefabViewerWindow + 154 ImageDragDrop + 116 InlineImageThumbnail + 105 PluginToolbar + 72 PluginSettings + 37 ClipboardPaste = 906 tests
       - Total EditMode: ~3000+ green (was 2623)

10. **Sprint 1B: Assembly Split + Interactive Permissions (v0.29.2)**
   - **Chat Assembly Split (asmdef)**: UnityMCP.Editor.Chat split into two: `UnityMCP.Editor.Chat.CLI` (protocol, parsing, backends, control flow) and `UnityMCP.Editor.Chat.View` (UI windows, rendering, cards). CLI assembly compiles when main plugin is broken (zero View dependencies); View always depends on CLI. Enables frontend reload before backend fully healthy. Asmdef references one-way: View → CLI → Editor core.

10. **Chat Resilience Sprint (v0.30.5): Codex Silent Abort + Inactivity Watchdog**
   - **Inactivity Watchdog (v0.30.5)**: `MCPChatWindow.Drain.cs` tracks `_lastEventTime` and compares it with the configured inactivity timeout while a backend is running. Codex has a 300s minimum; other backends have a 30s minimum. It resets on turn start and every drained event.

### UI Animation System (docs-critical-review branch)

**Editor UI Rebrand + Shared building blocks (v1.28.0):**

- **`BiomeLabel.cs`** — Centralized single source of truth for user-facing display strings and log tags (v1.28.0). Static class with EditorPrefs toggle `UseEmoji` (default true). Exposes:
  * `DisplayName` — "🧬" (emoji mode) or "Biome" (text mode)
  * `Tag` — "🧬" or "Biome" for log prefixes
  * `Changed` event — fires on main thread when toggle is changed
  * Affects all editor UI: Setup Wizard menu, Hub window title, Settings foldout title, Chat window label, MCP status label, all console log output. Changed event wired by MCPSettings.UI to live-update all visible labels.

**Shared building blocks and particle effects for all Biome editor surfaces (MCPHub, Settings, Chat, Wizard).**

- **`BiomeUI.cs`** — Static helper layer for all Biome editor windows:
  * `LoadCoreStyles(root, includeWizard)` — loads MCPHub.uss + MCPSettings.uss + ArcadeAnim.uss in one call (replaces 6-line boilerplate in every window)
  * Button factories: `PrimaryButton`, `SecondaryButton`, `QuietButton` → consistent BEM class composition
  * `Section(title, out body)` / `StatusLabel(text)` / `SetStatus(label, text, state)` — structural helpers
  * `SetExclusiveClass(element, activeClass, ...classes)` — single-call exclusive CSS class toggle (replaces Remove/Add pairs)
  * `ShakeX(element)` — 5-frame horizontal shake via `schedule.Execute`, GPU-friendly translate

- **`BiomeParticleBurst.cs`** — Two pooled particle classes, zero permanent update loops:
  * **`BiomeParticleBurst`** (12 particles) — event burst: radial disperse on `Emit(host)`, particles pooled per host element, generation counter prevents stale completions
  * **`BiomeAmbientParticles`** (9 particles, 8 patterns) — continuous ambient field driven by `ArcadeAnim.SmoothLoop`; each particle has a seeded `MotionProfile` (incommensurate sin/cos harmonics → smooth non-looping paths); pauses on `DetachFromPanelEvent`. Patterns: DataFlow, Tools, Shield, Chat, Sampling, Updates, Ecosystem, Timeline — each applies per-pattern motion modifier. `Attach(host, pattern, entryBurst)` factory; entry burst fires 220ms after `AttachToPanelEvent`. All elements use `UsageHints.DynamicTransform | DynamicColor` for GPU layer promotion.

- **`BiomeToggleGroup.cs`** — Accessible category disclosure widget:
  * Tri-state master toggle (`toggle-mixed` CSS class when partially enabled)
  * Per-item rows with individual Toggle controls, all wired to external `getValue`/`setValue`/`setAll` delegates
  * `Filter(query)` — case-insensitive substring filter, auto-expands group when filtering
  * Replaces raw Foldout usage in permissions and tool-enable UIs

- **`EcosystemHeaderAnim.cs`** — Shared semantic header for plugin/version pages:
  * `BuildPlugins()` — 7-node graph: nodes light up per registered plugin with `HasSettingsUI`, animated pulse sweeps across active nodes, polls `PluginRegistry.All` every 900ms
  * `BuildVersions()` — 7-node timeline scanner: sinusoidal scan beam sweeps nodes, used by VersionPickerPage
  * `SetVersionIndex(root, index, total)` — maps list selection to node highlight + `ArcadeAnim.PulseOnce`
  * Both variants attach `BiomeAmbientParticles` (Ecosystem and Timeline patterns respectively)

**Modified surfaces:**

- **`MCPHubUI.cs`** — Hub home page now wraps content in `ScrollView(Vertical)` with `biome-page-scroll` / `biome-page-content` classes. Style loading replaced by `BiomeUI.LoadCoreStyles`. Status labels use `BiomeUI.StatusLabel`.
- **`SettingsPageFactory.cs`** — All pages gain `biome-page` class. Plugins page now renders `EcosystemHeaderAnim.BuildPlugins()` header; plugin detail cards expand inline (accordion) instead of nav-push. Chat page scroll uses `biome-page-scroll` class.
- **`StatusAmbientAnim.cs`** — Added `UsageHints.DynamicTransform | DynamicColor` to scanline/sonar/dots for GPU layer promotion. Grid state changes gated behind `previousState` guard (skips redundant class swaps). Replaced manual `Remove/AddToClassList` pair with `BiomeUI.SetExclusiveClass`. Per-dot references captured for future per-dot animation.
- **`MCPChatWindow.FlowBar.cs`** — Particle-based data stream replaces CSS-class sweep animation: 7 pooled `VisualElement` particles + aura layer, all driven by `ArcadeAnim.ControlledSmoothLoop(_flowBar, AnimateFlowBar)`. Sending vs. receiving phases modulate particle motion rather than toggling fill classes.

## Reload Stability (v0.42.0, commit 39672a0)

Root cause: v0.42.0 asmdef split (7→9 assemblies) amplified 3 latent bugs into crashes and stale DLLs. Addressed via 13 surgical fixes + 39 regression tests.

### Crash Prevention

1. **Socket Poll Freeze (MCPStatusWindow)**: OnDisable now stops Socket.Poll() cleanly during domain reload (was blocking main thread indefinitely)
2. **Reload Port Leak (ReloadMiniServer)**: Client tracking + graceful close on Stop prevents fd exhaustion + reload freeze
3. **Window Layout Crash**: [MovedFrom] attributes on MCPStatusWindow/MCPSettings/etc moved across assemblies (Editor assemblies moved to Wizard, was losing window layout)
4. **Use-After-Free (TeardownCore)**: Drains `_mainThreadQueue` before domain unload (was referencing deallocated memory post-domain)
5. **Tundra Digest Cache**: Removed unconditional deletion of digestcache (SIGABRT in RegisterAssemblyDefinition during reload)

### Stale DLL Detection Pipeline

**ComputeStamp (compile_state.py)**: Now iterates all UnityMCP.* assemblies (was checking only one assembly, blind to breakage in other .dlls)
- Detects stale .mvfrm files via MVID comparison (per assembly)
- Aggregates into single STALE verdict if ANY assembly diverges
- **v0.66.0: GetDllFreshnessToken ~ prefix filter** — skips files starting with ~ (Unity ignores them; prevents false-positive stale detection from editor temp files)

**ReloadGuard (C# CLI assembly)**:
- Constructor barrier calls `AssetDatabase.Refresh()` on init
- `ForceUnlock()` triggers additional `AssetDatabase.Refresh()` + `RequestScriptCompilation()`
- Exception-safe: asymmetric lock rollback in `OnTurnStarted` to prevent deadlock

**PID Liveness Check (lockfile.py)**:
- Port file discovery now verifies process is alive via `_is_zombie(pid)` check
- Blocks stale PID lockfiles from ghosting commands (fast server restart without wait)

**TCP Probe in is_startup_in_progress (server.py)**:
- False "Unity busy" detection fixed by probing TCP port during startup detection
- Distinguishes real startup grace from transient disconnect

### Reload Timing

**DOMAIN_RELOAD_EXPIRY_S**: Increased 30s → 90s → 120s for 9-assembly reload window (v0.66.0)
- v0.42.0 increased assembly count 7→9 (main, Chat CLI, Chat View, Wizard, Reload, reload tests, Chat tests, Wizard tests)
- v0.66.0 further increased to 120s to accommodate large file compiles and cross-asmdef compile checks
- Longer window accommodates full serialization + reload + recompile cycle
- Bridge heartbeat retry logic uses this window to avoid false "domain stuck" timeouts

**_DISCONNECT_WINDOW_S**: Also 120s (synchronizes with reload window, v0.66.0)

### Asmdef Isolation

**Wizard.asmdef** (v0.42.0): `autoReferenced: false` enables independent compilation when core/Chat broken
- Prevents Wizard compile errors from blocking MCP startup (diagnostic UI still available even during crashes)

### Test Infrastructure (39 regression tests, commit 39672a0)

**Python** (`test_reload_stability.py`, 300 LOC):
- ComputeStamp multi-assembly detection (test_compute_stamp_detects_stale_in_any_assembly)
- PID liveness fallback (test_port_discovery_skips_zombie_pids)
- TCP probe avoids false startup detection (test_is_startup_probes_tcp)
- DOMAIN_RELOAD_EXPIRY edge cases (test_domain_reload_expiry_90s_holds)

**C#** (145 LOC across 3 test files):
- ReloadMiniServerTests: client tracking, graceful shutdown (85 tests)
- ReloadGuardTests: exception safety, ForceUnlock flow (98 tests)
- MCPStatusWindowSchedulerTests: OnDisable stop behavior (37 tests)
- MovedFromAttributeTests: layout crash isolation (25 tests)
- ReloadStabilityTests (Wizard, Editor): full pipeline integration (91 tests)

**Verification**: 39 new regression tests all green on macOS/Windows (domain reload stress: 100+ recompile cycles)

## Icon Canvas Design System (v0.55.10)

**Unified procedural icon rendering for theme-agnostic UI:**

- **IconCanvas.cs** (163 LOC): Procedural builder (18×18 canvas, 2px strokes, near-white ink 0.92). Fluent API: Line/Poly/Closed/Circle/Disc/Point/Rect/RoundRect. Distance-to-segment rasterizer with round-cap lines. Theme-invariant: readable on both Unity dark (#383838) and light (#C8C8C8) backgrounds.
- **AnnotationIcons.cs refactor** (v0.55.10): Reduced from 480→285 LOC by migrating to IconCanvas API. Procedurally renders Pen/Line/Arrow/Rect/Ellipse/Text/Erase/Save toolbar icons + region mode indicators (Lasso/Rectangle/Circle/PointByPoint).
- **RegionIcons.cs refactor** (v0.55.10): Unified regional Lasso/Rect/Circle indicators into IconCanvas pattern.
- **Tests**: 144 IconCanvasTests covering all drawing primitives, rasterization edge cases, pixel correctness.
- **Architecture**: Single-source-of-truth for visual identity. Eliminates asset drift (icon.png hardcoded colors vs. theme change). Cached after first render (no per-frame cost).

## Arcade Animation System (v0.52.0+)

**Unified animation primitives for UI consistency:**

- **ArcadePalette.cs** — Centralized color constants (Up=#3ad29f, Listen=#e8a23a, Down=#6e2b3a, Accent=#e94560) + `StateClass` seam for connection status color
- **ArcadeAnim.cs** — Shared animation library with USS class toggles and short transform sequences:
  * `AnimateClass(el, hiddenClass, visibleClass, delayMs)` — generic class-toggle animator base
  * `FadeIn`, `SlideInRight`, `ShakeX`, `PulseOnce`, `FlashClass`, `GlowPulse` — common effects
  * `CountUp` — numeric label animation (0 → N over duration)
  * `StaggerFadeIn`, `Typewriter` — sequential element effects
- **ArcadeAnim.uss** — Shared UI Toolkit transitions. Web CSS `@keyframes` and
  `animation-name` are intentionally forbidden because UI Toolkit does not
  support them.
- **Per-window HeaderAnims** — DRY builders following `VisualElement Build()` pattern:
  * `SamplingHeaderAnim.Build()` — 7-bar frequency analyzer for Sampling page
  * `StatusAmbientAnim.Build()` — scanline + grid + sonar ring overlay for Status window
  * `WizardStepAnim.cs` — slide transitions + progress bar for Setup Wizard
  * `WizardJourneyAnim` (WizardAmbientAnim.cs) — 4-node animated progress track shared by every wizard screen; packet + aura glide between nodes via `ArcadeAnim.SmoothLoop`
  * `SkillsInstallAnim` (WizardAmbientAnim.cs) — module-stream animation for InstallSkills screen; speed adjusts on `SetWorking(true/false)` during active install
- **WizardUI.cs** — DRY factory for wizard navigation buttons (`Primary`, `Secondary`, `Quiet`) and `Navigation(back, ...actions)` layout row; delegates to `BiomeUI` for base button styles
- **Architecture:** Effects use USS class toggles and element-owned schedulers.
  Recurring state probes run at restrained cadences, transform-heavy elements
  declare usage hints, and schedulers pause when their visual is detached.
  Unity theme variables provide the neutral palette; arcade colors are semantic.
- **Tests:** 27 NUnit tests (ArcadePaletteTests 7, ArcadeAnimTests 6, SamplingHeaderAnimTests 3, StatusAmbientAnimTests 5, WizardStepAnimTests 5, BiomeParticleBurstTests 4) — note: individual file counts may differ from totals due to `[TestCase]` expansion; use `[Test]` grep for raw method count

## Playtest Composer Subsystem (v0.75.0)

**Visual DSL builder: drag GameObjects onto the Composer and auto-generate playtest scripts without writing DSL by hand.**

Menu: `MCP/Playtest Composer` (Shift+Alt+P). Rewritten from IMGUI to UI Toolkit in v0.75.0.

### Components

- **PlaytestComposerWindow.cs**: UI Toolkit `EditorWindow`. `ListView` with `DynamicHeight` virtualization. Toolbar: Run / Save / Load / Copy DSL / Copy for AI / + Smart Command. DSL preview flushes every 400 ms via `schedule.Execute`. Delegates to: PlaytestDslExporter (build DSL), PlaytestStepValidator (gate Run button), ComposerStateStore + PlaytestFileHelper (persistence).

- **VisualStep.cs** (`[Serializable]`): Single-step data model. 14 fields: `type` (StepType), `description`, `path`, `position`, `delay`, `query`, `op`, `value`, `timeout`, `component`, `method`, `args`, `message`, `abortOnFail`. `Clone()` for duplicate.

- **PlaytestDslExporter.cs** (pure static, no Unity API — fully NUnit-testable): Converts `List<VisualStep>` → DSL string. `Export(steps, globalAbort)` prepends `ABORT_ON_FAIL` when set. `StepToDsl(step)` prepends `DESC …` when description is non-empty. `FromParsed(PlaytestStep)` provides roundtrip from parser (used by Load). Handles 17 `StepType` cases.

- **PlaytestStepElement.cs** (UITK `VisualElement`, 282 LOC): Per-row visual editor for one `VisualStep`. `Bind(step, onDirty, onDuplicate, onDelete)` / `Unbind()` lifecycle. Shows inline validation error. Fields adapt to `StepType`.

- **PlaytestStepValidator.cs** (pure static): `GetValidationError(step)` → string|null per type. `IsScriptValid(steps)` → bool (gates Run button).

- **PlaytestSmartDrop.cs** (static): `ShowActionMenu(go, onCreated, onFinished, anchor)` — `GenericDropdownMenu` with 10 actions: Move, Teleport, Assert, WaitUntil, Invoke, Monitor, Set, Click, Capture, AssertNear. Uses `ComponentSerializer.GetPath` for scene path.

- **PlaytestDropHelper.cs** (static, 159 LOC): `AttachMultiDnD(list, onDrop)` — multi-object drag-and-drop onto `ListView`. `ShowComponentPicker(go, step, type, onFinished, anchor)` — `GenericDropdownMenu` cascading component → field/method selection.

- **ComposerStateStore.cs** (static): Persists/restores `ComposerState` (steps + globalTimeout + globalAbort + lastFilePath) to `Library/PlaytestComposerState.json` via `JsonUtility`. Survives domain reload. `_testOverride` seam for NUnit.

- **PlaytestFileHelper.cs** (static, 42 LOC): Save/Load `.playtest` text files via OS file dialog. Load parses DSL → `VisualStep[]` via `PlaytestParser` + `PlaytestDslExporter.FromParsed`.

- **PlaytestComposerButton.cs** (Chat.CLI): `IToolbarButtonProvider` (`MenuOnly=true`, `Order=20`). Registers "Composer" in MCPChatWindow hamburger menu (≡). Opens PlaytestComposerWindow.

### Smart Command Window (NlCommandWindow)

`NlCommandWindow.Show(steps, list, onDirty)` — modal `EditorWindow` for natural-language step entry. Text passed to `NlComposerBridge` → `NlStepParser` → VisualStep(s) appended to composer list.

### Tests (v0.75.0)

| File | Coverage |
|------|----------|
| PlaytestComposerTests.cs | Window state, step lifecycle (~116 tests) |
| ComposerStateStoreTests.cs | Load/Save/path override (~98 tests) |
| PlaytestDropHelperTests.cs | DnD + component/field pickers (~225 tests) |
| PlaytestDslExporterTests.cs | All 17 StepTypes + roundtrip (~412 tests) |
| PlaytestStepValidatorTests.cs | Per-type validation rules (~305 tests) |

## Level Design Toolkit (v0.46.0+, F1-F5)

**Chat-Integrated Visual Tools:**

1. **F1: Token Counter + Context Progress Bar** (replaces USD cost display)
   - **ModelContextWindows.cs** — Context window size per LLM (hardcoded: Fable 5 → 1M, GPT-5/5.4 → 1M, GPT-4.1 → 1M, gpt-4 → 128k, o3/o3-pro/o4 → 200k, Claude/Opus/Sonnet/Haiku → 200k, Gemini → 1M, Kimi/Moonshot → 128k, Codex model → 192k, Codex fallback → 1M)
   - **TokenFormat.cs** — Extended `FormatReadout()` displays `↑input ↓output | ▓▓▓▓░░░░░░ 40%` (input+output count + progress fill as Unicode bar)
   - **ContextProgressBar.cs** — UIToolkit visual bar with 20% output reserve (OutputReserve = 0.8f); bar hits 100% at 80% input fill to account for model output tokens
   - **TokenResetTests** — Verify counter resets on backend/model/inactivity-timeout switch

2. **F2: Component Field Chips** — Right-click Component header in Inspector → "Attach Field" dropdown
   - **FieldChipProvider.cs** — Chip provider for individual component fields (priority 200, between Script and Scene)
   - **FieldContextMenu.cs** — Inspector context menu listener, routes field selection
   - **ChipKindKeys.cs** — New ChipKind: `Field` + `AnnotatedScreenshot` (supports v0.46 annotation flow)
   - **FieldChipProviderTests, FieldContextMenuTests** — Full menu + selection flow coverage

3. **F3: Native Screenshot Button + Chip**
   - **ScreenshotService.cs** — Wrapper around existing ScreenshotCapture, captures camera view to file
   - **ScreenshotToolbarButton.cs** — Toolbar button (📷 icon), OnClick calls ScreenshotService, emits chip + injects into chat
   - **ScreenshotServiceTests, ScreenshotToolbarButtonTests** — Service + button lifecycle

4. **F4: Full Annotation Editor** (Annotation/ folder, 11 files)
   - **AnnotationCanvas.cs** — Drawing surface (Texture2D-backed, pixel-level rasterization)
   - **AnnotationCommand.cs** — Command pattern: pen/line/arrow/rect/ellipse/text/erase (base class + 7 subclasses)
   - **AnnotationHistory.cs** — Undo/redo stack (command list, index tracking)
   - **AnnotationToolState.cs** — Active tool + brush color/size state (mutable, live-updated)
   - **AnnotationToolbar.cs** — Tool palette + color picker + undo/redo buttons (UIToolkit buttons)
   - **AnnotationEditorWindow.cs** — EditorWindow host (canvas + toolbar side-by-side)
   - **AnnotationRasterizer.cs** — Rasterize commands to Texture2D (line bresenham, circle/ellipse scanline fills)
   - **AnnotationDrawer.cs** — Preview command strokes (GL lines, circles, text)
   - **AnnotationCompositor.cs** — Flatten command stream to final PNG (rasterize all + encode)
   - **AnnotationIcons.cs** — Procedural vector icons for toolbar buttons + RegionTool overlay (230 LOC: Pen/Line/Arrow/Rect/Ellipse/Text/Erase/Save icons via Painter2D)
   - **AnnotateToolbarButton.cs** — Chat toolbar button to launch AnnotationEditorWindow
   - **AnnotatedScreenshotChipProvider.cs** — Chip kind for annotated images (markdown `![](path.png)` with annotation metadata JSON)
   - **Tests**: 10+ NUnit test files covering all components (canvas rasterization, undo/redo, metadata serialization)

5. **F5: Raycast World Coordinates** in Annotation Metadata
   - **AnnotationRaycaster.cs** — Scene raycast from mouse position + camera (returns world XYZ + GameObject + hit distance)
   - **AnnotationMetaWriter.cs** — Embeds raycast hits into annotation metadata JSON (for chat reference: "annotated pixel at world 15.2, -3.5, 42.1 on Player")
   - **Tests**: AnnotationRaycasterTests (228 cases), AnnotationMetaWriterTests (64 cases) covering raycast edge cases + metadata serialization

6. **Region Icons** (RegionIcons.cs, moved to RegionTool/Rendering/)
   - Procedural Painter2D vector rendering for Lasso/Rect/Circle/PbP tools + overlay UI
   - Replaces hardcoded icon assets, resolves v0.46 black-flash issue

7. **Region hasFocus Guard** (RegionRenderer.cs)
   - Prevents black GL rendering flash when Scene View loses focus
   - Checks EditorGUIUtility.editingTextField to hide region overlay during text input

8. **Chip Thumbnails** (ChipPillFactory.cs)
   - Inline thumbnail previews (32x32px) for image chips in both input and response
   - Lazy-load from ScreenShots/ directory, fallback graceful if file missing

9. **Configurable Inactivity Timeout**
   - Moved from hardcoded 90s (Claude) / 300s (Codex) to **BackendConfigStore** (default 180s)
   - **ChatSettingsSection** → General → Inactivity timeout slider (30–600s)
   - Persists to Library/MCP_ChatBackendConfig.json, per-backend override available

## Tool Categories

**v0.83.0**: 18 legacy themed categories → 8 canonical keys. Old names remain as aliases in `_CATEGORY_ALIAS` (full backward compat). CORE shrank 24→15 (9 demoted to SYSTEM tier1). TIER1 = CORE + per-ToolSpec `tier1=True`. SOURCE OF TRUTH: `tool_specs._SPECS[name].category`.

**P-12440 Phase 1**: CORE reduced 15→13 (4 demoted, 2 promoted); TIER1-only reduced 32→20 (13 tools demoted to themed categories). Total visible tools: 47→33 (CORE + TIER1). Promotes compile_preflight, mcp_status to CORE. Demotes apply_scene_change, scene_change_plan, verify_after_change, resolve_scene_refs from CORE to TIER1, and 13 other tools from TIER1 to themed categories (alias_status, ask, ask_user, configure_objects, console_mark, get_console_since, get_test_results, get_test_run, lint_playtest, release_smoke, resolve_test_request, setup_objects, undo_last).

### CORE (13, always visible, full schema)
batch, compile_preflight, create_object, editor, execute_code, get_compile_errors, get_component, get_console, get_hierarchy, inspect, manage_component, mcp_status, set_property

### Category: SCENE (29)
apply_scene_change†, autofit_collider, check_colliders, configure_objects, delete_object†, find_objects, get_components_list, get_object_detail, get_selection, get_spatial_context, navmesh_query, object_diff, ping_object, region_clear, rename_object, scene†, scene_change_plan†, scene_diff, scene_environment, search_scene†, set_active†, set_material, set_parent†, set_properties, set_property_delta, set_sibling_index, setup_objects, spatial_query, transfer_object

### Category: COMPONENTS (4)
auto_wire, references, unwire_event, wire_event

### Category: ASSETS (7)
asset, material, material_audit, prefab, project_settings, scriptable_object, shader

### Category: MEDIA (14)
analyze_lod_culling, animation, animator, create_ui, particle, render_analyze, screenshot†, screenshot_baseline, screenshot_compare, set_rect, timeline, ui_intent, validate_layout, vfx_intent

### Category: VERIFY (9)
await_compile†, compile_preflight, diagnose, lint_scene_refs†, resolve_scene_refs, scan_scene, scene_health, validate_references†, verify_after_change†

### Category: RUNTIME (17)
console_mark, debug, debug_animator, debug_physics, get_console_since, get_frame_stats, get_memory, get_metrics, get_watches, invoke_method, move_to, profile, query_state, set_runtime_property, snapshot, wait_until, watch

*(get_perf removed v0.85.1 — use get_frame_stats)*

### Category: TESTS (15)
export_playtest_aliases_to_defs, get_test_count, get_test_progress, get_test_results, get_test_run, lint_playtest, lint_playtest_suite, resolve_test_request, run_playtest†, run_playtest_suite, run_tests†, run_tests_wait†, sync_playtest_aliases_from_defs, test_step, validate_playtest_aliases

*(run_playtest_file removed v0.85.1 — use run_playtest path=)*

### Category: SYSTEM (34)
alias_status, animator_intent, apply_template, ask, ask_user, auto_fix, budget_status, checkpoint, discover_tools†, doctor, execute_code, fingerprint, get_capabilities, get_changes, get_enabled_tools, get_schema, list_connections, list_skills, list_templates, load_session, mcp_status, menu, permission_prompt†, recompile, reconnect_unity†, release_smoke, resolve_tool_schema†, save_session, save_skill, save_template, set_llm_config, smart_build, sync_unity†, undo_last, use_skill

† = tier1=True (always visible)

**Backward-compat aliases** (`_CATEGORY_ALIAS`): object→[SCENE,COMPONENTS], animation→MEDIA, asset→ASSETS, advanced→SYSTEM, ui→MEDIA, runtime→[RUNTIME,TESTS], connection→SYSTEM, session→SYSTEM, profiling→RUNTIME, rendering→MEDIA, debug→RUNTIME, SCENE_EDIT→SCENE, ANIMATION→MEDIA, SHADERS_MATERIAL→ASSETS, VFX→MEDIA, UI→MEDIA, SCREENSHOTS→MEDIA, UNIT_TESTS→TESTS, DEBUG→RUNTIME, ADVANCED_CODE→SYSTEM, SESSION_SKILLS→SYSTEM, CONNECTION→SYSTEM, META→SYSTEM, PROFILING→RUNTIME, RENDERING→MEDIA, PLUGINS→SYSTEM

## C# Commands (CommandRouter)

### Meta (non-mutating)
ping, get_version, get_enabled_tools, get_disabled_tools, set_tool_catalog

### Read (non-mutating)
get_hierarchy, get_component, get_components_list, get_object_detail, find_objects, inspect, get_console, get_compile_errors, compile_status, screenshot, search_scene, validate_references, validate_layout, get_spatial_context, fingerprint, scan_scene, check_colliders, get_schema, get_changes, scene_diff, run_tests, get_test_results, recompile, checkpoint

### Write (mutating)
create_object, delete_object, set_property, set_property_delta, set_active, wire_event, unwire_event, manage_component, set_parent, set_material, batch (mutating=false), execute_code

### Consolidated (action-based)
scene (new/open/save/discard), animation (get/create/edit/add_key/remove_key/remove_curve/set_keys/set_loop/preview), timeline (get/create/edit/add_track/remove_track/add_clip/remove_clip/set_binding/set_timing/mute/unmute/lock/unlock/preview), references (get/find_to/remap), editor (state/play/stop/pause/select/project_path), animator (get/add_param/add_state/add_transition/set_default/remove), particle (get/create/set/apply), shader (get/create/set/graph_get/graph_create/graph_node/graph_edge/graph_get_layout/graph_set_layout/graph_auto_layout), asset (find/get_info/create/move/duplicate/delete/validate_move/get_dependencies/import_settings/export_package/import_package), material (create/get/set/copy/list_properties), prefab (save/create_variant/apply/revert/get_overrides/unpack), scriptable_object (create/get/set/list_types/find), project_settings (get/set), spatial_query (nearest/in_front_of/objects_in_radius/bounds_info/raycast/spatial_map), create_ui, set_rect, menu (execute/list)

### Runtime (Play Mode only)
invoke_method, set_runtime_property, query_state, wait_until, move_to, test_step, run_playtest

## Key Systems

### Capability Gating (Python: `tools/gating.py`, v0.70.0: categories derived from _THEMED_CATEGORIES)
- **CORE tools** (13, `core=True` in ToolSpec): locked, always visible, full schema. See list above in "### CORE" section. Separate from TIER1 (CORE + `tier1=True` tools) — TIER1 tools are always visible but not locked. Example: `is_core("get_hierarchy")` → True, `is_core("discover_tools")` → False (TIER1/SYSTEM, not CORE)
  - **T4 (v0.64.0): get_console Filter Params** — `keyword` (substring match across all log lines) + `count_only` (return only count, no text). `count_only` avoids returning the full log payload. Sample use: `get_console(keyword="Error", count_only=true)` → `"3 errors"`. Gating.py updated for tool filtering.
  - **C6 (v0.70.0): Derived Categories** — `_THEMED_CATEGORIES` is now single source of truth. At import time, derived categories list computed (all categories minus internal ones). Eliminates manual enum-sync drift.
- **Themed catalog** (single source of truth): `get_catalog()` returns dict with 8 categories (v0.83.0: 18 themed → 8: SCENE, COMPONENTS, ASSETS, MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM — old names kept as aliases; CORE as category, not separate key); public tools only, with extension-registered tools added separately. Format simplified for token economy (CORE → categories["CORE"]).
- **Catalog serialization (v0.18.0+)**: Plain-text format sent to C# (`set_tool_catalog`): `CORE:tool1,tool2\nSCENE:tool3,tool4\n...` via `CatalogParser.Parse()` (no JSON encoding). Reduces ~40% wire size vs JSON + eliminates C# JSON deserializer cost.
- **Filtering pipeline**: (1) apply TIER1+session gating via `_apply_gating()`, (2) subtract disabled set from Unity MCPSettings via `_filter_tools()` (cache=None → gating-only fallback). Approach is "hide-disabled-set" (NOT allowlist — Python-only tools not in Unity's CSV wouldn't be wrongly hidden)
- **Sessions**: session-enabled via `discover_tools(category, enable)` (legacy CATEGORIES dict still works for back-compat)
- **Plugin self-registration**: `gating.register_tools("category", tools_set)` lets plugins add to CATEGORIES. Platform controls TIER1 membership (no tier1= escape hatch for plugins)
- **Push catalog**: `_push_catalog()` sends Python-authoritative catalog to Unity on connect/reconnect via `set_tool_catalog` command (plain-text, TCP-only, silent on failure)
- **Cache model**: `_disabled_tools_cache` (refreshed on connect/reconnect); None ⇒ gating-only mode

### Plugin System

**Python** (`plugins/__init__.py`):
- 3-source discovery: (1) pkgutil built-in modules, (2) `importlib.metadata.entry_points(group="unity_mcp.plugins")` for pip-installed packages, (3) `UNITY_MCP_PLUGIN_DIRS` env var for filesystem paths
- Each plugin module: implements `register(mcp, send_fn, args_fn)` to self-register tools
- Disable via env: `UNITY_MCP_SKIP_PLUGINS=prefix1,prefix2` (comma-separated prefixes)
- Plugin API facade: `unity_mcp/plugin_api.py` — stable re-exports (RO, RW, RW_IDEM, DEL) + `register_dsl_tools()`, `register_read_cmds()`, `register_write_cmds()`, `register_tools()`, `register_features()`

**C#** (`IMCPPlugin.cs` + `PluginRegistry.cs`):
- `IMCPPlugin` interface: Name, CommandPrefix, RegisterCommands(), OnDomainReload(), AdditionalCommands
- `PluginRegistry.Register()` — called from plugin's `[InitializeOnLoad]` static constructor
- `PluginRegistry.RegisterAllPlugins()` — called from CommandRouter.RegisterAll()
- One-way asmdef dependency: plugin asmdef → UnityMCP.Editor

### Middleware (Python: `middleware.py` + `middleware_paths.py`, 23 layers, env UNITY_MCP_MIDDLEWARE=1)
1. Retry Watchdog — blocks identical write calls within 5s TTL
2. Confidence Decay — decreases on writes (-0.08), increases on reads (+0.15)
3. Taint Tracking — warns on ObjectReference write to unread paths
4. Periodic State Injection — auto get_hierarchy every 10 write calls (v0.83.0: gated on `is_write()`, ~4000 tokens/session saved)
5. Path Cache — hierarchy paths, fuzzy match via Levenshtein
6. Dead Write Elimination — warns overwrite without read
7. Starvation Monitor — detects 5 identical responses
8. Blast Radius Tags — warns on high-blast commands; read-only batches exempt (v0.78.10: `_is_batch_readonly()` gate in `middleware_guards.py`; **v0.83.0**: WRITE_CMDS/READ_CMDS derived from `tool_specs._SPECS[name].mutability`; `is_write(cmd, args)` + `ACTION_READS` in `middleware_types.py` for per-call action-aware classification of 12 action-based tools)
9. Incremental Verification — checkpoint every 5 mutations; read-only batches exempt (v0.78.10)
10. Workflow Phase FSM — warns after 3+ consecutive writes; read-only batches treated as reads (v0.78.10)
11. Visual Verification — Haiku-based screenshot verification (sampling)
12. Play Mode Auto-Routing — reroutes set_property → set_runtime_property
13. find_objects Cache Bypass — serves from hierarchy cache
14. Batch Conflict Scan — detects duplicate writes, create+delete no-ops
15. Post-mutation Snapshot Verification — verifies prop=value in response
16. Component Cache — caches known components per path
17. Console Error Categorization — hints for NullRef, MissingComponent, FormatException
18. PrefetchCache — predicted reads after writes
19. HierarchyDiff — returns unified diff when <50% changed
20. Distiller — heuristic + Haiku background distillation of large responses (**v0.23.0: full param + cache key fix**)
21. Disambiguator — resolves ambiguous paths via context clues
22. SchemaGuard — pre-flight argument validation
23. Asymmetric Reflection — compares write args vs read-back snapshot

**Play Mode Fail-Fast Guard (middleware_guards.py, feat/tool-disambiguation):** `check_play_mode_required(cmd)` blocks `_RUNTIME_ONLY_CMDS` (derived from `tool_specs._SPECS[name].runtime_only=True`; `watch_add` C# sub-command added manually) before TCP when `_play_state_known=True` and `is_playing=False`. State tracked via `_play_state_known` flag — set on first `track_editor_state()` parse. Returns early (`_early_return`) without dispatching to Unity.

### Watch System (Python + C#, v0.59.0; v0.70.0 B4: 5 tools → 1)

**Play Mode Field Monitoring:**

- **Python API** (`tools/watch.py`): **B4 (v0.70.0): Consolidated to 1 MCP tool** — 5 separate watch tools (watch_add, watch_get, watch_remove, watch_clear, watch_reset) → single `watch(action, ...)` with action-dispatch:
  - `watch(action="add", path, component, field, condition="", trigger="log", interval_ms=500)` → watch ID
  - `watch(action="get")` → active watches + recent log entries
  - `watch(action="remove", watch_id)` → delete by ID
  - `watch(action="clear")` → remove all watches
  - `watch(action="reset", watch_id)` → re-arm triggered watch
  - **Rationale**: Token economy (1 tool in catalog, 1 schema definition). Symmetric with existing action-based tools (scene, animation, asset, etc.).
  - **Conditions**: Optional comparison (e.g., `< 10`, `> 0`, `== null`) — if matched, trigger action
  - **Trigger actions**: `"log"` (default) prints value change to console; `"pause"` pauses editor on trigger
  - **Interval**: Polling frequency in milliseconds (default 500ms, ~2 samples/sec)

- **C# Runtime** (6 files + SessionState persistence):
  - **WatchEntry**: Serializable state — id, path, component, field, condition, action, intervalMs. Non-serialized: LastValue, Triggered, LastSampleTime, ChangeCount, ErrorCount
  - **WatchCondition**: Comparison parser — parses `< 10`, `> 0`, `== null` into opcode + value
  - **WatchEvaluator**: Roslyn C# reflection + condition evaluation. Fetches field value, compares against condition, returns triggered boolean
  - **WatchRegistry**: SessionState storage. Persists active watches across domain reloads (key = "UnityMCP_Watches")
  - **WatchScheduler**: EditorApplication.update polling loop. Cycles through registry, samples each watch at configured interval, emits logs to console
  - **WatchCommandHandler**: Maps 5 watch MCP tools to C# commands via CommandRouter
  - **Flow**: CLI tool call → WatchCommandHandler → WatchRegistry mutation → (on next update cycle) WatchScheduler samples → Roslyn eval → condition check → action emit
  - **Thread Safety**: SessionState handles main-thread serialization; all Roslyn evaluation on main thread

### Additional env-gated features
- **ToolHinter** (`UNITY_MCP_HINTS`, default ON): suggests underused tools
- **SceneBrief** (`UNITY_MCP_SCENE_BRIEF`): injects scene context on first call
- **SpeculativeLayer** (`UNITY_MCP_SPECULATION`): speculative prefetch
- **LessonStore/LessonRecorder** (`UNITY_MCP_LESSONS`): learns from usage patterns
- **ProactiveWatchdog** (`UNITY_MCP_WATCHDOG`): background validate_references + console scan
- **SessionContext/Inferrer** (`UNITY_MCP_INFERENCE`): argument inference
- **CostTracker/BudgetRouter** (`UNITY_MCP_BUDGET`, default ON): Haiku spend tracking

### v0.23.0 Tool Fixes
- **compressor.py**: `_FIELD_ALIASES` dict for field projection (bypass distill)
- **objects.py**: `full` param to bypass distill filtering
- **scene.py**: `full: bool = False` parameter for scene tools
- **middleware_async.py**: distill cache key collision fix (include full flag)

### Playtest File Execution (Python: `tools/runtime.py`, v0.79.1)
- `run_playtest(path="Playtests/smoke.playtest")` — C# reads Assets-relative `.playtest` file server-side; ~15 tokens instead of 300-800 inline DSL
- `path` and `script` are mutually exclusive (ValueError on both/neither)
- `_explicit_path=True` passed in args → middleware bypasses length check for file paths
- `defs` param works with both `path` and `script` modes (prepended for script mode, passed to C# for path mode)
- Path traversal guard in C#: `GetFullPath(path)` + `StartsWith(Application.dataPath)` check
- Replaces `scenarios.py` (`run_scenario`, `save_scenario`, `load_scenario`, `list_scenarios`) — removed v0.79.1

### Auto-Batch (Python: `tools/autobatch.py`)
- `setup_objects(specs)` — create+configure multiple objects (one per line DSL)
- `set_properties(path, props)` — set multiple properties (component.prop=value)
- `configure_objects(config)` — configure multiple objects (/Path component.prop=value per line)
- All expand internally to `batch` commands

### Intent Meta-Tools
- `do(intent, dry_run)` — NL → Haiku plan → validate → batch execute
- `ask(question)` — NL read-only question → deterministic route → Haiku summarize
- `animator_intent`, `vfx_intent`, `ui_intent` — domain-specific NL intent tools (Tier2, discoverable via discover_tools)

### Durable Test Infrastructure
- **TestRunService / TestRunObserver / TestRunStore**: one immutable
  `request_id -> run_id -> utf_guid` identity, atomic manifest and leaf events,
  reload-safe reconciliation, explicit terminal outcome, and durable environment
  restoration evidence. A timeout or transport disconnect is never completion.
- **UnityMcpTestBase**: mandatory per-test transaction for the run-owned ordinary
  scene, exact object/window/asset/preview ownership, global test seams, bounded
  EditMode Task waits, and observable cleanup failures. Compatibility bases such
  as `SceneTestBase` and `MultiSceneTestBase` specialize this contract; they do
  not replace its lifecycle.
- **Scene isolation**: the runner snapshots the user's ordinary scene setup once
  per run, switches UTF to a clean owned scene, and restores the snapshot without
  saving unknown dirty state. Preview scenes are never guessed or closed: tests
  use `CreateOwnedPreviewScene()`, while durable count drift fail-stops the next
  test and invalidates final cleanup evidence.
- **Invocation**: repository runs use `run_unity_tests.py`; consumer projects use
  one `run_tests_wait`. Direct `run_tests`/`get_test_run` is reserved for protocol
  tests and recovery tooling.
- **Compile gate**: before NUnit, require `sync_status` ready and `diagnose` with
  `compile=idle`, fresh assemblies, `errors=No compilation errors`, `log=clean`,
  and `reload_failed=false`. A stale DLL must never count as test evidence.
- **ObjectDiffHelper**: compares Transform properties (Position, Rotation, Scale,
  LocalScale) alongside other components for verification gates.

### Playtest System (C#: PlaytestRunner + PlaytestParser)
- DSL commands (25): MOVE, WAIT, WAIT_UNTIL, ASSERT, ASSERT_CONSOLE_CLEAN, ASSERT_BATCH, ASSERT_NEAR, TELEPORT, SNAPSHOT, INVOKE, SET, LOG, TIMESCALE, CAPTURE, ASSERT_CAPTURED, INVARIANT, ASSERT_CONSERVED, SIMULATE, MONITOR, TRACE_FLOW, ASSERT_CTA, MOVE_PATH, SECTION, WAIT_CAPTURED (capture then wait for condition), SWEEP_PATH (sequential waypoint traversal, expands to N MOVE steps)
- **ASSERT GameObject shorthands (v0.89.0)**: `ASSERT /Obj|activeSelf`, `ASSERT /Obj|activeInHierarchy`, `ASSERT /Obj|tag`, `ASSERT /Obj|layer`, `ASSERT /Obj|name` — resolved directly from `GameObject` fields, no component lookup. Supported in both `/path|prop` and `/path|GameObject|prop` forms.
- **Virtual fields (v0.89.0, `ResolveVirtualField`)**: synthetic read-only fields on components: `Animator|currentState` (active clip name from layer 0), `Rigidbody|speed`, `Rigidbody2D|speed` (velocity magnitude, Unity 6: `linearVelocity`). Resolved before normal reflection path.
- DSL directives (not steps): ABORT_ON_FAIL (global abort flag), DESC "text" (sets label for next step, consumed by `PlaytestStep.Label`)
- DSL modifiers: `AS "text"` suffix on ASSERT — inline description in report
- PlaytestState tracks state across steps
- PlaytestConfig ScriptableObject for project-specific config; **v0.78.9**: aliases from `PlaytestConfig` are auto-injected as a `VAL` block before the user script (via `PlaytestAliasHelpers.FormatVALBlock`). INCLUDE/later VAL lines override these defaults (last-write-wins). Unifies `.asset` and `.defs` alias sources so run_playtest respects project aliases without explicit INCLUDE.
- Monitor/Simulator registries for extensibility
- Global timeout 120s
- **AND/OR compound WAIT_UNTIL (v0.74.0)**: `WAIT_UNTIL /path|Comp|field op val AND /path2|Comp2|field2 op2 val2` — flat chains only, AND and OR cannot be mixed in one step. Extra conditions stored in `PlaytestStep.Queries/BatchOps/BatchValues/IsOr`. Evaluated by `PlaytestRunner.EvalCompound()` (pure, testable without Unity).
- **ABORT_ON_FAIL / ABORT (v0.74.0)**: global DSL directive `ABORT_ON_FAIL` (top-level line) or per-step `ABORT` token on `WAIT_UNTIL`; also `abort_on_fail=True` Python param on `wait_until` and `run_playtest`. On step timeout: `EditorApplication.isPlaying = false`. `HasGlobalAbort` now on `ParseResult` (v0.78.x, was `PlaytestParser.HasGlobalAbort(script)`).
- **Method dispatch via `()` (v0.74.0, RuntimeHelper.cs)**: field path segment ending with `()` (e.g., `IsFullHP()`) invokes a zero-arg method via reflection. Also supports method args: `HasItem(sword)`, `DistanceTo(5,0,3)` via `(args)` syntax. `MethodInfo` cached per `(Type, methodName)` pair; cache cleared on domain reload.
- **WAIT_UNTIL last value (v0.74.0)**: timeout message includes last observed value: `(last: 12)`.
- **MOVE_PATH (v0.74.0, PlaytestParser.cs)**: `MOVE_PATH x1,y1,z1 > x2,y2,z2 [> ...] [TIMEOUT n]` — parser expands waypoints into N sequential MOVE steps.
- **SECTION / DESC (v0.74.0)**: `SECTION "title"` emits a label step (visible in test reports). `DESC "description"` sets `PlaytestStep.Label` on the NEXT emitted step (consumed, does not emit own step).
- **MACRO / CALL (v0.74.0, preprocessor)**: `MACRO name $1 $2 ... END_MACRO` defines a named template; `CALL name arg1 arg2` expands inline before parsing. Positional substitution uses `ReplaceWholeWord` (no `$1`-in-`$10` collision). Guards: nested MACRO blocked, circular depth 10, arg count validated. Forward references and nested CALL supported. Phase 0 collects all MACRO definitions; Phase 0.5 expands CALL directives (supports forward references and nested CALL).

- **ParseResult (v0.78.x)**: `PlaytestParser.Parse()` now returns `ParseResult` (not `List<PlaytestStep>`). Fields: `Steps`, `VarDefs` (name → @query), `Warnings` (non-fatal parse notes), `HasGlobalAbort`. Implements `IEnumerable<PlaytestStep>` + implicit cast to `List<PlaytestStep>` — zero breaking changes for existing callers. `PlaytestStep.ShallowClone()` added for VAR-expansion (arrays share refs with original by design).

- **INCLUDE (v0.78.x, Phase -1)**: `INCLUDE filename` directive at the top of a script — expanded before any other processing via `IncludeResolver` delegate injected into `Parse(script, resolver)`. Runs before macro collection.

- **VAL (v0.78.x, Phase 0.7)**: `VAL $name value` defines a compile-time constant. All `$name` sigils in subsequent lines are expanded inline. VAR declarations preserve their `$name` token (only the @-query is expanded). `_DSL_KEYWORDS` blocklist (40 entries) prevents VAL values from injecting commands. `SigilRegex` (compiled, internal): `\$([A-Za-z_][A-Za-z0-9_]*)`. Unity `Tag` strings auto-injected as VAL defs before parsing (spaces → underscores). Unresolved sigils → `ParseResult.Warnings` (non-fatal, logged as `Debug.LogWarning`). **Phase 0.8 (sigil warning scan)**: After VAL expansion, all non-VAL/non-VAR lines are scanned for remaining `$sigil` matches; each unresolved sigil that wasn't defined as VAL or VAR is appended to `ParseResult.Warnings`.

- **VAR (v0.78.x, lazy runtime resolve)**: `VAR $name @path|comp|field` — binds a variable to a live Unity field. Name stored in `ParseResult.VarDefs`; NOT expanded at parse time. `PlaytestVarRegistry` is created from VarDefs after parse and resolves `$name` sigils just before each step executes (`Phase.Ready`). Expansion via `ExpandStep(step)` returns a shallow-cloned `PlaytestStep` with all string fields substituted. `currentExpanded` cached per step, reset on `AdvanceStep()`. Reading delegate injectable for testability (falls back to `PlaytestRunner.ReadValue`).

- **PlaytestVarRegistry (v0.78.x)**: Holds VAR bindings: `name → (path, comp, field)`. `Register(name, atQuery)` parses `@path|comp|field`. `ExpandVars(text)` uses `PlaytestParser.SigilRegex` to expand. `ExpandStep(step)` clones and expands Path, Query, Value, Component, Method, Args, Message, RawPosition, Queries[], BatchValues[]. Unknown sigils pass through unchanged. `ReadValueFn` delegate injected for test isolation. `HasAny` property gates expansion (no-op when no bindings registered).

- **Provenance tracking (playtests ROI sprint, PlaytestParser.cs)**: Each `PlaytestStep` now carries a `RawLine` field recording the original DSL source line (including line number). Used by `PlaytestRunner.Snapshot.cs` and `PlaytestLinter.cs` to produce precise error messages referencing source location.

- **PlaytestRunner.Snapshot.cs (playtests ROI sprint)**: `BuildFailureSnapshot(step, config)` — called when `snapshot_on_failure=true` on assertion/timeout failure. Extracts `$sigil` names from `step.RawLine` via `PlaytestParser.SigilRegex`, reads their current runtime values, appends recent console errors (error/exception level). Output prepended to failure report as `snapshot:` block. Unresolvable sigils silently skipped.

- **PlaytestLinter.cs (playtests ROI sprint)**: Static DSL linter, no Play Mode required, no Unity scene state. Two entry points: `LintFile(projectRelativePath)` and `LintScript(script)`. Three passes: (1) raw line scan (unknown keywords, structural issues), (2) `PlaytestParser.Parse()` with warning capture, (3) semantic checks (evidence steps present, VAL/VAR consistency). Returns line-tagged `ERROR`/`WARN`/`INFO` issues.

- **SceneRefResolver.cs (playtests ROI sprint)**: Resolves reference tokens (`$alias`, `/path`, `t:TypeName`) against the live scene hierarchy. `ResolveMany(refs, fields) → List<RefResult>`. Each `RefResult` carries: `Input`, `Status` (OK/MISS/AMB), `Path`, `Active`, `InstanceId`, `SceneName`, `Fields` (per-field validation if requested). Used by `lint_scene_refs` and `resolve_scene_refs` commands.

- **SceneRefLinter.cs (playtests ROI sprint)**: 3-pass read-only linter. Extracts path tokens from DSL lines (skips VAL/VAR/meta lines and DSL verb tokens; ALIAS removed from skip list v0.92.x) → validates each against live scene via `SceneRefResolver`. Issues: `ERROR` (MISS), `WARN` (AMB or inactive). Line-tagged output compatible with `PlaytestLinter.LintIssue` format.

- **PlaytestPositionResolver (v0.78.x, internal static, ~59 LOC)**: `Resolve(string raw) → Vector3`. Handles two forms: (1) literal `x,y,z` comma-separated floats; (2) `@path.position [+ (dx,dy,dz)]` — looks up a scene GameObject by path, reads its `transform.position`, then applies optional offset vector. Throws `ArgumentException` on parse failure or missing object. Used by MOVE and TELEPORT step handlers.

- **get_aliases command (v0.78.x, CommandRouter.ObjectHandlers.cs)**: Returns bare `name=path|comp|field` lines from `PlaytestConfig.aliases`. `BuildAliasSection()` builds the `--- ALIASES ---` block; `GetAliasesText()` strips header/footer and returns only the content lines (or `"no aliases"`). Registered as a read command (AllowedDuringCompile). Session-init pattern: call once at session start to seed `_alias_cache`; aliases are NOT embedded in every `get_hierarchy` response (token economy). **AliasType filtering (v0.77.12):** `BuildAliasSection` skips `VarRuntime` entries (VAR DSL-only) and emits `ValConst` as `name=literal` (no pipes); only `ValPath` and `ValConst` appear in the response.

- **alias_status command (v0.78.9, AliasExpander.cs + RegisterMetaCommands)**: `alias_status` returns a three-line health report: `loaded: <empty|N aliases>`, `count: N`, `stale: <true|false>`. `IsStale` is true when `AliasExpander._hasLoaded=true` but `_table=null` (cache evicted since last load). Python MCP tool `alias_status()` (tools/meta.py) simply forwards to this command with no args and returns the report string. Useful for diagnosing why $sigil expansion silently no-ops.

- **AliasType enum (v0.77.12, PlaytestConfig.cs):** `ValPath=0` — `VAL $name /path|Comp|field`; `ValConst=1` — `VAL $name literal_value` (no pipes, single value); `VarRuntime=2` — `VAR $name @/path|Comp|field` (runtime resolve, skipped in `get_aliases`). `QueryAlias` serialized class now has `type` field; backward-compat default is `ValPath(0)`.

- **PlaytestAliasCardBuilder.cs (v0.77.12, static, 251 LOC)**: Extracted card rendering from `PlaytestAliasWindow`. `BuildCard(idx, config, so, onChanged, onDelete)` → `VisualElement`. Row1: TypeDropdown + name field + status dot + Copy/X. Row2: rebuilt in-place on type change — ValConst gets single `constValue` field; ValPath/VarRuntime get path + cascading comp/field dropdowns + DnD. Status dot: valid (green) / partial (yellow) / empty (no alias name). `RefreshCompDropdown` / `RefreshFieldDropdown` use `PlaytestDropHelper.GetUserComponents` and `GetMemberNames`.

- **PlaytestAliasHelpers.cs (v0.78.x, internal static)**: Pure static helpers for Alias Composer UI — no Unity API dependencies, fully unit-testable. `FormatLine(alias)` — type-dispatched: ValPath → `"VAL $name path|comp|field"`, ValConst → `"VAL $name literal"`, VarRuntime → `"VAR $name @path|comp|field"` (no trailing pipes when comp/field empty). `ExportToDefs(aliases)` writes `.playtest` preamble block. Name sanitization: lowercase + underscores only.

- **PlaytestAliasWindow.cs (v0.78.x)**: EditorWindow at MCP → Alias Manager (Shift+Alt+A). Manages `PlaytestConfig.aliases` via UIElements: add/remove alias rows, live token-budget label, preview of exported DSL. Card rendering delegated to `PlaytestAliasCardBuilder`. `PlaytestAliasButton.cs` (23 LOC, Chat/CLI assembly) implements `IToolbarButtonProvider` — adds "Aliases" button to MCPChatWindow footer toolbar.

- **Middleware Post-Hook Registry (`middleware_hooks.py`, v0.80.0 C13)**: `POST_HOOKS: dict[str, list[PostHookFn]]` + `@register_post(cmd)` decorator + `run_post_hooks(cmd, result, middleware)` function. Alias extraction previously inlined in `middleware_pipeline.py` moved to `@register_post` hooks in `middleware_alias.py`. Pattern allows any module to register post-call side effects without touching the pipeline.
- **Alias middleware pipeline (v0.78.x)**: `Middleware._alias_cache: dict` (name → pipe value, cleared on `reset_session()`). Two hooks: Hook 1 (pre-send, in `middleware_pipeline.py`) — resolves `$name` sigils in args via `middleware_alias.resolve_aliases_in_args(args, cache)` (whole-value match only; comma-split for `paths`/`queries`); Hook 2 (post-response, via `@register_post` in `middleware_alias.py` since v0.80.0) — populates cache from `get_hierarchy` response (`--- ALIASES ---` block stripped before LLM sees it) and from explicit `get_aliases` call. **v0.78.8**: Batch `$` WARN removed — C#-side `AliasExpander.cs` now handles $sigil expansion in batch DSL natively, making the warning obsolete. **v0.78.9 `_warm_alias_cache` (server.py)**: `async _warm_alias_cache(bridge_)` auto-seeds `_middleware._alias_cache` on every connect and reconnect — calls `get_aliases` via bridge, parses response with `parse_aliases_from_get_aliases`, and sets `_middleware._alias_cache` directly. Non-fatal (exceptions silently swallowed). Eliminates the need for an explicit `get_aliases` call at session start.

### New Tool Patterns (playtests ROI sprint)

- **Transaction pattern** (`tools/transaction.py`): Two-step safe scene editing.
  - `scene_change_plan(goal, targets, dry_run)` — pre-flight: compile check → console errors → target resolution via `resolve_scene_refs` → checkpoint. Stores plan in-memory with TTL 600s. Returns `plan_id` + baseline status.
  - `apply_scene_change(plan_id, commands, verify, save)` — executes batch mutations, optionally validates references + console, optionally saves scene. Enforces plan_id TTL.
  - Category: SCENE tier1.

- **Verification pipeline** (`tools/verify.py`): `verify_after_change(changed_files, test_filter, run_tests_mode, playtests, mark_id, timeout)` — additive gate chain. Gates run in order; failure at any gate reports which gate failed + lists skipped gates:
  1. `await_compile` (always)
  2. `get_compile_errors` (always)
  3. `get_console_since mark_id` (if mark_id provided)
  4. `run_tests_wait mode filter` (if run_tests_mode provided)
  5. `run_playtest_suite paths` (if playtests provided)
  Returns `PASS: gate1 + gate2 + ...` or `FAIL: <gate> gate failed\n  <detail>\nnext gates skipped: ...`. Category: SYSTEM tier1.

- **Console watermarks** (`tools/console.py`): `console_mark(label)` — pure Python, no TCP, returns a `mark:<timestamp>[:<label>]` string. `get_console_since(mark_id, level, count)` — computes age from timestamp and passes `since=` to `get_console`. Pattern: mark before an operation, query after to see only new logs. Category: DEBUG tier1.

- **run_tests_wait** (`tools/testing.py`): Preferred consumer-project wrapper around the durable direct protocol. Preserves one request/run identity, resolves a lost ACK, polls `get_test_run(run_id)` to reconciliation, and returns an observational `TIMEOUT` without marking the Unity run complete. Category: TESTS tier1. Repository/disposable-worker verification uses `run_unity_tests.py`.

- **Playtest suite runner** (`tools/runtime.py`):
  - `run_playtest_suite(paths, ...)` — multi-file runner. `paths` accepts glob pattern (resolves via `list_playtest_files` C# command), comma-separated list, or newline list. Returns compact `SUITE: X/Y passed (Zs)` header + per-file line + failure details. `stop_on_fail` aborts suite on first failure; `stop_after` exits Play Mode on completion.
  - `run_playtest` gains `snapshot_on_failure` param — on failure, delegates to `PlaytestRunner.Snapshot.BuildFailureSnapshot` for data snapshot.
  - `run_playtest_file` removed v0.85.1 — use `run_playtest(path=...)` instead.

- **Scene ref tools** (`CommandRouter.Registration.cs` + `SceneRefResolver.cs` + `SceneRefLinter.cs`):
  - `resolve_scene_refs(refs, fields)` — validates reference tokens against live scene, returns OK/MISS/AMB per token. SCENE tier1.
  - `lint_scene_refs(script_or_path)` — extracts path tokens from DSL lines and validates them. SCENE tier1.

- **Playtest lint tools** (`CommandRouter.Registration.cs` + `PlaytestLinter.cs`):
  - `lint_playtest(path)` — lint a single `.playtest` file (3-pass: structural → parse → semantic). TESTS tier1.
  - `lint_playtest_suite(pattern)` — lint multiple files matching a glob. TESTS tier1.

- **Alias defs sync tools** (new C# commands, CommandRouter.AliasHandlers.cs):
  - `export_playtest_aliases_to_defs` / `sync_playtest_aliases_from_defs` / `validate_playtest_aliases` — round-trip alias DSL between PlaytestConfig and `.defs` files. TESTS tier1.

- **mcp_status** — Meta tool returning MCP server + connection health summary. Category: SYSTEM tier1.

### Conformance Testing Infrastructure (WI-1/1b/4)

**Dual-Worker Test Environment:**

- **SECOND_PROJECT_PATH + SECOND_PORT env vars** — Support for running two independent Unity projects in parallel on separate ports. Conformance suite detects both and skips if unavailable.
- **ConformanceWorker class** (`server/tests/conformance/workers.py`): Lifecycle fixture for a single Worker. Methods: `gate(bridge)` — verify Worker is ready (compile clean, scene clean, hierarchy valid); `prove_absent(bridge)` — teardown validation (verify test objects removed, no leftover state).
- **Conformance test suite** (`server/tests/conformance/`): TCP-based integration tests marked `@pytest.mark.conformance`. Single-worker fixture (conformance_worker). Tests verify tool contracts: read_ops (hierarchy/component/console), write_ops (create/set/delete), batch DSL parsing, alias expansion, playtest runner, error recovery. **CI job (ci-conformance.yml)**: unit-gate → conformance-single → conformance-dual pipeline.

**Cross-Project Isolation Testing:**

- **dual_worker_session fixture** (`server/tests/cross_project/conftest.py`): Two independent Workers. Marked `@pytest.mark.cross_project`. Requires UNITY_MCP_SECOND_PORT + UNITY_MCP_SECOND_PROJECT_PATH. Worker B expected to have `MCPSettings.ReadOnly = true`. Tests verify: ReadOnly mode blocks mutations while allowing reads (test_read_only.py), port/scene namespace isolation (test_isolation.py), worker identification (test_identity.py), fault injection + recovery (test_fault_injection.py).

- **PortFileManager.ReadOnly property** — Exposes MCPSettings.json ReadOnly flag. Reads from Library/MCP_Port.json for runtime access. Blocks all write commands in CommandRouter.CheckGuards when true.

- **Conformance runners** (`scripts/conformance_runner.py`): Orchestrates multi-Worker test execution. Launches two independent Python pytest processes, merges STDOUT/STDERR, reports pass/fail matrix. Used in CI/.github/workflows/ci-conformance.yml for repeatable durable validation.

- **Fault proxy** (`scripts/fault_proxy.py`): Chaos testing interceptor. Injects TCP delays, partial message corruption, connection drops, and timeout scenarios. Used in test_fault_injection.py to stress connection recovery paths.

- **Minimizer** (`scripts/minimize_repro.py`): Binary-search test range reduction. Given a failing test pattern, finds minimal reproducible subset. Used for CI diagnostics when conformance suite detects regressions.

### batch fields/compress Filtering (v0.78.x, C#: FieldProjector + DefaultStripper)

Applied by `ApplyFieldsCompress(args, result)` in `CommandRouter.ObjectHandlers.cs`. Called by both `inspect` and `get_component` handlers.

- **`fields` param**: Comma-separated list of field names passed to `inspect` or `get_component`. Routes to `FieldProjector.Project(result, fields)` — filters serialized key-value output to only the requested fields. Reduces response size for batch operations querying specific properties.
- **`compress=true` param**: Routes to `DefaultStripper.Strip(result)` — removes default/zero-value lines from serialized output (e.g., `layer = 0`, `tag = Untagged`, inactive booleans). Reduces noise for complex components.
- **FieldProjector.cs** (67 LOC): Pure static. Parses key-value lines, retains only lines whose key matches a requested field (case-insensitive). Handles multi-line values (indented continuation lines follow their key).
- **DefaultStripper.cs** (62 LOC): Pure static. Strips lines matching a default-value pattern set (zero numerics, false booleans, empty collections, known Unity defaults like `Untagged`, `Default`, `None`).

### MultiView Screenshots (C#: MultiViewCapture)
- Camera modes: default, overview, overview_game, multi_view, single_view
- multi_view: 4-panel grid (Front, Left, Top, Isometric)
- Parameters: path, cellSize, supersample (1-4), custom angles, zoom, offset, fixed_size, highlight, show_colliders
- Returns file path + optional manifest (for highlight markers)

### Chat Relay System (v0.66.6+, replaces CliBackendBase + 5 backend variants with unified RelayBackend)

**Architecture:** Python sidecar (`chat_relay.py`) manages single CLI backend lifecycle via output_format discriminator. C# RelayBackend communicates via TCP (same 4-byte BE prefix as MCP bridge). CLI output → ACP event normalization. Role-aware ping distinguishes relay-backed connections from direct MCP probes. ACP-only: no legacy v1 pipe format fallback or protocol negotiation.

**Python Components:**
- **backend_def.py** (v0.71.0: shared SERVER_NAME constant, TTL login-shell cache, DRY _run_login_shell helper): Backend definitions with 5 output format types (output_format enum). ClaudeDef (reads_stdin=True, OUTPUT_FORMAT_STREAM_JSON), CodexDef/KimiDef/AgyDef/OpenCodeDef (reads_stdin=False, respective JSON formats). Env vars: UNITY_MCP_PORT passed through env_set for all non-Claude backends. --format flag added to _BLOCKED_FLAGS. Backwards-compat: ANTHROPIC_API_KEY no longer stripped from Claude. **v0.71.0**: SERVER_NAME constant ("unity-biome-mcp", shared with C# PermissionConfig.cs). MCP_BLANKET derived as `f"mcp__{SERVER_NAME}"` = `"mcp__unity-biome-mcp"` (hyphens NOT converted to underscores per MCP convention). **login_shell_path() TTL retry (v0.71.0)**: Successful PATH lookups cached for process lifetime. Failed empty-result cached for 30s TTL (_LOGIN_PATH_RETRY_TTL), then retried — transient shell failures no longer permanently disable PATH prepending. **_run_login_shell() helper (v0.71.0)**: DRY consolidation of shell invocation for both login_shell_path() and _which_via_login_shell(), reducing code duplication and easing future shell command changes.

- **chat_relay.py**: Standalone TCP server spawned by Unity, survives domain reload. Single-client design (displaces previous on reconnect). Manages CliSession lifecycle, buffer, _transform_fn dispatch. Commands: `send` (to CLI), `events` (long-poll), `set_mode` (respawn CLI with new mode), `status` (health check), `close_stdin` (unblock single-turn backends). Deferred spawn: single-turn backends (reads_stdin=False) respawn at _cmd_send with actual prompt if no prompt at _cmd_start. _TRANSFORM_FNS dict maps output_format → transform function. EOF handling: synthetic error/done result JSON emitted via _transform_line (never backend's transform).

- **cli_session.py** (v0.71.0: login-shell PATH prepend, 16 MiB line limit, DEVNULL stdin guard, stderr capture): Subprocess wrapper with 4 shipped crash-fixes pinned by characterization tests. Lifecycle: spawn (with env isolation), write_line, read_stdout_line (with UTF-8 error recovery), kill (SIGTERM→SIGKILL with 2s grace). SessionMeta dataclass tracks backend/mode/model/mcp_port/prompt/config_dir for mode-switching re-spawn. close_stdin() mechanism for single-turn CLI unblocking. **v0.71.0 Spawn Fixes**:
  1. **DEVNULL stdin for single-turn backends**: reads_stdin=False → asyncio.subprocess.DEVNULL (prevents Codex SIGTRAP crash from reading piped stdin)
  2. **stderr capture**: Surfaces backend crash reason to chat UI via asyncio.subprocess.PIPE
  3. **16 MiB line limit**: Codex/CLI emit large single-line NDJSON tool results (e.g., full scene hierarchy); default 64 KiB overflows, raises ValueError. Pinned via `limit=16 * 1024 * 1024`
  4. **login-shell PATH prepend** (v0.71.0): Calls `await login_shell_path()` to prepend shell's full PATH so node-based CLIs (Codex = `#!/usr/bin/env node`) resolve. Empty login_path skips mutation (graceful fallback). Monkeypatching target: `unity_mcp.backend_def.login_shell_path` (local import in start() necessitates fully-qualified target).

- **relay_buffer.py**: Reconnect-safe ring buffer (maxlen=500, ~30s @ 15 lines/sec). Append-only log with monotonic seq IDs. `enqueue()` mutates lines (escape \n/\r). `cmd_events(after_seq, timeout_ms)` implements long-poll with asyncio.Event signaling. Status field tracks seq/buf/dropped counts.

- **stream_transform.py**: Pure stateless CLI output → pipe-format converter. Five transform functions:
  - `_transform_line`: Claude stream-json NDJSON → pipe-format (stateful tool accumulator)
  - `_transform_plain_text_line`: Agy stdout wrapping → `t|text` events (line 116)
  - `_transform_codex_line`: Codex NDJSON → pipe-format (tool call / result dispatch, line 124). **v0.71.0: Aggregates output_tokens + reasoning_output_tokens** for o3/o3-pro reasoning token accounting
  - `_transform_opencode_line`: OpenCode NDJSON → pipe-format (text/step_finish/error/tool_start, line 181)
  - `_transform_kimi_line`: Kimi NDJSON → pipe-format (role dispatch: assistant=text, meta=session_hint, line 220)
  All handle EOF gracefully, never raise. Selected per backend via _TRANSFORM_FNS dict in chat_relay.py:26-32.

**Pipe-Format Protocol (text-based binary replacement for NDJSON):**
  - Prefixes (all single-char): `t` (text delta), `e` (error), `ar` (auto-reply), `rl` (rate limit), `si` (session init), `hb` (heartbeat), `ss` (session state), `tc` (tool call start), `tr` (tool result), `pp` (permission prompt), `au` (ask user), `tp` (tool progress), `d` (done with cost)
  - Format: `prefix|field1|field2|...` (trailing fields may contain `|`)
  - Tool call: `tc|name|toolId|argsJson`
  - Tool result: `tr|toolId|ok|text`
  - Session: `si|sessionId`
  - Done: `d|sessionId|cost|inTok|outTok`
**C# Components:**
- **`RelayBackend`** is the single C# backend. It owns the selected backend ID,
  mode, model, MCP port, session ID, system prompt, and tool-call accumulator.
- **`RelayChatProcess.StartViaRelay`** sends semantic `start` with those fields.
  User turns use `send`; mode changes use `set_mode`; cancellation uses `kill`.
- **`RelayChatProcess.PollLoop`** requests `events` every 100 ms and reconnects
  with a bounded retry on transport failure. There is no 30-second Chat
  heartbeat in this path.
- **ACP event dispatch:** Incoming ACP events (text_delta, tool_call, tool_result, error, etc.) route directly to `MCPChatWindow.HandleEvent()` with no intermediate parser. New ChatEventKind cases: `PlanUpdate` (agent plan steps), `FileChange` (changed files), `CapabilitiesChanged` (provider capabilities).
- **`PlanStepCard`** (C# UI component) renders plan steps in chat transcript with Approve/Reject buttons for user decision on agent-generated actions.
- **`RelaySpawner`** uses `RelayCommandResolver` to choose the local
  venv/uv/Python command or the version-pinned `uvx` console script, then reports
  spawn failures with captured stderr.

**Python ownership:**

- `chat_relay.py` implements `start`, `send`, `events`, `set_mode`,
  `close_stdin`, `kill`, and `status`.
- `backend_def.py` resolves binaries from the login-shell `PATH`, builds CLI
  arguments, and writes temporary MCP configuration.
- Claude keeps one stdin-driven process. Codex uses `codex exec`; Kimi,
  Antigravity (`agy`), and OpenCode use per-turn processes.
- Backend output is normalized by `stream_transform.py` before C# sees it.

**Session and settings boundaries:**

- Claude can consume a persisted resume ID. Deferred startup for per-turn
  backends currently drops the selected resume ID from `SessionMeta`.
- The C# start payload forwards model selection, but not stored binary
  overrides, permission options, startup timeouts, or extra arguments.
- Chat inactivity uses a 300-second minimum for Codex and a 30-second minimum
  for other backends.

### Typed Context Tags (C#: ChipKind + ResponseTagInliner, v0.15.0 F10)
- **ChipKindDetector.cs** — Pure `Detect()` method categorizes chips: Hierarchy, Scene, Script, Prefab, Material, Texture, ScriptableObject, Asset
- **ChipData.Kind** — Each chip carries a `ChipKind` enum
- **ChipConfig.cs** — Per-kind depth config (none|path|summary|full), persisted in BackendConfigStore
- **Send-side (input):** ChipContextResolver.EmitTyped() formats as `[hierarchy:/Player #123]`, `[script:PlayerController]`, `[scene:.../Main.unity]` for AI consumption; visual chips show left-side kind-prefix (color-coded)
- **Receive-side (response):** ResponseTagInliner.Apply() parses ONLY exact `[kind:ref]` format (conservative regex, no false positives on markdown/code/bare brackets); renders compact colored pills with `<link>` click-nav (symmetric with input)
- **Tests:** ChipKindDetector 13/13, ResponseTagInliner 17/17 (false-positive guards), EmitTyped 7/7, DepthFor 10/10, ChipConfig 3/3

### Extensible Chip-Kind Registry + Composed Inline Field (v0.15.8 F11 + v0.16.0 F12)
- **IChipKindProvider** — Public interface for third-party plugins: Key, Priority, CanHandle, Create, IconName, HexColor, FormatPayload, DefaultDepth, Navigate
- **ChipKindRegistry** — Public static registry; plugins call `Register(provider)` from `[InitializeOnLoad]`. Detection: `Resolve(obj, assetPath)` returns first provider where `CanHandle` true (sorted by Priority). Supports dynamic Unregister + per-key lookup
- **Priority Convention:** <100 overrides built-in type, 100–800 built-ins, >800 extends (new kinds). 8 built-in providers: Hierarchy/Scene/Script/Prefab/Material/Texture/ScriptableObject/Asset
- **Inline Rendering (F12 refactor):** Replaced overlay stack (InlineChipOverlay/UitkCharRect/NbspReservation/TokenSpan) with **composed `InlineChipField`** — a flex-row VisualElement with pill children + trailing TextField. Pills are layout children, not overlays, eliminating mis-positioning and vanish-on-type bugs. Atomic backspace-at-0 removes last chip (standard tag-input UX). `InlineChipModel` is pure headless data (no rendering). `ChipPillFactory` builds pills shared by input field and response rendering.

### @Mention Autocomplete (v0.41.4)
- **MentionTokenParser.cs** — Pure static backward scan from cursor to find `@` prefix + alphanumeric query. Allocation-free, handles multi-word paths.
- **MentionFuzzyScorer.cs** — Allocation-free fuzzy scoring with 26-bit bitmask pre-filter (early exit for impossible matches). Scores by word-boundary match > positional match > character count.
- **SceneMentionIndex.cs** — Hierarchy index with VersionTracker dirty-flag tracking. 3000-entry cap (same as HierarchySerializer.MAX_NODES). Auto-rebuild on scene changes.
- **AssetMentionIndex.cs** — Asset database index via OnAssetsChanged. Caps asset count. Implements IDisposable for cleanup on domain reload.
- **RecentMentionSource.cs** — Selection.activeGameObject + 2000-point score boost. Always suggests last-selected object.
- **MentionCoordinator.cs** — Merges sources, dedup by path (set uniqueness), sort by score desc, cap at maxResults (typically 8).
- **MentionPopup.cs** — UIToolkit ScrollView popup (focusable=false for input field focus). Max 8 rows visible, keyboard-navigable (arrow keys, Enter select, Esc dismiss).
- **MCPChatWindow.Mention.cs (partial)** — Debounce 100ms on text change. On @query match: show popup. Keyboard intercept (Up/Down/Enter/Esc). Blur → dismiss.
- **InlineChipField.ReplaceMentionRangeWithChip** — Delete @mention text, insert chip at cursor with proper spacing. Offset tracking post-replacement.
- **6-layer modular design:**
  ```
  User types "@Ca" → ChangeEvent → MentionTokenParser → MentionCoordinator
  → [SceneMentionIndex, AssetMentionIndex, RecentMentionSource] → MentionFuzzyScorer
  → merge/dedup/sort → MentionPopup.Show() → user selects → ReplaceMentionRangeWithChip → ChipData
  ```
- **Tests:** 72 NUnit tests (MentionTokenParserTests, MentionFuzzyScorerTests, SceneMentionIndexTests, AssetMentionIndexTests, MentionCoordinatorTests, MentionPopupTests, MentionIntegrationTests, MentionPerfTests, MentionEdgeCaseTests)
- **@-Mention Configuration & Recency Tracking (v1.28.0):** `MentionConfig` serialized in BackendConfigStore controls popup behavior. `MaxPopupRows` (3–20, default 8) bounds visible results. `MentionSortOrder` enum (ByRelevance, ByName, ByType, ByRecency) selectable via chat settings dropdown. **MentionHistory.cs** persists path→timestamp in Library/MCP_MentionHistory.json (lazy loaded, max 100 entries, main-thread only). `RecordCommit(path)` fires on chip-insertion; `GetTimestamp(path)` enables ByRecency sort. Older entries evicted on overflow. **Right-Click Context Menu on Popup Rows:** Four actions: (1) Add to Context (injects chip), (2) Copy Reference (to clipboard), (3) Ping in Hierarchy (scene objects), (4) Ping in Project (assets). Wired via MentionRowActions.cs with keybind discovery + inline label rendering.
- **Chip Display Overrides (F12 P4):** `ChipDisplayOverride` struct + parallel arrays in `ChipConfig` support per-kind LLM-payload depth (none/path/summary/full) and graphical color customization. Settings form enumerates all registered kinds (built-in + 3rd-party) dynamically with depth dropdown + color field. `ChipPillFactory.ColorResolver` static seam (set once on window open, consulted by both input and response pills). Zero core edits needed for 3rd-party customization.
- **Show LLM Payload:** Right-click context menu reveals exact byte-for-byte AI payload (symmetry test enforces match)
- **Reload Survival:** `PendingTurnState v5` serializes `KindKeys[]` parallel to chip paths; on resume, re-binds by key (fallback: re-detect if provider not registered yet)
- **Breaking Change (BUG B):** `ChipConfig` default depth `"summary"` → `"path"` (token-minimal). Restore via F9 settings form. Marked in-code: `// BREAKING (v0.16.0)`.
- **Response Pills (F12 P7):** `ResponseTagInliner.Split()` + `MixedParagraphRenderer` render response-side `[kind:ref]` tags as graphical pills (leaf name, click→ping/select, tooltip=full ref) in paragraphs and lists. `RefParser` (inverse of FormatChipRef) strips ` #id` from hierarchy refs before lookup. Pills colored via shared `ChipPillFactory.ColorResolver` (live-updated on settings change).
- **No Auto-Selection (F12 P3+P5):** Removed legacy auto-prepend of SelectionSummary. Context flows exclusively through explicit typed chips (prevents duplicate/verbose context). SelectionSummary class kept for depth="summary" resolution in ChipContextResolver.
- **Tests:** ChipKindRegistryTests, InlineChipModelTests, ChipPillFactoryTests, InlineChipFieldTests, ChipDisplayOverrideTests, ResponseTagInlinerTests, MixedParagraphRendererTests, NewSessionTests — 1581/1586 EditMode pass (5 pre-existing reds)

### Bare-Name Normalization + Add-to-Context (v0.17.14 F14, v0.64.0: eager detection)
- **BareNameNormalizer.cs** (v0.64.0: T1 eager detection) — Converts bare scene object names in LLM responses to `[kind:path #id]` bracket tags (F14a). **T1 (v0.64.0): Eager Detection** — `_resolver?.Refresh()` called in OnSend with null-safe delegate guard. Objects without "/" path prefix auto-normalize and highlight in chat. Mirrors longest-first scan logic: protected ranges include existing `[kind:ref]` tags + triple-backtick fenced code blocks (never re-tagged). Word-boundary rules prevent partial matches. Handles ambiguous names gracefully (skips single-char, allows case-insensitive match with word bounds check). `SceneObjectNormalizationTests` (37 tests).
- **ChipPillFactory.AddToContextAction** — Right-click "Add to context" seam on response pills. MCPChatWindow wires via `OnEnable`/`OnDisable` to attach the menu to response pill segments. Preserves full ChipData (kindKey + instanceID) instead of re-deriving via path-only resolution. Injects chip directly into `InlineChipField.AddChip()`.
- **Display + LLM Text Split (ChipTextInterleaver):** UserMessage send path now splits `rawText` (TextField with @mentions, displayed in bubble as chip strip) from `llmText` (with `[kind:ref]` tags, sent to AI). `ToDisplayText()` emits @DisplayName with spacing (leading space if needed, trailing space, Trim). `ToLlmPayload()` reuses ToDisplayText then appends chip context block. `BuildFromRaw()` strips @mentions before building clean segments.
- **InlineChipField @mention Injection:** `AddChip()` inserts "@DisplayName " at cursor; `RemoveChipAt()` strips corresponding @mention text. `InlineChipModel.AdjustOffsetsAfterTextChangeInclusive` adjusts chip offsets for TextField mutations. MCPChatWindow.Send.cs uses BuildFromRaw instead of Build.
- **Tests:** 201 BareNameNormalizerTests (16/17 fenced-block edge cases + lowercase match + bare-name cycle tests). ChipTextInterleaverTests expanded to 186 tests (R1–R5 BuildFromRaw coverage + @mention spacing edge cases). AssistantBubbleNormalizationTests (68 tests for frozen-bubble normalization flow). PillContextMenuTests (93 tests for right-click injection). New E2E chips integration: M1–M10 (interleaver), E2E_1–E2E_3 (normalization in bubble). 1586/1591 EditMode pass (5 pre-existing reds).

### Context Menus + Unified @-Mention Path (v0.17.17 F15a-F19, v0.20.0 Phase 1)
- **HierarchyContextMenu.cs** — Menu item `GameObject/Add to Chat Context`. Right-click any GameObject in the Hierarchy window, option appears to inject the selected object as a chip into the chat input. Includes validation to ensure the object is valid before injection.
- **ComponentContextMenu.cs** — Menu item `CONTEXT/Component/Add to Chat Context`. Right-click any Component in the Inspector, option appears to inject the parent GameObject as a chip into the chat input. Includes validation before injection.
- **Unified Chip Rendering Path (v0.20.0 Phase 1, P0 fix):** All scene object refs now route through ONE path: AtMention/BareName → `[kind:ref]` → ResponseTagInliner → MixedParagraph → ChipPillFactory pill. Deleted the secondary SceneNameLinker.Linkify path (static mutable `MarkdownInline.Linker` seam) which was rendering refs as `<link><u>Name</u></link>` between pills. Gated the scene-wide BareNameNormalizer pass behind `MCPChat.DisableSceneNameNorm` kill-switch to disable if needed. RefreshResolver (renamed from RefreshLinker) called before FinalizeAssistant in Drain TurnDone so objects created mid-turn are visible to normalization.
- **Leading-Space Guard (F15c):** Consolidated space-handling in `InlineChipField.AddChip()`, `InsertChipAt()`, `InjectMentionAt()` via `prependSpace` parameter. Chips no longer glue to surrounding text; @mention format preserved on round-trip (space before chip, no space after).
- **Tool-Detail CSS (F19):** Response tool cards now render with correct flex-layout: `tool-chip--expanded { flex-direction: column }` stacks details vertically; `tool-detail { flex-shrink: 0 }` prevents content collapse during overflow.
- **Tests:** BuildFromRawDefensiveTests (65), ContextMenuTests (102), F15bScenePillPipelineTests (104), F15cSpaceAfterChipTests (76), F19ToolDetailTests (54), NormalizationPipelineTests (7, v0.20.0), MixedParagraphBreakTests additions, SceneObjectNormalizationTests assertions fixed. Total 32 new tests.

### UX Features (v0.15.0 F1–F10 + v0.15.8 F11 + v0.16.0 F12)
- **F1 (Token Reset):** TokenResetTests ensure counters reset on backend/model switch
- **F2 (Cascade Restore):** TurnUndoTracker.RestoreFromIndex() reverts any earlier turn + all later turns (reverse order)
- **F3 (Approve Gate):** Button shows only when turn has real tool calls (_turnHasToolCalls flag)
- **F4 (Hierarchy #ID):** ChipContextResolver appends `#<instanceID>` to scene object refs (SelectionSummary.Summarize disambiguation)
- **F5 (Inline Chips):** InlineChipField composed control (flex-row of pill children + TextField) replacing overlay stack; drag-drop, removable ✕ button, context menu "Add Selection"
- **F6 (Auto-Scroll Toggle):** EditorPref gate (default ON) for scroll behavior during streaming
- **F7 (Status Distinction):** ChatBackendProbe detects Chat-active vs CLI-listening (3-state: Down/Listen/ChatActive); domain-reload safe (per-call resolution)
- **F8 (No Beta Labels):** Removed "(Beta)" from chat toggle + settings foldout
- **F9 (Settings Form):** Per-backend config form → own JSON → CLI args; includes per-kind chip depth/color overrides (see BackendConfig above)
- **F10 (Typed Tags):** Kind-aware input/output chips with configurable depth (see Typed Context Tags above)
- **F11 (Extensible Registry + Inline Render):** IChipKindProvider public interface + ChipKindRegistry for third-party chip kinds (see Chip-Kind Registry above)
- **F12 (Chip UX Overhaul):** Composed inline-chip field (P1+P2), removed auto-selection (P3+P5), per-kind display settings (P4), response scene-object pills (P7), new-session/clear button (P6). See Extensible Chip-Kind Registry + Composed Inline Field above for details.
- **F13 (Chip Input/Display Architecture Fix):** Unified inline-chip architecture (flex-row composite field), @mention display injection, offset-drift fix, comprehensive TDD coverage. Send path splits rawText (display) from llmText (AI). Re-render after normalization preserves sent state. API cleanup (PositionedChip, ChipTextInterleaver API). Test DRY (ChipTestHelpers).
- **F14 (Bare-Name Normalizer + Context Menu):** LLM response bare object names converted to `[kind:ref]` tags (BareNameNormalizer, fenced-code protected). Right-click "Add to context" on response pills (ChipPillFactory.AddToContextAction). Full chip data preserved (kindKey+instanceID). See Bare-Name Normalization + Add-to-Context above for details.
- **Settings Hub:** `MCPSettingsHub` owns the `MCP/Settings` window and routes to
  focused Tools, Permissions, Chat Settings, Plugins, Updates, and Version
  Picker pages. `MCPSettings` remains the static settings data API.

### Editor UI Windows (C#: UIToolkit)
- **MCPSettings** (MCPSettings.cs): Pure static data class — catalog persistence, EnabledTools state, no EditorWindow. Public API preserved for backward compatibility.
- **Tools page** (`MCPSettingsUI.cs`): Per-tool enable/disable toggles,
  categories, search, presets, and plugin registrations.
- **Permissions page** (`MCPSettingsPermUI.cs`): Stored Chat deny-set
  configuration; the current relay start request does not forward this deny-set.
- **Chat Settings page** (`ChatSettingsSection.cs`): Inactivity timeout, model
  selection, context-chip display, and extension settings. Stored binary and
  launch controls are not currently forwarded by the relay start request.
- **MCPStatus Window** (MCPStatusWindow.cs, **T7: hierarchical connection list**): Connection status monitor. UIToolkit-based with smooth `ArcadeAnim.SmoothLoop` orb + halo pulse (speed tracks connection state: connected=3.4 Hz, listening=2.0 Hz, stopped=1.1 Hz). Main buttons: **Restart** (primary), **Diagnose**, **Setup Wizard**, **Check for Updates**. **Maintenance** foldout (collapsed by default): Reimport, Kill MCP (danger). `BiomeAmbientParticles` (Ecosystem pattern) attached to stage. Stylesheet: `MCPStatus.uss`. **T7**: Server list section displays per-port hierarchy: live TCP connections (via `TakeSnapshot()`), dormant bridges (via `DormantBridgeScanner.Scan(port, activePids)`), and kill buttons with multi-bridge confirmation dialogs. Per-connection row shows kind, state, idle duration, and uptime. CSS hierarchy support added (`.server-entry`, `.connection-row`, `.dormant-section`, etc.).
- **Stylesheet Helper** (MCPEditorUtils.LoadStyleSheet): Shared two-path loader for `.uss` files, called by windows (DRY; handles package-relative asset lookup).

### Code Execution (C#: CodeExecutor, v0.59.0: Play Mode + Security Hardening)
- **Roslyn C# execution via `execute_code` command**
- `execute_code` is mutating and can change runtime state, scenes, or assets.
- **Allow All**, the current default, bypasses security scanning. **Standard**
  blocks dangerous APIs and runtime reflection access; **Strict** also blocks
  type-information reflection.
- `undo_label` groups Unity Undo-compatible mutations, but arbitrary external
  side effects are not guaranteed to be reversible.

### Undo Group Primitives (C#: UndoGroupHelper)
- **UndoGroupHelper** (`UndoGroupHelper.cs`, public core API): Reusable named-group rollback primitive with 4 methods: `OpenNamedGroup()`, `CloseNamedGroup()`, `RevertToBeforeGroup()`, `CanRevert()`.
- **F6 (Chat, v0.11.0):** `TurnUndoTracker` + `RestoreButton` consume this API to wrap each agent turn in an Undo group; Restore button reverts the turn's mutations. Only the last turn's button is active.
- **F27 (shipped v0.6.1):** Batch Undo rollback (opt-in `atomic=true`) reuses the same primitive. It reverts Undo-recorded Unity changes on first failure via `OpenNamedGroup`/`RevertToBeforeGroup`; external side effects are not covered.

### Spatial Queries (C#: via spatial_query command)
- Actions: nearest, in_front_of, objects_in_radius, objects_in_polygon, bounds_info, raycast, spatial_map

### Code Intelligence (Python: `tools/code_intel.py`)
- `compile_preflight(file_path, new_content)` — validates C# without disk write
- `await_compile()` — blocks until Unity compilation finishes

## Dual-Channel Reload Recovery (v0.27.4)

**Reload Package:** Independent UPM package `com.unity-biome-mcp.reload` (separate asmdef, references:[]) runs background mini-server on port 9600+ (SO_REUSEADDR bind-retry). Persists discovered port to `Library/MCP_Port.json`. AssetImportWorker gate prevents interference with import pipeline. **Rationale:** When main plugin compilation breaks, domain reload is blocked; the reload package compiles independently and provides recovery channel.

**Recovery Ladder (Python: `reload_ladder.py` T0-T5):**
- **T0 (baseline):** Synchronous diagnose check, 1 poll
- **T1 (force_refresh):** Call C# force_refresh + poll main MVID (30 polls × 15s = 7.5min timeout)
- **T2 (AssetDatabase.Refresh):** Out-of-band full refresh via reload port + poll (3s sleep before poll)
- **T3 (RequestScriptCompilation):** Out-of-band compile request via reload port + poll
- **T4 (reimport fallback):** Last attempt, 20s polls (no max)
- **T5 (play mode fallback):** Enter/exit Play mode to force compile via main thread, 2s wait

**Sole Healing Proof:** MVID-delta (`main_mvid` before/after each tier). Frozen MVID + compile error = BROKEN_DOMAIN sentinel (domain stuck, manual reimport needed).

**Integration:** `sync.py _attempt_recovery()` calls `run_ladder(start_tier=2)` on REIMPORT-NEEDED verdict.

## Ask Tool Scene Queries + Permission Dialogs (Unreleased)

**Problem:** ask tool rejected valid scene location/object queries ("where is X", "list objects", "show hierarchy") because patterns were too narrow. Additionally, `ask_user` tool was blocked during compilation despite being read-only, preventing permission dialogs at critical moments.

**Solution:**

1. **SCENE_QUERY Pattern Expansion (ask/router.py + ask/plans.py)**:
   - Extended `UNITY_NOUNS_RE` with 23 spatial/hierarchy nouns: transforms, rigidbodies, colliders, renderers, lights, cameras, meshes, terrains, particles, children, parent, waypoints, coordinates, bounds, regions, etc.
   - Added two new SCENE_QUERY patterns: (1) "positions|locations|coords|coordinates|where (is|are)" and (2) "what|which|where|list|show|find|get" + "objects|gameobjects|transforms|nodes|children|parent"
   - Fallback: any question with a Unity noun but no specific matching pattern routes to SCENE_QUERY instead of rejection (was "no matching context")
   - **SCENE_QUERY plan execution**: `get_hierarchy(depth="5")` → Haiku summarization for results >200 chars → hint "scene objects, positions, and transforms"
   - Reduces hallucination by grounding answers in actual scene state

2. **ask_user Guard Unblock (CommandRouter.cs + permission_prompt_tool.py)**:
   - Moved `ask_user` to `IsAlwaysAllowed` (bypass MCPSettings.IsToolEnabled check) and `IsAllowedDuringCompile` (no assembly access needed, read-only UI card)
   - Added to same compile-safe allow list as ping, get_version, screenshot, get_console (7 others)
   - **Error message sanitization** in permission_prompt_tool.py: catches ask_user exceptions, simplifies to user-friendly "Unity not connected" or "ask_user unavailable" (hides TCP/socket internals)
   - Enables Claude to show permission dialogs and user choice cards during compilation windows

**Tests:**
- Python: +15 tests in test_ask.py (spatial patterns, fallback behavior, E2E scene queries)
- Python: +2 tests in test_permission_prompt_tool.py (ask_user routing, error handling)
- C#: +2 NUnit tests in CommandRouterTests.cs (implicit coverage via registration checks)

**Test Count Impact:** Python unit tests increased from 2,597 → 2,840 (243 net new across all modules)

## Implementation Notes (for Developer)

### Data Flow
```
Claude → MCP tool call → TCP send → Unity dispatch → Serialize → TCP response → MCP return
```

### Key Constraints
- Unity API only on main thread
- TCP callback → ConcurrentQueue → EditorApplication.update
- Max message size: 10MB
- Default timeout: 25s (C# side)

### Wave 3: Tool-Gating Fix + Settings UI

**P0 — Hide-Disabled-Set Model (server.py + gating.py):**
- **Problem**: Unity MCPSettings form checkboxes saved zero tokens because `_filter_tools` kept any tool where `is_visible(name)` (true for all TIER1 ≈ every tool).
- **Solution**: Switched from "allow list" to "hide-disabled-set" approach:
  1. Unity reports disabled tools via `get_disabled_tools` CSV (per MCPSettings form state)
  2. Python `_filter_tools` applies gating (TIER1 + session-enabled), then subtracts disabled set
  3. Escape hatches: `FORCE_VISIBLE` set preserves connectivity tools (discover_tools, get_enabled_tools, reconnect_unity, list_connections, do, ask, editor, get_console, get_compile_errors)
  4. Cache model: `_disabled_tools_cache` refreshes on connect/reconnect; None → gating-only fallback (no TCP)
- **Why not allowlist**: Python-only tools aren't in Unity's CSV; allowlist would wrongly hide them

**P1 — Python-Authoritative Catalog + UIToolkit Settings (gating.py + MCPSettings.cs + 3 new files):**
- **Single Source of Truth**: `gating.get_catalog()` returns themed JSON with 8 categories (v0.83.0: SCENE, COMPONENTS, ASSETS, MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM — old names kept as aliases for back-compat) + public tools only
- **Push Mechanism**: `_push_catalog()` sends catalog to Unity via `set_tool_catalog` on connect/reconnect (TCP-only, silent on failure)
- **Persistence**: Unity saves to EditorPref `UnityMCP_Catalog`; MCPSettings queries via `GetCatalog()` / `SetCatalog(json)`
- **UIToolkit Rewrite**: `MCPSettings.cs` now uses UIToolkit (foldout groups, tri-state group masters, search, presets Minimal/Full/No-visuals, CORE locked, separate Plugins section)
- **New C# Files**: `CatalogParser.cs` (JSON→dict), `MCPSettingsUI.cs` (foldout builder), `MCPSettingsCategoryGroup.cs` (tri-state logic), `MCPSettings.uss` (styling)

### Wave 1 Hardening Fixes (Middleware Error-Dedup & Path Caching)

**F16 — Error-Dedup Gate (middleware.py):**
- **Problem**: Gated on whole-body substring scan (`raw_ok = not any(kw in result for kw in ("Failed","Error","err:"))`) that fired on SUCCESS payloads merely containing "Error" (e.g., `get_console` with Error-level logs, an object named "ErrorHandler"), truncating the 2nd identical read to 80 chars and poisoning hierarchy-diff cache. Same flag incorrectly fed `LessonRecorder.record`, so successful reads accrued bogus "fail" lessons.
- **Fix**: Gate on `protocol_err` (the protocol dict `ok` flag captured at dict-flattening step). Same flag now feeds both dedup logic AND LessonRecorder.
- **Also fixed**: `dedup_error` key collision (was `[:80]` → prefix collisions) now keys on FULL message. `_error_dedup` is a bounded `OrderedDict(256)` with LRU eviction to prevent unbounded growth.

**F17 — Negative-Path Cache Poison (middleware.py):**
- **Problem**: `resolve_path_live` cached "absent" paths for 10s TTL even on transient `search_scene` TCP failures, poisoning that path for the full duration. Any `create_object`/`rename` during that window would be blocked because the target was already marked "not found".
- **Fix**: No longer write negative-path cache when `search_scene` TCP call raised (guarded by `search_ok` flag). Additionally, any `WRITE_CMDS` command now clears the entire negative-path cache (a create/rename can make a previously-absent path resolvable).

**F05 — DRY Refactor (middleware.py):**
- **Problem**: `_read_cacheable` set was defined twice (line duplication).
- **Fix**: Hoist to module-level `_READ_CACHEABLE` frozenset.

## Test Infrastructure

### Python Tests: (See CLAUDE.md Commands section for live count)
- Default: `PYTHONWARNDEFAULTENCODING=1 pytest -m "not live and not live_cli and not live_chat and not monkey" --ignore=tests/live --strict-markers -q` — non-live, non-stress unit tests
- With Unity: `PYTHONWARNDEFAULTENCODING=1 UNITY_MCP_PORT=<port> pytest -m "live and not live_cli" -q` — 78 live integration tests, $0 cost (requires Unity running, sampling disabled)
- Real CLI: `PYTHONWARNDEFAULTENCODING=1 UNITY_MCP_PORT=<port> UNITY_MCP_VISUAL_VERIFY=1 pytest -m "live_cli" -v` — 4 real CLI tests, ~$0.001/call (requires Unity + claude CLI, visual verification enabled)
- Test order: unit → C# EditMode → C# PlayMode → live integration → live_cli (live/live_cli always last, occupy TCP)
- **v0.71.0 Guard Tests**:
  - `test_server_name_consistency.py` — Regex-based cross-language drift guard. Verifies Python `SERVER_NAME` and `MCP_BLANKET` match C# `PermissionConfig.cs` constants. Pure text file assertion, $0 cost. Prevents v0.70.7-class bugs (silent duplicate MCP server registration from divergent naming).
  - `test_cli_session_spawn.py` — Characterization tests pinning 4 shipped CliSession.start() crash-fixes in asyncio.create_subprocess_exec kwargs: (1) DEVNULL stdin for single-turn backends (Codex SIGTRAP prevention), (2) stderr capture for crash reporting, (3) 16 MiB line limit (NDJSON overflow guard), (4) login-shell PATH prepend. RED here signals live regression, not missing feature. Tests are currently PASS — baseline pin.

### Live Test Isolation (server/tests/live/)
- **Session-scoped PlayMode**: `_play_mode_session` fixture enters PlayMode once, reuses across 16+ tests
- **GridTest scene auto-open**: `_ensure_gridtest_scene` auto-loads Assets/Scenes/GridTest.unity at session start
- **Per-test scene reload**: `_reload_scene()` uses EditorSceneManager.LoadSceneAsyncInPlayMode (~0.5s, full state isolation without restart)
- **Resettable collectibles**: GridPlayer.ResetState() resets MoveSpeed + re-enables all collectibles via SetActive(true)
- **Test ordering**: edit-mode (first) → play-mode (session reused) → destructive/reconnect (last)

### C# NUnit Tests: 756 tests (EditMode + PlayMode combined)
- 754 passed (2 pre-existing failures: `MCPPrefabTests.Revert_RevertsChanges`, `MCPValueParserTests.ValueParser_Enum_NegativeInt` — unrelated to Wave 1)
- Mixed edit/play mode tests in Unity Test Runner (independent of live tests, no mutex)

### Key Fixtures (conftest.py)
- `bridge_response(data, ok, err, file)` — factory fixture for mock bridge responses
- `mw` — shared Middleware() instance
- `send_fn` — shared AsyncMock
- `_isolate_home` — prevents ~/.unity-biome-mcp/ pollution (autouse)
- `_reset_metrics` — resets METRICS singleton (autouse)
- `_clean_unity_env` — clears env var pollution (autouse)
- `_enable_validate` — guards SchemaGuard module-level mutation (autouse)

## Code Locations

**Python** (90+ modules):
- `server/src/unity_mcp/server.py` — MCP server setup, lifespan, dynamic filtering
- `server/src/unity_mcp/bridge.py` — UnityBridge TCP client, BridgeState enum (DISCONNECTED|CONNECTED|DOMAIN_RELOADING|FAILED)
- `server/src/unity_mcp/bridge_retry.py` — **NEW (v0.70.0, C8)** RetryPolicy class: unified retry decisions (exceptions + hints)
- `server/src/unity_mcp/bridge_result.py` — **NEW (v0.70.0, C1)** unwrap_bridge_result() pure function for response unpacking
- `server/src/unity_mcp/bridge_heartbeat.py` — HeartbeatMixin loop (15s ping, 2–5s reconnect polling, startup grace deadline)
- `server/src/unity_mcp/bridge_reload_state.py` — DomainReloadTracker dataclass (30s expiry, marks domain reload state shared with send())
- `server/src/unity_mcp/connection_slot.py` — ConnectionSlot: single connection management
- `server/src/unity_mcp/lockfile.py` — PID lockfile with fcntl.flock
- `server/src/unity_mcp/compile_state.py` — CompileStateProbe heuristic
- `server/src/unity_mcp/paths.py` — **NEW (v0.70.0, C2)** unity_mcp_dir() function: centralized port file directory logic
- `server/src/unity_mcp/utils.py` — **v0.70.0, C5**: unified KV regex supports dotted keys (Component.prop=value)
- `server/src/unity_mcp/config/merger.py` — **v0.71.0**: Shared SERVER_NAME constant ("unity-biome-mcp"), _OLD_NAMES migration tuple for stale keys, MCP_BLANKET derivation
- `server/src/unity_mcp/middleware.py` — 23-layer middleware pipeline (core)
- `server/src/unity_mcp/middleware_paths.py` — PathResolverMixin extracted from middleware.py. **v1.4.0**: `_last_segment()` helper uses bracket-aware scanning to extract the final path segment (handles paths with embedded `/` in bracket-protected names). Multi-scene cache now stores scene-qualified paths (`SceneName:path`) alongside unqualified paths. `_match_by_name()` filters out scene-qualified paths for unqualified lookups. `_search_candidate()` uses `_last_segment()` for robust leaf-name matching. **v1.31.0**: Detects `&` prefix for persistent RefManager refs in hierarchy node cache (persistent refs instead of transient hex IDs, C# Hierarchy integration via HierarchySerializer.cs).
- **Tools** (split from scene.py in v0.70.0, B2):
  - `server/src/unity_mcp/tools/scene.py` — get_hierarchy, set_parent, search_scene, get_spatial_context (residual)
  - `server/src/unity_mcp/tools/console.py` — **NEW (v0.70.0)** get_console (keyword+count_only), clear_console
  - `server/src/unity_mcp/tools/screenshot.py` — **NEW (v0.70.0)** screenshot (camera modes, Haiku describe)
  - `server/src/unity_mcp/tools/testing.py` — **NEW (v0.70.0)** recompile, await_compile, health checks
  - `server/src/unity_mcp/tools/editor_control.py` — **NEW (v0.70.0)** editor (state/play/stop/pause/select/project_path)
  - `server/src/unity_mcp/tools/gating.py` — **v0.70.0, C6**: categories derived from _THEMED_CATEGORIES (single source of truth)
  - `server/src/unity_mcp/tools/watch.py` — **v0.70.0, B4**: consolidated watch(action=...) tool (was 5 separate)
  - `server/src/unity_mcp/tools/tool_specs.py` — ToolSpec v2 metadata (category, core, tier1, timeout_s, mutability, runtime_only) — single source of truth; derives WRITE_CMDS/READ_CMDS/_RUNTIME_ONLY_CMDS at import. `middleware_types.is_write(cmd, args)` uses ACTION_READS for per-call classification.
- `server/src/unity_mcp/metrics.py` — MetricsRegistry singleton
- `server/src/unity_mcp/sampling.py` — SamplingService for visual verification
- **Chat Relay System (v0.66.6+):**
  - `server/src/unity_mcp/chat_relay.py` — Standalone TCP sidecar (entry: `python -m unity_mcp.chat_relay`). Single-client design, spawned by Unity, survives domain reload. Manages CliSession, RelayBuffer, stream transform. Commands: start, send, events (long-poll), set_mode, status, close_stdin, kill.
  - `server/src/unity_mcp/cli_session.py` — Subprocess wrapper (lifecycle: spawn, write_line, read_stdout_line, kill). SessionMeta tracks backend/mode/model/mcp_port for mode-switching respawn. Env isolation (strip/set lists).
  - `server/src/unity_mcp/relay_buffer.py` — Reconnect-safe ring buffer (maxlen=500). Append-only log with monotonic seq IDs, long-poll via asyncio.Event, dropped counter.
  - `server/src/unity_mcp/stream_transform.py` — Pure NDJSON→pipe-format converter. Stateless _ToolCallAcc accumulator for multi-line tool args. Unknown input → empty list (never raises).
  - `server/src/unity_mcp/backend_def.py` — Backend definitions (Claude, Codex, Kimi, Agy, OpenCode) with arg builders, parsers, binary name, model config.
- `server/src/unity_mcp/tools/` — 24+ tool modules (scene, console, screenshot, testing, editor_control, objects, asset, animation, batch, codegen, skills, spatial, ui, connection, runtime, gating, autobatch, intent tools, code_intel, watch, debug_tool, etc.)
- `server/src/unity_mcp/tools/watch.py` — **v0.70.0, B4**: unified `watch(action=...)` tool (was 5 separate: watch_add, get_watches, watch_remove, watch_clear, watch_reset)
- `server/src/unity_mcp/tools/debug_tool.py` — Symptom classifier + batch command generator (debug_tool, debug_animator, debug_physics, get_memory; get_perf removed v0.85.1)
- `server/src/unity_mcp/tools/runtime.py` — Play Mode tools (invoke_method with private/static support, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest)
- `server/src/unity_mcp/debug/` — Debug subsystem (snapshots.py: state capture + diff)
- `server/src/unity_mcp/plugins/` — plugin auto-discovery (3-source loader)
- `server/src/unity_mcp/plugin_api.py` — stable public API for external plugins
- `server/src/unity_mcp/reflect/` — Asymmetric Reflection (rules_objects, rules_runtime, rules_batch)
- `server/src/unity_mcp/som/` — Set-of-Mark visual annotation
- `server/src/unity_mcp/screenshot_describe/` — semantic screenshot description
- `server/src/unity_mcp/budget/` — cost tracking with file lock
- `server/src/unity_mcp/hinter.py` — ToolHinter post-call patterns
- `server/src/unity_mcp/schema_guard.py` — pre-flight validation
- `server/src/unity_mcp/schema_cache.py` — LRU component schema cache
- `server/src/unity_mcp/clarifier.py` — Disambiguator
- `server/src/unity_mcp/distiller.py` — ResponseDistiller
- `server/src/unity_mcp/degrade.py` — Graceful Degradation helper
- `server/src/unity_mcp/visual_diff.py` — visual regression testing
- `server/src/unity_mcp/sampling_postproc.py` — Haiku output normalizer
- **Tests** (guard + characterization):
  - `server/tests/test_server_name_consistency.py` — **NEW (v0.71.0)** Cross-language drift guard: Python SERVER_NAME/MCP_BLANKET must match C# PermissionConfig.cs. Regex-based text assertions. $0 cost, runs in unit suite.
  - `server/tests/test_cli_session_spawn.py` — **NEW (v0.71.0)** Characterization tests pinning 4 CliSession.start() crash-fixes (DEVNULL stdin, stderr capture, 16 MiB line limit, login-shell PATH prepend). RED signals live regression.

**C#** (165+ files, 17850+ LOC, review sprint v0.70.0: B1 CommandRouter.Registration split + C7 PendingAskRegistry + C4 ExtractVector3 + B3 CommandOptions demoted to internal, v0.71.0: shared SERVER_NAME constant in PermissionConfig, v0.80.0: CommandRouter SRP split +3 partials (AliasHandlers/ScreenshotHandlers/ToolsCache), UNITY_MCP_CHAT define removed — Chat always compiled, BackendConfigStore.WithModel immutable clone, GetCapabilities emits mutating_cmds + runtime_cmds, PlaytestStep semantic aliases C5, VisualStep composition C6-A):
- **Core** (55+ files): MCPServer, CommandRouter (7 partials: main + **v0.70.0 B1: Registration** + **v0.80.0 SRP: ObjectHandlers, AliasHandlers (68L), ScreenshotHandlers (72L, FileHandler delegate for OCP), ToolsCache (42L)** + MediaHandlers), **v0.70.0 C7: PendingAskRegistry** (isolated ask_user state machine), CommandRegistry/Validator, IMCPPlugin/PluginRegistry, ObjectManager, ValueParser, InputNormalizer, BatchHelper, HierarchySerializer, ComponentSerializer, RefManager, WirePrefix, ErrorHelper, RuntimeHelper, PlaytestRunner (2 partials), PlaytestParser, MultiViewCapture, CodeExecutor, SearchHelper, SpatialHelper, AnimationHelper, AnimationCurveCompactor, TimelineHelper, AnimatorControllerHelper, ParticleHelper, ShaderHelper, ShaderGraphHelper, UIHelper, ReferenceHelper, AssetDatabaseHelper, ProjectSettingsHelper, MaterialHelper, PrefabHelper, ScriptableObjectHelper, MCPSettings (data class), MCPSettingsHub, MCPHubUI, MCPSettingsUI, MCPSettingsPermUI, MCPStatusWindow, McpServerScanner, MCPStatusModel, MCPStatusBarWidget, MCPActions; **v0.80.0: GetCapabilities emits `mutating_cmds` + `runtime_cmds` sets** (Python `_warm_cmd_flags()` syncs at connect/reconnect); **v1.31.0: MCPStatusWindow server list with Kill buttons, changelog DRY rendering; McpServerScanner phantom detection + CleanPhantomFiles; MCPActions.KillByPort(port) API; WirePrefix.cs constants for wire protocol; RefManager & base62 prefix with persistent hierarchy refs; AnimationCurveCompactor vector/color grouping; BackendConfigStore.WithModel** immutable clone — `MCPChatWindow.ApplySelectedModel` collapsed 110→1L; **T1: McpServerScanner multi-lock support** — `ScanDetailed()` returns `UnityServerInfo[]` with per-port connection list (`McpConnectionInfo[]`) and `LiveTcpCount`; `Scan()` backward-compat wrapper; `ClientSlot.CountActive()` + `MCPServer.ConnectedClientCount` property support live TCP count injection; test seam `OverrideLiveTcpCountGetter`; **T2: MCPActions multi-bridge termination** — NEW `TerminateResult` enum (Killed/Stale/NotFound), `TerminateByPid(port, pid)` surgical kill by process ID + clean port files only when last bridge dies, `CountBridgesOnPort(port)` query active bridges per port, `StopAllOnPort(port)` kill all bridges on port, private `CleanPortDiscoveryFiles(port)` DRY extract consolidates cleanup across both kill paths; MCPStatusWindow confirm dialog for multi-bridge kill operations; 10 NUnit tests (TerminateByPid edge cases, CountBridgesOnPort filtering, StopAllOnPort atomicity)
- **Debug Subsystem (v0.59.0)** (12 files): MCPDebugPanel, MCPDebugUI (5 partials: WatchRows, EvalBar, AddWatch, ConsolePreview), DebugOverlayDrawer, SparklineHelper, ProfilerHelper, MemoryHelper, AnimatorHelper, PhysicsHelper, WatchEntry, WatchCondition, WatchEvaluator, WatchRegistry, WatchScheduler, WatchCommandHandler (+ 10 test files: WatchEntryTests, WatchRegistryTests, ProfilerHelperTests, SparklineHelperTests, WatchEvaluatorTests, WatchCommandHandlerTests, MCPDebugUITests, WatchConditionTests, MemoryHelperTests, AnimatorHelperTests, PhysicsHelperTests). Stylesheet: MCPDebug.uss.
- **Chat Module** (130+ files, v0.29.2 split into CLI + View assemblies, v0.66.6 unified RelayBackend):
  - **CLI Assembly** (UnityMCP.Editor.Chat.CLI, protocol + single RelayBackend, compiles independently when main broken):
    - **Relay Backend (v0.66.6+, ACP-only):** RelayBackend (single implementation, owns RelayChatProcess + SessionState persistence), RelayChatProcess (TCP client to chat_relay.py sidecar), RelaySpawner (manages relay process lifecycle, free port discovery). ACP event dispatch: incoming agent events route directly to MCPChatWindow.HandleEvent() with no intermediate parser. Replaces 5 old CliBackendBase subclasses (Claude, Codex, Kimi, Agy, OpenCode now managed server-side). Zero CLI-specific knowledge — semantic commands only. SessionId persisted to SessionState across domain reloads.
    - **Infrastructure:** ChatEvent (13 types: TextDelta, Error, AutoReply, RateLimit, SessionInit, Heartbeat, SessionState, ToolStart, ToolResult, PermissionPrompt, AskUser, ToolProgress, Done), ChatTranscript, IChatBackend, ChatBinaryResolver, ChatMcpConfigWriter, PendingTurnState, ReloadGuard, SentTextCache, StderrRingBuffer, ToolCallAccumulator
    - **Tools & Input:** ToolVerbMap, ToolCallRecord, ToolChipGrouper, ToolDetailBuilder, ToolGroupState, ToolGroupSummary, UserTurnBuilder, UserToolResultParser
    - **Backend Configuration (v1.31.0):** ModelPresets (preset entries with contextWindow field, SetOverrides cache, ForDropdown API), ModelContextWindows (per-model context window configuration), BackendSettingsForm (ListView for model presets, reorderable, add/remove), ChatSettingsSection (model presets wiring for all 5 backends)
    - **UX/Formatting:** TokenFormat, ChatActivityState, ChatLabel (chat-text class for text wrapping, v1.31.0), ChatRefResolver, CopyableText, CopyTextBuilder, InputHeightCalc, JsonArrayScan, ArgTokenizer, ArgQuoting, CopyAsMcpRef (Cmd+Shift+C shortcut, v1.31.0)
    - **Chip Infrastructure (shared):** ChipContextResolver, ChipKindDetector, InlineChipData, InlineChipModel, ChipPillFactory, BareNameNormalizer
    - **Chip Providers (v0.59.0):** PropertyContextMenuBridge (Inspector context menu), ComponentChipProvider (component-level chip), FieldChipProvider (single-field chip), ChipPropertyFormatter (DRY serialized property rendering)
  - **View Assembly** (UnityMCP.Editor.Chat.View, UI rendering, depends on CLI):
    - **Windows & Cards:** MCPChatWindow (11 partials: Drain, FlowBar, Chips, InlineChips, Selector, Approve, Slash, Session, Resize, Send, EventHandlers), RestoreButton, TurnUndoTracker, SelectionSummary, CompileAutoFix, EditorStateSnapshot, ToolPing, **ToolApprovalCard** (RiskClassifier, SessionAllowlist), **AskUserCard** (radio/checkbox/freetext), **PlanStepCard** (Approve/Reject buttons for agent plan steps)
    - **Response Rendering:** ResponseTagInliner, MixedParagraphRenderer (paragraph pills), RefParser (ref parsing for response pills)
    - **UX/Formatting (View-specific):** EnterKeySend, EnterKeyLogic, ChatRefAction, CopyTextBuilder, InputHeightCalc, TokenFormat
    - **Rendering:** Markdown/ (MdBlock, MarkdownParser, MarkdownParser.Blocks, MarkdownInline, IChatBlockRenderer, ChatBlockRendererRegistry, ChatBlockRendererFactory, MarkdownBlockRenderer, MarkdownBlockRenderer.Table, MarkdownBlockRenderer.List, ImageBlockRenderer, ChatLinkify), Mermaid/ (MermaidGraph, MermaidParser, MermaidLayout, MermaidLayout.Layers, MermaidBlockRenderer, MermaidView, MermaidEdgePainter)
    - **Styling:** MCPChatWindow.uss, ApproveButtonFactory, ApproveHelper
  - **Test Suites (v0.66.6+, 300+ new relay tests)** (60+ NUnit files, split by assembly):
    - CLI tests (RelayBackendTests, RelayBackendConstructionMonkeyTests, RelayBackendDrainMonkeyTests, RelayChatProcessTests, RelaySpawnerTests, HandleEventAcpCardsTests, ToolVerbMapTests, PendingTurnStateTests, SentTextCacheTests, ArgTokenizerTests, ArgQuotingTests, BackendConfigStoreTests, ChatActivityStateTests, ChatMcpConfigWriterTests, ChatBinaryResolverTests, ChipContextResolverTests, ChipKindDetectorTests, BareNameNormalizerTests)
    - View tests (ToolApprovalCardTests, AskUserCardTests, PlanStepCardTests, EnterKeySendTests, RestoreButtonTests, TurnUndoTrackerTests, SlashRegistryTests, SlashPopupTests, InlineChipModelTests, InlineChipFieldTests, ChipPillFactoryTests, ChipDisplayOverrideTests, ApproveFlowTests, ResponseTagInlinerTests, ResponseTagPillTests, MixedParagraphRendererTests, NewSessionTests, TokenResetTests, SelectionSummaryTests, NormalizationPipelineTests, Markdown/Mermaid render tests, ChatLinkifyTests)

## TDD Scenarios (for Developer)

### Phase 0: TCP Skeleton
1. **test_tcp_connect**: client connects → connection established
2. **test_tcp_send_receive**: send bytes → receive echo
3. **Test_Server_AcceptsConnection**: listener starts → client connects

### Phase 1: Reading Scene
1. **test_get_hierarchy_returns_text**: call tool → text tree returned
2. **Test_Serialize_FormatsCorrectly**: scene objects → text format

## Review Checklist (for Reviewer)

- [ ] Token efficiency: text format, not JSON
- [ ] Thread safety: Unity API only on main thread
  - [ ] **C# Socket I/O (v0.54.1):** All socket awaits use `ConfigureAwait(false)` (RunAcceptLoop, HandleClientAsync, HandleConnectionAsync). No Editor API on ThreadPool continuations.
  - [ ] **C# Main-Thread Marshaling:** All `Debug.Log*`, `RefManager.Invalidate()`, `EditorApplication.QueuePlayerLoopUpdate()` wrapped in `_mainThreadQueue.Enqueue()` lambda.
  - [ ] **C# Domain Stamp Cache:** Volatile `_domainStamp` field for ThreadPool fast-path `get_version` (SessionState not thread-safe).
  - [ ] **Python Reconnect Cooldown:** Both `send()` and `_send_with_retry()` gate on `_reconnect_cooldown_ok()` before reconnect (no burst storms).
- [ ] Error handling: graceful degradation
- [ ] Reconnection: heartbeat-driven reconnect
- [ ] Guards: compile, play mode, runtime, tool enable
- [ ] **Multi-scene API** (skill: `.claude/skills/multi-scene.md`):
  - [ ] No raw `SceneManager.sceneCount > 1` — use `SceneContext.Current.IsMulti`
  - [ ] No hand-built `"sceneName:/" + path` — use `ComponentSerializer.GetPath(go)`
  - [ ] Scene iteration uses `SceneContext.Current.Scenes`, not raw `GetSceneAt(i)`
  - [ ] New tool returning paths: tested in both single and multi-scene mode

## Setup Wizard and Updates Hub

**Setup Wizard**
- **4-screen flow:** Welcome → Pick Backend → Configure → Install Skills.
- **10 backends:** Claude Code, Claude Desktop, Cursor, Windsurf, VS Code, Codex, Kimi, OpenCode, Antigravity, and Rider AI Assistant.
- **BackendDescriptor.cs:** owns display metadata, discovery hints, `InstallMechanism` (`PythonConfig`, `CliCommand`, `ChatAuto`, or `ManualInstructions`), and project-auto-configuration state.
- **ConfigureScreen.cs:** reports the backend-specific result. Depending on the descriptor and installation type, it uses generated project configuration, the Python helper, Chat auto-configuration, or manual instructions.
- **SetupDiagnostics.cs:** checks `uvx`, supported Python fallback versions, and local connection prerequisites.

**Updates Hub + Changelog Viewer (v0.42.0)**
- **UpdatesPage.cs** (80 LOC) — New Hub page (registered via SettingsPageFactory):
  - "Check for Updates" button (disabled during check, 3s cooldown)
  - Changelog area with foldout entries per version (IsNewer versions expanded by default, colored background)
  - Uses ChangelogReader.Parse() to extract entries + MarkdownInlineFormatter.ToRichText() for markdown rendering
- **ChangelogReader.cs** — Parses CHANGELOG.md:
  - Returns `List<ChangelogEntry>` (Version, Date, Content, IsNewer)
  - Locates CHANGELOG.md via ChangelogReader.LocatePath() (Assets/ relative path)
  - Content (markdown) rendered via MarkdownInlineFormatter
- **UpdateBanner.cs** — Existing banner UI (already in place, no changes)
- **UpdateChecker.cs** — Existing PyPI poller (already in place, no changes)
- **MarkdownInlineFormatter.cs** (59 LOC) — NEW, extracted to Editor/ base assembly (v0.42.0):
  - Pure static method `ToRichText(span)` → Unity rich-text
  - Patterns: `**bold**`, `*italic*`, `_underline_`, `` `code` ``, `[link](url)`
  - Uses Unicode non-characters (﷐/﷑) for collision-proof code-span placeholders
  - Reused by UpdatesPage (changelog rendering) and MarkdownInline (Chat assembly, delegates here)
- **MarkdownInlineFormatterTests.cs** (66 tests) — Unit tests for all markdown patterns
- **UpdatesPageTests.cs** (60 tests) — Changelog rendering, update check button behavior

**Wizard Assembly Split (v0.42.0)**
- **UnityMCP.Editor.Wizard.asmdef** — New separate compile unit, references: Editor (core). Enables Wizard to compile independently if core/Chat broken
- **UnityMCP.Editor.Wizard.Tests.asmdef** — Parallel test assembly, references: Wizard + Chat (for integration tests), Test assembly
- **Moved to Wizard/**:
  - SetupWizard, WizardScreen, WizardScreenHost, WizardAnimUtils, WizardAssemblyInfo
  - SetupDiagnostics (was at root)
  - MCPDiagnosePanel, MCPDiagnoseWindow, MCPStatusWindow (diagnostic windows)
  - AiToolCardFactory (reusable card builder, also used by PickBackendScreen)
  - Tests: SetupWizardTests, SetupDiagnosticsTests, WizardAnimUtilsTests, AiToolCardFactoryTests
  - (new) BackendDescriptorTests, ConfigureScreenTests, PickBackendScreenTests, Screens/ folder

**Python Config TOML Merger (v0.42.0)**
- **merger.py extended** — `merge_toml_mcp(path, section_name)` for Codex backend (TOML config support)
- **Merge logic**: Preserves user settings, upserts only MCP entry (diff-based approach)
- **Python 3.9 compat** — `Optional[X]` instead of `X | None` for Union types
- **Error handling** — ValueError raised on corrupt JSON (instead of silent fail)
- **Tests** — 25 new tests in test_config_module.py covering TOML merger edge cases

**Tool Input Click Router (v0.41.9 → v0.42.0)**
- **ChipClickRouter** — DRY pattern for chip click handling (input field + response)
- **Scope**: Hyperlink navigation, inline chip interactions
- **Tests** — InputChipClickTests.cs (199 tests) for input chip interactions

**NUnit Test Count (v0.42.0)**
- **Total: 3908 EditMode + PlayMode** (was ~2900), +1000+ new tests across Wizard, Chat, Updates, Markdown modules
- **Green: 3908** (4 pre-existing reds in unrelated areas)

## v0.44.0 Features: Arcade Level Up UX, Codex Config Hardening

**Arcade LevelUp Celebration Panel (v0.44.0)**
- **LevelUpPanel.cs** — 4-state machine: Idle (waiting) → Animating (bar fill + sparkles) → Done (completion badge) → Diff (release notes)
- **LevelUpAnimator.cs** — Progressive XP bar with AnimationCurve, particle sparkles via Instantiate
- **ReleaseDiff.cs** — Parse CHANGELOG.md, extract version entries, compute release notes diff (version A→B)
- **LevelUpAnim.uss** — Complete animation stylesheet (bar fill, particle, badge emerge, slide-out diff panel)
- **UpdatesPage integration** — Swapped UpdateBanner → LevelUpPanel (conditional on version change)
- **Tests** — LevelUpTests.cs (12 tests): state transitions, animation timing, release diff parsing

**Codex Config Cleanup & Backup/Restore (v0.44.0)**
- **Python side**:
  - **merger.py** — Strips stale `[mcp_servers.unity]` duplicates on first write (idempotent safety)
  - Creates `.bak` backup before modifications (first-write-wins, manual restore via WizardConfigWriter)
  - **install.py doctor** — Warns about stale Codex entries (diagnostic hint)
- **C# side**:
  - **WizardConfigWriter.cs** — `HasBackup()` detection + `RestoreConfig()` recovery method
  - **AiConfigScreen.cs** — Restore button in UI (manual rollback on config corruption)
  - **Tests** — WizardConfigWriterTests.cs (9 tests): backup creation, restore logic, merge safety

**Stability & Bug Fixes (v0.44.0)**
- **ReloadMiniServer.cs** — Fixed CS1503 (explicit TcpClient variable, C# type inference)
- **HelperTests.cs** — Removed MCPServer.Stop() from test teardown (was killing TCP prematurely)

**NUnit Test Count (v0.44.0)**
- **Total: 3945 EditMode + PlayMode** (was 3912), +33 new tests (12 LevelUp + 9 Config + 12 misc)
- **Green: 3945** (5 pre-existing reds, same as v0.42.0)
- **Python pytest**: see CLAUDE.md Commands section for live count and exact test command

## Chat Core System (T9-T24)

Unified multi-provider agent relay with canonical event stream, session authorization, and workflow coordination.

### Multi-Provider Agent Relay (adapters/)

**Architecture**: Abstract adapter pattern for CLI-based backends.
- **Protocol layer**: `adapters/protocol.py` — EventContext, AcpPayload, shared defs
- **ACP (Agent Communication Protocol) adapters** (ACP-only, no legacy fallback):
  - `acp.py` — Claude/OpenCode subprocess launched with `--format acp`, event-streaming output
  - `acp_parser.py` — Line-by-line parser: timestamp extraction, event kind dispatch, delta aggregation
  - `claude_acp.py`, `codex_acp.py` — Provider-specific credential/model routing
- **Testing**: `fixture.py` — FixtureAdapter: deterministic in-process event generation

**Provider-specific event filtering** — `AgentEvent._PROVIDER_EVENT_KINDS` dict limits event subset per backend (Claude gets all 16 kinds, Codex excludes `thought_delta`/`session_resumed`, etc.). Unknown providers fall back to full list.

**Event emission lifecycle**: subprocess stdout → ACP parser → AgentEvent envelope → serialization → chat relay consumer.

### Session Identity & Authorization (T5)

**SessionIdentity** — per-connection contract:
- `session_id`: UUID, stable across bridge reconnects
- `lock_token`: opaque string for lockfile metadata + permission verification
- `agent_id`: agent tool agent name (if subagent invoked)
- `display_name`: user-facing session label
- `started_at_utc`: session birth timestamp

**ClientHelloPayload** — initial TCP handshake combines all session metadata in one frame. Backward-compatible fallback to legacy 3-roundtrip project check + get_version for old C# clients.

**PermissionBroker** — per-session MCP tool consent management:
- Receives `permission_prompt` events from relay
- Caches user decisions per tool per session (ephemeral, SessionState-based)
- Blocks tool execution until consent granted (or auto-approves trusted tools)

**GlobalConfig** — server-wide settings:
- Model presets (context window, cost per token)
- Backend selection (Claude, Codex, Kimi, Agy, OpenCode)
- Feature flags (ACP mode enablement, relay behavior)

### Context Briefs (Brief + BriefBuilder)

**Brief** — lightweight context envelope returned on-demand:
```
{
  compile_errors: [],      # from get_compile_errors
  console_errors: [],      # recent console.Error entries
  hierarchy_summary: "",   # get_hierarchy(depth=3) text snapshot
  selection: [],           # current scene selection
  profiler_metrics: {...}  # CPU/GPU/memory/draw calls
}
```

**BriefBuilder** — lazy assembly:
- `build(scope='full'|'compact'|'minimal')` — configurable detail level
- Prefetch compile status + probe hierarchy in parallel
- Profiler snapshot via ProfilerBridge (non-blocking ring buffer)
- Returns Brief or ToolError if compile unstable

**Usage**: Injected on first agent turn + available as explicit `brief` MCP tool.

### Atomic Transactions (Changeset)

**Changeset** — group related mutations with automatic rollback on failure:
- `mutations: List[MutationRecord]` — each tool call + response
- `files: Dict[str, BeforeAfter]` — changed asset paths + diffs
- `journal: TransactionJournal` — audit trail (timestamp, actor, change reason)

**ChangesetCoordinator** — transaction orchestration:
- `begin()` → capture baseline state
- `apply(commands)` → execute batch with change tracking
- `commit() | rollback()` → finalize or undo atomically

**ChangesetStore** — persistent transaction history:
- JSONL format: timestamp-indexed entries
- Query: `by_date(start, end)`, `by_actor(agent_id)`, `by_scope(scene|asset|component)`
- Retention: configurable (default 30-day archive)

### Checkpoints (Checkpoint + CheckpointStore)

**Checkpoint** — full scene state snapshot:
- Scene asset references (paths)
- GameObject hierarchy + components + properties
- Asset state (prefabs, scriptable objects, textures)
- Metadata: name, timestamp, actor, reason

**CheckpointManifest** — consistency guard:
- Checksums per scene/asset/component
- Detects partial save failures (rollback if mismatch)
- Forward-compatible: unknown fields preserved

**CheckpointStore** — save/load/list operations:
- `save(name, reason)` → checkpoint_id (UUID)
- `load(checkpoint_id, verify=true)` → atomic rollback
- `list()` → paginated snapshot inventory
- Cleanup: TTL expiry (default 90 days) + user deletions

### Plan Workflow (Plan + PlanStore)

**Plan** — agent-generated action plan:
```
{
  id: UUID,
  goal: "Create player health system",
  steps: [
    { tool: "create_object", args: {...}, rationale: "Base player prefab" },
    { tool: "manage_component", args: {...} },
    ...
  ],
  approval_state: "pending" | "approved" | "rejected",
  expires_at: timestamp
}
```

**Plan lifecycle**:
- Agent creates via `plan_tool.create(goal, steps)`
- User approves/rejects via UI or `plan_tool.approve/reject(plan_id)`
- `apply_scene_change(plan_id)` executes approved plan as ChangeSet
- TTL cleanup: unapproved plans expire after 10 minutes

**PlanStore** — plan persistence:
- Query: `by_status(approved|rejected|pending)`, `by_agent(agent_id)`
- Metadata: agent intent, estimated complexity, cost prediction

### Conversation History (history/)

**HistoryEntry** — canonical message unit:
- `kind: 'user' | 'assistant' | 'tool'` — message source
- `timestamp: datetime` — UTC millisecond precision
- `metadata: dict` — tool name, cost tokens, model used, etc.

**HistoryStore** — JSONL-based persistence:
- One entry per line, 5-column format: `kind|timestamp|text|metadata|attachments`
- Atomic append (no partial reads)
- Backward-compatible: unknown columns ignored

**HistoryManager** — lifecycle coordination:
- Lazy load from disk on first access
- Auto-trim on exceeds max entries (default 500)
- Auto-archive on exceeds max size (default 50MB)
- Retention policies: time-based (30-day default) + count-based (500 default)

**Retention** — configurable eviction:
- `TimeBasedRetention` — delete entries older than N days
- `CountBasedRetention` — keep only most recent N entries
- `CompositeRetention` — apply multiple policies
- On-demand: `cleanup()` executes eviction policies

### Four New MCP Tools

1. **`brief`** — get on-demand scene context
   - Parameters: `scope` (full|compact|minimal)
   - Returns: compile status, console errors, hierarchy snapshot, profiler metrics
   - Tier: TIER1, category: SYSTEM, read-only

2. **`changeset`** — query transaction history
   - Actions: `status` (query current), `list` (by date/actor), `replay` (reapply past changeset)
   - Returns: mutations, file diffs, journal entries
   - Tier: TIER1, category: SCENE, read-only

3. **`checkpoint`** — manage scene snapshots
   - Actions: `save`, `load`, `list`, `delete`
   - Parameters: name, reason (for audit trail)
   - Returns: checkpoint_id or loaded state
   - Tier: TIER1, category: SCENE, read/write

4. **`plan`** — manage agent action plans
   - Actions: `create`, `list`, `approve`, `reject`, `edit`, `cancel`
   - Returns: plan_id, approval status, step details
   - Tier: TIER1, category: SYSTEM, read/write

## Related

- Skills: `.claude/skills/`
- Changelog: `CHANGELOG.md` (root, single source of version history)
