# Repository Test Policy

This document is the canonical test-authoring policy for Unity Biome MCP.
It applies to this repository and its disposable workers. It is not installed
into consumer projects with `ClientSkills`.

The canonical Unity test project uses Unity `6000.0.83f1` and the Editor's
built-in Unity Test Framework `1.6.0`. Product code, fixtures, and runners target
the Unity `6000.0` contract; do not add newer-Unity compatibility branches.

## Three Test Carriers (CI & local)

Tests live in **three** locations; different CI lanes exercise each:

| Location | What | CI Lane | Mode | When to update |
|----------|------|---------|------|----------------|
| `unity-plugin/Editor/Tests/` | Plugin unit tests (shipped in UPM) | pr-unity-core, master-conformance | EditMode, PlayMode | Always — primary location |
| `unity-test-project/Assets/Tests/Editor/` | EditMode corpus and integration | pr-unity-core | EditMode | When plugin API or contract changes |
| `unity-test-project/Assets/Tests/PlayMode/` | PlayMode corpus (E06a, E06b) | unity-tests.yml PlayMode job | PlayMode | When runtime DSL/MCP protocol changes |

**Coverage (Wave E):**
- **EditMode:** 9 fixture files (F, I1, I2, I3, A/B/C chain, INVOKE args, L long, MOVEMENT profiles) via `PlaytestCorpusEditModeTests`
- **PlayMode:** 3 Play-bound files (C_shared_finish coroutines, DSL_types, I3_independent_pass) via `PlaytestCorpusPlayModeTests` (E06b, `-testPlatform PlayMode --filter PlaytestCorpusPlayModeTests`)
- **Player (fan-out):** 6 `.playtest` files in `StreamingAssets/Playtests/` via `scripts/run_player_playtests.py` (`--jobs N`, `@needs player` tag filter) — runs Player builds on Linux, macOS, Windows
- **Python .suite lane:** Stress-tests EditMode + PlayMode carriers with stateful A→B→C chain via `run_playtest_suite(tag="@suite-only")` (tests/live/test_playtest_suite_corpus.py)

**Total honest CI coverage: 18/22 files** (9 EditMode + 3 PlayMode + 6 Player).

When changing RefManager, ComponentSerializer, ValueParser, or any public API:
grep for stale references in BOTH C# test projects before committing. Player
tests verify parity via the `Compare()` core contract.

## Version-Agnostic Tests (no `#if` in test code)

Tests must never use `#if UNITY_*` preprocessor guards. A guarded test means
zero coverage on the excluded version — the opposite of what CI compat matrices
exist for.

When production code branches on Unity version (`#if UNITY_6000_4_OR_NEWER`),
test the **behavioral contract** that holds on ALL versions, not
implementation details that differ.

```csharp
// WRONG — tests a numeric range that is version-specific
[Test]
public void RawId_FitsIn32Bits()
{
    Assert.LessOrEqual(ObjectIdCompat.GetRawId(go), uint.MaxValue); // fails on 6.4+
}

// WRONG — #if guard hides the test on 6.4+, zero coverage there
#if !UNITY_6000_4_OR_NEWER
[Test]
public void RawId_FitsIn32Bits() { ... }
#endif

// RIGHT — behavioral invariant that holds on every Unity version
[Test]
public void ObjectId_RoundTrip_ResolvesToSameObject()
{
    var go = TrackOwnedObject(new GameObject("RoundTrip"));
    var rawId = ObjectIdCompat.GetRawId(go);
    Assert.That(ObjectIdCompat.ResolveObject(rawId), Is.SameAs(go));
}
```

Rule: test the contract (round-trip, uniqueness, null safety), not the
encoding (bit width, hex length, numeric range). If a test assertion would
fail on a different Unity version, it is testing an implementation detail.

## C# Fixtures

Every Unity fixture inherits the narrowest supported base:

| Base | Use |
|---|---|
| `UnityMcpTestBase` | Logic or explicitly owned non-scene Unity state |
| `SceneTestBase` | Tests that open, create, or mutate a scene |
| `SceneCleanTestBase` | Scene tests that also detect leaked root objects |
| `MultiSceneTestBase` | Additive and multi-scene behavior |

**MCPFeedbackFixture** (`unity-test-project/Assets/MCPFeedbackFixture/`): Real conformance test
fixture with C# components (FixtureState, FixtureMover, FixtureReceiver, FixtureId, FixtureAsync),
12 DSL playtest files (9 EditMode-capable, 3 Play-bound-only), shared definitions, and suite configurations. The fixture scene (`McpFeedbackFixture.unity`) loads into both EditMode and PlayMode test carriers to verify protocol contracts end-to-end:

- **EditMode carrier** (`PlaytestCorpusEditModeTests.cs`): Runs 9 Edit-capable files (F, I1, I2, I3 independent pass tests; A/B/C shared-state chain; INVOKE args; L long pass; MOVEMENT profiles) via `PlaytestRunner.Run(..., requiresPlayMode: false)` — tests DSL execution, EditMode MCP dispatch, and shared state persistence within a session
- **PlayMode carrier** (`PlaytestCorpusPlayModeTests.cs`): Runs 3 Play-bound-only files (C_shared_finish with coroutine callbacks, DSL_types, I3_independent_pass) via `PlaytestRunner.Run(..., requiresPlayMode: true)` — tests runtime coroutines, Play-only MonoBehaviour tick, and DSL type assertions
- **Python .suite lane** (`tests/live/test_playtest_suite.py`): Stress-tests both carriers via `run_playtest_suite()` with `--tag @suite-only` filter on A/B/C (stateful chain), verifying restart semantics and cumulative state across suite runs

Use this fixture as the MCP + DSL protocol compliance baseline.

Use native NUnit/UTF attributes such as `[TestFixture]`, `[Test]`, `[SetUp]`,
and `[TearDown]`. Do not introduce aliases for discovery or lifecycle.

Register ownership immediately after acquiring a resource:

- `RegisterCleanup(Action)` for exact synchronous restoration;
- `TrackOwnedObject<T>(T)` for `UnityEngine.Object` instances;
- `CreateOwnedEditorWindow<T>()` for a window that cannot alias a user's window;
- `TrackOwnedScene(Scene)` for a scene opened by the test;
- `CreateOwnedPreviewScene()` for a fixture-owned preview scene;
- `TrackOwnedAsset(string)` for an exact path below `Assets/TestsTemp`;
- typed `SetEditorPref*` and `DeleteEditorPref*` helpers for direct preference
  changes, and `ProtectEditorPref*` before invoking a production writer.

The common base owns final cleanup. Do not duplicate its scene reset, registry,
reload, relay, update-check, domain-stamp, log-policy, or `SyncHelper.Ops`
restoration in fixture teardown. Install a sync double with
`SyncHelper.OverrideOpsForTest`. Use `OnBeforeIsolationCleanup()` only when
ownership registration cannot represent a fixture-specific cleanup action.
Cleanup errors fail the test.

Pre-existing scenes, assets, Editor windows, and preview scenes are not owned by
the runner. Never save user scenes, clear their dirty state, close ambient
windows or previews, call `Undo.ClearAll()`, or delete unregistered assets.
Editor-window tests use `CreateOwnedEditorWindow<T>()`, not `GetWindow<T>()`,
`GetWindowWithRect<T>()`, or a production `ShowWindow()` entry point.

## Asynchronous Tests

Write new asynchronous tests as `[Test] async Task` and await every task before
the fixture ends. All waits must be bounded and cancellation-aware.

Do not use:

- `[UnityTest]`, `[UnitySetUp]`, or `[UnityTearDown]`;
- `IEnumerator` test or helper methods;
- `async void` or fire-and-forget work;
- `Thread.Sleep`, `.Wait()`, or `.Result`;
- `Assert.ThrowsAsync` or `Assert.DoesNotThrowAsync` under UTF 1.6;
- `AssetDatabase.Refresh()` in test source.

Use `WaitForEditorUpdatesAsync` for bounded EditMode Editor ticks. In PlayMode,
an `async Task` may await the matching Unity `Awaitable` API for runtime frames.
`Task.Delay` is only for bounded wall-clock backoff with cancellation; it is not
frame synchronization.

## Disposable Worker Boundary

Use a disposable worker for tests that may reload or restart Unity, recompile,
write source or assembly definitions, mutate packages or project settings,
refresh imported source, manipulate process-global UTF callbacks, or
intentionally crash or hang a subsystem.

Mark destructive C# tests with a reason-bearing
`[BiomeWorkerOnly("specific reason")]`. Do not add one-time setup or teardown to
a worker-only fixture because it could execute before the per-test guard.

Repository persistence tests use injected temporary storage. They do not write
live discovery files, production port caches, or singleton state. While a TCP
listener is running, its bound endpoint is authoritative; configuration and
cached ports are only pre-bind or stopped-listener inputs.

## Durable Unity Runs

Run repository C# tests through the standalone durable runner against the
already-open canonical test project:

```bash
python3 run_unity_tests.py EditMode \
  --project /absolute/path/to/unity-test-project \
  --filter UnityMCP.Editor.Tests.ExampleTests \
  --timeout 1800 \
  --json
```

Omit `--filter` for a complete suite. Do not open a second ordinary Editor.
Only explicit fault and reload lanes create a disposable project copy.

Every run is identified by `request_id`, `run_id`, and `utf_guid`. A disconnect,
caller timeout, partial aggregate, or uncorrelated latest result is not a
verdict. Only a reconciled terminal snapshot for the exact run is evidence.

**Test Run Durability (MCP-TRANS-008, MCP-SUITE-006):** TestRunHandle + TestRunRegistry
provide in-memory metadata persistence so run state survives transport disconnect and
caller timeout. The bridge CommandLedger tracks op_id → delivery state for command-level
idempotency. SuiteVerdict separates inner (per-file assertion) verdicts from outer
(lifecycle/transport) verdicts so cleanup failures do not mask passing test results.

The low-level `run_tests` protocol is dispatch, not completion. Resolve
`START-UNKNOWN` with the original `request_id`. A correlated `state=prepared`
intent may be continued once with the identical payload and assigned `run_id`.
After dispatch, observe or cancel that run; never create a replacement logical
run. Consumer agents use `run_tests_wait` and do not reproduce this protocol.

## Python Tests

Keep unit tests hermetic and restore module state with pytest fixtures or
`monkeypatch`. Every live-Unity pytest uses the shared per-test
`unity_state_owner` fixture. It records exact owned state, registers restoration
before the test, restores in `finally`, verifies the restored state, and fails
on cleanup errors.

Live tests fail closed when required project state, tools, or transitions are
unavailable. Only the explicitly paid `live_cli` lane may skip because its
external dependency was not enabled. Run that lane only with
`UNITY_MCP_RUN_LIVE_CLI=1`, valid credentials, and an explicit cost and network
expectation.

Use registered Python markers and C# `TestCategories` constants. Important
boundaries are:

- `live`: requires the project-pinned Unity endpoint;
- `live_cli`: paid external CLI or API;
- `monkey` / `Stress`: stress and chaos behavior;
- `conformance`: portable MCP conformance, combined with `live`;
- `cross_project`: two project-pinned Editors, combined with `live`;
- `slow`: Python tests longer than five seconds;
- `RequiresGraphics`, `InteractiveVisual`, and `Perf`: specialized lanes;
- `WorkerOnly` and `FaultInjection`: disposable-worker-only behavior.

Prefer module-level `pytestmark` when every test in a Python module shares the
same requirement. Prefer class-level NUnit categories and category constants.
Do not duplicate a category already applied by an attribute such as
`BiomeWorkerOnly` or `RequiresGraphicsDevice`.

## Test Layers

The repository uses specialized test layers to verify protocol contracts and
conformance invariants:

**Regression guard tests** (`server/tests/test_regression_guards.py` and v1.46+ audit
coverage suite) — Named guards RG-01 through RG-11 prevent specific confirmed bugs from
reoccurring; each guard documents the prior break and its fix. Audit coverage tests
validate protocol lifecycle fences (session identity, command durability, Play Mode
readiness), edge cases (console overflow, zero-match filters, pre-existing dirty state),
and schema prerequisites. All are CI-safe. ~14 new Python modules + 557-line guards suite.

**Seam tests** (`server/tests/seams/`, markers: `live + conformance`) — Live
round-trip conformance tests that verify batch completeness, surface consistency,
differential behavior (batch vs. sequential), and invariants against a running
Unity endpoint. ~113 tests covering core tool contracts.

**Wire tests** (`server/tests/wire/`, marker: `wire`) — Protocol-level CI tests
without a running Unity process. Use `FakeUnityServer`, MITM fault injection, and
cassette playback to validate TCP shape, timeout behavior, command ordering, and
error recovery. ~26 tests; run in `ci-python.yml` without editor dependency.

See `.claude/skills/testing-tdd.md` section "Cross-Boundary Test Layers" for
implementation patterns, fixture usage, and conformance gating details.

## EditMode DSL Execution

The playtest DSL now supports EditMode execution through the `# @needs editmode` header directive:

- **Header parsing** (B05): `PlaytestHeaderScanner.Scan()` detects `# @needs editmode` at parse time. `AsyncRunPlaytest` uses this to opt out of the Play Mode gate
- **ExecutionPolicy** (B06): `PlaytestRunner.Run(..., requiresPlayMode: false)` bypasses Play Mode checks. EditMode steps run through `EditorApplication.update` ticks (not `delayCall`) via a centralized `MainThreadDispatcher`
- **Mutation guard** (B08): EditMode MCP steps are validated at dispatch time; mutations are rejected immediately if called from `execute_code` or other runtime paths
- **Fixture scene** (B21): EditMode carrier opens the MCPFeedbackFixture via `EditorSceneManager.OpenScene(..., Additive)` to resolve loose ASSERT/INVOKE paths against real GameObjects
- **No `fresh` in EditMode** (B05): `run_playtest(fresh=true, script="# @needs editmode")` errors before Run() — Play Mode restart cannot be used in EditMode scripts

## Policy Vector Tests (PR-04)

36 C# policy-vector tests cover command read/write, batch eligibility, retry-safety,
and metadata classification for 5 representative commands (e.g., `set_property`,
`get_component`, `run_playtest`). Each command's registration site declares
`MutatingArgsPolicy` delegate (for argument-aware classification) and `NotBatchable`
bool. Vectors validate parity between:

- Python `WRITE_CMDS` and C# mutating flag
- Batch eligibility per command and argument combinations
- Retry-safety guarantees (e.g., `move_to` is retriable even if mutating)
- Metadata (timeout, category, required/optional params)

These tests live in `unity-plugin/Editor/Tests/CommandRegistryPolicyTests.cs` and
ensure that central lists are no longer needed for new commands; metadata is
purely declarative at registration.

## Plugin Registration Atomicity Tests (F3)

Two suites verify that failed plugin registration leaves zero callable state:

**Python tests** (`server/tests/test_atomic_plugin_registration.py`):
- Snapshots all registries (tools, READ/WRITE_CMDS, dsl_tools, gating, budget)
- Exercises `register()` that throws during command addition
- Validates zero tools, zero gating entries, zero budget features remain

**C# tests** (`unity-plugin/Editor/Tests/PluginRegistryTests.cs`):
- `CaptureForTest()` and `RestoreForTest()` per-plugin
- Duplicate command registration throws when `CallerIsPlugin=true`
- Empty `CommandRegistry.Register` call before plugin cleanup

Invariant: a plugin that crashes during `register()` cannot leave stale callable
state that would execute in subsequent runs.

## DSL Preflight Validation Tests (F1)

Player and Editor tests validate that `PlayerProfilePreflight.Validate()` correctly
rejects unsupported DSL constructs before execution:

**Pure Dotnet tests** (runs in `Unity-MCP.Playtest.Core.Tests.csproj`):
- Detects `# @needs editmode` header
- Detects SETUP/TEARDOWN blocks
- Detects EXPECT_FAIL modifier
- Detects compound WAIT_UNTIL AND/OR
- Detects ASSERT...TIMEOUT retry logic
- Detects empty main section
- Returns detailed error naming offending line

**Player runtime tests** (E09 lane):
- Pre-scan gate halts before Play Mode entry on violation
- Error message is player-readable

## SourcePatch Off/Absence Proof Tests (PR-06)

Two C# contract tests prove SourcePatch module boundary isolation:

**SourcePatchReloadContractTests.cs:**
- Real lazy-reconciliation to Off confirmed via state machine walk
- Concurrent-sync epoch drift resolves to Recovery (not false-Off)
- `ForceUnreconciledForTests()` seam enables controlled test timing

**Python boundary scan** (`scripts/tests/test_source_patch_reload_control_boundary.py`):
- Locks `SyncHelper.TriggerSync` out of SourcePatch control files
- Allows only sanctioned FSR adapter import
- CI enforces: violation = build failure

Invariant: SourcePatch Off leaves normal writes and domain reload unintercepted;
algorithm swaps happen via composition, not core edits.

## Pure Dotnet Lane for Parser Core

The engine-free `UnityMCP.Playtest.Core` assembly (v1.53.0+) can be tested outside Unity using dotnet:

```bash
dotnet test unity-plugin/Tests~/Pure/UnityMCP.Playtest.Core.Tests.csproj -c Release
```

**Why the `~` folder name:** Unity's asset importer automatically skips folders ending with `~`, so this project folder and its compiled artifacts stay invisible to the Editor. This prevents `Microsoft.NET.Test.Sdk` references (which are incompatible with Editor compilation) from breaking the Editor assembly build. The `.csproj` pulls `Runtime/Playtest/Core/*.cs` files directly, avoiding any Unity-specific dependencies.

Use this lane for:
- Parser correctness (DSL tokenization, operator precedence, macro expansion, etc.)
- Numeric utilities (`Float3`, `NumericParsing`)
- `Compare()` parity across platforms

Tests here must not reference Unity or Editor types. Any new utility pulled from Core must have matching coverage in this lane before merging.

## Offline Contract Validation: Freshness, Reload Readiness, and Adapter

Three additional offline NUnit projects validate critical contracts without Editor:

**`unity-plugin/Tests~/AssemblyFreshness/UnityMCP.AssemblyFreshness.Tests.csproj`**
- Proves DLL/PDB bytecode comparison logic in `AssemblySourceFreshness`
- Validates import-before-global-Refresh readiness contract
- No Unity Editor required; pure C# reflection

Run with:
```bash
dotnet test unity-plugin/Tests~/AssemblyFreshness/UnityMCP.AssemblyFreshness.Tests.csproj -c Release
```

**`unity-plugin/Tests~/SourcePatchReadiness/UnityMCP.SourcePatchReadiness.Tests.csproj`**
- Proves state-machine transitions (Off → OnReady → Busy → Recovery)
- Validates ACK-based patch lease lifecycle
- Confirms reload-block-reason propagation across domain boundaries

Run with:
```bash
dotnet test unity-plugin/Tests~/SourcePatchReadiness/UnityMCP.SourcePatchReadiness.Tests.csproj -c Release
```

**`unity-plugin/Tests~/MutationAdapterContract/AdapterContract.Tests.csproj`**
- Validates SourcePatch seam contract and adapter Apply outcomes
- `AdapterApplyOutcomeTests` exercises all Apply result types (Pass, NoChange, Fail, Obsolete)
- `Seam.csproj` compiles the public seam types; seam-drift negative control ensures renamed members break the build
- Adapter sources fetched and pinned by `scripts/gauntlet/fetch_adapter_sources.py` (SHA256 atomic lock)

Run with:
```bash
dotnet test unity-plugin/Tests~/MutationAdapterContract/AdapterContract.Tests.csproj -c Release
```

All three projects use the `~` folder convention to stay invisible to the Editor. They validate implementation-critical runtime invariants that cannot be observed through Editor UI alone. CI gates all three before running live mutation tests.

## Test Taxonomy and Lanes

Test organization is data-driven via two canonical JSON files:

**`Tests/taxonomy-map.json`** (C13): Single source of truth for cross-language test dimensions (pytest markers, C# TestCategories, DSL `@needs` header values). Each dimension maps to its representation in pytest, C#, and DSL. Example dimension entries:
- `live`: pytest marker, Python-only
- `slow`: pytest marker + C# `TestCategories.Slow` constant
- `editmode`: DSL header value `@needs editmode` (PlayMode default, EditMode opt-in)
- `playmode`: DSL header value `@needs playmode` (symmetric to editmode)

**`Tests/biome-test-lanes.json`** (C15): 4 lanes matching real CI jobs (pr-python-core, pr-unity-core, master-conformance, nightly-full). Each lane specifies:
- `filter`: layer/mode/environment/speed/include-tags/exclude-tags/exclude-capabilities selectors
- `source`: exact CI job reference (file path and line number)
- Cross-checked against taxonomy-map.json by `scripts/tests/test_lanes_config.py`

**Enforcement** (C18): `scripts/check_test_metadata.py` is run in CI and locally to validate:
1. Every `[Category(...)]` in C# resolves to a `TestCategories.*` const (or allow-listed wrapper)
2. Every lane filter field references a known taxonomy dimension
3. Every `.playtest` `@needs` value has a matching taxonomy-map dimension

**Python test-lane directory convention:** ~446 root-level `server/tests/test_*.py` files are NOT migrated to per-lane subdirectories (unlike per-lane pytest markers). Each test file carries its own marker set. The lane filter configuration generates pytest `-m` expressions that CI lanes use to select tests at runtime.

## Why No C# .suite Driver

C# EditMode tests cannot enter Play Mode mid-test (would require domain reload), so a suite fixture in C# cannot coordinate stateful PlayMode runs (A→B→C without reset). The Python `.suite` lane (`tests/live/test_playtest_suite.py`) bridges this by calling `run_playtest_suite(pattern="Assets/MCPFeedbackFixture/PlayTests/*.playtest")` with `--tag @suite-only` to select A/B/C (marked `# @suite-only` in their headers). Python's async/await model lets it orchestrate multiple runs with shared state recovery between them.

## Source Patch (Mutation Mode) Qualification

Optional FSR-based body-only source patching uses a dedicated CI qualification matrix
in `.github/workflows/fsr-qualification.yml`. Qualification requires two pass cells
(Unity 6000.0.65f1 on macOS ARM64 and Linux x64); Windows x64 is documented as
INFRASTRUCTURE_BLOCKED (headed-GUI unavailable on GH-hosted runners) and engineering-supported
with CI qualification pending. **The 6000.0.65f1 qualification lock is frozen for v2.0.0;
re-qualification on the current product baseline (6000.0.83f1) is a post-release follow-up.**
U_MAX (6000.5.10f1) is shelved in P2-07 for a reviewed compatibility change with new matrix evidence.

**Test fixtures:** New C# tests use existing `UnityMcpTestBase`, `SceneTestBase`, and
`BiomeWorkerOnly` patterns. Source Patch mutations are forbidden in standard T5
(read-only) test projects; only mutation-specific fixtures with the disposable worker
marker use `mcp_status() → source_patch_state` to verify ON/Recovery transitions and
confirm provider registration.

**CI qualification scope:**
- Install/compile/Roslyn-loader proof per platform
- Mutation Mode intent → ON-ready path verification (zero compile, retained state)
- Logical OFF via intent → exact one domain reload path (receipt-based validation)
- Physical package removal → package-absent clean compile proof
- Post-qualification runs are deterministic focused seams, not full-suite multiplication

Adapter SHA pinning is maintained in `scripts/source_patch_provider_pin.json`; any
binary or dependency change reopens the full matrix. See the CI qualification matrix
in `.github/workflows/fsr-qualification.yml` and `scripts/fsr_qualification_lock.json`
for the locked Unity window, platform attestation, and evidence structure.

## Cross-Runtime Parity Gate (`csharp_parity` marker)

Several Python tests read C# source text directly (regex/substring scan) to
pin a wire-format literal, constant, or timeout value to its C# emitter —
e.g. `test_sync_compile_guard.py::test_sync_compile_guard_text_matches_csharp_compile_branch`,
`test_editor_control_tools.py::test_noop_recovery_result_matches_csharp_constant`,
`test_reload_module_boundary.py`, `test_source_patch_reload_control_boundary.py`,
the C#-parity test in `test_tool_specs.py`, `test_timing_invariants.py`,
the C#-source-scan tests in `test_mvid_tracking.py`, and the C#-scanning tests
in `test_playtest_async.py`. These run in CI, but a developer working only the
C# side and running `run_unity_tests.py EditMode` would not naturally trigger
them, so drift can land unnoticed until the next full Python CI pass. After
any C# edit that changes a wire-format string, guard literal, timeout
constant, or boundary-scanned file, run `uv run pytest -m csharp_parity -q`
plus the scripts C#-scanning tests (`scripts/tests/test_pure_core_asmdef_boundaries.py`,
`scripts/tests/test_unity_test_source_hygiene.py`, `scripts/tests/test_taxonomy_map.py`)
before reporting the C# task green.

## N3 A/B Reload Identity Harness (Local-Only, Deferred CI)

`scripts/run_ab_reload_identity.py` proves cross-worker reload identity
(nonce/MVID/counter, cross-identity rejection, lost-ACK, compile-error
recovery) between two simultaneously running headed Unity instances (Worker
A and Worker B). This lane is local-only for v2.0.0 — deferred from CI per
the ROI panel decision, not an oversight — because it needs a memory-safe
runner: the owning Unity plus two headed disposable workers hit `warn`
memory pressure on a 32 GB machine (measured ~31 GB used during a live run).
GH-hosted runners do not have this headroom alongside the other lanes.

Invocation (both workers already launched and disposable-marked):
```bash
python scripts/run_ab_reload_identity.py \
  --worker-a-dir /private/tmp/biome-ab-a --port-a 9620 \
  --worker-b-dir /private/tmp/biome-ab-b --port-b 9630 \
  --unity /path/to/Unity --mode both \
  --receipt /tmp/ab-reload-receipt.json --confirm-disposable-worker
```
`--port-a`/`--port-b` default to 9620/9630. Run twice in a row for evidence
parity with the other durable lanes. Receipts from the qualifying run live in
`Plans/Reviews/n3-ab-reload-2026-09-08/run{1,2}-receipt.json`, validated by
`scripts/gauntlet/ab_reload_receipt.py::validate_receipt`.

## Documentation and Skill Checks

Run `python scripts/check_skills_freshness.py --strict` after changing bundled
skills or agents. Strict mode exits non-zero for findings classified as errors;
warnings still require human triage. The checker is a heuristic static guard, so
even a clean report does not prove that a workflow remains semantically current.
Review the affected instructions against the live tool and product contracts.

## CI Lanes and Acceptance Order

**Core CI lanes** (data-driven by `Tests/biome-test-lanes.json`):
- `pr-python-core`: Python quick-check (35s via focused markers)
- `pr-unity-core`: C# EditMode + PlayMode corpus on PR branches (pr-gating)
- `master-conformance`: Seams/conformance live suite on master branch
- `nightly-full`: Complete Python live suite + Player fan-out (requires graphics)

**Mutation Regression Lane** (`.github/workflows/mutation-regression.yml`):
- Opt-in, enabled by `UNITY_MCP_RUN_MUTATION_LIVE=1` environment variable
- Python suite (`server/tests/mutation`) with pytest marker `mutation_live`; run via `scripts/run_mutation_regression_cell.py --mode full` (driver, 1800 s budget per job)
- Requires a disposable provider worker: `BIOME_FINAL_PORT_A`, `BIOME_WORKER_A` with FastScriptReload package installed and marked disposable
- Stories S11–S16c: explicit disable (S14), source restore (S15), provider absence (S16), isolation (S16), remove (S16b), re-add (S16c)
- Supersedes legacy `fsr-qualification.yml`; CI-gated by pure dotnet projects (`AssemblyFreshness`, `SourcePatchReadiness`, `MutationAdapterContract`) in `ci-pure-dotnet.yml`

**Player fan-out runner** (v0.81.4+):
```bash
python scripts/run_player_playtests.py \
  --jobs 2 \
  --project /path/to/unity-test-project \
  --timeout 1800 \
  --builds-dir /tmp/player_builds
```
Filters `.playtest` files by `@needs player` tag, builds standalone Player for
each platform (Linux, macOS, Windows), runs in parallel, returns matrix:
```
Player CI: 6/6 passed
  Linux:   3/3 passed
  macOS:   3/3 passed
  Windows: 3/3 passed (skipped on infrastructure)
```

**Acceptance Order (pre-release):**
Freeze executable files before a formal release gate. Run these lanes
sequentially, with no edits or parallel test process:

1. `server/.venv/bin/python -m pytest scripts/tests -q`
2. `server/.venv/bin/python -m pytest install/tests -q`
3. From `server`: `uv run pytest tests -m 'not live' -q`
4. Complete C# EditMode suite twice against one disposable worker (Worker A), followed by
   cleanup fault injection and the domain-reload scenarios.
5. Python `.suite` lane: `uv run pytest tests/live/test_playtest_suite_corpus.py -m "live" --tag @suite-only -q`
6. Player fan-out: `python scripts/run_player_playtests.py --jobs 2 --project ... --timeout 1800`
7. **Mutation regression lane** (separate disposable provider worker): Set `BIOME_FINAL_PORT_A`, `BIOME_WORKER_A` to Worker A's final port and path, then run mutation suite (never concurrent with other Unity lanes; separate provider-equipped worker preferred): `uv run pytest server/tests/mutation -m mutation_live -q` or `python scripts/run_mutation_regression_cell.py --mode full`
8. Rediscover and verify the final worker port (Worker A).
9. From `server`, run project-pinned deterministic `tests/live` with the final
   host, port, and `UNITY_MCP_PROJECT_PATH`.

Retain commands, counts, durations, run identities, port transitions, and paid
lane skips. A focused pass is development evidence, not a release verdict.

## Review Gate

Reject a test change when any applicable answer is no:

- Does every Unity fixture use an approved base and exact ownership?
- Can cleanup complete and report errors after setup, body, or teardown failure?
- Are async lifetimes awaited and synchronization mechanisms appropriate?
- Are destructive operations isolated to a disposable worker?
- Do persistence tests avoid live storage and endpoint state?
- Does live Python state restoration use `unity_state_owner`?
- Does Unity evidence name the exact run and reconciled terminal state?
- Would the test remain independent under arbitrary ordering and repetition?

## References

- [Unity 6 Test Framework manual](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html)
- [UTF 1.6 changelog](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/changelog/CHANGELOG.html)
- [Unity Awaitable](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.html)
