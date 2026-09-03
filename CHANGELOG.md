# Changelog

> **v0.95.0 Rebrand:** Server name `unity-mcp` → `unity-biome-mcp`, data dir
> `~/.unity-mcp/` → `~/.unity-biome-mcp/`, UPM package `com.unity-mcp.editor`
> → `com.unity-biome-mcp.editor`. Prior installs auto-migrate on first server start.
> GitHub repo: `unity-kiss-mcp` → `unity-biome-mcp`.

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Test run timeout on Editor focus loss or domain reload:** `run_tests_wait`/`get_test_run` no longer hang indefinitely when the Editor loses focus or undergoes a domain reload. Implemented disk-fallback protocol and health-value gates to detect stalled test dispatch and recover via direct test-run store reads; durable protocol now includes explicit `health` field and `expected_count` for safer gate validation
- **Zero-match filter produces silent failure or stuck `Finalizing` state:** Running tests with a filter that matches no tests (e.g., typo in filter name) now returns an explicit `ZERO_TEST_MATCH` issue in the result, allowing the next run to proceed instead of silently hanging or remaining in `Finalizing` state; added 180-second ceiling on `Finalizing` expiry tied to execution boundary events
- **`await_compile` and `get_compile_errors` return stale cached results:** Removed `get_compile_errors` from `_READ_CACHEABLE` and `recompile` from `GATE_PRIORS` so each call after a C# edit immediately polls live compilation status instead of returning outdated `[CACHED]` responses
- **Ghost Unity process appears alive to status checks:** Compile paths (`get_compile_errors`, `await_compile`, `sync_unity`) now return explicit `UNITY-UNREACHABLE` error when the Editor process is hung or dead; `mcp_status` exposes `unity_status=unreachable` via improved liveness detection (~30-second ping intervals)
- **After Editor restart, Python client loops on connection refused when port changes:** Added fast-path port rediscovery with explicit `[port changed]` warning when the Editor binds to a different TCP port after restart, eliminating repeated `ConnectionRefused` cycles; corrected off-by-one error in MCPServer.StartAsync bind-retry loop boundary so the final allowed retry actually attempts the original port instead of immediately falling back to free-port allocation
- **"Level Up!" button in Setup Wizard hangs on double-click and shows `Unable to add package` error:** UPM operations now guarded by a single in-flight-state checker that disables the button during updates; safe to re-click immediately. `UpmErrorClassifier` and `UpmOperationGuard` prevent phantom "package add already running" errors
- **Plugin or server update overwrites manual changes to `.env`, custom config keys, or TOML sections:** Configuration files now use atomic swap (`File.Replace`) instead of delete-then-move, preserving concurrent edits even under file-locking scenarios (Windows OneDrive/antivirus). Deep-merge strategy for TOML config; per-target `LastSyncedVersion` tracking prevents unnecessary rewrites
- **Random HTTP/TLS probes on the TCP port flood the console with repeating desync warnings:** Added `DesyncWarnLimiter` to classify known foreign protocol probes (HTTP, TLS handshakes) and emit at most one warning per 30 seconds, reducing console noise
- **`uitk_file` action on Windows fails reading `.uxml`/`.uss` files with `escapes Assets/` path validation error:** Path escaping now correctly handles Windows backslashes (`\`) as part of the canonical path, not as escape sequences; fixed `ToAbsPath` resolution for relative UXML/USS includes
- **Setup Wizard enables configurations for all detected agent tools (VS Code, Codex) even if not installed:** Wizard now only enables agent configs for tools that are actually installed, preventing stale or phantom agent entries in the MCP configuration

### Changed

- **Main-thread dispatcher refactored to eliminate timeout windows:** Replaced `EditorTickOnce` with a centralized `MainThreadDispatcher` that uses `EditorApplication.update` directly (not `delayCall`), making it immune to focus loss and domain reload hangups. Dispatcher runs snapshot-bounded Drain cycles with per-action exception handling; `delayCall` now restricted to GUI-only contexts (Chat, Wizard, menus)
- **Atomic file writes for all config and state files:** `MCP_Port.json`, `{pid}.port`, editor state, wizard config, and durable test-run store now use `AtomicFile.Swap` (temp file + `File.Replace`) to prevent data loss under concurrent file-locking (Windows OneDrive/antivirus/network paths)
- **Durable test-run protocol gates for recovery:** Added `health` (no_test_progress, editor_unresponsive) and `expected_count` fields to test-run results for safer terminal-state detection; `ZERO_TEST_MATCH` warning flags empty-filter matches. Disk fallback and lifecycle-gate self-heal allow runs to recover without stalling
- **Error classification on unreachable Editor:** Compile paths return explicit `UNITY-UNREACHABLE` error instead of timeout; `mcp_status` reports `unity_status=unreachable` alongside liveness values (`connected`, `connected-stalled`, etc.)

### Documentation

- Updated `AI/architecture.md` to document `MainThreadDispatcher`, `AtomicFile.Swap`, durable test-run protocol gates, and error classification
- Updated `AI/structure.md` with new C# modules: `AtomicFile.cs`, `MainThreadDispatcher.cs` (replacing `EditorTickOnce`), hygiene test guards, and desync limiters

### Test Results

- **C# NUnit:** 9,276 passed / 0 failed / 34 skipped (×2 runs, run-6bf0491c, run-9b12374c; health=healthy, build_coherent)
- **Python live:** 278 passed / 0 failed
- **Python conformance/seams:** 290 passed / 42 failed (pre-existing; byte-identical to master):
  - 41 failures: seams batch completeness/surface tests — `BatchHelper.cs` v1.41.0 (d3fc6c9f) writes `[N] {result}` while test expects `[N] ok:`; unrelated to this fix
  - 1 failure: `test_screenshot_succeeds` — empty screenshot on background Editor; unrelated to this fix
  - Note: seams live suite does not run in CI (`ci-conformance`: `-m "not live"`)
- **Repository unit:** 1,180 passed / 1 failed (pre-existing: `test_installed_runtime_lease.py::test_installed_entrypoint_serves_public_stdio` — environment venv issue without mcp SDK, byte-identical to master)
- **Install unit:** 89 passed / 1 skipped
- **Server non-live unit:** 6,857 passed / 1 skipped / 634 deselected
- **Wire protocol:** 26 passed
- **Code quality (ruff):** All checks passed

## [v1.51.0] — 2026-09-01

### Added

- **Mutation Mode (Source Patch MVP) — Optional FSR-based body-only source patching:**
  - `editor(action="mutation_mode", enable=true|false)` toggles intent for optional in-memory method-body patching
  - MCP Settings Hub checkbox: **Mutation Mode (experimental)** in General section (P2-04). Polls state every 600ms; disabled states include Provider Absent, Busy, Disabling, Recovery, and Play Mode. Recovery state shows a recovery warning label.
  - `mcp_status` exposes `source_patch_intent`, `source_patch_provider`, `source_patch_state`, `source_patch_op`, `source_patch_recovery`
  - State machine: `Unavailable` (no package) → `Off` (default) ↔ `OnReady/Busy` (intent ON, provider ready) ↔ `Recovery` (failed write)
  - Supported scope (MVP, qualified): existing sync non-generic methods in `Assets/`, body-only edits, single file at a time, Mono backend, Unity 6000.0.65f1; platforms: macOS ARM64 + Linux x64 (CI-qualified), Windows x64 (engineering-supported, CI qualification pending)
  - `asset(action="write_text")` on `.cs` files routes internally via optional provider when ON; falls back to standard compile if provider absent
  - Physical package install/remove via git-pin when Editor stopped; exact one domain reload on OFF transition
  - New C# modules: neutral `SourcePatch` asmdef, `SourcePatchHost` seam, auto-refresh lease coordination (`IAutoRefreshLeasePort`/`UnityAutoRefreshLeasePort`), legacy-write guard (`SourcePatchHost.GuardLegacyCsWrite`), and UI toggle (`MutationModeToggle`/`MutationModeToggleState`)
  - CI qualification: two required-pass cells (Unity 6000.0.65f1 on macOS ARM64, Linux x64); Windows x64 documented as INFRASTRUCTURE_BLOCKED (headed-GUI unavailable on GH-hosted runners); in `.github/workflows/fsr-qualification.yml`

### Documentation

- New user guide: `docs/features/mutation-mode.md` (workflow, limitations, FAQ)
- Updated contributor skill: `.claude/skills/mutation-mode.md` (intent vs. capability, state machine, release-tier limitations)
- Added `AI/architecture.md` section on Source Patch boundaries and state machine
- Added `AI/structure.md` entries for SourcePatch modules and seams
- Added `AI/testing.md` section on FSR qualification and mutation-specific fixtures

### Fixed

- **ON-path source writes no longer trigger `[ScriptCompilation]` request:** `UnitySourcePatchBytesPort.Write` now uses raw `File.WriteAllBytes` instead of `ImportAsset`, avoiding spurious compile-request events that caused Recovery transitions
- **Path validation in `source_patch_write` (ROI #1):** Added `SourcePatchPathGuard` pre-effect boundary check rejecting traversal (`..` segments), absolute paths, Packages/ prefix, and non-.cs files before any Read/Write/Lease effects; violations return VALIDATION rejection, no Recovery state
- **Recovery exit edge (ROI #2):** Added Recovery → Disabling state machine edge so `editor(action="mutation_mode", enable=false)` from Recovery is now legal, triggering the same causal Domain Reload a normal disable uses and releasing any AutoRefresh lease still held from the failed mutation; lease field added to `SourcePatchCoordinator` with `ReleaseHeldLease()` method

### Limitations (Release-Tier)

- MonoBehaviour-derived mutable types not supported (fail-closed Recovery; workaround: mutate utility classes, hold MonoBehaviour instances)
- Async/iterator/lambda/local-fn/generic/overloaded methods not admitted in preflight
- No Play Mode mutations; state not preserved across Play Mode cycle
- ON transitions auto-OFF after domain reload (receipt-based recovery prevents stale session)
- Platforms: macOS ARM64 and Linux x64 (CI-qualified); Windows x64 (engineering-supported, CI-qualification pending)

## [v1.48.1] — 2026-08-21

### Changed

- **RefManager: stateless Base62 refs (`&4GFdMM`) replace counter-based refs (`&1`, `&2`):**
  - Ref values are now derived from Unity `GetInstanceID()` encoded as Base62 — stable
    across domain reloads and independent of assignment order
  - Wire format prefix `&` is unchanged; only the suffix format changes
  - `IsRef` tightened to explicit ASCII `[0-9a-zA-Z]` ranges (rejects locale chars)

### Fixed

- **Unstable ref IDs on domain reload:** counter-based IDs reset after every domain
  reload, causing same object to get different refs on each `get_hierarchy` call;
  Base62 instanceID encoding eliminates the counter and the aliasing hazard
- **Stale-cache guard:** `CommandRouter.ObjectHandlers` now includes `&` prefix
  in defensive stale-ref detection alongside `#` and `$`

## [v1.48.0] — 2026-08-21

### Added

- **RefManager universal Object support — `AssignAny` / `ResolveAny`:**
  - `RefManager` now tracks any `UnityEngine.Object` (not just `GameObject`), enabling
    stable short refs for materials, assets, audio clips, and other asset types
  - `AssignAny(obj)` → `&N` ref; `ResolveAny(ref)` → original object
  - Python MCP server updated: reflect calls, autobatch guards, and docstrings aligned
    with the expanded API surface

### Changed

- **Wire protocol: ObjectReference serialization `$HEX` → `&ref`:**
  - C# serializer now emits `path &N` (e.g. `Assets/Foo.mat &3`) instead of
    `path $DEADBEEF` for all ObjectReference fields
  - Unifies with the `&N` numeric-ref convention already used by `search_scene` (v1.47.0)
  - Python parser accepts `&ref` (new), `$HEX`, and `#decimal` — old Unity plugin output
    remains readable; new plugin output requires Python server ≥ v1.48.0
  - Chat/Chip pipeline migrated end-to-end to `&ref` format

### Fixed

- **Sign-extension in hex object IDs:** negative `GetInstanceID()` values produced
  16-char hex strings; capped to 8 chars (max valid representation)
- **`RefManager.IsRef` cleanup:** removed obsolete `$digits` branch that incorrectly
  matched `$1234`-style tokens as refs; `IsRef` now exclusively matches `&N` refs
- **`&ref` parser edge cases:** `&` in object names (e.g. "Tom & Jerry") correctly
  distinguished from ref tokens via preceding-char guard

## [v1.47.1] — 2026-08-21

### Added

- **C# test coverage boost — 430+ new unit tests across 30+ subsystems:**
  - Coverage targets critical MCP paths: CommandRouter, ComponentSerializer,
    PlaytestParser, PlaytestRunner, TestRunService, TestRunFinalizationCoordinator,
    ValueParser, ObjectManager, SyncHelper, RuntimeHelper, and 20+ more subsystems
  - Best-practice refactoring: all test fixtures now use `TrackOwnedObject` ownership
    pattern per `AI/testing.md` policy — eliminates manual `[TearDown]` cleanup
  - Every test reviewed against `AI/testing.md` checklist by code-reviewer agents

- **InputNormalizer bool aliases:** `true/false/yes/no/on/off` (case-insensitive)
  normalize to `True`/`False` in `set_property` and playtest DSL commands

### Fixed

- **CompileErrorCapture.InjectForTest:** respects `MaxErrors` cap (was unbounded
  in test seam, allowing >50 errors to accumulate)

## [v1.47.0] — 2026-08-20

### Fixed

- **Protocol audit coverage — 4 defects + 7 critical gaps + 120 tests:**
  - **Playtest DSL correctness:** ASSERT_ONE_ACTIVE now checks activeInHierarchy (inclusive parent state) instead of activeSelf; strict mode for saved .playtest files raises fatal error on unresolved `$sigil` (file-based=strict, inline=lenient) — prevents silent failures in released fixtures
  - **Media and ref consistency:** Screenshot filenames include GUID to prevent collisions; search_scene returns compressed `&N` refs (numeric ID) instead of `$HEX` format
  - **Transaction safety:** Detect pre-existing dirty state in apply_scene_change before mutation; Undo group capture is now infallible
  - **Test filter robustness:** Zero-match filters raise ToolError with isError=true instead of silent empty results
  - **Version tracking:** BiomeVersion.cs centralizes Plugin (semantic) and Protocol (numeric) constants; mcp_status now exposes plugin_version + protocol_version + python_version for cross-version diagnostics
  - **Play Mode lifecycle:** PlayModeEpochTracker (new component) emits monotonic play_epoch and world_ready flag; world_ready becomes true after first EditorApplication.update (post-Awake/Start) for robust readiness gating
  - **Regression guard rewrites:** 3 defective guards (RG-06, RG-07, RG-10) rewritten for correctness; 8 atomic regression contract tests added; cross-project reconnect atomicity validated
- **Test infrastructure:** 14 new Python test modules (67 functions covering protocol contracts, edge cases, lifecycle fences, and schema validation); 9 new C# test files (30+ methods for parser provenance, console capture boundaries, command-router enforcement, and overload selection). Total: Python suite 6577/6577 passed.

## [v1.46.1] — 2026-08-20

### Fixed

- **SonarCloud reliability improvement (36 Python + 6 critical, 15 C# static analysis):**
  - **Protocol & transport (Python):** Frame size guard (10MB OOM prevention), concurrent connect() race with asyncio.Lock, CancelledError bridge leak cleanup, JSON dict validation in _read_response, reload mark deduplication, ledger close() drain tracking
  - **Correctness guards (Python):** PlayReadinessTracker null state/epoch regression/timeout, circuit-success false positive on pre-TCP guard exit, stream_transform 5 null-value handling fixes, pipe_parser non-dict JSON/float ValueError/whitespace, ui_intent cellSize crash/duplicate names/unqualified parent, batch timeout + quoted params, regex dead-code elimination
  - **Advanced fixes (Python):** Eliminate O(n²) regex backtracking in create_object (S8786), circuit probe refresh on reconnect, schema set derivation from specs, post-hook exception isolation, acp type narrowing (S5655), structured PLAYTEST timeout format on runtime error
  - **C# static analysis:** Remove async from 22 test methods (CS1998), rename Finalize()→Build() in drawing modes (CS0465), migrate 8 deprecated Unity APIs to 6.0 equivalents (CS0618), log empty catches instead of swallowing, resolve 3 test regressions from fixes
  - **Test infrastructure:** Conformance fixture tests renamed to descriptive names (Baseline_RunIdentityEmit_Succeeds, Baseline_IntentionalFail_ForVerdictValidation, etc.); PLAYTEST DSL timeout assertion updated
  - New 28 unit tests covering reliability fixes, regression guards, and edge cases

## [v1.46.0] — 2026-08-20

### Added

- **Selective Agent Config Writing:**
  - AgentConfigPrefs: EditorPrefs-backed storage layer for enabled agent keys; first-run auto-detects installed agents from home-dir config directory
  - AgentConfigSettingsPanel: New toggles in MCP Status window "Agent Configs" foldout; persists immediately to EditorPrefs
- **Agent Config Adoption:**
  - ProjectConfigFormats.Adopt() and ProjectConfigToml.Adopt(): Hand-edited entries without version marker now adopted by surgically inserting "_v" marker, preserving all user content (custom args, env vars)

### Changed

- **Update Flow Reliability:**
  - UpdateChecker.ForceCheckAsync(): Cancels stale requests before fetching from network; UnityWebRequest.timeout set to 15s to prevent indefinite hangs
  - UpmPluginUpdater.Update(): Added 120s timeout guard in Poll and PollReload lambdas; _timeProvider seam for testing
  - VersionPickerPage Align button: Shows "Aligning..." spinner during UPM resolution, disables interaction, displays result dialog; removes standalone AlignBoth method
  - ProjectConfigWriter.Run(): Filters targets by enabled agent keys from AgentConfigPrefs; existing config files always updated (migration safety)

### Fixed

- **Domain Reload Port Stability:**
  - Python server now evicts duplicate (same-ppid) and orphaned MCP server processes at startup, preventing 7+ minute lingering after domain reload
  - MCPServer bind retry count increased from 4 to 6 on macOS/Linux to match Windows budget; total same-port retry wait increases from 2.4s to 6.0s for better socket state recovery

## [v1.45.0] — 2026-08-20

### Added

- **Protocol reliability & lifecycle fencing (MCP-TRANS-008, MCP-SESS-024, MCP-LIFE-004, MCP-SUITE-006):**
  - CommandLedger + CommandStatus FSM: durable op_id tracking survives transport disconnect
  - EditorIdentity + SessionIdentityMismatch: enforce same-project reconnection
  - CapacityBusyError: typed rejection when Unity TCP slots full
  - PlayReadinessTracker: epoch + world_ready handshake for robust Play Mode entry
  - _enter_fresh_play gate with wait_for_ready: playtest execution waits for actual world readiness
  - FailureCategory enum + categorize_failure: typed protocol-level failure diagnostics
  - TestRunHandle + TestRunRegistry: durable in-memory test run metadata persists across transport disconnect
  - SuiteVerdict + format_layered_verdict: separate inner (assertion) from outer (lifecycle) verdicts
  - MCPFeedbackFixture: 10 C# components + 5 EditMode tests + 11 PlayTest DSL files for conformance

### Changed

- CommandRouter.dedup_applied flag: explicit dedup tracking for idempotency verification
- ToolSpec.plane property: computed from direct_only + unity_transport
- Generation-aware compile fence in code_intel.py: prevent stale Symbol detection across compile
- SCENE_STATE_NEUTRAL_WRITES middleware: mark playtest fixture writes with neutral semantics
- verify_after_change suite passthrough: test suites can use verify gates without stopping Play

## [v1.44.0] — 2026-08-19

### Fixed

- **Unity 6.4–6.5 compatibility:** Add `ObjectIdCompat` bridge for instance-ID → EntityId API migration; add `UIPanelHost` abstraction to support both `UIDocument` (6.0–6.3) and `PanelRenderer` (6.4+)
- **Playtest DSL:** Accept `|PanelRenderer|` and `|UI|` component tokens in paths; normalize to `|UIDocument|` at parse time for version-independent scripts
- **Diagnose output:** Reorder fields to place multiline `all_errors=` last; add `substate=`, `port=`, `port_fallback=` for clearer agent diagnostics
- **DiagnoseCommand:** Optimize asmdef lookup with `ScanAssets` and `ScanPackages` seams to reduce O(N*M) scans to O(N+M)

## [v1.43.0] — 2026-08-19

### Fixed

- Close 27 Python issues from SonarCloud top-100 remediation:
  - **Complexity (S3776 ×10):** Extract helpers in `bridge_heartbeat` (57→5), `runtime` (69→10), `transaction` (43→7), `batch` (41→3), `verify` (33→4), `server` (26→12), `uitk_intent_tool` (25→15), `middleware_pipeline` (18→13), `compressor` (16→8), `middleware_reads` (16→13)
  - **Regex (S8786 ×5, S6019, S6326 ×2):** Fix backtracking in `rules_objects`, `middleware_paths`, `transaction`; replace repeated spaces with quantifiers in `scene`
  - **Style/correctness:** Rename snake_case (S1542 ×2), add logging to empty except (S2737), extract duplicate literal (S1192), remove unused async (S7503 ×2), prefix unused param (S1172), remove redundant parens (S1110), narrow bare except (S5754), flatten nested ternary (S3358)
  - **Correctness (not in top-100):** CancelledError propagation in `bridge_heartbeat`, `server`, `sampling`; atomic file writes in `skills/scene`; bounded I/O in `lockfile`, `server_filtering`; task GC in `server`
- Add 35 new unit tests for extracted complexity-reduction helpers and regex fixes

## [v1.42.1] — 2026-08-18

### Fixed

- Reduce cognitive complexity: `middleware_pipeline.py` (31→12, 18→10, 45→8), `reload_ladder.py` (19→6)
- Fix `doctor.py` exception order — `TimeoutError` was dead code, now correctly shows "frozen" message
- Fix `compile_state.py` try/except structure for static analysis clarity
- Fix `server_filtering.py` `_strip_deferred_schemas` return type (S3516)
- Remove redundant exception classes in `bridge.py`, `connection_slot.py` (S5713)
- Update `sonar-project.properties` python version to 3.14
- 24 new unit tests for extracted helpers

## [v1.42.0] — 2026-08-18

### Breaking

- **Python 3.14+ required** — dropped support for Python 3.10, 3.11, 3.12, 3.13

### Changed

- Remove `from __future__ import annotations` from 227 files (PEP 649 native)
- Replace `asyncio.get_event_loop()` → `get_running_loop()` across codebase
- Replace `asyncio.ensure_future()` → `create_task()` across codebase
- Replace `asyncio.iscoroutinefunction()` → `inspect.iscoroutinefunction()`
- Replace `asyncio.TimeoutError` → builtin `TimeoutError` across codebase
- Replace `Optional[X]` → `X | None`, `typing.AsyncGenerator` → `collections.abc`
- Fix PEP 649 TYPE_CHECKING regression: runtime-needed imports restored with `# noqa: TC`
- Fix `test_resolve_binary_does_not_block_event_loop` timing race on Python 3.14
- All CI workflows, Dockerfile, release-policy updated to Python 3.14
- `ruff target-version` updated to `py314`

### Added

- `AI/python-guidelines.md` — comprehensive Python 3.14 best practices reference
- 11 fence tests in `test_python314_compat.py` preventing regression to deprecated patterns
- `install/tests/test_version_check.py` — version guard validation
- Version guard in `install.py` for friendly error on Python < 3.14

### Removed

- `hypothesis-jsonschema` dependency (unused)
- `tomllib` try/except fallbacks (stdlib since 3.11)
- `from __future__ import annotations` across entire codebase

## [v1.41.0] — 2026-08-17

### Fixed

- **S3**: `compress=true` now wired to bridge args + empty section collapse
- **D4**: `discover_tools(enable=true)` notification fires even without ctx (fallback to active session)
- **R2**: Undo response warns about persistent asset files (`warn: N asset file(s) not reverted`)
- **U19**: Screenshot detects ScreenSpaceOverlay canvases and warns in response
- **Batch Bug A**: `IsFailureResult` checks first line only (prevents false positive on multi-line output)
- **Batch Bug C**: `[N]` remap regex anchored to line start (prevents data corruption in hierarchy)
- **5 reflect logic bugs**: manage_component Error/substring match, create_object path spaces, delete_object/set_active error masking

## [v1.40.0] — 2026-08-17

### Added

- **Reflect rules expansion** (9→40+): factory pattern, 83.6% coverage of mutating commands — `rules_scene.py`, `rules_assets.py`, `rules_system.py`, `factory.py`, `coverage.py`
- **Conformance test generator** (`scripts/gen_conformance.py`): 190 YAML test cases from tool schemas (94 valid, 82 invalid, 6 seam, 8 batch)
- **Gauntlet contract generator** (`scripts/gen_gauntlet_contracts.py`): 1→103 formal contracts (94 routing + 8 mutation + 1 existing)
- **Hypothesis StateMachine** (`server/tests/stateful/`): `UnitySessionMachine` with 7 rules, 2 invariants, 50×15 step sequences against `StatefulFakeServer`
- New pytest marker: `stateful`

### Fixed

- **U3**: ScrollView creates scrollable structure (anchors + horizontal disabled)
- **V8**: UILinter detects broken ScrollRect (S3/S6 checks)
- **R3**: Undo response includes mutation count
- **U11**: `attach_uitk` warns when `PanelSettings=null`
- **U14**: Error message redirects to `inspect_uitk` instead of unreachable `uitk_file`
- **R2/U19**: Documentation warnings for undo asset limitation and SSO canvas

## [v1.39.0] — 2026-08-17

### Fixed

- **G2**: Batch silent command dropping — `skipCount` tracking with `skip:K` in summary; completeness invariant `ok+err+skip==N`
- **G1**: Path resolver strips `$ref` suffixes correctly instead of substituting scene summary
- **V6**: `is_playing` cache updates from `mcp_status` responses with 5s TTL
- **V7**: `verify_after_change` reports `PASS(N/5)` gate fraction instead of bare `PASS`
- **X4**: `editor(action=play)` returns `"requested"` immediately instead of blocking with Thread.Sleep
- **X10**: `count_only=true` preserves overflow warning in console gate

### Added

- **Seam test framework** (`server/tests/seams/`): 113 live conformance tests — round-trip verification, batch completeness, surface consistency, differential batch-vs-sequential
- **Wire contract tests** (`server/tests/wire/`): 26 CI tests without Unity — FakeUnityServer, MITM fault injection, protocol shape validation
- New pytest marker: `wire` for protocol-level tests

## [v1.38.0] — 2026-08-16

### Fixed

- **Tool contracts and surface integrity**: `discover_tools` now exposes CORE tools separately, removing direct-only tools from themed categories to prevent batch-validation errors. CommandRouter registers Python-only tools (checkpoint_create, checkpoint_restore, brief_build, get_changeset) so batch dispatch rejects them with clear errors. `get_component` accepts `component=` as an alias for `type=` for backward compatibility with set_property naming.
- **UI creation robustness**: `create_ui(type="ScrollView")` now creates a canonical hierarchy with full-stretch Viewport, top-left-anchored Content, and ContentSizeFitter for proper growth. Root Image color is applied if supplied; omitted PanelSettings fields remain unset during `attach_uitk`.
- **UI validation**: `lint_ugui` detects eight issues: [S1-S5] ScrollRect structural problems (missing viewport/content, anchor mismatch, no growth mechanism, duplicate Masks, unwired Scrollbars) and [G1-G3] general layout issues (zero-size active RectTransforms, raycast-blocking Images, LayoutGroups with no active children).
- **Batch undo safety**: Non-atomic root batches now open a named undo group (`MCP Batch`) capturing all sub-command mutations; tracked mutations populate UndoGroupStack for undo_last targeting. Nested batches propagate mutation state so the root captures the full transaction.
- **Server status accuracy**: TCP bind fallback is now tracked with `_portFallback` and exposed in `FormatStatusResponse` as `port_fallback` and `bind_failed` fields. `SO_REUSEPORT` socket option is applied on macOS only (removed from Linux conditional).
- **Verify gate reporting**: `verify_after_change` now appends `| SKIPPED: gate1, gate2` suffix when optional gates are omitted, so test plans clearly show which gates were not run.
- **Scene transaction improvements**: `scene_change_plan(dry_run=False)` is now the default, creating a checkpoint and plan on success. Console errors are now filtered to the time window since plan creation in `apply_scene_change`, reducing false positives from pre-existing log noise.
- **Text filtering robustness**: `_short_description` now checks for both sentence terminators (`'. '` and `'.\n'`) to extract the first complete sentence.

## [v1.37.2] — 2026-08-16

### Fixed

- **Port lifecycle reliability**: Domain reload (PlayMode enter/exit) now reconnects with 1-second backoff instead of 5–60-second exponential cooldown, reducing reconnect latency. Windows socket teardown uses `SO_LINGER(0)` to send RST and avoid TIME_WAIT accumulation. Windows port bind retry increased from 4 to 6 attempts with 600ms base delay to reduce fallback-port drift during longer TIME_WAIT windows. Port persistence (`SaveRuntimePorts`) now thread-safe for async bind-retry loops via pre-cached path parameters. Server status display adds `SubState` enum (BindFailed, CompileFailed, Compiling, PortMismatch) for more accurate failure reporting in the UI.

## [v1.37.1] — 2026-08-16

### Fixed

- **Intent tool gating**: `ui_intent`, `vfx_intent`, and `uitk_intent` are now visible in the MCP tool list after `discover_tools`. Five compounding defects prevented them from appearing: missing `tier1` flag, absent full-schema entry, `discover_tools`/`get_catalog` count mismatch, `reconnect_unity` wiping session state unconditionally, and no `CommandRegistry.Ready` guard in batch.

## [v1.37.0] — 2026-08-16

### Fixed

- **UI Toolkit authoring**: `attach_uitk` validates supplied UXML and Panel Settings assets before its single Undo mutation, leaves an omitted Panel Settings field unset, and no longer leaves a partial `UIDocument` after invalid input. `lint_uitk(fix=True)` now fails explicitly without reading or changing the file, `uitk_file(action="read")` returns verbatim UTF-8 text and works on read-only Unity workers, and failed imports remove files newly created by that call. `uitk_intent` uses the public attach fields and reports attempted paths on failure; live element lookup again honors `ref`, then `name`, then `selector`.
- **UI Toolkit registration**: The five direct UI Toolkit wrappers are registered once, eliminating duplicate-registration warnings during server startup and schema export.
- **Atomic scene changes**: `scene_change_plan` now refuses to create a plan while the console contains errors. `apply_scene_change` rejects empty input and commands without proven Unity Undo coverage, rechecks Edit Mode immediately before transport, uses an atomic stop-on-error batch, recognizes plain and multiline handler errors, and only verifies or saves after a successful apply. Broken references, console errors, and unrecognized verification results block saving.
- **Hierarchy summaries and Chat cards**: Summary mode now honors the requested scene, including for unqualified roots. Hierarchy compression recognizes current `&` references, respects tree depth, and preserves the final sibling marker. Chat renders summaries and multi-scene trees with a 20-entry initial view, while process-local compact references are not recycled after invalidation and cannot select a newly assigned object from a stale card.
- **Region queries**: `spatial_query(action="objects_in_polygon")` accepts either inline vertices or a stored `region_id`, validates any supplied vertices, and rejects calls that provide neither.
- **Plugin command prefixes**: Canonical `CommandPrefix` values omit the trailing underscore. Legacy trailing underscores remain supported, while separator-boundary matching prevents one plugin from claiming similarly named commands.
- **Playtest Composer**: The step picker now exposes only exporter-supported types. Recognized DSL steps that the visual editor cannot modify retain their exact source line; static `VAL` and `PATH_PREFIX` uses export as resolved, self-contained commands instead of unresolved sigils.
- **Read-only and tool-surface enforcement**: Action-aware guards now distinguish observational and mutating forms of UI Toolkit files, NavMesh, waits, profiling, metrics, change consumption, and diagnostics. Package export, screenshots and comparisons, arbitrary C# execution, test steps, playtest runs, and verification are treated as writes; runtime-only writes retain their explicit Play Mode allowances. Default diagnostics and suggestion-only `auto_fix` remain reads, while cleanup is blocked on read-only endpoints. Structured discovery no longer advertises async, special-dispatch, or file handlers as batchable.
- **Screenshot output safety**: Captures reject non-PNG and out-of-project destinations before writing. Screenshot capture and comparison consistently expose their artifact side effects to Python and Unity read-only guards, while screenshot baseline names reject empty, path-like, and traversal strings. Repeated-capture guidance now counts captures only since the latest non-capture mutation, and captures no longer invalidate scene-read caches or advance the scene mutation checkpoint.
- **Batch result integrity**: Python preflight errors retain zero-based command ordinals and contribute to the terminal error count. Unity batch execution recognizes handler-returned dry-run/bulk failures and timeouts, so stop and atomic modes cannot report those outcomes as successful.
- **Test and Playtest evidence**: `verify_after_change(restart_between=True)` now starts and resets Play Mode through observed state transitions. Start, stop, restart, cleanup, empty matches, and zero-test suites fail closed. Unfiltered consumer EditMode suites no longer inherit a repository-specific size floor. Global `ABORT_ON_FAIL` now stops after any failed step or automatic console failure and skips remaining steps, including teardown; per-step `WAIT_UNTIL ... ABORT` remains timeout-only.
- **Sampling safety**: Server-side sampling now starts Claude only for profiles configured with the `claude` backend. Other backends fail closed with a warning, and model environment overrides preserve the selected backend.

### Changed

- **ClientSkills**: The shipped inventory is now 12 skills and 4 agents. The retired `unity-test-reviewer` agent and `test-authoring.md` policy are removed; transactional upgrade migrations delete only recognized, unmodified legacy files and preserve customized conflicts. Claude and Codex stages report their independent rollback boundaries, intercepted converter interruptions roll back the active stage, and post-commit cleanup failures no longer turn a successful conversion into a false failure. During Codex sync, the Wizard locks navigation and install choices and writes the version marker only after the still-open page observes success.
- **Release checks**: ClientSkills-only changes now run their contract tests. Changelog changes run a required byte-for-byte mirror check, with `scripts/sync_changelog.py` as the canonical synchronization command.
- **Documentation**: Public, contributor, development, and ClientSkills documentation was revised in English with task-first guidance, source-backed examples, canonical cross-links, and fewer stale or duplicated pages.

## [v1.36.0] — 2026-08-15

### Added
- **UI Toolkit tools**: `inspect_uitk` (VE tree inspection with `~N` refs), `uitk_element` (8-action query/mutation), `uitk_file` (12-action UXML/USS CRUD), `attach_uitk` (wire UIDocument + PanelSettings), `lint_uitk` (structural UXML/USS validation A1-A6), `uitk_intent` (NL → DSL → UXML/USS generation via Haiku).
- **uGUI tools**: `lint_ugui` (diagnose missing EventSystem, Canvas without GraphicRaycaster), `list_events` (read persistent listeners on UnityEvent fields).
- **Tool categories**: `UGUI` (4 tools) and `UITOOLKIT` (6 tools) — separate foldouts in Settings, both discovery-gated (not TIER1). `discover_tools("ugui")` / `discover_tools("uitoolkit")` for targeted discovery.
- **Playtest DSL**: `CLICK`, `FILL`, `FOCUS` steps for UI Toolkit via typed alias (`VAL $hud /Path|UIDocument`); 4-segment addressing (Variant B).
- **create_ui enhancements**: new types Toggle, Slider, InputField, ScrollView; `render_mode` param (SSO/SSC/WorldSpace); `font_min`/`font_max` for TMP autoSize.
- **set_rect**: `pos3` param for anchoredPosition3D (WorldSpace Canvas).
- **ui_intent DSL**: ContentSizeFitter (`hfit`/`vfit`), LayoutElement (`prefW`/`prefH`/`flexW`/`flexH`), padding/childAlignment/cellSize for layout groups.
- **ClientSkills**: split `unity-ui-authoring` → `unity-ugui-authoring` + `unity-uitoolkit-authoring` with wire_event bridge guidance and deadly traps documentation.

### Changed
- **`validate_layout` → `validate_triggers`**: renamed to reflect actual behavior (3D collider/trigger overlap check, not UI layout), moved from MEDIA to SCENE category. No backward compatibility alias.
- **`discover_tools("ui")`**: now returns `UGUI` + `UITOOLKIT` tools only (previously returned all MEDIA).
- **Canvas selection**: `create_ui` uses deterministic `FindOrCreateCanvas` with name-first lookup (G3 fix).
- **Settings catalog**: `GetCatalogCategories()` always returns `_defaultCatalog` — stale EditorPrefs no longer hide new categories on plugin update.

## [v1.35.1] — 2026-08-15

### Fixed

- **CI stability**: Windows `chmod(0o600)` guard (POSIX only), Linux unity-setup timeout increased (30min), xvfb exit-code 5 race condition handling, changelog diff downgraded to warning during development.

## [v1.35.0] — 2026-08-15

### Changed
- **Chat relay ACP-only migration**: Removed legacy v1 pipe protocol support. All chat backends now exclusively use Agent Communication Protocol (ACP) format output. No protocol negotiation or feature flagging; ACP is always-on.

### Removed
- **Legacy pipe adapter** (`adapters/legacy.py` + LegacyCliAdapter class) — v1 pipe format no longer supported
- **Plan MCP tools** (`plan_tool.py`) — removed: `plan_create`, `plan_approve`, `plan_reject`, `plan_edit`, `plan_status`
- **Plan data model** (`plan.py` + `plan_store.py`) — agent-generated action plan storage and workflow
- **Relay event parser** (C# `RelayEventParser.cs`) — replaced by ACP HandleEvent direct dispatch
- **Protocol version negotiation** — UNITY_MCP_ACP_* environment flags (OPENCODE, CODEX, CLAUDE) no longer used; ACP required

### Added
- **Chat UI cards for plan events** (`PlanStepCard.cs`) — visual Approve/Reject buttons for agent-generated plan steps
- **Extended HandleEvent support** — three new ChatEventKind cases: `PlanUpdate`, `FileChange`, `CapabilitiesChanged`

## [v1.34.0] — 2026-08-14

### Added

- **Chat Core System (T9-T24)**: Unified multi-provider agent relay architecture with canonical event stream.
  - **Agent Communication Protocol (ACP) adapters**: Support for Claude, Codex, and OpenCode subprocess backends via ACP format output mode. Feature-flagged via environment variables (UNITY_MCP_ACP_OPENCODE, etc.).
  - **Agent event model**: Canonical `AgentEvent` envelope with 16+ event kinds (session_started, turn_completed, tool_call_completed, cost_update, capabilities_changed, etc.), provider-specific event filtering, and forward-compatible schema versioning.
  - **Multi-provider routing**: Dynamic backend selection (Claude, Codex, Kimi, Agy, OpenCode) with protocol abstraction via `adapters/` package (acp.py, legacy.py, fixture.py for testing).
  - **Session identity & authorization**: `SessionIdentity` tracks session ID, lock token, agent ID, display name, started timestamp. `PermissionBroker` manages per-session MCP tool permission prompts and consent caching.
  - **Global configuration**: `GlobalConfig` singleton for server-wide settings (model presets, backend selection, feature flags).
  - **Context briefs**: `Brief` + `BriefBuilder` for on-demand scene context injection (compile errors, console, hierarchy, selection, profiler data).
  - **ChangeSet tracking**: Atomic multi-command transaction support via `Changeset` + `ChangesetCoordinator`. File capture, journal, and store for mutation tracking and rollback.
  - **Checkpoint save/restore**: Full scene state snapshots with `CheckpointStore` and `CheckpointRestore`. Manifest-based consistency checking.
  - **Plan workflow**: `Plan` + `PlanStore` for agent-generated action plans with approval/rejection workflow and TTL-based cleanup.
  - **Conversation history**: JSONL-based conversation store with retention eviction policies. `HistoryManager` coordinates store, models, and retention.

- **Four new MCP tools for Chat Core workflows**:
  - `brief` — on-demand context brief retrieval (compile status, console errors, hierarchy snapshot, profiler metrics)
  - `changeset` — query atomic transaction history and mutations
  - `checkpoint` — save/load/list scene checkpoints with manifest validation
  - `plan` — create/approve/reject/edit agent action plans

- **Client identity in TCP handshake (T5)**: `ClientHelloPayload` dataclass combines session identity, role, and connection metadata in single initial frame. Backward-compatible fallback to legacy project check for old C# clients.

- **Connection metadata tracking (T3)**: Per-connection snapshots with `ConnectionSnapshot` struct (11 fields: Kind, State, RemoteEndpoint, Label, SessionId, DisplayName, LastCommand, InFlightCount, idle time, connected timestamp, generation). `ClientActivityState` enum (Active/Idle/Dormant/Closing) tracks per-entry lifecycle.

- **Dormant bridge detection (T7)**: `DormantBridgeScanner` static class identifies bridge processes holding lock files but not in active TCP slots. Hierarchical server list in MCPStatusWindow shows both live and dormant bridges.

- **Chat relay v2 schema**: `protocol/chat-relay/v2/agent-event.schema.json` with JSON Schema validation for incoming events. Validation tests ensure ACP adapter compliance.

### Changed

- **Multi-lock file support**: `McpServerScanner` now detects multiple MCP connections per port via `server-{port}-*.lock` file enumeration. `UnityServerInfo` includes per-port `McpConnectionInfo[]` list.

- **Idle watchdog enhancements**: Transport-layer activity tracking split from user-initiated work via `_on_transport_activity` callback (T4). `BridgeState` enum expanded with DORMANT and WAKING states for low-power mode.

- **Profiler context bridge**: ProfilerChipProvider enables profiler to chat handoff with performance metrics as context.

### Fixed

- **ACP adapter compliance**: Parse errors in agent event streams now bubble to surface instead of silently failing.

- **Permission broker initialization**: Session authorization now properly gated on backend handshake completion.

- **Chat Core Hardening (Architecture Review)**:
  - **Atomic writes in ContentStore** — `store()` now writes to temporary file, then `os.replace()` atomically to prevent partial writes on crash.
  - **Path traversal guards in CheckpointStore** — `_path()` validates checkpoint_id format (alphanumeric + hyphens/underscores) to prevent directory traversal.
  - **Finalize barriers in ChangeSetCoordinator** — `append()` now checks `status != "open"` before operations to prevent mutations on closed changesets. `record_file_mutation()` now infers operation kind (create/delete/modify) from before/after refs.
  - **Fingerprint consistency in SessionIdentity** — Both project_id and config_dir fallback paths now use 12-char fingerprints (was mixed 8/12). Token hash parsing now gracefully handles invalid hex input.
  - **Safe file operations in checkpoint eviction** — `_safe_mtime()` and `_safe_size()` helpers catch OSError during stat() calls to prevent eviction crashes on missing/inaccessible files.
  - **Precondition checking in CheckpointRestore** — `restore_files()` now pre-checks all blobs exist before any writes, preventing partial restores.
  - **Dead code removal & DRY** — `_TRANSFORM_FNS` consolidated into `adapters/legacy.py` (shared by chat_relay and stream_transform). `ensure_history_manager()` unifies init/get lifecycle.
  - **26 new tests** covering edge cases: path validation, atomic write failures, eviction with missing files, token hash errors, fingerprint length consistency.

### Test Coverage

- **Python unit tests**: 6500+ passed (Chat Core modules: +218 new tests covering adapters, agent_event, brief, changeset, checkpoint, history, permission_broker, plan, session_identity)
- **C# EditMode**: 7950+ passed
- **New test files**: test_acp_adapter.py, test_agent_event.py, test_brief_builder.py, test_changeset.py, test_checkpoint_store.py, test_history_manager.py, test_permission_broker.py, test_plan.py, test_plan_store.py, test_session_identity.py, test_client_hello.py

## [v1.33.0] — 2026-08-13

### Added
- **Compact ref IDs** — RefManager uses `&` prefix + base62 encoding (`&1`, `&a`, `&Z`) instead of transient hex IDs. Backward-compatible parsing for `$N` digits.
- **WirePrefix constants** — centralized wire protocol prefix constants (`&` = Ref, `$` = Alias).
- **McpServerScanner** — discovers running MCP servers from port files, detects alive/dead via PID check, `CleanPhantomFiles()` removes stale entries.
- **MCPStatusWindow server list** — shows all detected MCP servers with port, PID, alive/dead status, and per-server Kill button.
- **KillByPort API** — `MCPActions.KillByPort(port)` kills a specific server by port, cleans lock + port files.
- **AnimationCurveCompactor** — groups `.x/.y/.z` into vectors, `.r/.g/.b/.a` into colors, omits unchanged properties.
- **Model presets** — `contextWindow` field on `ModelPresetEntry`, `SetOverrides()` cache, `ForDropdown()` API, ListView UI for all 5 backends.
- **CopyAsMcpRef** — `Cmd+Shift+C` shortcut copies selected GameObject's MCP ref to clipboard.

### Fixed
- **Phantom server detection** — `McpServerScanner.FindLock()` now searches lock files in correct directory (`~/.unity-biome-mcp/` root, not `ports/` subdir).
- **Chat text wrapping** — `ChatLabel.Selectable()` adds `chat-text` CSS class; `ChatTranscript` uses `flex-start` alignment + `width: 100%` on inner container for proper label wrapping.
- **ThinkingBlock USS selector** — changed `>` (direct child) to descendant selector to match UIToolkit Foldout internals.
- **Changelog rendering** — MCPStatusWindow now uses `MarkdownInlineFormatter.ToRichText()` (DRY with Updates page).
- **Tool card text wrapping** — added `white-space: normal/pre-wrap` USS rules for bash, agent, component-read, diff, and settings labels.
- **Play Mode stale refs** — `playModeStateChanged` hook invalidates scene caches on mode transitions.
- **ValueParser ref support** — RefManager fast-path for `&N` refs before hex branch in `SetObjectReference`.

## [v1.32.0] — 2026-08-12

### Fixed
- **Token consumption indicator accuracy** — Context progress bar now reads all four token consumption values (`input_tokens`, `cache_creation_input_tokens`, `cache_read_input_tokens`, and `output_tokens`) from the backend API response. Previously ignored cache tokens, showing 0.6% when the context was actually 188% full, or 0.009% when correct fill was 25%. The bar now displays the accurate effective context window fill.
- **Token indicator hiding on unknown usage** — When the backend does not report token consumption (e.g., during a cold relay startup), the indicator hides instead of showing a misleading 0% fill.
- **Relay connection stability — three defects:**
  - **Leaked poll thread** — `RelayBackend.Stop()` now sets an `_active` flag to prevent `OnRelayReady` from starting a new process thread during a cold start that fires on an abandoned backend; fixes the invisible poll thread that displaced the new backend's connection.
  - **Dropped multi-window callbacks** — `RequestSpawn` with `IsPending` now accumulates waiter callbacks instead of silently returning; when spawn resolves (success or error), all accumulated callbacks fire via `DrainWaiters`, fixing the second MCPChatWindow's `onReady` never being called and leaving its turn stuck with a 30-second timeout.
  - **Two-window relay displacement war** — Added single-window policy: `OnEnable` detects a second simultaneous `MCPChatWindow` via `Resources.FindObjectsOfTypeAll`, sets `_isDisplacedWindow`, skips backend creation, and displays a clear message; prevents the relay from being fought over by two UI windows.
- **Silent exception catches now log** — Secondary render passes in tool cards now catch non-IO exceptions and log them as warnings instead of silently swallowing them, making errors visible in the console during manual QA.

### Added
- **Context progress bar state thresholds** — Visual states for context window fill (Normal < 70%, Warn 70–89%, Danger 90–99%, Overflow ≥ 100%) apply as CSS state classes (`context-bar--normal`, `context-bar--warn`, `context-bar--danger`, `context-bar--overflow`); fill bar colors controlled by semantic tokens (`--chat-bar-fill`, `--chat-bar-fill-warn`, `--chat-bar-fill-danger`).
- **Overflow context label** — When the context window is exceeded (≥ 100%), the progress bar label shows the actual percentage (e.g. "226%") so the user can see how far over the limit the input has gone.
- **Clickable reference pills in table cells** — Tool result tables now render `[kind:ref]` tags in data cells as interactive pills; clicking navigates to the referenced object/asset (applies to all markdown table content, not just headers).
- **Secondary-pass protection for tool cards** — Tool card subclasses now call `RunSecondaryPass()` when enriching card content in a secondary render pass; the helper enforces marker-after-content ordering structurally, making it impossible to place a secondary marker before content even if the caller tries.
- **Guard against unhandled transcript entry kinds** — `TranscriptSerializer` guard replaced with enum exhaustiveness check (`Enum.IsDefined`) instead of a hardcoded constant; auto-updates when new `TranscriptEntry.Kind` values are added, eliminating the need for manual version bumps.
- **OpenCode tool results parsing** — Tool use completion events from OpenCode are now parsed and handled correctly (the previous handler was written for an incorrect format and never worked).

### Test Coverage
- **Python unit tests:** 6356 passed (was 6289)
- **C# EditMode:** 7950+ passed, 19 skipped
- **New test cases:** 19 context bar threshold + overflow tests, 3 relay spawn + waiter tests, relay window displacement policy tests, secondary-pass exception logging tests, transcript Kind exhaustiveness test

## [v1.31.0] — 2026-08-12

### Added
- **Four new tool card renderers for read-only tools:**
  - `ScreenshotCard` — displays screenshot results as a clickable thumbnail preview (max 160px height)
  - `HierarchyCard` — renders `get_hierarchy` results as a text-tree with depth-indented nodes; shows first 20 nodes, "Show more" button for remainder; click on any node selects it in the Hierarchy window
  - `BashCard` — shows command description, command line, and stdout/stderr output with 20-line preview and "Show more" button; marks exit code errors with red border; indicates truncation at ≥2000 characters
  - `ComponentReadCard` — displays component inspection results with clickable object path, component type(s), and property list (20 visible, "Show more" for rest); supports `get_component`, `inspect`, and `get_components_list` tools
- **Tool result display limit raised from 200 to 2000 characters** — longer tool outputs now display in full in chat, enabling complete scene trees and command outputs in tool cards (context payload unaffected; only displays truncated)
- **ToolCardBase framework** — shared rendering lifecycle for tool cards enforcing "content before marker" order; idempotency guard prevents duplicate rendering; exception safety ensures retry on build failure

### Fixed
- **AgentCard empty args forever empty** — empty arguments no longer block subsequent renders with real data
- **Failed turn not closed** — incomplete turns now close properly, preventing next response from flowing into orphaned bubble
- **Trailing newline hides diff display** — unified diff rendering now strips trailing newlines before display
- **List number overflow blocks render** — markdown list item number parsing no longer fails on high item counts
- **Subprocess spawn in headless mode** — background process start now checks `SystemInfo.graphicsDeviceType` before launching windowed processes (CI headless safety)
- **Reload loses all but first screenshot** — `ImageBlockRenderer` cache now persists across assembly reload; texture references survive via asset path lookup
- **Backend switch mid-turn freezes input** — backend changes during active turn now properly abort pending state instead of blocking input for 2 minutes
- **Saved agent clears backend preference** — agent selection now preserves previously active backend configuration

### Test Coverage
- **Python unit tests:** 6289 passed (was 6247)
- **C# EditMode:** 7923 passed, 19 skipped

## [v1.30.0] — 2026-08-12

### Added
- **Tool card renderer registry** — `ToolCardRendererRegistry` enables plugins to register custom renderers for tool chips by name (keep-first semantics); public `Register()` and `Unregister()` API
- **Four built-in tool card renderers:**
  - CodeEditDiffRenderer — displays unified diff per changed file (Edit, Write, MultiEdit) with intra-line highlights
  - MutationDiffCard — scene mutations (set_property, set_active, create_object, etc.) with before/after values and object navigation
  - TaskChecklistCard — accumulated task list (TaskCreate, TaskUpdate) with per-task status display (open/in-progress/completed)
  - AgentCard — subagent invocation; displays subagent type and task description from call args, plus result summary (~200 char) if result received
- **Model thinking blocks** — display extended thinking / reasoning as a collapsible section; enabled by default, ephemeral (not persisted in transcript after reload)
- **Agent @-mentions** — users now delegate work to subagents by typing `@` in chat input; `AgentMentionSource` discovers agents from `{projectRoot}/.claude/agents/*.md` and `{homeDir}/.claude/agents/*.md` with ancestor-directory walk; agents render as chips and trigger the Agent tool when invoked
- **AgentMissDetector** — warns via chip if a message contained `[agent:name]` mention but the model never invoked the Agent tool
- **Tool result passthrough** — Claude tool results now reach Chat; `stream_transform` emits `tr|` pipe-protocol events (truncated to 200 characters). Results surface as a short summary inside AgentCard; other cards use them to enrich already-rendered content
- **Human-readable tool labels** — `ToolVerbMap` provides friendly verbs for all built-in Claude Code tools (Agent, Edit, Write, Read, Bash, WebSearch, etc.) in tool-start chips
- **Nested markdown bullet lists** — depth-tracking parser renders indented list items; per-item depth preserved in layout
- **Clickable markdown links** — inline `[text](url)` now renders as clickable links (via `<link="url:...">` tag system); right-click context menu with "Navigate" and "Add to context" options
- **Table column alignment** — markdown table cells respect alignment hints (`:---`, `:---:`, `---:`)
- **Semantic color tokens** — `Chat.Tokens.uss` defines semantic palette with light-theme support

### Changed
- **`set_property` returns previous value** — response format now `"{prop} = {actual} (was {old})"` when prior value is available; gracefully falls back to `"{prop} = {actual}"` if the read fails; backward compatible (consumers reading the prefix are unaffected)
- **Agents removed from backend dropdown** — agent selection moved from persistent footer dropdown to per-message @-mention system for cleaner UX

### Fixed
- **Markdown links now clickable** — links from chat output were rendered as dead text; click handlers now installed on `<link>` tags via ChatRefAction
- **Nested lists no longer fragment** — nested markdown bullet lists were breaking into separate paragraphs; depth-tracking parser now preserves indentation structure
- **Click handlers on markup links** — links embedded in message output via the link tag system now route clicks to navigation handlers
- **Space before chip eaten by bold** — whitespace before a chip was consumed during bold-text normalization; StripOrphanBold guard now applied after chip detection
- **Image cache bounded to 50 entries** — `ImageBlockRenderer` texture cache now evicts oldest entries (FIFO) when reaching capacity; prevents VRAM leaks from accumulated screenshots

## [v1.29.0] — 2026-08-11

### Added
- Scene Drag & Drop into chat — `.unity` files from Project window create scene chips
- Hierarchy header right-click `🧬MCP/Copy Ref` for scenes (SceneHeaderContextMenu)
- `MentionRowActions.PingInProject` for scene chips
- `ServerUpdater` — auto-reinstall Python server when plugin version is newer (UPM update)
- `PluginUpdateMonitor` — C# version change detection on domain reload

### Changed
- Chip refs unified to `$HEX` format — `[hierarchy:/Player$3E8]` replaces `[hierarchy:/Player#1000]`
- Scene chips use name-only path — `[scene:MyScene]` instead of `[scene:Assets/Scenes/MyScene.unity]`
- `FindAssets` exact-match for scene name resolution (no substring false positives)
- `AssetMentionIndex` delegates to `SceneChipProvider.Create` for consistent scene bracket format

### Fixed
- `_default_is_uvx_install()` false-positive for Homebrew/system Python
- `proc.wait()` timeout guard in `ServerUpdater` — prevents stuck `_updating` flag
- `asyncio.create_task` lifecycle in bridge — stored reference prevents GC

## [v1.28.0] — 2026-08-10

### Added
- **Editor UI rebrand** — all user-facing editor strings now use 🧬MCP menu label with emoji toggle. `BiomeLabel.cs` centralizes display name and log tag, settable via EditorPrefs. Affects Setup Wizard, Hub, Settings, Chat windows, and all logged output.
- **@-mention extensions** — `MentionConfig` (serialized in backend config) controls popup max rows (3–20, default 8) and sort order (Relevance, Name, Type, Recency). `MentionHistory` persists path→timestamp for recency sorting. Right-click popup on mention rows adds 4 actions: Add to Context, Copy Reference, Ping in Hierarchy, Ping in Project.
- **MentionHistory recency tracker** — stores per-path last-commit timestamps (max 100 entries) in Library/MCP_MentionHistory.json. Enables ByRecency sort order in @-mention popup.

### Changed
- **Property context menu unified** — all property context menu items consolidated under single submenu with raised priority. Expanded PropertyContextMenuBridge to support ScriptableObject targets (not just scene GameObjects). Added "Copy Ref" action to chip context menus.
- **FieldChipProvider asset paths** — now resolves asset-path chips in addition to scene paths, enabling copy-as-reference for material, texture, and scriptable-object fields.
- **Unified object ID format** — RefManager outputs `$HEX` format (e.g. `$3E8`) instead of mixed `$a`–`$zz` and `#decimal` formats. Input parsing accepts both formats for backward compatibility. Tightened `IsRef` check to lowercase-only hex strings.
- **Object reference docstrings** — updated `set_property`, `rename_object`, and `get_component` docstrings to document `$hexId` format (e.g. `$3E8`) and note `#instanceID` as legacy.
- **Python reflect module** — updated reflect rules to handle both `$HEX` and `#decimal` object ID formats for verification after mutations.

### Test Coverage
- **C# EditMode**: 7481 passed, 0 failed
- **Python unit tests**: 6248 passed
- **Python live integration**: 313 passed

## [v1.27.0] — 2026-08-10

### Fixed
- **Startup probe crash at capacity** — `EOFError` (from `asyncio.IncompleteReadError`) is now caught in `bridge._verify_candidate_project`, preventing server crash when Unity rejects a 9th connection
- **Retry watchdog data loss** — the deduplication guard now tracks per-write generation so legitimate repeated saves after intervening mutations are no longer suppressed
- **Python-local writes bypass read-only** — `save_skill`, `save_template`, `save_session` now check `UNITY_MCP_READ_ONLY` before writing files
- **Read-only guard blocks valid reads** — replaced command-level classification with action-aware `is_write(cmd, args)` so read actions of mixed commands (`bake/status`, `package/list`, `scene_environment/get`) work in read-only mode; RO denial now raises `ToolError` (`isError=true`)
- **Distiller hints unsupported `full=true`** — hint now says `full=true` only for tools that support it; others say `"distilled for brevity"`
- **Reconnect port identity split** — port/host assignment moved inside try block so failed reconnect preserves the original working port
- **Doctor false unhealthy** — diagnose timeout increased from 3s to 10s with injectable `_diagnose_timeout` parameter
- **PlayTest suite auto_play leaks Play Mode** — moved auto_play block inside try/finally so `editor(stop)` always executes on error
- **`await_compile` false clean** — added settle window (default 4s, `UNITY_MCP_COMPILE_SETTLE_SECS`) before returning on idle to handle Unity file-monitor latency
- **Bulk `set_property` dry_run always mutates** — `dry_run` flag now forwarded in the `find_type` bulk path instead of hardcoded `false`
- **Component lookup fails after AddComponent** — `ResolveComponent` now uses `ComponentSerializer.FindComponent` instead of unreliable string-based `GetComponent`
- **`set_parent` on prefab child silent no-op** — now throws `InvalidOperationException` for non-root prefab instance children in Edit Mode
- **FBX subasset dry_run skips validation** — `ObjectReference` paths validated in dry_run; null guard added in subasset assignment loop
- **`wire_event` ignores disambiguation params** — `target_component_type` and `parameter_types` now forwarded from JSON args through the registration lambda
- **Nested Animator state lookup fails** — `FindStateAcrossLayers` now uses recursive search through sub-state-machines with cycle guard
- **`lint_scene_refs` false-positive on INCLUDE aliases** — linter now reads `ValDefs` from parse result and skips known aliases
- **INVOKE fails with custom implicit types** — `ConvertValue` now probes `op_Implicit`/`op_Explicit` operators with reflection cache cleared on domain reload
- **Nightly CI broken** — two monkey tests fixed (missing `patch.dict(BACKENDS)`); install/scripts steps now use `if: always()`
- **`FindObjectsOfType` deprecation** — migrated 4 call sites to `FindObjectsByType` (Unity 6 CS0618)

### Added
- **`discover_tools` mutability field** — tool surface line now includes `mutability=read|write`
- **Python 3.10 CI matrix** — ubuntu-latest job added for the advertised `>=3.10` requirement
- **3 new PlayTest corpus files** — `player_ci_bounds`, `player_ci_multi_move`, `player_ci_reset` expanding Grid game coverage to 47 steps

### Removed
- Dead `perf` pytest marker (0 tests used it)

### Test Coverage
- **Python unit tests**: 5346 (was 4815, +531 from v1.26.0 pipeline)
- **C# EditMode**: 7413 passed, 0 failed, 19 skipped
- **Live integration**: 313 passed (headless, 1 requires_graphics skipped)
- **30+ new tests** covering all 22 defects

## [v1.26.1] — 2026-08-09

### Added
- **Hosted disposable Unity conformance** — the conformance workflow now provisions its own Linux, macOS, and Windows Unity fixture projects on GitHub-hosted runners instead of depending on pre-opened editors.
- **Batchmode MCP bootstrap guard** — the Unity editor plugin can start in batchmode only when explicitly enabled for CI, with an optional bootstrap scene for deterministic hosted conformance.
- **Standalone Player PlayTest CI slice** — optional player build/playtest workflow validates the checked-in text-mode GridTest PlayTest corpus and expected-failure receipts.

### Fixed
- **Unity EditMode CI false-green** — removed the invalid category expression passed through `-testFilter` and made the workflow fail closed when Unity reports zero executed tests.
- **PlayTest path fixture isolation** — temporary path tests no longer delete the checked-in `Playtests/` corpus during full EditMode runs.
- **Cross-platform CI process handling** — hosted conformance cleanup and MCP monitor paths now work consistently across macOS, Linux, and Windows.

### Test Coverage
- **Python CI gate:** Linux/macOS/Windows matrix green on the release feature branch.
- **Unity EditMode CI gate:** Linux/macOS/Windows matrix green with non-empty test execution.
- **Hosted conformance gate:** disposable Unity workers green on Linux, macOS, and Windows.

## [v1.26.0] — 2026-08-08

### Added
- **Unified test taxonomy** — 12-tier categorization system (T0–Tg) across Python markers and C# NUnit categories
- **`TestCategories.cs`** — 7 compile-safe category constants (`Stress`, `RequiresGraphics`, `FaultInjection`, `LiveCLI`, `InteractiveVisual`, `Perf`, `WorkerOnly`)
- **Nightly full regression workflow** (`nightly.yml`) — runs all tests (including monkey, slow, GPU-dependent) at 02:00 UTC + manual dispatch
- **`slow` and `perf` pytest markers** — registered in pyproject.toml for test tier classification

### Changed
- **`--strict-markers`** enforced in pytest — typos in marker names now fail immediately
- **`--timeout=30`** default per-test timeout via pytest-timeout
- **Monkey tests** standardized to module-level `pytestmark` (was per-function decorators)
- **`unity-tests.yml`** — added `-testFilter` to exclude FaultInjection, LiveCLI, RequiresGraphics, InteractiveVisual, WorkerOnly categories from standard CI

### Fixed
- **Conformance marker bug** — 7 conformance test files now include `pytest.mark.conformance` (was silently skipped by CI dual-worker job)
- **`RequiresGraphicsDeviceAttribute`** gains `[Category("RequiresGraphics")]` (DRY: applied on attribute, inherited by all 7 fixtures)
- **`BiomeWorkerOnlyAttribute`** gains `[Category("WorkerOnly")]` (DRY: same pattern)
- **Orphaned `test_docs_transform.py`** moved from `tests/` to `scripts/tests/` (now runs in CI)
- **Duplicate `[dependency-groups]`** removed from pyproject.toml

### Docs
- **test-authoring.md** — comprehensive categorization guide with DO/DON'T examples and 8-step test selection decision tree

## [v1.25.0] — 2026-08-08

### Added
- **Cross-project conformance suite** — 8 conformance gates (connect, read, write, batch, playtest, alias, error recovery) with ConformanceWorker session fixture
- **Dual-worker cross-project tests** — isolation, identity, fault injection (TCP proxy + JSONL trace minimizer) across Worker A (RW) and Worker B (RO)
- **ReadOnly MCP mode** — `IsReadOnly` property blocks mutating commands on read-only workers; `CheckGuards()` returns `READ_ONLY_BLOCKED`
- **`[RequiresReadWrite]` test attribute** — marks test classes/methods that need a RW worker; `EnforceReadWriteRequirement()` returns skip reason for `Assert.Ignore` on RO workers
- **Centralized 3-port allocation** — `PortResolver.BindFreePort(startFrom, skipPort, skipPort2)` atomic scan+bind eliminates TOCTOU race; `PortFileManager.EnsurePorts()` resolves main+chat+reload atomically
- **CI conformance pipeline** — 3-job design with release hard gate and SHA mismatch warning
- **Conformance runner CLI** — `conformance_runner` for consumer project validation

### Fixed
- Reload port no longer collides with chatPort or main port (WI-6/6b)
- `scene` command correctly classified as non-mutating (P-414: per-action mutation tracking)
- `RequireReadWriteBoundary` fires after isolation active — NUnit soft-skip no longer cascades to derived SetUp
- `console_mark` overflow returns warning, not error (P-413)
- `editor(play)` refreshes Python play guard (P-415)
- Failed playtest suite now stops Play Mode via try/finally (P-336)
- Component cache invalidated after `manage_component` (P-416)
- Scene dirty flag verified after save in `apply_scene_change` (P-414)

### Test Coverage
- **Python unit tests**: 4807 total (was 4776, +31)
- **Python live tests**: 332 (was 287, +45) — conformance gates, cross-project isolation, fault injection
- **C# test attributes**: 6415 (was 6367, +48) — port resolver, ReadOnly guards, batch atomic, command registry
- **Test inventory**: 12065 entries (was 11941, +124)

## [v1.24.0] — 2026-08-07

### Changed
- **UPM package description** — expanded with bullet-pointed feature list (150+ tools, 10+ clients, 8800+ tests); plain-text with `▪` bullets for UPM Details panel
- **UPM keywords** — expanded from 5 to 17 for discoverability (added AI client names, feature terms, taxonomy)
- **documentationUrl** — switched from GitHub README anchor to GitHub Pages site
- **Package type** — added `"type": "tool"` for UPM editor-tool categorization

### Added
- **8 package manifest validation tests** — bullet structure, keyword quality (no duplicates, lowercase, no spaces), documentation URL, Unicode round-trip, type field
- **OpenUPM registration** — package listed on openupm.com as `com.unity-biome-mcp.editor`

### Test Coverage
- **Python unit tests**: 4776 total (was 4768, +8) — 8 new package.json validation tests
- **Test inventory**: 11941 entries (was 11933, +8)

## [v1.23.0] — 2026-08-07

### Fixed

**Protocol / Schema**
- **apply_scene_change transaction boundary** — unsaved changes detected before scene mutation; prevents silent data loss (P-033)
- **get_watches phantom removal** — removed stale `get_watches` entry from `_PYTHON_ONLY` set (P-342)
- **console_mark/get_console_since tier promotion** — promoted to tier1; was inaccessible via default tool filter (P-350)
- **Full schema keep for timeline/animation/animator** — tools retain full schema instead of being pruned (P-091)
- **Full schema keep for checkpoint** — checkpoint tool schema preserved through filtering (P-363)
- **_stdio_alive grace window** — 30s grace period after last confirmed write prevents false broken-pipe on slow clients (P-348)
- **Screenshot paths outside Temp/MCP** — accept screenshot output paths in any writable directory (P-383)
- **Wave 3 C# fixes** — serialization, DSL, transport, and verification corrections (P-390, P-343, P-384, P-305)

**Test Infrastructure**
- **106 C# EditMode test failures** — `CloseLeakedEditorWindows` ordering, try-catch + `DestroyImmediate`, `MainThreadDispatcher.Clear()` isolation, `EditorPrefs` isolation in chaos tests
- **EditorWindow zombie cleanup** — `CreateOwnedEditorWindow` and `MCPChatWindow.TestIsolation` hardened with try-catch + unconditional `DestroyImmediate`
- **Python live test_chat_ui_monkey** — orphan MCPChatWindow cleanup instead of `pytest.fail()`
- **Python live test_sync_live** — `_wait_compile_idle()` for domain reload recovery after sync

### Added
- **Dual-project port isolation tests** — verify port file management across concurrent Unity projects (P-323)

### Test Coverage
- **Python unit tests**: 4768 total (was 4752, +16) — server + scripts + install
- **C# EditMode tests**: 6367 passing (was 6361, +6)
- **Live integration tests**: 287 passing
- **Test inventory**: 11933 entries (was 11911, +22)

## [v1.22.1] — 2026-08-07

### Fixed

**Transport / Protocol**
- **Concurrent TCP serialization** — asyncio.Queue serializes overlapping MCP requests; eliminates interleaved frame corruption under parallel tool calls (P-092)
- **Transport status accuracy** — dead stdio pipe and offline TCP socket now distinguished in `mcp_status` response (P-320)
- **Mutation retry idempotency** — `operation_id` deduplication via `DedupRegistry` prevents re-applying the same mutation on network retry (P-322)
- **Read-only endpoint enforcement** — write commands blocked if active connection profile is read-only (P-324)
- **direct_only tools TCP guard** — tools flagged `direct_only` rejected at TCP dispatch layer (P-011)

**Playtest DSL**
- **VAR alias path preservation** — `VAR $alias` expansion in `WAIT_UNTIL` preserves full `/path|Comp|field` ref (P-262)
- **Suite lifecycle FSM** — `run_playtest_suite` state machine fail-closed; transition receipts prevent ghost suites (P-325, P-336)
- **Compound timeout assignment** — `HasExplicitTimeout` flag set correctly on compound helper steps (P-263)
- **Parser tokenizer sharing** — `PlaytestParser` tokenizer reused in `SceneRefLinter` (P-287)
- **run_playtest_suite schema keep** — added to full-schema keep list; was pruned during tool filtering (P-321)
- **INVOKE @scene syntax** — `@scene` prefix routes to correct scene context with structured diagnostics (P-304)

**Scene / Camera**
- **UI Transform substitution** — `get_component`/`inspect` return `RectTransform` when `Transform` requested on UI object (P-210)
- **Physics sync before spatial read** — `Physics.SyncTransforms()` called before bounds query (P-160)
- **Camera ref null-out on fresh reload** — Camera references cleared before `run_playtest(fresh=true)` domain reload (P-109, P-291)
- **scene_change_plan lifecycle source** — `EditorApplication.isPlaying` read directly instead of cached state (P-339)
- **apply_scene_change reference threading** — target paths passed through to `validate_references` (P-098)

**Component / Serialization**
- **get_component/inspect path resolver unification** — both tools call same `ComponentSerializer.Serialize` resolver (P-107)
- **set_property typed component selector** — object reference properties specify component type explicitly (P-258)
- **wire_event overload resolution** — correct listener overload when `typed_component` and `param_types` provided (P-335)
- **RuntimeHelper argument converter registry** — `IArgumentConverter` registry with `Parse` fallback (P-073)
- **ParticleSystem renderMode enum** — corrected `RenderMode.Stretch3D` to `Stretch` (P-117)

**Console / Verification**
- **get_console_since MCP-synthetic filter** — internal heartbeat entries excluded from results (P-051)
- **Console watermark epoch ordering** — monotonic epoch counter prevents mark ID wrap (P-NEW-3)
- **Screenshot dimension validation** — validates PNG width/height before returning (P-317)

**Fingerprint / Alias**
- **Stable fingerprint mode** — deterministic output regardless of scene load order (P-021, P-108, P-106)

**Tool Metadata**
- **15 Python-only tools marked direct_only** — prevents them from appearing as TCP commands (P-NEW-1)
- **Visible tool count corrected** — matches actual filtered surface after reclassification (P-319)

### Added
- **scene `save_copy` action** — saves current scene to specified path without changing active scene (P-NEW-2)
- **WAIT_STABLE DSL step** — waits until numeric field stops changing within threshold (P-110, P-305)
- **CAPTURE_MIN/MAX, ASSERT_MIN/MAX DSL steps** — capture min/max over window, assert bounds (P-110, P-305)

### Changed
- **editor_state.py parser extracted** — EditorApplication state parsing moved to dedicated module

### Test Coverage
- **Python unit tests**: 4752 total (was 4636, +116) — server + scripts + install
- **C# EditMode tests**: 6361 passing (Unity 6000.0.65f1, was 6286, +75)
- **Live integration tests**: 287 passing
- **Test inventory**: 11911 entries (was 11720, +191)

## [v1.22.0] — 2026-08-06

### Added
- **Tool schema postprocessor pipeline** — Injects parameter descriptions and `additionalProperties: false` on all tool schemas at registration; zero runtime cost
- **Parameter descriptions in tool schemas** — 197 descriptions centralized in `_param_descriptions.py`, with per-tool overrides and common fallbacks (e.g. `path`, `compress`, `timeout`)
- **Tool title auto-generation** — All 148 MCP tools now have a canonical title in their schema
- **Side-effect and confirmation clarity** — Added "Modifies/Deletes/Enters X. No confirmation required." to 16 tools: `animator`, `asset`, `auto_wire`, `get_metrics`, `prefab`, `project_settings`, `wire_event`, `unwire_event`, `delete_object`, `manage_component`, `package`, `run_playtest_suite`, `region_clear`, `navmesh_query`, `run_tests_wait`, `shader`, `watch`

### Fixed
- **Animator `has_exit_time=False` bug** — Transition exit time now correctly sends "false" instead of None when `has_exit_time=False`
- **get_metrics annotation** — Changed from RO (read-only) to RW (read-write) because `reset=True` mutates telemetry counters; no confirmation required when resetting

### Changed
- **Tool annotation semantics** — `get_metrics` is now explicitly mutable (RW) rather than read-only (RO)
- **CI linting pipeline** — Removed `mcp-lint` NodeJS tool; all schema validation now via `mcp-tool-card-linter` (Python-based, single linter)
- **Export tools format** — `export_tools.py --format toolsmith` now includes tool titles and annotations in output

### Test Coverage
- **Python unit tests**: 5561 total (was 5545, +16) — server + scripts + install
- **C# EditMode tests**: 7268 passing (Unity 6000.0.65f1)
- **Live integration tests**: 277/278 passing

## [v1.21.0] — 2026-08-06

### Fixed
- **Port field shake** — `isDelayed = true` on port IntegerField prevents keystroke-triggered validation and ShakeX animation
- **Port discovery race** — `discover_port_with_retry()` retries up to 4× when Unity hasn't written `.port` file yet
- **Windows CWD path mismatch** — `os.path.normpath` + `_is_path_prefix()` fix Unity `/` vs Python `\` path comparison
- **Kill MCP** — scans all `server-*.lock` files instead of port-specific glob
- **SaveProjectSettings** — `TrySaveProjectSettings` returns bool + logs warning on failure
- **Diagnose panel** — skips Python server directory check for uvx/UPM git installs
- **SetupDiagnosticsTests** — cross-platform venv layout (both `bin/python` and `Scripts/python.exe`)

### Added
- **Restart MCP button** in port settings UI — appears after port change
- **3-OS CI matrix** — Python tests on Linux, macOS, Windows; Unity EditMode on all three
- **pytest-timeout** — 30s global timeout, 120s for realtime test
- **VC++ 2010 runtime** — `choco install vcredist2010` on Windows CI for Unity 6
- **Git Bash discovery** — 5-step fallback chain for `_find_bash()` on Windows
- **Concurrency group dedup** — `github.head_ref || github.ref` prevents duplicate CI runs

### Changed
- **22 cross-platform test fixes** — `_is_pid_alive` mock, `encoding="utf-8"`, timer precision, bridge shutdown mode
- **Unity cache disabled on Windows CI** — fresh Hub install ensures VC++ runtimes

## [v1.20.0] — 2026-08-06 — P-12440 Production Backlog Release

### Phase 1: Tool Surface Reorganization
- **Tool surface optimization** — Reorganized MCP tool tiers: 47 → 34 always-visible tools (-28% token overhead)
  - **CORE tools**: 15 → 13 (demoted `do`, promoted `compile_preflight`, `mcp_status`)
  - **TIER1**: 47 → 34 total (CORE + 21 category-promoted tools)
- **Demoted to category tools**: `delete_object`, `set_parent`, `scene`, `search_scene` (SCENE), `resolve_scene_refs` (VERIFY)
- **Demoted from CORE**: `do` → SYSTEM (direct_only=True)
- **Tool Metadata DRY**: ToolSpec now single source of truth for all tier/category/flag changes (M8 architecture pattern)

### Phase 2–6: Production Bug Fixes & Improvements
- **5 P1 critical fixes** — PlaytestRunner fresh=true double-reload, INVOKE multi-arg, ValueParser component refs, ValidateReferencesHelper particles, transaction null-safety
- **12 P2 high-priority fixes** — ObjectManager warnings, WAIT_UNTIL re-expansion, AliasExpander 3-pass, ConsoleCapture dedup, autobatch paths, Circuit Breaker, PlaytestParser edge cases, bridge retry tracking
- **8 P2 improvements** — ReferenceHelper depth-first traversal, ScreenshotCapture fallback, Play Mode checks, SceneHelper settle verify, layer handling, TELEPORT sync, float precision, discover_tools hints
- **21 P3 medium fixes** — Category tools, runtime checks, serialization edge cases
- **5 P4 low improvements** — Test suite params, middleware hooks, export_tools hashing, AllowNull attribute

### Test Coverage
- **Python unit tests**: 5545 total (server + scripts + install)
- **C# EditMode tests**: 7268 green (Unity 6000.0.65f1)
- **Live integration tests**: 277/278 passing

## [v1.19.0] — 2026-08-05

### Added
- C# code coverage via Unity Code Coverage 1.2.6, uploaded to Codecov with `csharp` flag and `carryforward` support
- SonarCloud source-only C# quality scan (`ci-csharp-quality.yml`) with PR decoration
- OpenSSF Scorecard workflow with OIDC auth, SARIF upload, and weekly schedule
- Test pyramid visualization in `stats.svg` — 4 data-driven horizontal bars (C#/Python/Stress/Live)
- Codecov flag separation: `python` and `csharp` flags with independent `carryforward`
- SonarCloud and OpenSSF Scorecard badges in README

### Changed
- Python Codecov upload now tagged with `flags: python`
- `stats.svg` height expanded 304 → 380px for pyramid section

## [v1.18.1] — 2026-08-05

### Added
- **MCP Tool Schema page** (`docs/tools-schema/`) — auto-generated MkDocs page with per-tool parameter tables, JSON schemas, and toolsmith quality scores; deployed to GitHub Pages under Quality tab
- **`scripts/generate_schema_page.py`** — renders tool schema page from exported definitions + toolsmith lint report; supports both audit and lint report formats
- **Docs workflow schema generation** — docs CI generates tool schema page at build time with full toolsmith scores

### Changed
- **Score badge thresholds** — red 0–59, yellow 60–79, green 80+ (was 70/90)
- **CI rebase strategy** — `git pull --rebase -X theirs` for auto-generated data conflicts

## [v1.18.0] — 2026-08-05

### Added
- **Quality Report** (`docs/quality/REPORT.md`) — auto-generated transparency report with test results, code coverage, and per-tool quality scores; updated on every push to master
- **Test results aggregation** (`scripts/collect_test_results.py`) — parses pytest junitxml and NUnit XML into unified JSON; supports multiple suites across platforms
- **CI test result collection** — Python CI exports junitxml, Unity test workflow aggregates C# EditMode results from Linux/macOS/Windows into `unity-tests.json`

### Changed
- **README badges split by ecosystem** — Python row (CI + Coverage + Tool Quality) and C# row (Linux/macOS/Windows EditMode) for clarity
- **Glama card → score badge** — compact inline badge instead of large card SVG
- **Quality badge links to REPORT.md** — clickable badge navigates to full quality breakdown
- **CI toolsmith parser fixed** — correct keys `issues_by_severity.error`/`.warning` + `score` (was `total_errors`/`total_warnings`/`average_score`)
- **CI bash word splitting fixed** — inlined pytest commands instead of variable expansion
- **Badge job race condition fixed** — `git pull --rebase` before push prevents CI bot conflicts

### Removed
- **MkDocs quality dashboard** — removed broken `docs/quality/index.md`, JS, CSS (replaced by REPORT.md)

## [v1.17.0] — 2026-08-05

### Added
- **Quality pipeline infrastructure** — three new scripts for automated linter validation and metrics tracking
  - `export_tools.py` — exports MCP tool definitions to JSON (toolsmith or mcplint format) for schema linting
  - `quality_delta.py` — parses linter reports (mcp-toolsmith, mcp-lint), computes quality metrics, writes dashboard data
  - `check_skills_freshness.py` — static validation: scans skills/agents for stale references, tool spec parity, version mismatches (7 checks)
- **Tool Quality Dashboard** — MkDocs page at `/docs/quality/` with inline JS rendering latest linter metrics, score history sparkline, per-linter breakdown
- **CI quality gates** — `tool-quality` job in ci-python.yml runs export_tools + linters + quality_delta; release-preflight enforces quality checks
- **pytest-cov + Codecov integration** — `.github/codecov.yml` configuration, pyproject.toml coverage config
- **5 Hypothesis property-based tests** (`test_property_based.py`) — TCP framing, JSON serialization, command envelope round-trips, bridge protocol invariants
- **GitHub Actions SHA-pinning** — all 8 workflows (17 unique actions) pinned to commit SHAs for supply-chain security

### Changed
- **Ruff rules expanded** — added SIM (simplify), C4 (comprehensions), RET (return), TCH (type-checking), PERF (performance); fixed 103 violations across 54 files
- **Tool quality metric baseline established** — mcp-tool-card-linter + mcp-lint running on every PR

## [v1.16.0] — 2026-08-05

### Added
- **Shader Graph auto-layout** — `graph_auto_layout` action detects and resolves overlapping nodes via BFS topological sort (layered layout with configurable h_gap/v_gap)
- **Shader Graph get/set layout** — `graph_get_layout` reads node positions, `graph_set_layout` writes them back; both support full and short format
- **Overlap detection** — `CountOverlaps` O(n²) pairwise AABB check reports overlap count before and after layout
- **Cycle guard** — `ComputeLayout` BFS terminates in O(n²) iterations even with cyclic edges
- **ShaderGraph editor window guard** — `AutoLayout` refuses to run if the Shader Graph editor is open (prevents file/editor conflicts)

## [v1.15.0] — 2026-08-05

### Added
- **SETUP/TEARDOWN DSL blocks** — playtest scripts support setup/teardown sections; setup failure skips main steps and jumps to teardown
- **SET_ACTIVE** — first-class playtest DSL command for toggling GameObject active state
- **console_clear_buffer** — MCP command resets dropped-problem count without clearing the ring buffer
- **warm_type_cache** — automatic TypeCache refresh after `sync_unity` prevents stale `manage_component`
- **STALE_CACHE error code** — `get_component` returns structured error for stale ref/entity lookups
- **prefab instantiate** — new `prefab` action with optional `parent` parameter for instantiation under a target object
- **discover_tools cross-check** — startup validation ensures CATEGORIES tools have matching ToolSpec entries

### Fixed
- **INVOKE** — supports optional C# parameters and arity-based overload matching
- **batch conflict analyzer** — uses `id` as key for ID-only deletes (no false warnings)
- **batch Python-only params** — strips `full` parameter before forwarding to C# TCP
- **CONSOLE_ERR** — during playtest step now correctly increments failed count
- **verify_after_change** — ignores synthetic dropped-count console lines
- **sync_unity** — inner sends respect outer deadline (no double-timeout)
- **editor(play)** — polls until Play Mode is stable instead of returning immediately
- **set_property** — accepts null/empty to clear ObjectReference fields
- **scene_health** — reports MeshFilter with null sharedMesh
- **render_analyze** — guards MeshFilter lookup by renderer subtype (no crash on non-MeshRenderer)
- **validate_references** — root-level paths with leading slash resolve correctly
- **discover_tools** — static categories cross-check (not plugin-injected)
- **CommandRegistry snapshot** — updated for `console_clear_buffer` and `warm_type_cache`

### Changed
- Bump `actions/upload-pages-artifact` 3 → 5, `softprops/action-gh-release` 2 → 3

## [v1.14.0] — 2026-08-04

### Added
- Unity EditMode NUnit CI — parallel test execution on Linux, macOS, Windows via buildalon actions
- `RequiresGraphicsDeviceAttribute` — auto-skips GPU-dependent tests in headless CI
- `SkipOnWindowsAttribute` — marks Windows-incompatible tests with per-test reasons
- Per-platform NUnit test reporting via dorny/test-reporter with job summaries
- CI status badges for Python CI and Unity EditMode Tests (Linux, macOS, Windows) in README
- Canonical `unity-test-project/` worker committed for CI and local test execution

### Fixed
- 92 pre-existing Windows platform test failures (paths, shell, relay) resolved via skip attributes
- UTF-8 encoding for CI job summary on Windows (cp1252 emoji crash)
- Unity Hub 3.20+ Linux path regression pinned to Hub 3.19.5
- Windows 2025 runner WMI license timeout pinned to windows-2022

## [v1.13.0] — 2026-08-04

### Added
- GitHub Actions CI pipeline — ruff linting, pytest matrix (Python 3.11/3.12), README facts check, badge auto-update
- CodeQL security scanning for Python (weekly schedule + push/PR triggers)
- Dependabot configuration for pip and GitHub Actions dependency updates
- GitHub Release automation from CHANGELOG.md extraction on tag push
- Release preflight workflow for pre-release validation
- Version consistency check as PR gate
- PR auto-labeling via labeler (area labels for server, plugin, docs, CI, scripts)
- CODEOWNERS file for code ownership and review routing
- Pull request template with checklist and test evidence tiers
- Ruff linting configuration in `server/pyproject.toml` (E/F/I/UP/B rule sets)

### Fixed
- Codebase-wide ruff violations across 133 files (import sorting, unused imports, typing modernization)
- Relay pipeline test race condition on Python 3.12 (`wait_for_events` accumulates until terminal event)
- Pre-existing test failures in scripts/tests (stale SKILL.md kwargs, missing mkdocs.yml fixture)
- Re-exports in server.py, chat_relay.py, describer.py, update_readme.py preserved with `# noqa: F401`

## [v1.12.0] — 2026-08-03

### Added
- `docs/tools/tests.md` — new page documenting 9 testing tools (run_tests, run_tests_wait, get_test_run, etc.)
- `docs/tools/spatial.md` — new page documenting 9 spatial tools (spatial_query, navmesh_query, check_colliders, etc.)
- Documentation for 35+ previously undocumented tools across assets, runtime, diagnostics, and scene categories
- `bake`, `build`, `package`, `render_analyze` tool documentation in assets.md
- Debug, profiling, watch, snapshot tool documentation in runtime.md
- `verify_after_change`, `scene_health`, `validate_references`, `serialized_field_rename_audit` documentation in diagnostics.md
- Transactional scene edit docs (`scene_change_plan`, `apply_scene_change`) in scene.md
- Path escaping syntax documentation (v1.4.0: `\/`, `[bracket]`) in playtest.md
- Code execution QoL features (v0.89.0) in code-execution.md

### Fixed
- Removed ghost tool `set_runtime_property` from all user-facing documentation
- Fixed `scene_name` → `scene` parameter name in scene.md
- Fixed `paths` → `pattern` parameter name for `run_playtest_suite` and `lint_playtest_suite`
- Fixed `set_property` Play Mode claim (Edit Mode only; Play Mode writes lost on stop)
- Fixed `build` action parameter documented as optional (now correctly marked required)
- Fixed `find_objects` batch example using wrong param names
- Fixed broken intent-tools.md anchor links
- Fixed `scene_health` focus options to match C# source (hierarchy/naming/duplicates/origins/missing/empty/disabled)
- Marked `audio` and `input` as read-only project_settings targets in ClientSkills

### Changed
- DRY cleanup: wire_event/unwire_event canonical in components.md only (forwarding stubs elsewhere)
- DRY cleanup: console_mark/get_console_since canonical in diagnostics.md only
- DRY cleanup: resolve_scene_refs/lint_scene_refs canonical in diagnostics.md only
- Actualized AI/ docs: tool counts (148 user-visible, 154 total, 47 TIER1, ~5088 tests)
- Actualized 7 ClientSkills + 1 agent for v1.10–v1.11 (build/package/bake routing, transactional edits, path escaping, execute_code QoL)
- Replaced duplicate Token-Saving Patterns in prompting-tips.md with cross-link to tool-guide.md

## [v1.11.0] — 2026-08-03

### Added
- `bake` MCP tool: trigger lighting and occlusion bake operations from Claude (`BakeHelper.cs`)
- `build` MCP tool: invoke Unity BuildPipeline player builds with target/path control (`BuildHelper.cs`)
- `package` MCP tool: list, add, and remove Unity Package Manager packages (`PackageManagerHelper.cs`)
- `project_settings` tool: read/write PlayerSettings, QualitySettings, Physics, and Time fields (`ProjectSettingsHelper.cs` extended)
- `asset` tool: import, move, copy, delete, and query asset database entries (`AssetDatabaseHelper.cs` extended)
- `navmesh_query` tool: bake NavMesh, query agent positions, and sample paths (`NavMeshHelper.cs` extended)
- `editor` tool extensions: editor state, pause/play control, and layout management (`EditorStateHelper.cs` extended)

### Changed
- `CommandRouter`: registered 3 new command handlers (bake, build, package) and extended 4 existing handlers
- `tool_specs.py`: ToolSpec entries added for all new and extended tools

## [v1.10.3] — 2026-08-03

### Added
- Durable test runner (`run_unity_tests.py`): request_id/run_id/utf_guid correlation, manifest validation, reconciled terminal evidence
- Domain reload acceptance harness (`run_unity_domain_reload_acceptance.py`): automated multi-cycle reload stability verification
- Fault injection runner (`run_unity_fault_injection.py`): cleanup fault lanes for test isolation validation
- Unity test worker creator (`create_unity_test_worker.py`): disposable worker project provisioning
- `UnityMcpTestBase`: canonical C# test base with owned cleanup, scene isolation, domain reload guards
- `docs/testing-reliability.md`: comprehensive testing reliability guide
- Windows bootstrap installer (`install/bootstrap.ps1`)
- `unity-test-reviewer` agent for C# test quality audits

### Changed
- TestRunner: cleanup order enforced (owned objects → scene restore → Undo), prevents dirty scene dialog
- TestRunner: domain reload resilience — guards against stale DLL, UTF state corruption, run_id drift
- Live test infrastructure: verified worker gate, project path validation, lease-based ownership
- Pre-commit hook: extended with test source hygiene checks

### Fixed
- EditMode tests no longer trigger "unsaved scene" dialog on completion
- Active Editor test runs stabilized: no interference from concurrent domain reloads
- Test isolation hardened: cross-test state leaks eliminated via owned cleanup protocol
- Bridge port rediscovery resilience improved for reload scenarios

## [v1.10.0] — 2026-08-01

### Added
- `set_parent` now works in both Edit Mode and Play Mode (unified API)
- ValueParser: enum gap/flags support, case-insensitive name match
- InputNormalizer: underscore field name normalization with _lowerCamelCase fallback
- GetSerializedFieldType improvements for better type inference
- SetObjectReference null guard for safer object reference handling

### Changed
- `set_runtime_parent` Python tool removed (use `set_parent` in any mode)
- `set_runtime_property` Python tool removed (middleware auto-routes `set_property`)
- ScreenshotCapture.FindCamera uses ComponentSerializer.FindObject (bracket-path support)

### Fixed
- Domain reload: port file sync, bridge recovery, TCP storm prevention
- Domain reload: heartbeat deadlock, stale timer, WatchdogTick latch
- TestRunner: Play Mode guards prevent UTF crash on domain reload
- SceneHelper.OpenScene: Play Mode guard prevents crash
- Port file leak: test cleanup restores discovery files after PortFileManager tests
- CleanStalePeerPortFiles: handles all file types (*.port, *.reload-port, *.chat-port)
- MCPServer: bind failure always logged (no silent swallow during shutdown)
- Bridge: going_away detection during version check prevents dead-socket reconnections

## [v1.9.1] — 2026-07-31

**Documentation:**
- Added wiki redirect page (`docs/wiki.md`) with auto-redirect to GitHub Wiki
- Added redirect support to build hook (`redirect_to` front matter)
- Added "Wiki" tab in docs site navigation
- Set up GitHub Wiki with sidebar and footer linking to docs site

## [v1.9.0] — 2026-07-31

**Documentation:**
- Added MkDocs build-time hook (`docs/hooks/transform.py`) that auto-converts GFM to site format
- Hook adds `markdown` attributes to HTML blocks and fixes image paths for directory URLs
- Custom Pipedream-style CSS theme (`docs/stylesheets/extra.css`) with dark/light mode
- Redesigned docs landing page (`docs/index.md`) with hero section and feature cards
- Added 43 unit tests for the build hook (`tests/test_docs_transform.py`)
- Simplified doc-keeper skill: agents write plain GFM, hook handles all transforms
- Restructured MkDocs navigation from 9 to 7 tabs

## [v1.8.0] — 2026-07-31

**Documentation Infrastructure:**
- Migrated documentation site from Jekyll (just-the-docs) to MkDocs Material
- Added `mkdocs.yml` with Material theme, instant navigation, dark mode, search, code copy, and full navigation tree
- Added GitHub Actions CI workflow (`.github/workflows/docs.yml`) for automated build and deployment to GitHub Pages
- Removed Jekyll-specific front matter from all 47 documentation files
- Renamed section landing pages: `docs/chat/using-chat.md` → `docs/chat/index.md`, `docs/plugins/quickstart.md` → `docs/plugins/index.md`
- Updated internal documentation cross-references to MkDocs file naming conventions
- Updated `AI/architecture.md` plugin documentation reference path
- Updated `.claude/skills/documentation-maintenance/SKILL.md` with MkDocs conventions

## [v1.7.3] — 2026-07-31 — Intent LLM settings

**Added:**
- `ui_intent`, `vfx_intent`, `animator_intent` to LLM Sampling settings UI and config pipeline
- All three intent features now configurable via MCP Settings → LLM Sampling panel

## [v1.7.2] — 2026-07-30 — Remove tracked .claude files

**Fixed:**
- Removed `.claude/agents/` and `.claude/skills/` from git tracking (were committed before `.gitignore` rule)

## [v1.7.1] — 2026-07-30 — Python 3.9 compatibility hotfix

**Fixed:**
- `claude_to_codex.py` crash on Python 3.9 (`TypeAlias` import, `X | Y` union syntax)
- Added `from __future__ import annotations` and replaced `TypeAlias` with `Union`

## [v1.7.0] — 2026-07-30 — Dynamic resources, search context, test stability

**MCP Dynamic Resources:**
- `biome://` URI scheme for dynamic resource discovery (scene GameObjects, project assets)
- `search_context` TCP command — tab-separated scene GO + asset search with type/limit filtering
- `SearchContextPlugin` delegate bridge (Editor → Chat.CLI without assembly reference)
- `AssetMentionIndex` with lazy caching and version-tracked invalidation
- Resource refresh wired into server lifespan and reconnect lifecycle

**Test run stability:**
- `SceneDirtiedGuard` — reflection-based `ClearSceneDirtiness` utility prevents "Save Scene?" popup
- `TestRunner` pre-flight expanded: handles untitled scenes (`path==""`) alongside dirty scenes
- `DeleteTempScene` race condition fixed — no more `NewScene()` call that creates untitled scene mid-pipeline
- `TestAssemblySetup.GlobalTearDown` — `Undo.ClearAll()` + `ClearAllScenesDirty()` before scene restore

**Infrastructure:**
- `VersionTracker.BumpForTest()` test seam for cache invalidation
- 27 Python resource tests + 2 dynamic resource tests + 16 C# SearchContext NUnit tests

## [v1.6.0] — 2026-07-30 — Serializer improvements, reload fast-fail, test cleanup

**Serializer text improvements:**
- Animation curves use compact `time:value` format with property aliases (`$pos`, `$rot`, `$scale`)
- Timeline track binding uses arrow syntax (`→ /ObjectName`)
- Animator serializer includes transition conditions and exit time

**Bridge and connection:**
- `send()` fast-fails with `DomainReloadError` during active domain reload (no TCP hang)
- `RefManager.Invalidate` deferred from connection to first slow-path command
- `IsSlowPath` seam for fast-path classification (ping, get_version, status, get_enabled_tools)

**Test cleanup hardening:**
- 22 C# test classes fixed: `SceneTestBase` inheritance, `Undo.ClearAll()` after `NewScene`
- `SceneCleanTestBase` infrastructure: added `Undo.ClearAll()` to prevent dirty scene flag
- Python live test fixtures: property mutation revert for gridtest, orphan_guard scene save
- `PortFileManagerTests`: `ResetForTests()` + try/finally port restore

**Quality gates:**
- Code reviewer rules hardened: `SceneTestBase` mandatory (Critical), property mutation revert (Major)
- Test quality checklist updated with scene cleanup and property mutation patterns

## [v1.5.0] — 2026-07-30 — Client workflow guidance

**Client skills and agents:**
- Consolidated the shipped guidance from 23 overlapping files into 11
  goal-focused folder skills with reusable references
- Replaced the broad editor agent with focused scene, C#, playtest, and
  diagnostics agents
- Added batch-first workflows, saved PlayTest artifacts, macro reuse, live
  schema discovery, and evidence-based verification guidance

**Installation and Codex conversion:**
- Added ownership-aware, transactional installation with conflict preflight,
  rollback, nested skill resources, and supported-release migration
- Hardened Claude-to-Codex generation with managed-file manifests, safe prune,
  path and symlink checks, rollback, and Python 3.10-compatible validation
- Added direct clean-install and legacy-upgrade coverage for 11 skills and 4
  agents

**Documentation and presentation:**
- Expanded the animated README hero, architecture, comparison, and inventory
  visuals while preserving deterministic marker-only stat updates and
  reduced-motion fallbacks
- Added a user guide for installing and safely updating project-local AI
  skills and agents

## [v1.4.1] — 2026-07-29 — README visuals hotfix

**Fixed:**
- Restored readable README visuals: animated hero, architecture, comparison
  hero, and stats SVGs with marker-only rendering
- Upgraded `readme_render.py` to marker-based content injection with expanded
  test coverage
- Removed unused `divider-biome.svg`

## [v1.4.0] — 2026-07-29 — Path resolution for special characters

**Fixed:**
- Backslash-escaped `/` and `\` in GameObject names for correct hierarchy path
  round-trips (`\/` for literal slash, `\\` for literal backslash)
- Bracket-aware path splitting (`SplitPathSegments`) so `[Zone A/Zone B]/Child`
  no longer breaks on the inner `/`
- Multi-scene path cache invalidation on scene load/unload
- Brace-depth-aware `return;` → `return null;` rewriting in `execute_code`
- Broadened `using`-hoisting regex to catch `using static`, aliases, and global
  usings in `execute_code`
- PlaytestParser: bracket/quote-aware tokenizer (`SplitTokens`) and
  operator-scan parser (`ParseQOV`) replace positional token indexing

**Tests:**
- `ComponentSerializerSpecialCharTests` — 16 round-trip cases for escaping
- `ComponentSerializerBracketFinderTests` — bracket `/` in `SceneObjectFinder`
- `PlaytestParserEdgeCaseTests` — compound AND, bool shorthand, multi-word value
- `CodeExecutorTransformTests` — using-hoisting and return-rewriting edge cases
- `MultiSceneHierarchyTests` — path cache invalidation
- Python: path resolution middleware, multi-scene cache, path cache tests

## [v1.3.1] — 2026-07-29 — README presentation and release safety

**Documentation:**
- Restored a compact GitHub README rhythm with mobile-readable animated Biome
  visuals, reduced-motion fallbacks, and a dedicated ecosystem divider
- Reworked the source-backed product comparison for narrow layouts, including
  the official Unity MCP Server and concise strengths and constraints for each
  product
- Removed volatile implementation counts from maintained architecture guidance

**Release tooling:**
- Made `server/pyproject.toml` the version source of truth and added
  rollback-safe synchronization for every generated version copy
- Replaced the publishing shell helper with a non-publishing release preflight
  and added changelog/version/presentation contract tests
- Made the UPM changelog an exact generated mirror of this canonical changelog

## [v1.3.0] — 2026-07-29 — Documentation refresh

**Docs:**
- Complete README rewrite with auto-generated stats and comparison hero
- Rewrote install guides for 10 AI clients (Claude Code, Cursor, Windsurf, Codex, Gemini, Junie, Kimi, OpenCode, VS Code, Rider)
- Added: comparison page, settings reference, chat usage guide, install index
- Refreshed all AI/ knowledge files to match current codebase
- Streamlined CONTRIBUTING.md and docs/README.md

## [v1.2.0] — 2026-07-29 — UI polish, animations, BiomeUI utilities

**C# — New UI Components:**
- `BiomeParticleBurst.cs`: pooled 12-particle radial event burst plus pooled ambient fields with 8 UI-specific motion themes; ambient loops pause while detached
- `BiomeToggleGroup.cs`: radio-button-style toggle group with tri-state master, accordion behavior, filter API
- `BiomeUI.cs`: shared UI utility class — style loading, button factories (`PrimaryButton`, `SecondaryButton`, `QuietButton`), `Section()`, `StatusLabel()`, `SetExclusiveClass()`, `ShakeX()` animation
- `EcosystemHeaderAnim.cs`: Plugins node graph + Version Picker timeline scanner header animations
- `WizardAmbientAnim.cs`: `WizardJourneyAnim` (4-node step tracker) + `SkillsInstallAnim` (module-stream animation for InstallSkills screen)
- `WizardUI.cs`: DRY factory for wizard button variants and navigation layout

**C# — Enhanced Animations:**
- All `*HeaderAnim.cs` files: enhanced with `BiomeAmbientParticles`, GPU `UsageHints`, improved motion patterns
- `ArcadeAnim.cs`: new `MotionHandle` + `ControlledSmoothLoop` API for fine-grained animation control
- `LevelUpAnimator.cs`: idle signal, spark effects, `SimulateCompletion` test hook
- `StatusAmbientAnim.cs`: GPU hints, refined ambient animation
- `MCPChatWindow.FlowBar.cs`: particle-driven FlowBar redesign (replaces CSS sweep with `ArcadeAnim.ControlledSmoothLoop` + pooled particles)

**C# — UI Styling:**
- `MCPHub.uss`: major expansion (+504 lines) — biome visual language, card layouts, scroll wrapping
- `MCPSettings.uss`: new settings page styles (+103 lines)
- `SetupWizard.uss`: wizard visual overhaul (+374 lines) — ambient animations, step transitions
- `LevelUpAnim.uss`: enhanced level-up celebration styles
- `MCPStatus.uss`: refined status page styling

**C# — Settings & Wizard Refactoring:**
- `MCPSettingsCategoryGroup.cs`, `PermCategoryGroup.cs`: simplified using `BiomeToggleGroup`
- `SettingsPageFactory.cs`: biome-page class + inline plugin accordion
- `BackendSettingsForm.cs`: CSS class refactor, Codex timeout clamping
- `ChatSettingsSection.cs`: auth probe cleanup on detach, warning styling
- All wizard screens enhanced: `AiConfigScreen`, `ConfigureScreen`, `InstallSkillsScreen`, `PickBackendScreen`, `WelcomeScreen`
- `MCPStatusWindow.cs`: Kill MCP + Reimport collapsed into Maintenance foldout

**Python — Tests:**
- `test_editor_ui_styles.py`: UI style validation tests (72 lines)

**Docs:**
- `docs/plugins/ui-toolkit-best-practices.md`: UI Toolkit best practices guide
- AI knowledge files updated: architecture, animation, ui, chat-view, particles, structure


## [v1.1.0] — 2026-07-28 — Windows connection stability, UPM package page

**Python — Windows TCP stability:**
- `bridge.py`: `SHUT_WR` instead of `SHUT_RDWR` on Windows — avoids RST packet on graceful close, prevents connection reset errors on client side
- `bridge.py`: reset `_pinned_port` on `ConnectionRefusedError` — forces port rediscovery instead of retrying a dead port after Unity reload

**C# — Windows TIME_WAIT (accepted sockets):**
- `ClientConnectionHandler.cs`: `LingerOption(true, 0)` on accepted socket — forces RST on close, eliminates TIME_WAIT for incoming connections on Windows
- `ClientSlot.cs`: `LingerOption(true, 0)` in eviction path — consistent with accepted socket behaviour
- `MCPServer.cs`: capture `origPort` before `SaveRuntimePorts()` — log message now shows the correct pre-fallback port

**UPM Package Manager page:**
- `unity-plugin/package.json`: added `description`, `keywords`, `documentationUrl`, `changelogUrl`, `licensesUrl` — Unity Package Manager now shows metadata and links
- `unity-plugin/LICENSE.md`: copy of licence for UPM inline display

**Tests:**
- `test_bridge_edge_cases.py`: SHUT_WR vs SHUT_RDWR path coverage
- `test_bridge_port_rediscovery.py`: `_pinned_port` reset on `ConnectionRefusedError`, full rediscovery cycle
- `test_package_json.py`: 8 package.json contract tests (required fields, URL format, keyword presence)
- `PortFileManagerTests.cs`: `SaveRuntimePorts` contract tests

## [v1.0.2] — 2026-07-28 — Windows port/chat fixes

**C# — Port lifecycle (Windows TIME_WAIT):**
- `ClientSlot.cs`: `LingerOption(true, 0)` on all close paths (`DisconnectAll`, `KillPhantoms`, eviction) — forces RST instead of FIN, eliminates TIME_WAIT on Windows, port freed immediately after domain reload
- `PortFileManager.cs`: new `SaveRuntimePorts(port, chatPort)` — updates `MCP_Port.json` + `{pid}.port` but NOT `MCPSettings.json` (user intent), preventing cascade port drift on Windows reload (9514→9516→9518...)
- `MCPServer.cs`: fallback bind now calls `SaveRuntimePorts` instead of `SavePorts` — retries configured port on next reload, no drift
- `PortFileManager.cs`: new `CleanStalePeerPortFiles()` — removes `.port` files from dead PIDs at startup, prevents stale discovery entries accumulating after hard crashes (6 new NUnit tests in `PortFileManagerTests.cs`)
- `MCPServer.cs`: log message "in TIME_WAIT" → "unavailable (address in use)" (more accurate on Windows)

**C# — MCP Chat freeze (Windows):**
- `PreviewPathResolver.cs`: `HasIllegalChars()` guard before `Path.GetExtension()` — prevents `ArgumentException` on Windows paths containing `|` (component/field chips like `PlayerController|Health|value`), eliminating 45s chat freeze (11 new NUnit tests in `PreviewPathResolverWindowsGuardTests.cs`)

**C# + Python — Port baking removed from permanent configs:**
- `WizardConfigWriter.cs`: `Entry()` no longer emits `UNITY_MCP_PORT` env block — Python uses `~/.unity-biome-mcp/ports/{pid}.port` discovery (updated on every bind including fallbacks)
- `ConfigureScreen.cs`: Global/Project scope toggle removed (no port to scope), tests updated accordingly
- `mcp_config_writer.py`: `write_claude_config`, `write_kimi_mcp_config`, `write_agy_settings`, `write_opencode_config` — `UNITY_MCP_PORT` env block only written when `mcp_port != 0`, skipped for fallback-written configs; prevents connection failures after Windows port drift and multi-project desync

<!-- tests: 4749 unit + 284 live + 4 live_cli + C# (compilation pending Unity focus) + 36 reload = 11573+ -->

## [v1.0.0] — 2026-07-26 — Documentation audit, v1.0.0 release

**Docs — Full audit (38 files, 85+ issues fixed):**
- 4-cycle audit (analyze→fix→deep-audit→verify) with 36 agents against source code
- Removed phantom tools from all docs (fuzz_playtest, find_references, semantic_at, save/run_scenario)
- Fixed all parameter names/defaults against Python+C# source (SET 5-token syntax, WAIT_CAPTURED label+mode, ASSERT_CONSOLE_CLEAN IGNORE keyword, ~= operator removed)
- Actualized numeric counts across all layers (142 MCP tools, 148 ToolSpec, 4703/284/4/6537/36 tests)
- Eliminated DRY violations (recompile, material, screenshot, wire_event → single-source + cross-refs)
- Added documentation for ~20 previously undocumented tools (run_tests_wait, execute_code, etc.)
- Fixed DSL examples in playtest.md against PlaytestParser.cs (comparison operators, SIMULATE syntax, CAPTURE syntax)
- New install guide: docs/install/junie.md

**Docs — README:**
- Removed BETA labels from badge wall, added RELEASE badge
- Updated all test/tool counts to current values

<!-- tests: 4703 unit + 284 live + 4 live_cli + 6537 C# + 36 reload = 11564 -->

## [v0.96.1] — 2026-07-24 — Hatch wheel build fix, Windows relay, port migration fallback

**Python — Build:**
- `pyproject.toml`: added `[tool.hatch.build.targets.wheel] packages = ["src/unity_mcp"]` — fixes wheel build failure after rebrand (project name `unity-biome-mcp` ≠ package dir `unity_mcp`)

**Python — Windows:**
- `chat_relay.py`: wrapped `loop.add_signal_handler()` in `try/except NotImplementedError` — prevents crash on Windows ProactorEventLoop
- 2 new unit tests: signal registration (happy path) + `NotImplementedError` guard

**Python — Migration:**
- `paths.py`: `iter_port_files()` — discovers `.port` files from both `~/.unity-biome-mcp/ports/` and legacy `~/.unity-mcp/ports/`, deduplicates by filename (new dir wins)
- Updated 4 call sites: `server_filtering.py`, `lockfile.py` (×2), `config/resolver.py`
- 3 new unit tests: primary dir, legacy dir, deduplication

**Python — Config:**
- `resolver.py`: `find_python()` now venv-first (was uvx-first) — dev clones get local venv, not uvx
- `mcp_config_writer.py`, `resolver.py`: `uvx --quiet` flag — suppresses stderr noise in MCP hosts
- `README.md`: removed hardcoded `UNITY_MCP_PORT=9500` from MCP config example — auto-discovery handles port selection
- `install.py`: `setup` and `update` now auto-generate `.mcp.json` with venv-based server command

## [v0.96.0] — 2026-07-24 — Security levels redesign, relay spawner stability

**C# — Security Levels:**
- Renamed `SecurityLevel` enum: `Normal` → `Standard`, `Permissive` → `AllowAll`
- `AllowAll` skips all security scans — no pattern matching, no regex filtering
- Default changed to `AllowAll` for frictionless development
- Fixed `IsAllowedAssembly` for Unity 6 in-memory assemblies (7 test failures)
- Fixed `TempDirScope` macOS symlink canonicalization (1 test failure)
- Security tests: `SetUp`/`TearDown` isolation, 5 new `AllowAll` tests

**C# — Relay Spawner Stability:**
- Always capture stderr from relay process (was local-only — silent crashes)
- 3× retry with 2s backoff on transient spawn failures
- Zombie process cleanup: kill previous process before each retry attempt
- `LooksAlreadyRunning` uses PID check instead of stale 3s TCP cache
- Retry logic in `ExecuteSpawn` covers both sync and async spawn paths
- 5 new relay stability tests including zombie kill verification

## [v0.95.0] — 2026-07-24 — Rebrand unity-kiss-mcp → unity-biome-mcp

**Rebrand:**
- Server name `unity-mcp` → `unity-biome-mcp` (SERVER_NAME, UPM packages, data dir, docs, URLs)
- Data directory `~/.unity-mcp/` → `~/.unity-biome-mcp/` with auto-migration on first start
- UPM packages `com.unity-mcp.editor` → `com.unity-biome-mcp.editor`, `com.unity-mcp.reload` → `com.unity-biome-mcp.reload`
- GitHub repo `unity-kiss-mcp` → `unity-biome-mcp` (301 redirect preserved)
- Internal identifiers preserved: `unity_mcp` Python module, `UnityMCP` C# namespace, `UNITY_MCP_*` env vars
- Legacy migration: `_OLD_NAMES = ("unity-mcp",)` strips stale config keys on upgrade
- 682 files updated, zero regressions across 11764 tests

## [v0.94.0] — 2026-07-20 — Deprecated code removal, Client Skills migration, Install AI Skills wizard

**Python — Deprecated Removal:**
- Removed `get_perf` tool stub (use `get_frame_stats`)
- Removed `run_playtest_file` tool stub (use `run_playtest path=...`)
- Removed `_DEPRECATED_KEYS` backward-compat dict from gating (15 old→new category aliases)
- Tool count: 144 → 142 public tools

**C# — Deprecated Removal:**
- `PlaytestParser.cs`: removed ALIAS keyword support (Phase 1 collection + Phase 2 substitution)
- `PlaytestLinter.cs`: ALIAS detection changed from WARN → ERROR
- `SceneRefLinter.cs`: removed ALIAS from skip-line keywords
- `GdSnapshotSerializer.cs`: output format `ALIAS @label` → `VAL $label`

**C# — Install AI Skills Wizard:**
- `SkillsInstaller.cs`: discovers `ClientSkills/` in UPM package, copies skills/agents to `.claude/` dirs
- `InstallSkillsScreen.cs`: UIToolkit wizard screen with file list, overwrite toggle, Codex sync
- `SkillsInstallerTests.cs`: 14 NUnit tests for installer logic
- WizardScreenHost: 3 → 4 screens, added InstallSkillsScreen
- Menu: **MCP → Install AI Skills** opens installer directly

**Client Skills (unity-plugin/ClientSkills/):**
- 23 consumer skills for MCP tool usage (scene, animation, physics, VFX, playtest DSL, performance, etc.)
- 2 agents: `playmode-tester` (Play Mode testing), `unity-editor-developer` (scene building/debugging)
- 1 script: `claude_to_codex.py` (converts Claude format to Codex format, tomllib optional for Python < 3.11)

**Docs & Assets:**
- Tool count updated 144 → 142 across README, SVGs, badges, `_meta.json`, GitHub description
- README: added "AI Skills & Agents" section with Wizard installation guide


---

## [v0.93.1] — 2026-07-19 — Patch: SpatialHelper nearest-first sort

**C# — SpatialHelper:**
- `objects_in_radius`: now sorts all hits by distance ascending before truncating to `cap`. Previously returned arbitrary order from `FindObjectsByType(SortMode.None)`, causing non-deterministic results when cap was active.

---

## [v0.93.0] — 2026-07-19 — Battle recheck fixes: run_playtest predicate, animator aliases, spatial cap, 7 blocker fixes

**Python — run_playtest:**
- `IsPlaytestSuccess` predicate parses both `" OK"` and `"PLAYTEST: X/Y"` formats (fixes false-failure on suite runs).

**Python — console:**
- `console_mark` token parsing handles `"ts:label"` format.

**Python — screenshot:**
- `output_path` forwarded to all 3 camera branches (overview/single/default).

**C# — AnimatorControllerHelper:**
- `get_parameters` / `get_states` alias normalization — compact format (`params=Speed:float:0`).

**C# — SpatialHelper:**
- `spatial_query` cap enforcement: output `"N objects within Xm (showing Y)"` + `"...+N more"` truncation.

**C# — ObjectManager.Transfer:**
- `transfer_object` copy: sets active scene before `Instantiate` so clone lands in target scene.

**C# — MaterialHelper:**
- `target=instance` uses `sharedMaterials` clone instead of `renderer.material` (avoids edit-mode error).

**C# — PrefabHelper:**
- `child_path ?? path` fallback; `mode`/`scope`/`format` params accepted by validator.

**C# — ScriptableObjectHelper:**
- Multi-field `Set`: per-field echo `ok: field = old → new`.

**C# — UIHelper:**
- TMP fallback to legacy `Text` component on `TextMeshPro` creation failure.

**C# — CommandRouter:**
- Timeline `director_path ?? path` fallback for `create` action.

**Test counts:** Python unit 4735 | C# EditMode: pre-existing failures unchanged

---

## [v0.92.0] — 2026-07-19 — API pragmatic review: envelope hardening, discover_tools UX, tool hardening, serialized field rename audit

**Python — Result Envelopes:**
- `move_to` and `ask_user` gain `isSuccess` predicates (P0: were missing, treated as always-ok).
- `BatchHelper.HasErrors` (C#): inner `ok:false` items now promote the batch envelope to `ok:false` at C# send time.

**Python — Tool Discovery:**
- `discover_tools`: canonical categories listed first (SCENE/COMPONENTS/ASSETS/MEDIA/VERIFY/RUNTIME/TESTS/SYSTEM), legacy aliases excluded by default (`include_legacy=False`).
- `structured=True` mode: returns per-tool surface/mutability info instead of plain name list.
- `sync_unity` added to `_SCHEMA_KEEP_FULL_EXTRA` (full schema served).

**Python — screenshot:**
- `output_path` param added as alias for `path`; `output_path` wins when both are provided.

**C# — MaterialHelper:**
- `target=shared|instance|asset` param — controls which material is mutated; response enriched with old→new values.

**C# — ScriptableObjectHelper:**
- `Set` echoes old→new values in response; missing field lists allowed field names.
- `Get` accepts `fields=` filter.

**C# — PrefabHelper:**
- `Save` accepts `mode=new|overwrite`.
- `GetOverrides` accepts `format=structured` for machine-readable diff.
- `Revert` accepts `scope=children` to recurse to nested prefab instances.

**C# — AnimationHelper:**
- `CreateClip`: try/catch + `DeleteAsset` rollback on failure (atomic).

**C# — UnityPreflightHints (NEW):**
- `Roslyn/UnityPreflightHints.cs` — static analyzer: checks serialized `Dictionary<>` fields, non-serializable interface/abstract field types, renamed fields without `[FormerlySerializedAs]`.
- Integrated into `CompilePreflightCommand` for proactive hints in `compile_preflight` results.

**C# — SerializedFieldRenameAudit (NEW):**
- `SerializedFieldRenameAudit.cs` — scans prefabs, scenes, and ScriptableObjects via YAML for stale field data after a field rename without `[FormerlySerializedAs]`.
- Exposed as `serialized_field_rename_audit` MCP tool (VERIFY category, read-only).

**Test counts:** Python unit 4703 | C# EditMode: +NUnit assertions updated (1 pre-existing failure)

---

## [v0.91.0] — 2026-07-19 — MCP real-project audit fixes: P0 data-loss, result envelopes, mutation tracking, schema parity

**Python — Result Envelopes:**
- `run_playtest`, `wait_until`, `test_step`: correct `isSuccess` predicates (P0 fixes).
- `BatchHelper.HasErrors` promotes inner `ok:false` to outer envelope.

**Python — Schema Parity:**
- 10+ tools added to `_SCHEMA_KEEP_FULL_EXTRA` (full schemas served).
- `configure_objects` / `setup_objects` marked `direct_only=True`.

**Python — Mutation Tracking:**
- `batch` records `ChangeWatcher` mutations per mutating op.

**Python — Compile Workflow:**
- STALE-DOMAIN gate checks errors before escalating; MANUAL-REQUIRED syncs state.
- `compile_preflight` validates empty param (Python + C#).

**Python — Deprecated stubs:**
- `get_perf` → `get_frame_stats`, `run_playtest_file` → `run_playtest(path=)`. Both raise `ToolError` with migration hint.

**C# — PrefabHelper:** `Edit` child_path TrimStart; `Revert` via `GetNearestPrefabInstanceRoot`; `Unpack` calls `SetDirty`.
**C# — UIHelper:** Atomic create with rollback; `Undo` after success only.
**C# — TransferObject:** `Instantiate→MoveToScene` before parent assignment.
**C# — SceneRefResolver:** Per-token try/catch (no abort-on-first-error).
**C# — ErrorClassifier:** `IOException` → `INTERNAL` category.
**C# — FileOutputHelper:** Reliable project root detection.
**C# — Particle create:** Name from path tail; single-segment guard.
**C# — Timeline create:** Auto-creates Director GO.
**C# — RenderAnalyzer:** Throws on invalid `action`.

**Test counts:** Python unit 4703 | C# EditMode 6537+ (1 pre-existing failure)

---

## [v0.90.0] — 2026-07-18 — Playtest DSL Sprint P0-P3: FOR loops, PATH_PREFIX, CAPTURE_FRAMES, ASSERT_CHANGED; reload stability hardening

**C# — PlaytestParser (DSL Sprint P0-P3):**
- `PATH_PREFIX /path` directive — applies path prefix to all `VAL` path aliases in the script; first occurrence wins, applied after `INCLUDE` expansion.
- `FOR $var IN start..end` / `END_FOR` — integer loop unrolling at parse time; max 10000 iterations; nested `FOR` supported.
- `CAPTURE_FRAMES n INTERVAL s [CAMERA name] [MODE strip|list] [LABEL name]` — captures N screenshots at fixed intervals (n≥2); grouped under `LABEL` for subsequent frame assertions.
- `ASSERT_FRAMES_DIFFER label` — asserts consecutive captured frames differ (motion/animation check).
- `ASSERT_FRAMES_STATIC label` — asserts all captured frames are identical (stability check).
- `ASSERT_CHANGED $name` — asserts value captured by `CAPTURE $name /path|Comp|field` has changed since capture.

**C# — New files:**
- `PlaytestRunner.FrameCapture.cs` — partial class: `CAPTURE_FRAMES` + frame assertion step execution, screenshot sequence + pixel-hash comparison.
- `PlaytestLaunchWindow.cs` — `MCP / Playtest Launcher` EditorWindow: run `.playtest` files without the Composer; file picker + output log.

**C# — Reload stability:**
- `SyncHelper.cs`: `_pumpActive` singleton guard prevents multiple concurrent `StartTickPump` coroutines (was N×300 pump accumulation on rapid reconnects). `isCompiling` early-exit in pump. `RequestScriptReload()` gated on `!isCompiling`. `Refresh()` called after `AllowAutoRefresh`.
- `TestRunner.cs`: dirty temp scene saved silently before `NewScene` in `RunFinished` — suppresses "Save modified scenes?" dialog during suite teardown.
- `SceneCleanTestBase.cs`: leaked object error message now includes object names.

**Python — Reload stability:**
- `bridge.py`: `DomainReloadError` in `send()` calls `self._reload.mark()` — tracks reload window from the connect path, not only from heartbeat.
- `bridge_heartbeat.py`: heartbeat skips reconnect when `_reload.is_active()` — prevents reconnect storm during domain reload window.

**New test files:**
- `PlaytestForLoopTests.cs` — FOR loop DSL: range expansion, nesting, max-iterations guard
- `PlaytestFrameCaptureTests.cs` — CAPTURE_FRAMES parser + ASSERT_FRAMES_DIFFER/STATIC
- `PlaytestPathPrefixTests.cs` — PATH_PREFIX applied to VAL path values
- `PlaytestCaptureStringTests.cs` — CAPTURE / ASSERT_CHANGED step types
- `Sprint3FrictionTests.cs` — integration tests for friction sprint features
- `RuntimeHelperInvokeTests.cs` — RuntimeHelper reflection invoke coverage
- `server/tests/test_bridge_retry.py` — RetryPolicy unit tests
- `server/tests/test_console.py` — console watermark + get_console_since tests
- `server/tests/test_objects.py` — objects tool (find_type, IsNullOrEmpty guard) tests

**Test counts:** Python unit 4681 | C# EditMode 6687 (1 pre-existing failure)

## [v0.89.0] — 2026-07-17 — Gamedev friction sprint: security levels, execute_code improvements, DSL extensions

**C# — CodeExecutor:**
- `SecurityLevel` enum (`Standard` / `AllowAll` / `Strict`) — dropdown in MCPSettings + MCPHubUI; three-tier blocked-pattern sets computed at class init; `AllowAll` is the default.
- `using Object = UnityEngine.Object;` added to auto-injected usings — no more ambiguity errors.
- `GetFields(` / `GetProperties(` unblocked in Standard mode (moved to Strict-only tier).
- `.GetValue(` / `.SetValue(` / `.Invoke(` moved to Standard+Strict tier (allowed in AllowAll) — `TryGetValue` was never blocked (dot-prefix requirement added).
- Security error messages include actionable `Suggestion:` hints for common blocked patterns.
- `return;` (bare void) auto-replaced with `return null;` in `WrapIfBareCode` — no more CS0161 for void-style snippets.
- User-written namespace `using` directives (e.g. `using System.Text;`) are hoisted above the generated class wrapper automatically.
- Error message on missing `Run()` method improved with explicit fix hint.

**C# — PlaytestRunner:**
- DSL `ASSERT` supports `activeSelf`, `activeInHierarchy`, `tag`, `layer`, `name` directly on the path (no component lookup required).
- `ResolveVirtualField`: `Animator.currentState` (returns active clip name), `Rigidbody.speed`, `Rigidbody2D.speed` (velocity magnitude) — no C# property exists for these, now synthetic.

**C# — ScriptableObjectHelper:**
- `create_scriptable_object` accepts `fields=` param — sets multiple fields in one call with orphan rollback on failure.

**C# — CommandRouter:**
- `inspect` accepts `type=` as alias for `components=` param.

**Python — Distiller:**
- `execute_code` added to `_SKIP_CMDS` — results never distilled (code output is always complete).

**Python — Server:**
- Stale port cleanup on startup now uses `tcp_probe=True` — actively probes ports before removing `.port` files.

**Test counts:** Python unit 4630 (unchanged) | C# EditMode: +217 security tests + 68 router tests + 127 playtest tests + 34 SO tests (pre-existing 1 failure)

## [v0.88.0] — 2026-07-13 — C# settings panel sync with ToolSpec v2 8-category taxonomy

**C# — Settings Panel:**
- `MCPSettings.cs`: default `_defaultCatalog` rewritten from 13 legacy categories to 8 canonical categories (CORE, SCENE, COMPONENTS, ASSETS, MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM) matching `get_catalog()` output from `tool_specs._SPECS`.
- `MCPSettingsUI.cs`: `_noVisualsOff` set simplified from 4 entries (`SCREENSHOTS`, `ANIMATION`, `SHADERS_MATERIAL`, `VFX`) to 1 (`MEDIA`).
- `CatalogParser.cs`: doc comments updated to reflect new category names (SCENE, SYSTEM).

**Python — Tests:**
- `test_catalog.py`: 3 new _SPECS↔catalog consistency tests: `test_all_spec_tools_in_catalog`, `test_spec_tools_in_exactly_one_category`, `test_no_phantom_tools_in_catalog`.

## [v0.87.0] — 2026-07-12 — MCP release stabilization: 5 P0 + 12 P1 fixes, direct_only gating, batch UX, media hardening

**Python — Gating:**
- `tool_specs.py`: new `direct_only: bool = False` field on ToolSpec; 21 tools marked `direct_only=True` (cannot be used inside `batch`): `do`, `ask`, `doctor`, `debug`, `snapshot`, `watch`, `get_metrics`, `ui_intent`, `vfx_intent`, `animator_intent`, `set_properties`, `budget_status`, `list_connections`, `list_skills`, `list_templates`, `navmesh_query`, `lint_playtest_suite`, `run_playtest_suite`, `screenshot_baseline`, `screenshot_compare`, `validate_playtest_aliases`.
- `do` demoted from CORE to SYSTEM (CORE 11→10, TIER1 46→45).

**Python — Batch:**
- `batch.py`: `on_error=continue` now filters direct_only lines before TCP dispatch; original line numbers remapped in result; `on_error=stop` raises ToolError immediately; imports `_SPECS` for direct_only lookup.

**Python — Compile / Diagnose:**
- `console.py`: `get_compile_errors` fetches `compile_status` and forwards `compile_status=state` to `editor_log.corroborate()` — better stale-DLL corroboration.
- `diagnose.py`: stale-dll verdict (`FAIL:stale-dll`) now gated on `compile != "idle"` to avoid false positives after a clean domain reload.

**C# — P0 fixes:**
- `UIHelper.cs`: `SetTMPText` wrapped in `try/catch TargetInvocationException` — TMP styling is cosmetic, swallowed silently.
- `TimelineHelper.cs`: `Undo.RecordObject` + `EditorUtility.SetDirty` on `PlayableDirector` binding — change is now undoable and saved.
- `ObjectManager.Events.cs`: `m_` + PascalCase normalization for `wire_event` AND `unwire_event` — both now fallback to `m_OnClick` style when bare field name not found.

**C# — P1 fixes:**
- `AnimationHelper.cs`: null guard on `clip_name` for animation create.
- `AnimatorControllerHelper.cs`: null/empty guard on `states` param for `add_state`.
- `MaterialHelper.cs`: all property set paths return `"ok: prop=value"` instead of bare `"ok"`.
- `SceneHealthAnalyzer.cs`: `ItemCap=20` on 3 finding lists — truncates with `"... and N more"` to prevent runaway output.
- `GameStateHelper.cs`: error message truncated to 200 chars.
- `CommandRouter.ToolsCache.cs`: `_hiddenFromCatalog` set filters 13 internal commands from the tool catalog (ping, get_disabled_tools, set_tool_catalog, force_play_stop, watch_add/remove/clear/reset, get_version, get_capabilities, set_client_label, get_aliases, list_playtest_files).
- `CommandRouter.ScreenshotHandlers.cs` + `ScreenshotCapture.cs` + `FileOutputHelper.cs`: `screenshot path=` param honored — writes PNG to the requested path; path traversal validation (must be within project root).
- `BatchHelper.cs`: async-only error message improved (clearer wording).

**Test counts:** Python unit 4630 (unchanged) | C# EditMode 6537 (1 pre-existing failure)

## [v0.86.0] — 2026-07-12 — Test quality review: 83 Python + 25 C# tests deleted, assertions hardened, RenderAnalyzer crash fix

**Deleted (Python — vacuous/self-testing/duplicate):**
- 83 tests removed across 59 test files; `test_schema_cache.py` deleted entirely (17 tests that only verified `dict` type and `is not None`).
- Vacuous patterns removed: `assert result is not None`, `assert isinstance(result, dict)`, mocks asserting their own return values, tests with no assertions.

**Deleted (C# NUnit — self-testing/duplicate):**
- ~25 tests removed: self-testing stdlib behaviour (e.g. `List.Contains`, `string.StartsWith`), duplicate coverage, tests asserting mock setup rather than production logic.
- 8 test method renames: `snake_case` → `PascalCase` to match NUnit convention.

**Strengthened:**
- ~45 Python assertions replaced: `is not None` / truthy-only → exact value checks (`== "expected"`, `== 42`, `== []`).
- ~10 C# assertions strengthened to exact expected values.

**Test Infrastructure:**
- TearDown/SetUp added to 5 C# test classes: `SetParentTests`, `UndoGroupHelperTests`, `EnabledToolsCacheTests`, `GetAliasesTypedTests`, `ColliderFitHelperTests` — prevents state leak between tests.
- `conftest.py` (live): `_cleanup_orphans` now retries with logging on failure.
- `test_multiscene_stress_live.py`: `_make_scenes` retries with logging on failure.

**Fixed (production):**
- `RenderAnalyzer.cs`: `MissingComponentException` crash — `try/catch` moved to cover the `GetComponent<MeshFilter>()` call, not just `sharedMesh` access.

**Test counts:** Python unit 4630 (was ~4710 before deletions) | C# EditMode 6537 (1 pre-existing failure) | Python live 254

## [v0.85.1] — 2026-07-12 — Audit fixes: deprecated tools removed, middleware hardened, scene TearDown cleanup

**Removed:**
- `get_perf` — fully removed from Python (`diagnostics.py`) and C# (`CommandRouter.Registration.cs`); use `get_frame_stats` instead.
- `run_playtest_file` — removed from Python (`runtime.py`) and `test_run_playtest_file.py`; use `run_playtest(path=...)` instead.

**Fixed:**
- `middleware_guards.py`: batch `blast_radius` now dynamic — scans inner commands instead of using a fixed value.
- `middleware_async.py`: batch read-only guard added in `maybe_inject_state`.
- `tool_specs.py`: `resolve_scene_refs` mutability corrected to `'read'` (was `'write'`).
- `RenderAnalyzer.cs`: try/catch `InvalidOperationException` on non-readable meshes (2 sites).
- `PlaytestLinter.cs`: no-evidence commands now `ERROR` (was `WARN`).
- `compressor.py`: `project_fields()` returns helpful message when 0 fields matched.

**Test Infrastructure:**
- `SceneTestBase.cs`: added `Undo.ClearAll()` after `NewScene` to fix dirty scene state.
- `PlaytestRunnerTests.cs`: `DefaultGameObjects` replaced with `EmptyScene, Single` fixture.
- `HelperTests.cs`: `SpatialHelperEdgeCaseTests` now inherits `SceneTestBase`.
- `RenderAnalyzerTests.cs`: 3 new NUnit tests for non-readable mesh exception handling.
- New `test_registration_parity.py`: zero-drift guard between `tool_specs` and registered MCP tools (3 tests).
- `test_schema_drift.py`: 2 new tests for tier1 description quality and no implementation leakage.

## [v0.84.0] — 2026-07-12 — Release stabilization: CORE 15→11, tier restructure, P0 fixes, token economy

**Phase 1 — CORE/TIER Restructuring:**
- CORE shrunk from 15 → 11: `delete_object`, `set_parent`, `scene`, `search_scene` demoted to TIER1 SCENE.
- TIER1 total: 46 always-visible tools (previously 59).
- Promoted to TIER1: `set_active`, `validate_references`, `execute_code`, `undo_last`.
- `run_playtest_file` deprecated (still functional; description says DEPRECATED — use `run_playtest path=`).
- 8 categories preserved: SCENE, COMPONENTS, ASSETS, MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM.

**Phase 2 — Description Standardization:**
- 7 CORE tool descriptions rewritten with anti-hallucination cross-references.
- 15 old uppercase alias keys (`SCENE_EDIT`, `ANIMATION`, `SHADERS_MATERIAL`, etc.) → `DeprecationWarning` on use.
- Description template: `[Imperative verb]. [NOT for X — use Y]. [enum]: a|b|c. [non-obvious param].`

**Phase 3 — P0 Fixes:**
- `await_compile`: stale false-positive fixed — gated on `compile_status` before polling.
- `mcp_status` alias count: now uses `CountConfigAliases()` (cached, O(1) on hot path).
- `run_playtest_suite`: gained `auto_play=False` param — does not enter Play Mode by default.
- Lint no-evidence severity: `WARN` → `ERROR`.
- Schema drift guard test added (`test_schema_drift.py`).

**Phase 4 — Parameter Consistency (BREAKING):**
- `create_ui` / `set_rect`: `fontSize`→`font_size`, `offsetMin`→`offset_min`, `offsetMax`→`offset_max`.
- `object_diff`: `pathA`/`pathB` → `path_a`/`path_b`.
- Both Python tool signatures and C# command parsers updated.

**Phase 5 — Middleware:**
- `check_verification_needed` threshold: 5 → 10 (reduces spurious verification prompts).
- `delete_object` blast_radius: 3 → 4 (wider safety net).
- REFLECT lines excluded from distillation (bug fix — was polluting distilled output).

**Phase 6 — Token Economy:**
- `get_component` / `inspect`: `_no_distill=True` automatically set when `fields=` provided.
- `get_perf` deprecated → redirects to `get_frame_stats` with a deprecation notice.
- `get_frame_stats`: gained `include=` param for filtering returned fields.

**Phase 7 — P1 Additions:**
- New tool: `release_smoke` (TIER1, SYSTEM) — runs status + aliases + compile gates in one call.
- `run_playtest_suite`: failure-only verbose report — matrix row for PASS, full block for FAIL.

## [v0.83.0] — 2026-07-11 — MCP tool restructuring: 8-category taxonomy + write classification

**Added:**
- `ToolSpec.mutability: Literal['read','write']` — single-source classification for all tools.
- `ToolSpec.runtime_only: bool` — declares Play Mode requirement in metadata.
- `ACTION_READS` dict — maps 12 action-based tools to their read-action sets.
- `is_write(cmd, args)` function — per-call read/write classification (action-aware).
- 8-category taxonomy: `SCENE`, `COMPONENTS`, `ASSETS`, `MEDIA`, `VERIFY`, `RUNTIME`, `TESTS`, `SYSTEM`.
- Full backward-compat alias layer for old 18 category names; `register_tools()` resolves old plugin keys.
- Bounding test for `WRITE_CMDS` size — prevents silent growth.
- 97+ new unit tests across 3 new test files.

**Changed:**
- Core tools shrunk from 24 → 15; 9 infrastructure tools demoted to `SYSTEM` tier1 (always-visible).
- `WRITE_CMDS` / `READ_CMDS` / `_RUNTIME_ONLY_CMDS` now derived from `_SPECS` (−67 LOC hardcoded sets).
- AUTO STATE injection gated on writes only (~4000 tokens/session saved).
- `_compile_clean()` now recognizes C# sentinel `"no compilation errors"`.
- Response prefix unified: `FAILED:` → `FAIL:` in diagnose output.
- Themed categories consolidated: 18 → 8 task-oriented groups.

**Fixed:**
- AUTO STATE hierarchy injected after read-only commands (wasted ~4000 tokens/session).
- `_compile_clean()` missed C# `"no compilation errors"` sentinel.
- `run_tests` / `run_playtest*` incorrectly classified as writes (caused false "consecutive writes" warnings).

## [v0.82.0] — 2026-07-11 — MCP Playtests ROI + Gameplay Workflow sprint

**Phase 1 — MCP Playtests ROI (TZ #31):**

- `run_playtest_file` + `run_playtest_suite` — file-based and suite-level playtest runners.
- `lint_playtest` + `lint_playtest_suite` — static analysis for DSL scripts and suites.
- `WAIT_CAPTURED` DSL keyword — delta-capture polling; waits until a field value changes.
- `SWEEP_PATH` / `MOVE_PATH DWELL` — smooth path movement with configurable dwell time.
- Bool ASSERT sugar — `ASSERT /path|Comp|boolField` without `== true`; bare field = truthy check.
- `COMPLETE_PURCHASE` / `INVOKE_REPEAT` — action helper macros for common gameplay sequences.
- `validate_aliases` / `sync_aliases` / `export_aliases` — bidirectional `.asset` ↔ `.defs` sync.
- Macro stack provenance — `source:`, `macro:`, `section:` fields in assertion failure reports.
- `snapshot_on_failure` — captures full data snapshot on assertion or timeout failure.
- `PlaytestLinter.cs`, `PlaytestAliasHelpers.cs`, `PlaytestRunner.Snapshot.cs` — new C# modules.
- `CommandRouter.AliasHandlers.cs` — alias commands extracted to dedicated partial class.

**Phase 2 — MCP Gameplay Workflow (TZ #37):**

- `run_tests_wait` — synchronous polling wrapper over `run_tests` fire-and-forget; polls `get_test_results` until done.
- `console_mark` + `get_console_since` — timestamp watermarks for isolating log output per operation.
- `verify_after_change` — 5-gate verification pipeline: compile → refs → console → playtest → screenshot.
- `resolve_scene_refs` — resolves `$alias`, `/path`, `t:Type` references to canonical scene paths.
- `lint_scene_refs` — 3-pass reference linter (existence, type, nullability); `SceneRefLinter.cs`.
- `mcp_status` — returns connection/compile/alias cache state in one call; `McpStatusCommandTests.cs`.
- `scene_change_plan` + `apply_scene_change` — transaction-style mutations with preview + atomic apply; `transaction.py`.
- Schema/catalog parity gate — 5 regression tests in `test_schema_parity.py`.
- `SceneRefResolver.cs`, `SceneRefLinter.cs` — new C# modules.
- `verify.py`, `transaction.py` — new Python tool modules.

**Fixed:**

- `RelayBackend` test race condition — `string.Join` moved inside lock to prevent concurrent-access flicker.
- `CommandRegistryCompletenessTests` updated for all new commands.

**Stats:** 59 files changed, +4216/−72 LOC. 4462 pytest + 6550 NUnit green (1 pre-existing failure).

## [v0.81.0] — 2026-07-11 — NUnit fixes, isCompiling latch recovery, SceneCleanTestBase, orphan guard

**Fixed:**
- NUnit: `RenameObject_Undo` — added `LogAssert.Expect` for expected undo log message.
- NUnit: `MultiClientPingLiveness` — timeout tuning to reduce flakiness.
- NUnit: `IsAllowedAssembly` (CodeExecutor) — added `internal` testability overload.
- `ReloadGuard` static ctor: `delayCall` initialization order fix.
- `SyncHelper.StartTickPump`: removed inverted-exit logic bug.

**Added:**
- `force_play_stop` command (`CommandRouter.Registration.cs`) — server-side play+stop via `delayCall`; `allowedDuringCompile=true`; safe to call in compile-latch state; returns immediately. Used by T5 reload ladder.
- `SceneCleanTestBase.cs` — abstract NUnit base class; snapshots root `GameObject` instance IDs in `[SetUp]`, fails + auto-destroys leaked objects in `[TearDown]`.
- `DiagnoseCommand`: new `isReallyCompiling` field — `MCPServer.IsReallyCompiling` event-driven flag; no stale post-reload latch.
- `diagnose.py` `_DiagnoseFields.is_really_compiling` — parses `isReallyCompiling=` from C# diagnose output.
- WEDGE-ENGINE detection enhanced: also fires when `iscompiling=true` + `is_really_compiling=false` + `stamp_frozen` — catches stale latch even when `CompileNotifier.IsCompiling=true`.
- `_orphan_guard` autouse fixture (`server/tests/live/conftest.py`) — snapshots + diffs root scene objects per test; auto-destroys leaked objects and fails the test.

**Changed:**
- `force_refresh` enhanced: conditionally unlocks `ReloadGuard` (clears `MCP_ReloadGuardLocked`, calls `UnlockReloadAssemblies` + `AllowAutoRefresh`), adds `RequestScriptReload()` + `RepaintAllViews()` — resolves pending-reload stuck state.
- `reload_ladder` T5: uses `force_play_stop` command instead of two separate `editor play` + `editor stop` calls + `asyncio.sleep`.

**Stats:** 17 files changed, +183/−37 LOC. 4388+ Python unit + 280 live + 6455+ NUnit green.

## [v0.80.0] — 2026-07-11 — values-driven refactoring sprint (SOLID/DRY/KISS/OCP/SRP)

**Added:**
- `middleware_hooks.py` — `POST_HOOKS` registry + `@register_post` decorator for OCP-compliant post-call hooks (C13). Alias extraction now via hooks instead of inline blocks.
- `editor_log_freshness.py` (82L) + `editor_log_wedge.py` (155L) — SRP split from `editor_log.py` (391→148L core) (C9).
- `bridge_socket.py` — `frame_write()`, `frame_read()`, `frame_read_with_timeout()` TCP framing helpers, used across 7 callsites in 5 files (M67).
- `CommandRouter.AliasHandlers.cs`, `CommandRouter.ScreenshotHandlers.cs`, `CommandRouter.ToolsCache.cs` — SRP partial class split (C8). ObjectHandlers 454→274L.
- `FileHandler` delegate on `CommandEntry` for OCP screenshot dispatch (C7).
- `GetCapabilities` emits `mutating_cmds` + `runtime_cmds` sets; Python `_warm_cmd_flags()` syncs at connect/reconnect (C1/C2).
- `BackendConfigStore.WithModel()` immutable clone pattern; `ApplySelectedModel` collapsed 110→1L (C10/C11).
- `PlaytestStep` 10 semantic alias properties (ObjectPath, ComponentType, FieldName, etc.) (C5).
- `VisualStep` composition: wraps `PlaytestStep` via `_step` backing field, 14 delegating properties (C6-A).
- `parse_pipe_fields()` utility in `utils.py` — DRY for pipe-separated field parsing (M13).
- `doctor.py` `_tcp_connect` async context manager — 2 TCP probes collapsed (M64).
- `JsonHelper.ScanBalanced` private method — 4 JSON methods collapsed, −60L (M3).
- `MCPServer` chat listener `SO_REUSEPORT` fix for macOS/Linux (M4).
- `PrefKeys.DisableSceneNameNorm` constant (M20).

**Changed:**
- Chat is now always-on: removed `UNITY_MCP_CHAT` compile define and guards from 38+ files. No more `#if UNITY_MCP_CHAT`, no `ChatSettingsHook.IsChatEnabled()`, no env var toggle.
- `_RUNTIME_ONLY_CMDS` changed from `frozenset` to `set` for contract sync.
- `ChatTranscript.AppendUserBubble` single→list forwarding, −53L (M19).
- `_is_pid_alive` deduplicated: `server_filtering.py` imports from `lockfile.py` (M9).
- 4 asmdef files: `defineConstraints` cleared of `UNITY_MCP_CHAT` (test asmdefs keep `UNITY_INCLUDE_TESTS`).

**Removed:**
- `ChatSettingsHookTests.cs` — tested deleted production code.
- `CloneWithModel` from `MCPChatWindow` — replaced by `BackendConfigStore.WithModel`.
- `Enable Agent Chat` toggle from `MCPHubUI` — chat is unconditional.
- 27 audit findings rejected as false positives after architect verification.

**Stats:** 125 files changed, −669 LOC net. 4388 Python unit + 280 live + 6455 NUnit green, 0 new failures.

## [v0.79.1] — 2026-07-11 — run_playtest path= parameter, scenarios/fuzzer removal, scene_session merge

**Added:**
- `run_playtest(path="Playtests/smoke.playtest")` — C# reads file server-side; ~15 tokens vs 300-800 inline. `path` and `script` are mutually exclusive. `defs` param works with both modes. `_explicit_path=True` bypasses middleware length check for file paths. Path traversal guard in C# (`GetFullPath` + `StartsWith` check).
- `test_playtest_path.py` (Python) + `PlaytestPathTests.cs` (C#) — new tests for file-based playtest execution.

**Removed:**
- `scenarios.py` — `run_scenario`, `save_scenario`, `load_scenario`, `list_scenarios` deleted. `run_playtest(path=...)` covers the use case with fewer tokens.
- `fuzzer.py` + `fuzz_playtest` tool — experimental property-based fuzzer removed.
- `scan_scene` `bands` param — dead param C# never registered.

**Fixed:**
- `check_colliders` path registration — C# moved from required to optional (matching Python signature).
- `use_skill` param split — naive `split(",")` replaced with regex handling parenthesized values like `pos=(0,5,0)`.
- `SamplingService` singleton — `runtime.py` now uses module-level singleton instead of fresh instance per call.

**Refactored:**
- Timeout constants — `_TCP_POLL_BUFFER = 5.0`, `_TCP_STEP_BUFFER = 10.0`, `_TCP_PLAYTEST_BUFFER = 20.0` replace magic numbers in `runtime.py`.
- `scene_session.py` merged into `scene.py` — 5 session functions (save_session, load_session, screenshot_baseline, screenshot_compare + helper) inlined; `scene_session.py` deleted.
- `_normalize_defs` — 4-line if/elif → 1-line expression; added `None` guard for comment-only defs in script mode.

**Tests:** 4375 Python passed (5 skipped) | 6452 C# NUnit passed (1 pre-existing failure, 8 skipped)

## [v0.79.0] — 2026-07-10 — TCP session persistence, alias pipe resolution, READ_CMDS audit

**Fixed — TCP churn (4 root causes):**
- `_ensure_heartbeat()` called on every send → duplicate tasks leaked → eliminated; tasks created only in `_reconnect()`, destroyed only in `close()`
- `connected` property returned false-negative after socket established → churn loop
- Fixed sleep during domain reload replaced by `asyncio.Event` reload gate (wakes immediately on reconnect)
- Premature close on ping stall — threshold raised to 6 windows (~6 min); prevents disconnect during App Nap / heavy compile

**Fixed — Heartbeat lifecycle:**
- Self-cancel guard added; tasks no longer leak on concurrent reconnect

**Added — Client identification:**
- `UNITY_MCP_CLIENT` env var injected into ping messages
- MCP `InitializedNotification` hook calls `set_client_label` automatically
- `set_client_label` command (`alwaysAllowed`, `allowedDuringCompile`)
- `RoleToLabel` expanded: codex, cursor, windsurf, claude-desktop
- `MaxClients` increased 4 → 8

**Fixed — AliasExpander pipe truncation (CRITICAL):**
- `AliasExpander.GetTable()` used `a.path` only → query aliases lost `|component|field`
- New `BuildPipePath(a)` helper preserves full pipe path for ValPath aliases
- `query_state queries=$alias` now resolves to `path|component|field` correctly
- `run_playtest` can use `PlaytestConfig` aliases without an explicit
  project-specific `INCLUDE`

**Fixed — Readonly batch blast radius false positive:**
- `_is_batch_readonly()` in `middleware_guards.py` checks all batch commands against READ_CMDS
- `editor` dual-use: `action=state` classified as read, other actions as write
- `_EDITOR_READ_ACTIONS = frozenset({"state", "project_path"})` for precise classification

**Fixed — READ_CMDS/WRITE_CMDS audit:**
- READ_CMDS expanded 15→43 entries (alias_status, diagnose, editor, get_*, query_*, etc.)
- WRITE_CMDS +`rename_object` +`set_sibling_index`, −`compress_hierarchy` (dead)
- All command classifications validated against C# `_RO`/`_RW` markers

**Added — Connection status:**
- `UnityBridge.status` semantic property: `connected`/`reconnecting`/`domain-reloading`/`disconnected`
- `list_connections` shows semantic status instead of binary connected/disconnected
- `alias_status` promoted to tier1 (visible in tool listing without category gating)

**Fixed — NUnit test failures (15 total, Unity 6 compat):**
- `BoxCollider` → `TestComp` in bridge tests
- `RenameObject_Undo` LogAssert fix
- `ParseAsync` Model guard
- `ToolVerbMap` / `ConfigWriter` prefix alignment
- `BuildAliasSection` iteration guard
- `TestRunner.DeleteTempScene()` cleanup via `delayCall` in `RunFinished()`
- `SceneTestBase` for test isolation (32 test classes)

**Tests (new):**
- `test_bridge_reload_gate.py` (5 pytest) — reload gate (asyncio.Event) behavior
- `test_bridge_role.py` (3 pytest) — client role / identification
- `test_connection_status.py` (12 pytest) — semantic status property
- `test_middleware_read_cmds.py` (59 pytest) — READ_CMDS/WRITE_CMDS classification
- `test_tool_schema_coverage.py` (11 pytest) — tool schemas + FastMCP contract tests
- `AliasExpanderTests.cs` (7+ NUnit) — pipe expansion, comma-separated, parentheses
- `PlaytestGlobalAliasTests.cs` (6+ NUnit) — FormatVALBlock roundtrip, GetTable consistency
- `AliasStatusTests.cs` (NUnit) — alias_status command wiring
- `BatchHelperParserTests.cs` (NUnit) — batch parser edge cases

## [v0.78.0] — 2026-07-09 — Typed alias cards, alias system (VAL/VAR/sigil/INCLUDE), batch DSL fields/compress

**Added — Typed alias cards (Alias Composer UI):**
- **`AliasType` enum** (`PlaytestConfig.cs`) — `ValPath` / `ValConst` / `VarRuntime`; drives per-card layout in the Alias Composer window
- **`PlaytestAliasCardBuilder.cs`** (208 LOC) — extracted card rendering from `PlaytestAliasWindow`; per-type layouts: `ValPath` → path field + cascading component/field dropdowns; `ValConst` → single `constValue` text field; `VarRuntime` → path-based with `@` prefix in DSL output
- **`BuildAliasSection`** (`PlaytestAliasHelpers.cs`) — skips `VarRuntime` cards (runtime-only, not emitted to DSL); emits `ValConst` without pipes; updated `FormatVALLine`/`FormatVARLine` accordingly
- **`GetMemberNames` / `GetZeroArgMethodNames`** (`PlaytestAliasWindow.cs`) — reflection-based population of component/field/method dropdowns; cascades on component selection change
- **Status dot** in Alias Composer window — visual connection indicator
- **Removed duplicate Window menu entry** (`SetupWizard.cs` — stale `MenuItem` removed)

**Added — Alias system (VAL/VAR/sigil/INCLUDE):**
- **`middleware_alias.py`** — parse/resolve/strip alias blocks; `parse_alias_block`, `resolve_sigils`, `strip_alias_block`; plugged into `middleware_pipeline.py` before playtest dispatch
- **VAL/VAR DSL keywords** (`PlaytestParser.cs`) — `VAL name /Path` for static aliases; `VAR name @/Path|Comp|field` for dynamic runtime aliases; `$name` sigil expansion at parse time (VAL) or step time (VAR)
- **`PlaytestVarRegistry.cs`** — runtime sigil store; populated from `get_aliases` response; used by `PlaytestRunner` during step expansion
- **INCLUDE directive** (`PlaytestParser.cs`) — `INCLUDE path/to/file.defs` imports VAL/VAR/MACRO defs from external file; symlink traversal hardened (max depth 4)
- **`get_aliases` MCP command** (`CommandRouter.Registration.cs`) — session-init command that returns alias table keyed by object path; replaces per-hierarchy alias block
- **`ParseResult.Warnings`** — non-fatal parse issues (unknown sigils, missing defs) returned alongside output; `HasGlobalAbort` moved into `ParseResult` flag (−11 LOC)
- **`PlaytestPositionResolver.cs`** — `@path.position + offsets` expression; resolves WorldSpace position at runtime
- **`PlaytestAliasWindow.cs`** — Alias Composer EditorWindow; drag-drop, Pick button, USS light/dark themes; accessible via MCP menu
- **`PlaytestAliasHelpers.cs`** — `FormatVALLine` / `FormatVARLine` helpers; no trailing pipes on empty component/field
- **`PlaytestAliasButton.cs`** — toolbar button to open Alias Composer from chat panel

**Added — Batch DSL fields/compress (C#-side filtering):**
- **`FieldProjector`** (`FieldProjector.cs`) — C# port of Python's `project_fields()`. Filters `inspect`/`get_component` response to only the named fields; dot-prefix syntax (`-fieldName`) excludes fields instead. Wired into `ExecInspect` and `ExecGetComponent` via `ApplyFieldsCompress` helper in `CommandRouter.ObjectHandlers.cs`.
- **`DefaultStripper`** (`DefaultStripper.cs`) — C# port of Python's `strip_defaults()`. Removes fields whose values match Unity's type defaults (0, false, empty string, identity quaternion, zero vector, etc.); field-specific overrides for known noisy keys. Activated when batch DSL line carries `compress=true`.
- **`fields` and `compress` optional params** (`CommandRouter.Registration.cs`) — registered as valid optional params so `CommandValidator` accepts them in batch DSL. `fields` wins over `compress` when both are present (matches Python no-strip semantics). Zero Python changes — `batch.py` passes DSL text through unmodified.

**Added — Test infrastructure:**
- **`get_test_progress` MCP tool** (`testing.py`, `TestRunner.cs`) — returns live running|ran|passed|failed|skipped|total|elapsed|eta snapshot while tests are in flight. Marked `allowedDuringCompile` so it can be polled across domain reloads.
- **`TestResultPersistence`** (`TestRunner.cs`) — persists test results to `~/.unity-mcp/test-results/` as JSON; `ResetOnReload` restores from file on domain reload instead of clearing `SessionState`, preventing result loss across recompiles.
- **`run_unity_tests.py`** (root) — self-contained TCP-based NUnit runner script. Auto-discovers Unity MCP port, reconnects across domain reloads, polls every 2s. Usage: `python run_unity_tests.py [EditMode|PlayMode]`.

**Changed:**
- Alias block removed from `get_hierarchy` response (session-init pattern via `get_aliases` instead)
- `TokenSavingsEstimate` formula accounts for alias block overhead
- `ALIAS` keyword marked deprecated with warning; `VAL` is canonical replacement
- MOVE_PATH label leak fixed; RawPosition VAR expansion fixed

**Fixed:**
- INCLUDE symlink traversal hardening (loop prevention + depth cap)
- Position resolver exception handling (null guard + fallback)
- Token bounds checks for ASSERT/WAIT_UNTIL
- SetTimeScale config caching
- FormatVALLine trailing pipe on empty fields eliminated (10-architect audit, 37 findings across 6 waves)
- **`ObjectComponentTests` TearDown** (`ObjectComponentTests.cs`) — adds `EditorSceneManager.NewScene` in `[TearDown]` to prevent Save Scene dialog blocking subsequent test runs

**Tests (42 new NUnit + 3 new pytest on this sprint):**
- `GetAliasesTypedTests.cs` (11 NUnit) — typed alias command wiring, AliasType response shape
- `PlaytestAliasHelperTypedTests.cs` (19 NUnit) — FormatVALLine/FormatVARLine per type, BuildAliasSection skips VarRuntime
- `PlaytestDropHelperMemberTests.cs` (12 NUnit) — GetMemberNames, GetZeroArgMethodNames, cascading dropdown population
- `test_middleware_alias.py` (+3 pytest) — typed alias passthrough, sigil expansion with AliasType
- `test_middleware_alias.py` (51 Python tests total) — parse, resolve, strip, pipeline wiring
- `test_middleware_alias_lifecycle.py` (19 Python tests) — session lifecycle, defs param, warnings
- `GetAliasesTests.cs` (10 NUnit) — command wiring, response shape
- `PlaytestVarTests.cs` (40 NUnit) — VAL/VAR parse, sigil expansion, unknown sigil passthrough
- `PlaytestValComboTests.cs` (10 NUnit) — combined VAL+VAR+INCLUDE scenarios
- `PlaytestValEdgeCaseTests.cs` (17 NUnit) — malformed input, deep nesting, circular refs
- `PlaytestAliasGridTestTests.cs` (30 NUnit) — grid coverage across all DSL combinations
- `PlaytestAliasModularityTests.cs` (6 NUnit) — modular alias file loading
- `PlaytestAliasRealWorldTests.cs` (4 NUnit) — end-to-end playtest scripts with aliases
- `PlaytestAliasStressTests.cs` (5 NUnit) — large alias tables, performance bounds
- `PlaytestAliasWindowTests.cs` (10 NUnit) — EditorWindow lifecycle
- `PlaytestPositionResolverTests.cs` (15 NUnit) — expression parsing, offset math, null paths
- `FieldProjectorTests.cs` (38 NUnit) — aliases, dot-prefix exclusion, structural passthrough, special chars `[]{}<>"`
- `DefaultStripperTests.cs` (40 NUnit) — type defaults, field-specific overrides, structural lines, edge values
- `TestProgressTests.cs` (6 NUnit) — GetProgress state machine, persistence round-trip
- `test_batch.py` (+3 Python passthrough tests), `test_testing_tools.py` (+2 Python TDD tests)
- C# NUnit: 6576 passed, 9 pre-existing failures (EditMode)

## [v0.77.0] — 2026-07-09 — tools gap sprint: 34 new actions across 7 domains

**Added — Timeline (animation.py → timeline()):**
- **M1: `reorder_track`** — move track to position index (reflection on `m_Tracks`, only permitted reflection hack)
- **M2: `duplicate_clip`** — duplicate clip on same track with configurable time offset
- **M3: `add_marker` / `remove_marker`** — add/remove `SignalEmitter` markers on timeline tracks
- **M4: `set_track_offset`** — set track offset mode (`auto`, `transform`, or `scene`)
- **M5: `set_duration`** — set timeline asset duration
- **M6: `add_sub_track`** — add a sub-track to a GroupTrack

**Added — Animator (animation.py → animator()):**
- **M7: `set_state_speed`** — set speed multiplier for an animator state
- **M8: `update_transition`** — modify existing transition parameters (duration, exit_time, has_exit_time)
- **M9: `set_avatar`** — assign avatar to Animator component from asset path
- **M10: `rename_state`** — rename an existing animator state
- **M10: `rename_param`** — rename an existing animator parameter

**Added — Animation (animation.py → animation()):**
- **M11: Color curve support** — `keys` accepts hex values (`#FF0000`) in Color property curves
- **M12: `set_wrap`** — set clip wrap mode (`loop`, `once`, `pingpong`, `clamp`)
- **M13: `set_framerate`** — set clip sample rate (frames per second)
- **M14: `get_clip_path`** — return asset path for an animation clip

**Added — Particle (animation.py → particle()):**
- **M16: `trails` module** — 9 settable properties via `set` action (lifetime, material, textureMode, etc.)
- **M17: `play` / `stop` / `pause`** — control particle system playback via new `action` values

**Added — Material (asset.py → material()):**
- **M19: `get_shader_errors`** — return shader compilation errors for a material
- **M20: `list_shaders`** — list available shaders with optional name filter param
- **M22: `set_fields`** — batch property setting (multiple `prop=value` pairs in one call)

**Added — Objects (objects.py):**
- **`clone_object`** — duplicate a GameObject with positional offset (`offset_x`, `offset_y`, `offset_z`)

**Added — UI (ui.py):**
- **`set_ui_style`** — apply USS inline styles to a UI element

**Added — VFX (vfx_intent_tool.py):**
- **`set_vfx_quality`** — set VFX quality level for a VFX Graph asset

**Added — Shader Graph (ShaderGraphHelper.Mutations.cs):**
- **`set_node_value`** — set input value on a Shader Graph node
- **`connect_ports`** — connect two ports in a Shader Graph
- **`add_node`** — add a new node to a Shader Graph

**Added — Tool metadata:**
- `shader` added to BATCHABLE set in `tool_specs.py`

**Tests:**
- `test_server_timeline.py` (10 tests) — M1–M6 timeline actions
- `test_server_animator.py` (14 tests) — M7–M10 animator actions
- `test_server_animation.py` (13 tests) — M11–M14 animation actions
- `test_server_particle.py` (18 tests) — M16–M17 particle actions
- `test_server_material.py` (17 tests) — M19–M22 material actions
- `test_server_shader.py` (46 lines) — Shader Graph mutations
- `test_server_objects_extra.py` (22 lines) — clone_object
- `test_vfx_intent.py` (34 lines) — set_vfx_quality
- Python unit: 4175 passed, 0 failed
- C# NUnit: 6126 passed, 10 failed (all pre-existing), 7 skipped

## [v0.76.0] — rename_object, NUnit 159 fix, get_test_results resilience, batch SO multi-field

**Added:**
- **ScriptableObject multi-field set** (`ScriptableObjectCommand.cs`) — `scriptable_object action=set` accepts a `fields` parameter with `\n`-separated `prop=value` pairs; sets N properties in a single load/save cycle (~68% token savings vs N separate calls); atomic — if any field is missing, no fields are written.
- **`rename_object` tool** (`objects.py`, `ObjectManager.RenameObject`, `CommandRouter.Registration.cs`) — renames a GameObject and returns the new scene path. Registered as `_RW_IDEM` (idempotent write). Undo-aware; marks scene dirty in Edit Mode. `set_property` docstring now cross-references `rename_object` for GO name changes. Added to `tool_specs.py` (129th ToolSpec, category `object`) and `CommandRegistryCompletenessTests` snapshot.

**Fixed:**
- **Batch quoting double-unescape** (`BatchHelper.cs`) — removed redundant `UnescapeJsonString` call in `ParseLines`; `ParseValue` now correctly handles `\"`, `\\`, `\n` inside quoted strings, fixing string values with embedded quotes in batch commands.
- **NUnit 159 failures** (`TestAssemblySetup.cs`) — added `CommandRegistry.InitDefaults()` to `[OneTimeSetUp]`; tests that relied on the registry being pre-populated (BlendTree, Capabilities, ScenePill) no longer fail when run in isolation. `ScenePillPipelineTests` tears down EditorPrefs keys to prevent cross-test pollution.
- **`get_test_results` domain-reload resilience** (`testing.py`) — wrapped TCP send in `try/except`; returns `"pending"` instead of raising `ToolError` when the domain reload drops the connection mid-poll.
- **Live test scene isolation** (`conftest.py`) — saves the active scene before entering Play Mode to prevent `SaveCurrentModifiedScenesIfUserWantsTo` dialog blocking test fixture teardown.
- **`timeout=0` sentinel shim** (`conftest.py`) — `wrapped_bridge` now resolves per-command timeout via `get_timeout(cmd)` instead of forwarding `0`; prevents instant-cancel on commands with no explicit timeout.
- **Hinter live test raw bridge bypass** (`test_hinter_real.py`) — replaced direct `bridge.send` calls with the `wrapped_bridge` fixture so the timeout shim is applied correctly.
- **`compile_status` regex** (`test_console_compile.py`) — pattern updated to also match `idle-failed` and `idle-stale` variants returned after a compile error.

## [v0.75.0] — Playtest Composer UI Toolkit, DSL Macros, Scenario Persistence, ShellHelper, NlComposerBridge

**Added — Playtest Composer (visual DSL editor):**
- **PlaytestComposerWindow** (`PlaytestComposerWindow.cs`) — full rewrite from IMGUI/ReorderableList to UI Toolkit (CreateGUI + ListView + USS). Exports to DSL via `PlaytestDslExporter`; imports via `PlaytestParser`; domain-reload-safe via `[SerializeField]`. Chat toolbar button (`PlaytestComposerButton.cs`) opens the window.
- **PlaytestStepElement** (`PlaytestStepElement.cs`) — per-row `VisualElement` with a dedicated sub-panel per step type: Move, Teleport, Wait, TimeScale, WaitUntil, Assert, Invoke, **Set**, **Click**, **Invariant**, **Capture**, **AssertCaptured**, **AssertNear**, **AssertConsoleClean**, Log, Section. Invalid steps render with a red tint.
- **PlaytestComposer.uss** — USS stylesheet for the Composer window; theme-neutral.
- **Context-aware drag & drop** (`PlaytestDropHelper.cs`) — drag a `GameObject` or `Component` from Hierarchy/Inspector onto a step row; `ShowComponentPicker` and `ShowFieldPicker` open `GenericDropdownMenu` pickers; `ApplyMember` fills `path/component/method` for both `Invoke` and `Set` step contexts. `StopPropagation()` on pointer-down prevents `TextField` from swallowing drag events.
- **PlaytestSmartDrop** (`PlaytestSmartDrop.cs`) — bulk drop zone; dropping a multi-selection of GameObjects creates one `Move` step per object. `AttachMultiDnD` wires the zone to the window.
- **PlaytestStepValidator** (`PlaytestStepValidator.cs`) — validates `VisualStep` fields per step type (required path, positive timeout/delay, non-empty query, etc.); run gate rejects scripts with any invalid step.
- **ComposerStateStore** (`ComposerStateStore.cs`) — JSON persistence to `Playtests/` directory (outside `Assets/`); `Save`/`Load`/`Exists`/`List` API; all fields `[SerializeField]` for domain-reload survival.
- **VisualStep.Clone()** — right-click context menu exposes Duplicate; deep-copies all fields.
- **PlaytestFileHelper** (`PlaytestFileHelper.cs`) — extracted file-path helpers (normalize name, resolve `Playtests/` directory).

**Added — DSL extensions:**
- **MACRO/END_MACRO/CALL** (`PlaytestParser.cs`) — define reusable DSL blocks with positional parameters; CALL expands inline before parsing; nesting up to depth 10; missing END_MACRO or nested MACRO definitions throw `ArgumentException`.
- **MOVE_PATH** (`PlaytestParser.cs`) — `MOVE_PATH x1,y1,z1 > x2,y2,z2 [> ...] [TIMEOUT n]` expands into multiple `Move` steps at parse time; `>` separates waypoints; optional TIMEOUT applies to each leg.
- **SECTION** (`PlaytestParser.cs`) — emits a section-header step (`StepType.Section`) rendered as `--- label ---` in reports; separates logical phases in long scripts.
- **DESC** (`PlaytestParser.cs`) — `DESC "text"` preceding any step attaches a human-readable label (`PlaytestStep.Label`) to that step; no step is emitted for DESC itself.
- **AS "text" suffix on ASSERT** (`PlaytestParser.cs`) — inline description on any `ASSERT` line (`ASSERT /Obj|hp >= 100 AS "health full"`); stored in `PlaytestStep.Message` and surfaced in failure output.
- **Scenario persistence** (`server/src/unity_mcp/tools/scenarios.py`) — four new MCP tools: `save_scenario` (writes DSL to `~/.unity-mcp/scenarios/<name>.dsl`), `load_scenario`, `list_scenarios`, `run_scenario` (load + run_playtest in one call). Registered as `SESSION_SKILLS` / `UNIT_TESTS` tier.
- **GdSnapshotSerializer** (`unity-plugin/Editor/RegionTool/GdSnapshotSerializer.cs`) — converts `RegionSnapshot` GD annotations (point, region, polyline, measurement) into `ALIAS @label x,y,z` DSL lines. `ToPlaytestPreamble()` emits a complete preamble block from a snapshot collection.

**Added — infrastructure:**
- **ShellHelper** (`ShellHelper.cs`) — DRY extraction of shell primitives previously duplicated across `LoginShellCommand.cs` and `ChatBinaryResolver.cs`. Provides `ShellQuoteSingle`, `BuildLoginShellArgs`, `CreateLoginShellPsi` (cross-platform: `/bin/zsh` on macOS, `/bin/bash` or `/bin/sh` on Linux, null on Windows), and async `RunProcessAsync`. `EditorPrefsKeyPrefix` constant shared across all shell consumers. `LoginShellCommand` is now a thin wrapper with no logic of its own.
- **NlComposerBridge** (`NlComposerBridge.cs`) — spawns the active CLI backend (claude/codex/etc.) as a subprocess to convert natural-language game test descriptions into PlayTest DSL. Ships an embedded system prompt with the full DSL command reference. Test seams via `RunProcessOverride` / `ResolveBinaryOverride`.
- **NlCommandWindow** (`NlCommandWindow.cs`) + **NlStepParser** (`NlStepParser.cs`) — Smart Command panel: free-text NL input → `NlComposerBridge.Convert()` → parses response → appends new steps to the Composer.

**Fixed:**
- **Last observed value in WAIT_UNTIL timeout** (`PlaytestRunner.cs`) — timeout message now includes final read: `WAIT_UNTIL … — TIMEOUT after 5s (last: 12)` instead of a bare timeout.
- **ABORT false-positive** (`PlaytestParser.cs`) — ABORT detection moved inside the AND/OR extra-token loop; previously a value containing the substring "ABORT" could erroneously set `AbortOnFail`.
- **`GetMethod` AmbiguousMatchException** (`RuntimeHelper.cs`) — zero-arg path now uses `Type.GetMethod(name, flags, null, Type.EmptyTypes, null)`; with-arg path uses `GetMethods().FirstOrDefault(m => m.GetParameters().Length == argCount)`. Cache key includes arg count suffix to avoid cross-arity collisions.

**Internal:**
- **EvalCompound short-circuit** (`PlaytestRunner.cs`) — AND exits on first `false` condition; OR exits on first `true` condition, avoiding unnecessary reflection reads in compound wait steps.

**Tests:**
- `PlaytestDslExporterTests.cs` (23 NUnit cases): roundtrip export/parse for all step types.
- `PlaytestDropHelperTests.cs` (87 NUnit cases): component picker, field picker, `ApplyMember` for Invoke + Set contexts, multi-drop, `StopPropagation` guard.
- `PlaytestComposerTests.cs` (~36 NUnit cases): `Bind`, panel visibility per step type, description field, `Clone`.
- `PlaytestStepValidatorTests.cs` (42 NUnit cases): per-type validation error messages, null guard, `IsScriptValid`.
- `ComposerStateStoreTests.cs` (~35 NUnit cases): save/load/list/exists round-trips, bad name rejection, missing file.
- `ShellHelperTests.cs`: shell quoting correctness, cross-platform PSI factory, `BuildLoginShellArgs` format.
- `NlComposerBridgeTests.cs`: NL→DSL conversion via `RunProcessOverride` seam, system prompt embedding.
- `PlaytestDslExtensionTests.cs` (7 NUnit cases): SECTION step type, DESC label attachment, AS message on ASSERT, MOVE_PATH waypoint expansion, section preservation in compress_report.
- `PlaytestMacroTests.cs` (15 NUnit cases): MACRO definition, CALL expansion, positional args substitution, nesting depth guard, missing END_MACRO error, stray END_MACRO ignored.
- `GdSnapshotSerializerTests.cs` (10 NUnit cases): point, region, polyline, measurement serialization; multi-snapshot preamble; unknown type comment fallback.
- `test_scenarios.py` (13 new pytest cases): save/load/list/run round-trips, name validation, missing scenario error, run_scenario delegates to run_playtest.
- `test_runtime.py` (2 new cases): MOVE_PATH script forwarded unchanged, `_compress_report` preserves SECTION lines.

## [v0.74.0] — wait_until: Method Dispatch, AND/OR Conditions, Abort-on-Fail

**Added:**
- **Method dispatch via `()` convention** (`RuntimeHelper.cs`) — field path segments ending with `()` invoke a zero-arg method via reflection (e.g., `wait_until /Player|Health|IsFullHP() == True`). `MethodInfo` cached per `(Type, methodName)` pair; cache cleared on domain reload. Enforced as zero-arg only (throws on methods with parameters).
- **AND/OR compound conditions in DSL** (`PlaytestParser.cs`) — `WAIT_UNTIL` now accepts flat `AND` / `OR` chains: `WAIT_UNTIL /Player|Health|hp >= 100 AND /Player|Mana|value >= 50`. AND and OR cannot be mixed in the same step (throws `ArgumentException`). Extra conditions stored in `PlaytestStep.Queries/BatchOps/BatchValues/IsOr`.
- **`EvalCompound` static helper** (`PlaytestRunner.cs`) — `internal static bool EvalCompound(bool primary, string[] queries, string[] ops, string[] vals, bool isOr, Func<string,string> readFn)` reduces primary + extra conditions with AND/OR logic. Pure function, no Unity API calls — testable without runtime.
- **Abort on fail** (`PlaytestRunner.cs`, `RuntimeHelper.cs`, `PlaytestParser.cs`) — three surfaces:
  - `abort_on_fail=True` param on `wait_until` and `run_playtest` (Python `runtime.py`)
  - `ABORT_ON_FAIL` global directive in DSL script (parsed by `PlaytestParser.HasGlobalAbort()`, applies to all steps)
  - `ABORT` per-step token in `WAIT_UNTIL` line (sets `PlaytestStep.AbortOnFail`)
  - On timeout: `EditorApplication.isPlaying = false` — stops Play Mode immediately.

**Tests:**
- `WaitConditionTests.cs` (27 new NUnit cases): method dispatch happy path, zero-arg enforcement, cache miss/hit, AND compound pass/fail, OR compound pass/fail, mixed-operator rejection, `ABORT_ON_FAIL` directive parsing, per-step `ABORT` token, `EvalCompound` unit tests, `HasGlobalAbort` unit tests.
- `test_runtime.py` (4 new cases): `abort_on_fail=True` serialized to TCP args, `run_playtest` abort param forwarding, param omitted when `False`.

## [v0.73.1] — Command Registration Race Condition Fix

**Fixed:**
- **Command registration race condition** — `Bootstrap.cs` deleted. Registration moved from `[InitializeOnLoadMethod] Bootstrap.Init()` into `MCPServer.StartAsync()`, called before TCP bind. Commands are now guaranteed registered before the server accepts connections.
- **`CommandRegistry.Ready` gate** — `volatile bool Ready` flag added; reset in `Clear()`, set at end of `RegisterAll()`. `CommandRouter.CheckGuards()` returns `retry-2000` when `!Ready`, preventing command dispatch before registration completes.
- **Dead `EnsureEnabledToolsCacheWarm()` removed** — method deleted from `CommandRouter.ObjectHandlers.cs`; call site in `MCPServer.cs` replaced with `CommandRegistry.InitDefaults()`.

**Tests:**
- `RegistrationGateTests.cs` (6 new cases): `Ready` flag lifecycle, gate blocks dispatch before registration, gate unblocks after `RegisterAll`, `Clear` resets flag.
- `BootstrapTests.cs`, `EnabledToolsCacheTests.cs` updated to reflect Bootstrap deletion.

## [v0.73.0] — Tool Disambiguation & Play Mode Fail-Fast

**Fixed:**
- **`[Play Mode]` qualifier survives truncation** — `_short_description()` now preserves mode-qualifier prefixes during docstring truncation. Previously 11 runtime-only tools (`run_tests`, `get_test_results`, `set_runtime_property`, etc.) had their `[Play Mode]` marker stripped, making them appear mode-agnostic in the tool list.
- **`AI/batch.md`** — Added `screenshot` and `ask_user` to the non-batchable tools list; removed invalid batch examples that referenced non-existent command formats.

**Added:**
- **Cross-reference docstrings** — 14 pairs of similarly-named tools now include `use \`<tool>\`` pointer in their docstring (e.g. `get_component` ↔ `get_components_list`, `run_tests` ↔ `get_test_results`). Reduces LLM tool-selection errors when tool names overlap.
- **Fail-fast Play Mode guard** — Middleware layer blocks runtime-only commands (e.g. `set_runtime_property`, `get_runtime_property`) before the TCP round-trip when Edit Mode is confirmed, returning an immediate `[Play Mode required]` error. Saves one TCP call per misrouted invocation.

**Tests:**
- `BatchHelper` NUnit: 5 new cases covering async-command rejection, specialDispatch rejection, and runtime-command rejection in batch context.
- Docstring regression suite: qualifier-survival tests for all 11 `[Play Mode]` tools + cross-reference pointer validation for all 14 disambiguated pairs.

## [v0.72.0] — Token Counting for Reasoning Models + Context Window Hardening

**Fixed:**
- **Reasoning token output silently dropped** — `stream_transform.py` now includes `reasoning_output_tokens` in the total output token count for reasoning models (o3, o3-pro, GPT-5.5, Fable 5). Previously these tokens were read from the backend response but discarded, causing token budgets to underestimate actual consumption and context limits to be falsely reported as "clean".

**Added:**
- **Extended model detection** — `ModelContextWindows.cs` now recognizes latest models: GPT-5.5/5.4, Fable 5, gpt-4.1, o3/o3-pro, o4-mini with spec-compliant context window sizes. Codex fallback context window increased from 192k to 1M tokens (matches latest Claude).
- **Visual output reserve on context progress bar** — `ContextProgressBar.cs` now enforces a 20% output safety margin: the bar hits 100% fill at 80% input consumption, giving visual warning before the actual context limit is reached. Prevents edge-case overflow on high-token replies.

## [v0.71.0] — Revert MCP Config Key `unity-kiss` → `unity-mcp` (Chat Breakage Fix)

**Breaking:** MCP server config key reverted from `unity-kiss` (v0.70.8) back to `unity-mcp` — the rename caused Chat to break in both Claude Code and Codex due to stale name references in relay + C# config writers.

**Fixed:**
- `MCP_BLANKET` / `--permission-prompt-tool` now correctly derives from `SERVER_NAME` constant (`mcp__unity-mcp`) — was hardcoded to wrong value `mcp__unity` since introduction, causing permission prompts to fail silently.
- `config/validator.py` and `install/commands.py` doctor now use shared `SERVER_NAME` constant from `config/merger.py` — auto-migration and diagnostics work correctly across Python + C# config writers.
- `login_shell_path()` retries after 30s TTL on failure instead of permanently caching empty PATH — fixes Node-based CLI (Codex, OpenCode) spawn failures when initial PATH probe fails transiently.

**Added:**
- Cross-language drift guard (`test_server_name_consistency.py`) — Python ↔ C# `SERVER_NAME` and `MCP_BLANKET` can never silently drift again (enforced at build/test time).
- `CliSession` spawn kwargs characterization tests — pins stdin/stderr/limit/PATH contract for single-turn (Codex) vs multi-turn (Claude, Kimi) backends.
- Shared `SERVER_NAME` constant in Python (`config/merger.py`) and C# (`PermissionConfig.cs`) — single source of truth, all config writers import.

**Improved:**
- Consolidated `login_shell_path()` + `_which_via_login_shell()` into shared `_run_login_shell()` helper (DRY) — one code path for login-shell PATH resolution.
- All config writers (Python + C#) now derive server key from constants, not hardcoded literals — prevents accidental renames on future merges.

## [v0.70.8] — MCP Server Renamed `unity-kiss` (no tautology, auto-migrated)

- **Server name is now `unity-kiss`** — the config key was `unity-mcp`, which read as a tautology under Codex's `[mcp_servers.unity-mcp]` ("mcp" twice). Renamed to `unity-kiss` in **every** config writer — Codex TOML (project `.codex/config.toml` + relay inline `-c`), Claude/Cursor/Windsurf `.mcp.json`, Kimi `mcp.json`, Agy `settings.json`, OpenCode, and the Claude chat `--mcp-config`. Both writers of the Codex config (C# `ProjectConfigToml`, Python `backend_def` inline) use the same `unity-kiss` name, so they still deduplicate into one server (the v0.70.7 fix, kept).
- **Existing installs auto-migrate** — every merge (JSON and TOML, Python `merger.py` and C# `WizardConfigWriter`/`ProjectConfigToml`) recognises the old `unity-mcp` entry and **replaces** it with `unity-kiss` (key and value), so a prior install is renamed on next write rather than left as a duplicate second server. The foreign bare `[mcp_servers.unity]` (CoplayDev) is still stripped. The PyPI package (`uvx … unity-mcp`), temp filenames, and the `com.unity-mcp.editor` package id are unchanged.

## [v0.70.7] — Codex Chat: SIGTRAP & Duplicate MCP Fixed

Two remaining Codex-in-chat failures, both root-caused:

- **`codex exited -5` (SIGTRAP) — piped stdin** — Single-turn backends (Codex) take their prompt in argv and never read stdin, but the relay always spawned with `stdin=PIPE`. Codex saw a live-but-empty pipe, blocked on "Reading additional input from stdin…", then crashed with SIGTRAP (exit -5). `CliSession` now honours a per-backend `reads_stdin` flag and passes `stdin=DEVNULL` for single-turn backends, giving codex an immediate EOF. (Claude/Kimi still get `PIPE` — they stream turns over stdin.)
- **Duplicate Unity MCP server → hang** — The project `.codex/config.toml` registers the Unity server as `[mcp_servers.unity-mcp]`, but the relay's inline `-c` overrides used a *different* name (`mcp_servers.unity`). Codex merged them into **two** servers both pointing at port 9500, and the second registration hung the session (chat timeout on scene queries). The inline `-c` flags now use the same `unity-mcp` name, so they override the project entry into a single server instead of adding a duplicate.

## [v0.70.6] — Large Backend Output Fixed (chat -5 crash)

- **16 MiB stdout line limit** — Chat backends (Codex especially) emit large single-line NDJSON tool results — a full scene hierarchy is one line that can exceed 64 KiB. The relay's stdout reader used asyncio's default 64 KiB limit, so a big result raised `ValueError: Separator is not found, and chunk exceeds the limit`, killing the relay and the backend (surfaced in chat as `codex exited -5`). `CliSession` now reads with a 16 MiB line limit. Combined with v0.70.4's stderr surfacing and v0.70.5's login-shell PATH, this closes the Codex chat crash on scene queries.

## [v0.70.5] — Node-based Chat Backends Fixed (PATH)

- **Backends spawn with login-shell PATH** — Node-based CLIs (Codex is `#!/usr/bin/env node`, also OpenCode) crashed with `exit 127: env: node: No such file or directory`. Unity launched from Finder gives child processes a minimal PATH without `node`, so even a correctly-resolved `codex` binary failed when its shebang looked for `node`. `CliSession` now prepends the user's full login-shell PATH (cached `login_shell_path()`) when spawning any backend, so node/npm and other user tools resolve. Combined with v0.70.4's stderr surfacing, this was diagnosed from the now-visible `codex exited 127` chat error.

## [v0.70.4] — Chat Backend Errors Now Visible

- **Backend stderr surfaced to chat** — When a chat backend CLI (Codex, OpenCode, Kimi, …) failed, its stderr was discarded (`stderr=DEVNULL`), so a crash produced a silent disconnect with no message in the chat window. Claude writes errors to stdout (visible) but most CLIs write to stderr. Now `CliSession` captures stderr (`stderr=PIPE`) and, on a non-zero exit, the relay drains it into the chat error event — you see `"codex exited N: <reason>"` instead of nothing. Also fixed an EOF/returncode race (`await session.wait()` before checking exit code) that could report a crash as a clean "done".

## [v0.70.3] — Chat Relay Bootstrap, Cross-Platform & Multi-CLI Hardening

**Built-in Chat now works after UPM install (uvx bootstrap):**
- **Relay via uvx** — The in-editor Chat relay (`unity_mcp.chat_relay`) failed with "Python not found" on UPM installs because `RelaySpawner` looked for a local `../server` dir that isn't shipped in the package. It now launches through the same self-contained `uvx --from git@vX unity-mcp-relay` path the MCP server already uses. Added a `unity-mcp-relay` console-script + sync `main()`, and a `RelayCommandResolver` that is install-source-aware (Local → python/uv; Git/Registry → uvx). Fixes a latent argv-discard bug for Local+uv installs.
- **Async spawn + threading safety** — `RelaySpawner.Spawn()` split into main-thread `PrepareSpawn` / ThreadPool `ExecuteSpawn` / main-thread `CommitSpawn` so a cold `uvx` start (up to 45s) never freezes the editor and never touches Editor APIs off-thread. `RelaySpawnState` marshals results back via `MainThreadDispatcher`.
- **uvx warmup** — `RelayWarmup` pre-fetches the relay package into `~/.cache/uv` on package import so the first real Chat spawn is 1-2s instead of 10-45s.

**Cross-platform & multi-CLI:**
- **Windows PATH parity** — Backend CLIs (codex/opencode/claude/kimi) and `uvx` now resolve on Windows via a Registry-PATH probe (`HKCU`/`HKLM` + npm/cargo/uv/scoop/WinGet well-known dirs), matching the Unix login-shell resolver. Fixes silent "binary not found" when Unity didn't inherit the user PATH. (`WindowsPathProbe.cs` + `backend_def.py`.)
- **OpenCode env fix** — `_opencode_transform` emitted `"env"` but OpenCode's schema uses `"environment"`, silently dropping `UNITY_MCP_PORT` for OpenCode users. Corrected.
- **`configure --all`** — `install.py configure --all` auto-detects every installed AI client and writes all their MCP configs in one command.
- **Junie client** — Added the missing `junie` entry to `CLIENT_REGISTRY` (and project-config path map); `configure --tool junie` no longer fails.

**Versioning & release automation:**
- **Version-aware server pin** — After a plugin update, the per-project server pin (`.mcp.json @vX`) now re-syncs automatically on the post-update domain reload (`ProjectConfigWriter` uses a version-scoped session guard) — no cross-assembly coupling, no stale pin.
- **CI auto-release** — `.github/workflows/release.yml` creates the GitHub Release from any pushed `v*` tag (fixes the "no updates available" bug where a tag existed but no Release did) with a version-sync gate. `scripts/release.sh` bumps all 5 version artifacts + tags + pushes in one command. `sync_versions.py --check` added.

**Test debt cleanup:** Updated stale source-inspection/behavior tests that had drifted from earlier refactors (v0.67–v0.70: MainThreadDispatcher split, backend eager-init, ErrorClassifier prefix, `get_version` registry removal, `ClientConnectionHandler` split). Full EditMode suite: 5723 passed, 0 failed.

## [v0.70.2] — Changelog Ships With Package

- **CHANGELOG.md now included in the UPM package** — The Updates page showed "Changelog not found" in consumer projects because `CHANGELOG.md` lived only in the repo root, outside the `unity-plugin/` package subtree, so it never reached `PackageCache`. Root `CHANGELOG.md` is now mirrored to `unity-plugin/CHANGELOG.md` (with a stable `.meta`) via a versioned `.githooks/pre-commit` hook — root stays the source of truth, the package copy auto-syncs on commit. Enable on fresh clones with `git config core.hooksPath .githooks`.

## [v0.70.1] — UPM Install Hotfix

**Critical: GUID Collision Broke Plugin Install in Third-Party Projects:**

- **BatchHelper.cs.meta placeholder GUID** — Replaced hand-written sequential placeholder GUID (`1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d`) with a real unique GUID. On projects that happened to own the same placeholder GUID (e.g. an `Assets/Game/Shaders` folder), Unity detected a collision and — since packages live in an immutable folder where GUIDs cannot be reassigned — **silently ignored `BatchHelper.cs`**. This caused `CS0103: The name 'BatchHelper' does not exist` in 4 call sites (`CommandRouter.ObjectHandlers.cs`, `CommandRouter.Registration.cs`, `ObjectManager.Properties.cs`) and a cascading Burst/Mono.Cecil `Failed to resolve assembly 'UnityMCP.Editor.Wizard'` error. Diagnosed via 5-architect review sprint.
- **Orphan .meta cleanup** — Removed dangling `Chat/Markdown.meta` and `Chat/Viewers.meta` for empty (untracked) folders that triggered "meta file exists but folder can't be found" warnings on import.

## [v0.70.0] — 2026-07-03 <!-- 10-architect review sprint: RetryPolicy extraction, scene.py split, gating refactor, FORCE_VISIBLE fix, PendingAskRegistry extraction -->

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
- **feat: Codex App-Server Backend (Persistent JSON-RPC Sessions)** — New `CodexAppServerBackend` replaces `codex exec` spawn-per-turn model with persistent `codex app-server` sessions via direct stdio + JSON-RPC 2.0 protocol. One process per chat session (matches Claude model), eliminates TCP slot-thrash. Real token streaming via `item/agentMessage/delta` (240+ deltas/turn vs batched text). Protocol: `initialize` → `thread/start` → repeated `turn/start` calls with `mcpToolCall` items. MCP injection via `-c mcp_servers.*` flags at session init. Spike-verified with codex 0.137.0. Files: NEW `CodexAppServerBackend.cs`, `CodexAppServerParser.cs`, `Tests/CodexAppServerParserTests.cs` (15 test cases with real JSON-RPC fixtures); MODIFIED `BackendSpec.cs` (enum), `BackendRegistry.cs` (factory), `MCPChatWindow.cs` (factory switch), `BackendRegistryTests.cs` (baseline update to 3 backends).
- **fix: Prevent secondary MCP server registration from ~/.mcp.json** — Added `--strict-mcp-config` flag to `claude -p` subprocess invocation. When the in-Unity Chat agent spawns Claude, it now prevents auto-discovery of `~/.mcp.json` which was registering a second MCP server with key `"unity-mcp"`. Permissions UI (MCPPermissionsWindow) now labels tools as "in-Unity Chat agent" for clarity.
- **docs: Codex setup guide rewrite** — Updated `docs/install/codex.md` to lead with in-editor workflow (Window > MCP Chat > Codex dropdown). Documents correct argv injection via `-c` flags for both first turn and resume. Moved manual `.codex/config.toml` to appendix (CLI-only use).

## [v0.17.37] — 2026-06-07

- **refactor: Catalog format JSON → plain-text (v0.17.37, token economy)** — Changed `get_catalog()` format from JSON to plain-text line-delimited: `CORE:tool1,tool2\nSCENE_EDIT:tool3,...` sent over wire via `set_tool_catalog`. Reduces ~40% wire size, eliminates C# JSON deserializer. NEW `CatalogParser.cs` parses text → dict. Modified: `gating.py` (catalog dict now has categories["CORE"] not separate "core" key), `server_filtering.py` (push_catalog encodes text), test suite (test_catalog.py: no JSON validation, no "core" key check, plain-text format tests). BREAKING: catalog JSON structure changed; Unity plugins must call `CatalogParser.Parse()` instead of JsonUtility.FromJson.
- **refactor: Session file format JSON → plain-text (v0.17.37)** — `save_session` / `load_session` now store plain-text: `<timestamp>\n=== hierarchy ===\n<hierarchy>...`, avoiding json.dump/json.load. Faster parsing, no JSON codec overhead. Modified: `scene_session.py` (removed json imports, partition-based parsing), test suite (test_scene_session.py: no JSON parse tests). BREAKING: legacy `.claude/session-context.json` files incompatible; users must re-save sessions.
- **fix: middleware_pipeline wrap_send file+data handling** — New test file `test_middleware_pipeline.py` validates wrap_send correctly returns both manifest text AND file path when response contains both fields. Fixes edge case where `screenshot` command returns multiview data + PNG file.

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

## [v0.13.4] — 2026-06-04 <!-- svg: tech-debt sprint wave 1–3 (Python/C#/Chat) — pure quality -->

- **Tech-Debt Sprint: Python Tooling + C# Plugin + Chat Hardening (Server v0.7.1, Plugin v0.13.4, 6 commits)** (2026-06-04) — Six-wave quality sprint addressing dead code, stale config, and chat resilience. **Wave 1 (Python):** `gen_changelog_svg.py` dual-version support (v0.7.0 / v0.7.1 renders correctly), `batch` token economy (unnecessary key omissions), 23-layer middleware dead-code removal (3 obsolete layers deleted), port-discovery test suite added (25 new `test_read_unity_port.py` cases). **Wave 2 (C# Plugin):** CommandSchema params (summary/incremental/dry_run/force fields removed from schema, reducing 4-param noise for stateless tools); 15 dead command aliases dropped; SpatialHelper InvariantCulture (float→string locale-safe); RuntimeHelper.FindComponent DRY delegate (consolidated 3 search patterns); SearchHelper single-walk (removed redundant dual-loop); 8 dead `#if` guards removed. **Wave 3 (Chat):** Undo persistence across domain reload (PendingTurnState + Undo group tracking), PendingTurnState v2 header with staleness check + 60s grace-window back-compat, enabled-tools cache computed OFF the TCP read thread (warm-up before accept loop + cached via EditorPrefs), send re-entrancy guard, build-target define (UNITY_MCP_CHAT), stderr surfacing (StderrRingBuffer), turn↔batch tests. **Wave 3c (Chat DRY/UX):** SelectionSummary/ChipContextResolver dedup, PrefKey const dedup, ChatBinaryResolver negative-cache (+ResetCacheForTests seam), hardcoded hex → `isProSkin .chat-root--light` USS class, SlashPopup ScrollView (removed MaxVisible=5 cap). **Test Coverage:** 1726 Python non-live tests pass (1779 collected, 53 live deselected); 25 script tests; 1336 C# EditMode tests (1331 pass, 5 pre-existing baseline failures, zero regressions). Files: server/src/ (11 py touched), unity-plugin/Editor/ (15 cs touched + tests).

## [v0.13.3] — 2026-06-04 <!-- svg: Editor.log out-of-band corroboration — P0 compile-tool blindness fix -->

- **Out-of-Band Compile-Tool Corroboration via Editor.log (Server v0.7.0, P0)** (2026-06-04) — `get_compile_errors`, `await_compile`, `auto_fix`, and `ask` plans now cross-verify "clean" responses from the in-plugin C# reporter against Unity's `Editor.log` (out-of-band signal immune to plugin compile failures). New module `server/src/unity_mcp/editor_log.py` parses Unity 6 Bee/Csc error logs (anchored on `## Script Compilation Error for:` marker; fallback for legacy pre-Unity-6 single-assembly format). Only overrides C#'s "clean" when BOTH signals agree: log shows errors AND dll is stale (mtime check vs plugin source). Fresh/undeterminable dll → trusts C# (zero false positives). Wired into ALL FOUR result-surfacing callers (DRY pattern) via `init_corroboration()` + `corroborate()`. This fixes the P0 silent-blindness bug where compile failures in `UnityMCP.Editor` itself caused the reporter to answer "No errors" from stale bytecode (observed in prior sprint: `UndoGroupHelper` CS0117 masked for 5 hours). Validated against real Unity 6 (6000.3.0b7); SPOF now CORROBORATED. Test count: **1709 passed** (was 1652 → +57 new tests incl. real-format log fixtures). Files: `server/src/unity_mcp/editor_log.py`, `server/src/unity_mcp/tools/scene.py`, `server/src/unity_mcp/tools/code_intel.py`, `server/src/unity_mcp/tools/codegen.py`, `server/src/unity_mcp/ask/executor.py`, `server/tests/test_editor_log.py`, `server/tests/test_codegen_corroboration.py`, `server/tests/fixtures/unity6_compile_*.log`, `server/tests/test_ask.py`.

## [v0.13.1] — 2026-06-04 <!-- svg: atomic batch rollback — transactional scene edits -->

- **Atomic Batch Rollback (v0.6.1 / 0.13.1, F27)** (2026-06-04) — Opt-in `atomic=true` mode for the `batch` command enables transactional execution: on the FIRST failing operation, all prior operations in that batch are reverted via F6's reusable `UndoGroupHelper` primitive (`OpenNamedGroup`/`RevertToBeforeGroup`), leaving the scene exactly as before. Default `atomic=false` preserves backward-compatible non-transactional behavior; the param is token-neutral and NOT sent over wire when false. Nesting handled via `_batchDepth` counter: only the outermost batch (depth=1) opens and reverts the Undo group, ensuring nested batches also roll back under a single outer group. `atomic` parameter overrides `on_error` — when atomic, the batch always stops on first failure (atomic semantics take precedence). Error output adds a new `ATOMIC_ROLLBACK: reverted ops 0..K-1` line when rollback executes, or `op 0 failed, nothing to revert` when the first operation fails. Documented limitation: `execute_code` file-system side effects inside an atomic batch are NOT reverted (only Unity Undo-registered scene mutations roll back). 30 NUnit EditMode tests (MCPBatchAtomicTests) + 8 Python pytest tests green. Files: `server/src/unity_mcp/tools/batch.py`, `unity-plugin/Editor/BatchHelper.cs`, `unity-plugin/Editor/Tests/MCPBatchAtomicTests.cs`, `server/tests/test_batch.py`.

## [v0.12.0] — 2026-06-04 <!-- svg: scoped scene queries — search_scene root+limit + spatial center -->

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

[Unreleased]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.51.0...HEAD
[v1.51.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.48.1...v1.51.0
[v1.48.1]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.48.0...v1.48.1
[v1.48.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.47.1...v1.48.0
[v1.47.1]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.47.0...v1.47.1
[v1.47.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.46.1...v1.47.0
[v1.46.1]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.46.0...v1.46.1
[v1.46.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.45.0...v1.46.0
[v1.45.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.44.0...v1.45.0
[v1.44.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.43.0...v1.44.0
[v1.43.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.42.1...v1.43.0
[v1.42.1]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.42.0...v1.42.1
[v1.42.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.41.0...v1.42.0
[v1.41.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.40.0...v1.41.0
[v1.40.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.39.0...v1.40.0
[v1.39.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.38.0...v1.39.0
[v1.38.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.37.2...v1.38.0
[v1.37.2]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.37.1...v1.37.2
[v1.37.1]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.37.0...v1.37.1
[v1.37.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.36.0...v1.37.0
[v1.36.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.35.1...v1.36.0
[v1.35.1]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.35.0...v1.35.1
[v1.35.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.34.0...v1.35.0
[v1.34.0]: https://github.com/german-krasnikov/unity-biome-mcp/compare/v1.33.0...v1.34.0
