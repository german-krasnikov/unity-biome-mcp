# Test reliability

Unity MCP test results are accepted only from the durable, correlated test-run
protocol. A TCP response, a legacy aggregate count, or the absence of a visible
error is not proof that a test run completed.

The canonical test project and CI worker use Unity 6000.0.65f1 with the
Editor's built-in Unity Test Framework (UTF) 1.6.0. This is the same Unity
minimum declared by the MCP UPM packages; tests do not require a newer Editor
or an overlaid test-framework package.

UTF 1.8 was evaluated and rejected for this project. On Unity 6000.0 the test
framework is an Editor-owned core package: requesting 1.8 does not replace the
loaded built-in 1.6 assembly, while embedding 1.8 source requires later-Editor
internal APIs and a different NUnit extension package. Patching or vendoring
that stack would make the test runner itself a private fork. The canonical
acceptance lane therefore proves the package lock and the actually loaded
assembly are both UTF 1.6.0.

There is no newer-Editor compatibility branch in the Unity C# source tree. Do not add
`#if UNITY_6000_3_OR_NEWER`, `UNITY_6000_4_OR_NEWER`, or similar version gates
to product code, tests, fixtures, or runner infrastructure; the single supported contract is compiled
and exercised on Unity 6000.0.65f1.

Test fixtures use native NUnit/UTF discovery and lifecycle attributes. Common
executable policy belongs in `UnityMcpTestBase` and its scene specializations,
not in `BiomeTest`, `BiomeSetUp`, or `BiomeTearDown` aliases. Biome attributes
are declarative run policy only; `[BiomeWorkerOnly("reason")]` is the supported
example.

When the MCP listener is running, its bound socket endpoint is the source of
truth for the server and chat ports. `MCPSettings.json`, `MCP_Port.json`, and
cached `PortFileManager` values express configuration or fallback state; they
cannot override `TcpListener.LocalEndpoint`. Discovery files must publish the
bound endpoint. This distinction matters after domain reload and in
order-independent suites: a test may reset a cache while the live listener
continues serving on its already-bound port.

The reliability work addressed five independent failure modes rather than
masking them with reload locks:

- UTF's root aggregate can restart at zero or cover only the post-reload tail;
  the immutable expected-leaf manifest and append-only leaf journal are the
  completion authority.
- `SessionState` and a TCP connection are process-domain state, not durable run
  state; run identity, progress, cleanup, and terminal reconciliation are
  persisted below `Library/UnityMCP/TestRuns`.
- A stale `Library/ScriptAssemblies` DLL can make source edits appear active;
  acceptance records the loaded assembly MVIDs and a frozen build fingerprint.
- Shared `EditorWindow.GetWindow` lookup can capture and close a pre-existing
  user window; test windows are distinct objects owned by the common base.
- Socket `Poll` followed by `Available == 0` is racy when the handler can consume
  a readable ping concurrently. Normal client-slot lifetime is therefore owned
  by the handler's completion path, not by an eager liveness probe during add.

Scene save dialogs were a separate ownership failure: tests dirtied or replaced
state they did not own, then relied on fixture-specific teardown that could be
skipped or could compete with another fixture. The common base now owns exact
scene/object/asset rollback and reports cleanup failure instead of saving,
clearing dirty flags, or globally deleting assets.

## Cleanup fault-injection lane

The cleanup ordering probes live under
`unity-plugin/Editor/Tests/FaultInjection`. They are marked both
`[BiomeWorkerOnly("reason")]` (a reason-required NUnit `[Explicit]` policy) and
`[Category("UnityMCP.FaultInjection")]`, so a normal EditMode or PlayMode suite
does not execute them. They must run only in a disposable Unity worker project.
`UnityMcpTestBase` verifies that worker marker before derived setup or the test
body. Worker-only fixtures may not use one-time setup or teardown because those
hooks run before the per-test base guard.

The lane covers three native NUnit/UTF lifecycle failure paths:

| Scenario | Intentional failure | Required continuation |
| --- | --- | --- |
| `sync` | derived synchronous `[TearDown]` throws after dirtying the scene | the next fixture teardown, base isolation hook, registered cleanup, and scene rollback run |
| `async` | derived `Task` `[TearDown]` faults after dirtying the scene | the same cleanup chain runs after UTF observes the faulted task |
| `setup` | derived `[SetUp]` throws after registering ownership and dirtying the scene | the test body is skipped and all applicable teardown/ownership cleanup still runs |

Each fault writes an exact line-ordered sentinel below
`Library/UnityMCP/FaultInjection`. A separate exact-filter canary checks that
sentinel and requires one loaded, clean, empty scene. A stale sentinel cannot
produce a false pass because the fault probe truncates its scenario file before
recording the first step.

### Run the lane

Prerequisites:

1. Create a disposable copy of the Unity test project outside this repository.
2. Open it with the pinned Unity Editor and let it finish compiling.
3. Confirm `Packages/manifest.json` requests
   `com.unity.test-framework` `1.6.0`, and `Packages/packages-lock.json`
   resolves it as built-in `1.6.0`.
4. Confirm that worker's Unity MCP port file is present under
   `~/.unity-biome-mcp/ports`.

Run all fault/canary pairs:

```bash
python3 scripts/run_unity_fault_injection.py \
  --project /private/tmp/unity-mcp-worker \
  --confirm-disposable-worker
```

Run one pair while diagnosing a failure:

```bash
python3 scripts/run_unity_fault_injection.py \
  --project /private/tmp/unity-mcp-worker \
  --scenario sync \
  --confirm-disposable-worker
```

The script refuses projects inside the source checkout, verifies the discovery
file, asks the connected Editor for its canonical project path before dispatch,
and rechecks the path in every durable run snapshot. Do not bypass this guard or
run the explicit probes from the interactive user project.

### Acceptance contract

For every intentional fault, the runner requires:

- an exact `request_id` resolved to one durable `run_id`;
- terminal `outcome=failed`, exactly one expected leaf, and exactly one failed
  leaf containing the scenario's known failure marker;
- no run-level error issue, invalid outcome, missing leaf, conflict, or
  incomplete cleanup;
- complete manifest, `RunStarted`, `RunFinished`, and `RunFinalized` evidence;
- a coherent build fingerprint and actually loaded UTF version `1.6.0`.

For every canary, it requires the same durable evidence with
`outcome=passed` and exactly one passed leaf. The canary itself verifies the
sentinel order and clean-scene invariants. Therefore a teardown assertion that
escapes as UTF `RunError`, a skipped base cleanup, a stale result from another
run, or a dirty-scene leak fails the lane.

If the initial dispatch response is lost, the runner polls
`resolve_test_request` with the original request identity. A resolved
`state=prepared` record means the durable intent exists but UTF was not
dispatched; the runner may continue that exact intent by calling `run_tests`
with the same immutable request payload and must require the same `run_id`.
Once the state is `dispatched` or later, it never calls `run_tests` again and
never invents a successful start acknowledgment.

## Direct EditMode bootstrap ordering

The built-in UTF 1.6 source establishes this order in
`UnityEditor.TestRunner/TestRun/TaskList.cs`:

1. `StoreSceneSetupTask` records UTF's scene setup.
2. `CreateBootstrapSceneTask` replaces it with an untitled
   `NewSceneSetup.DefaultGameObjects` scene for EditMode.
3. `RunStartedInvocationEvent` calls registered test-run callbacks only after
   that replacement.

Consequently, a `RunStarted` callback cannot inspect the original named scene.
The MCP path prepares its durable scene transaction before `Execute()`. Direct
Unity UI/CLI runs may bridge the UTF bootstrap only in a disposable worker that
contains `Library/UnityMCP/disposable-worker.json`. The marker must match the
running Unity 6000.0 editor, exact project revision, actual UTF 1.6.0 assembly,
and a canonical named bootstrap asset. The current untitled scene must also be
clean and either empty or an exact Unity `DefaultGameObjects` fingerprint. The
2D fingerprint is `Main Camera` with only `Transform`, `Camera`, and
`AudioListener`; the 3D fingerprint additionally requires the exact default
`Directional Light` root.

Missing, stale, malformed, or mismatched evidence is rejected before opening a
scene. An unmarked interactive project never receives this worker-only
permission, even if its untitled scene happens to resemble Unity's default.

## Test authoring and ownership

Every Unity test fixture inherits `UnityMcpTestBase` or an approved scene
specialization. The base starts ownership tracking before derived setup and
performs the final scene and asset rollback after derived teardown. A fixture
does not need its own global scene reset.

Register local state immediately after acquiring or changing it:

```csharp
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Testing;

public sealed class InventoryTests : UnityMcpTestBase
{
    [Test]
    public async Task RefreshAsync_UpdatesRows()
    {
        var root = TrackOwnedObject(new GameObject("InventoryTestRoot"));
        RegisterCleanup(InventoryRegistry.ResetForTests);

        await RefreshInventoryAsync(root);
        Assert.That(root.transform.childCount, Is.GreaterThan(0));
    }
}
```

Use `TrackOwnedScene`, `CreateOwnedAdditiveScene`, or `CreateOwnedPreviewScene`
for scenes and `TrackOwnedAsset` for each exact path below `Assets/TestsTemp`. Use
`RegisterCleanup(Action)` for fixture-local synchronous state. An existing local
`[TearDown]` may restore narrow non-scene state, but it must not create or close
scenes, clear Undo, refresh the AssetDatabase, catch cleanup failures, or call
the base lifecycle manually.

Pre-existing Unity and SceneView preview scenes are external Editor state. The
runner and fixtures exclude them from the ordinary-scene transaction. The base
snapshots `previewSceneCount` before each test. A fixture creates and registers
its own preview atomically through the common factory:

```csharp
var preview = CreateOwnedPreviewScene();
```

`UnityMcpTestBase` retains the exact `Scene` value and closes it during final
cleanup. Direct `NewPreviewScene()` calls in test source are forbidden. Unity
6000.0 exposes a preview count but no public API for enumerating a lost preview
handle. The durable run baseline is checked before every test body, so a preview
leaked across domain reload fail-stops the next test instead of becoming its new
baseline. Per-test and run-level count mismatches are isolation failures, and
unknown ambient previews are never closed heuristically.

EditorWindow tests create their subject with `CreateOwnedEditorWindow<T>()`.
In particular, MCP Chat tests never call `GetWindow<MCPChatWindow>()` or the
production `MCPChatWindow.ShowWindow()`: either lookup can return a window that
predates the test. Headless tests initialize the owned UI tree directly;
interactive screenshot tests may show that owned instance. Fixture teardown
does not call `Close()` because the common base destroys all owned instances
after derived teardown, including failure paths.

`SceneCleanTestBase` is an approved legacy specialization whose internal
`[TearDown]` performs leak assertions through the managed scene transaction.
That implementation is part of the shared framework contract; ordinary
fixtures and new specializations must not copy it. New reusable cleanup belongs
in the protected base hooks.

Editor preferences are owned by the same transaction. Tests use typed
`SetEditorPref*` and `DeleteEditorPref*` helpers, or call the matching
`ProtectEditorPref*` helper before exercising production code that writes the
key. The first ownership snapshots both `EditorPrefs.HasKey` and the exact typed
value; base teardown restores that snapshot even after another cleanup fails.
Direct `EditorPrefs.Set*` / `DeleteKey` calls and `EditorPrefs.DeleteAll` are
rejected by source and reflection guards.

`UnityMcpTestBase` also snapshots `SyncHelper.Ops` before each test and restores
that exact instance during final cleanup. A fixture installs a sync double only
through `SyncHelper.OverrideOpsForTest(mock)`. It must not retain the previous
value or restore the seam in a hand-written `[TearDown]`; doing so creates
order-dependent state when setup or teardown fails. The base repairs and reports
a leaked double before the following test instead of silently accepting it.

The same base transaction owns `ReloadGuard` operations, lock balance and state
path; `LogAssert.ignoreFailingMessages`; the domain stamp; `CommandRegistry`;
settings, toolbar, panel, chip and plugin registries; the chat connection event;
`UpdateChecker`; `RelaySpawner`; and `RelaySpawnState`. Tests exercise those
systems through their test APIs and must not add fixture-local global
`Clear`/`Reset` snapshots or teardown restoration. The base restores the exact
prior state and reports a leaked seam.

Persistence tests follow the same ownership rule. `PortFileManager` tests pass
temporary paths to path-injected cores such as `SaveRuntimePortsCore` and the
directory overloads used for discovery/state cleanup. They never write the live
project cache, the user's discovery directory, or reset production static port
state. A persistence test proves file semantics in its own temporary storage;
it does not reconfigure the running MCP server.

New and modified asynchronous tests are `[Test] async Task`. `[UnityTest]`,
`[UnitySetUp]`, `[UnityTearDown]`, every `IEnumerator` test or helper method,
`async void`, `Thread.Sleep`, `.Wait()`, `.Result`, and unobserved tasks are
rejected by repository source guards. `AssetDatabase.Refresh()` is likewise
forbidden anywhere in test source, including bodies and helpers.
UTF 1.6 EditMode UI tests use the common-base `WaitForEditorUpdatesAsync`; they
must not await `Awaitable.NextFrameAsync`, which depends on the runtime player
loop. PlayMode runtime frame boundaries use the matching Unity `Awaitable` API.
External I/O uses bounded, cancellation-aware Task waits.
NUnit `Assert.ThrowsAsync` and `Assert.DoesNotThrowAsync` are also forbidden:
under UTF 1.6 they can synchronously hold the Editor main thread while an
awaited continuation is trying to return to it. Use ordinary
`try`/`await`/`catch`, then assert the captured exception.

Tests that start or stop a process-global MCP/reload listener are destructive
worker tests and must carry `[BiomeWorkerOnly("reason")]`. An ordinary active-
Editor suite may inspect listener state through injected seams, but it never
restarts the live transport.

The complete developer and reviewer contract is distributed with the plugin at
`ClientSkills/skills/unity-testing-verification/references/test-authoring.md`.

Python live tests cannot inherit the C# base. Their autouse `unity_state_owner`
fixture applies the symmetric rule: declare exact scene and asset ownership,
capture the baseline before the test, restore in `finally`, and fail the test run
when verification or cleanup fails. Prefix matching and session-wide orphan
sweeps are not ownership.

## Durable run operations

Create a worker from a fresh source snapshot. The command refuses to merge into
or delete an existing destination:

```bash
python3 scripts/create_unity_test_worker.py \
  --destination /private/tmp/unity-mcp-worker \
  --launch
```

After compilation and port discovery, run an exact filter or the full EditMode
suite through the correlated runner:

```bash
python3 run_unity_tests.py EditMode \
  --project /private/tmp/unity-mcp-worker \
  --filter UnityMCP.Editor.Tests.TestRunProtocolTests \
  --timeout 1800 \
  --json

python3 run_unity_tests.py EditMode \
  --project /private/tmp/unity-mcp-worker \
  --timeout 1800 \
  --json
```

The normal start acknowledgement contains `request_id`, `run_id`, `utf_guid`,
and `state=dispatched`. Store all three identities. If the TCP acknowledgement
is lost, resolve the same `request_id`. A correlated `state=prepared` intent is
continued once with the same mode, filter, request ID and already assigned run
ID; this is recovery of one logical run, not a second run. For `dispatched` or
later states, never call `run_tests` again. Poll only
`get_test_run(run_id)` for completion. A caller timeout reports a nonterminal
snapshot and never clears or changes the Unity lifecycle.

`ok=false` with a positive numeric `retry` value, for example `Server
initializing. Retry in 2s`, is a structured transient state. Before dispatch,
the client may wait and retry the same request. Once `run_id` is known, it must
re-resolve the original `request_id`, require the same `run_id`, and continue
observing that run; it must never call `run_tests` again. Ordinary `ok=false`
responses remain fatal. Reconnection chooses the newest advertised endpoint
whose canonical project path matches the requested worker, verifies that path
again on every endpoint, and uses an explicitly supplied old port only as a
fallback.

The durable evidence for each run is below:

```text
Library/UnityMCP/TestRuns/runs/<run-id>/
  run.json
  environment.json
  expected-tests.jsonl
  events.jsonl
  utf-results.xml
  summary.json
```

`summary.json` is accepted only when the expected manifest is sealed, every
expected leaf has one reconciled terminal outcome, `RunFinished` and cleanup
were observed, and no missing, unexpected, conflicting, corrupt, or
infrastructure evidence remains. A post-reload UTF root aggregate may be partial;
the immutable manifest and leaf journal remain authoritative.

### Domain reload acceptance

Reload acceptance runs only in a disposable worker. Its callback requests a
reload only after the exact boundary leaf has reported `Passed` and the control
record proves the expected scenario identity, ordinal, and phase. A failed,
skipped, duplicated, stale, or out-of-phase callback cannot advance the
scenario. After the final boundary, the harness archives its control record so
stale control cannot trigger a later run.

Acceptance requires one immutable `request_id` and `run_id` across every port
and observer generation, the exact expected leaf manifest, one passed terminal
attempt per leaf, the exact number of reload events, reconciled terminal
outcome `passed`, complete cleanup/finalization evidence, and an archived
control record. A TCP reconnect, partial post-reload UTF root aggregate, or
port change never relaxes those conditions.

Install the worker-only fixture while the disposable worker is stopped:

```bash
python3 scripts/run_unity_domain_reload_acceptance.py \
  --project /private/tmp/unity-mcp-worker \
  --prepare-only \
  --confirm-disposable-worker
```

Launch that worker, wait for compilation and port discovery, then run both the
one- and two-reload scenarios:

```bash
python3 scripts/run_unity_domain_reload_acceptance.py \
  --project /private/tmp/unity-mcp-worker \
  --scenario all \
  --confirm-disposable-worker
```

### Python live lanes

The default live suite is deterministic with respect to external paid services:

```bash
cd server
UNITY_MCP_HOST=127.0.0.1 UNITY_MCP_PORT=<worker-port> \
UNITY_MCP_PROJECT_PATH=/absolute/path/to/disposable-worker \
  uv run pytest tests/live -q
```

Tests marked `live_cli` are collected but skipped unless explicitly enabled.
They call installed external CLIs/APIs, may consume paid quota, and are not part
of the default stability verdict. Run that lane separately with valid credentials:

```bash
cd server
UNITY_MCP_RUN_LIVE_CLI=1 \
UNITY_MCP_HOST=127.0.0.1 UNITY_MCP_PORT=<worker-port> \
  uv run pytest tests/live -m live_cli -q
```

An external authentication, quota, or billing failure is reported as an
external-lane failure; it must not be reclassified as a durable runner or scene
isolation failure.

No other live test may skip because a Unity object, UI element, PlayMode
transition, reconnect, fixture, or local tool is unavailable. Required coverage
fails closed. Recovery is accepted only after a project-pinned bridge handshake
and successful command; an open TCP socket by itself is not evidence that the
intended worker recovered.

## Metrics and diagnosis

Every terminal snapshot retains project/editor identity, Unity and loaded UTF
versions, source/build/MVID fingerprint, request/run/UTF identities, mode and
filter, lifecycle and health, timestamps, duration, callback generation, and
exact leaf attempts. It also reports expected, started, finished, terminal,
passed, failed, skipped, inconclusive, cancelled, invalid, missing, unexpected,
and conflicting counts.

Lifecycle and health answer different questions. A run can remain `running`
while health is `reloading` or `editor_unresponsive`; transport loss does not
mean completion. Diagnose in this order:

1. Resolve the original `request_id` and obtain its exact `run_id`.
2. Read the current snapshot and last durable events for that run.
3. Confirm the worker PID, advertised project path, loaded UTF `1.6.0`, and
   immutable build fingerprint.
4. Distinguish ordinary leaf failure from missing evidence, UTF `OnError`,
   cleanup failure, source drift, and transport-only uncertainty.
5. Continue polling, cancel the exact `run_id`, or destroy the disposable worker.

Do not repair an ordinary run by deleting `Library/ScriptAssemblies`, Bee cache,
or result files. Do not clear SessionState, unlock reload assemblies, save an
unknown dirty scene, or accept a legacy `port-*.txt` aggregate. These operations
erase evidence or can make a mixed build appear valid.

If a test modifies scene state outside its ownership root, the run fails and a
recovery copy is retained below `Assets/UnityMCPRecovery/TestRuns/<run-id>` before
the explicit rollback. If durable evidence is torn or corrupt, it is preserved
and the run becomes incomplete. Rebuild a fresh worker after source/package
change or compile failure; never certify a run whose frozen fingerprint changed.

## Sequential acceptance lanes

For ordinary task-level acceptance, freeze executable files and run one gate at
a time against the already-open canonical `unity-test-project`: Python unit,
one complete C# EditMode run through `run_unity_tests.py`, then Python live.
This path must not launch another Editor window.

Formal release qualification adds a disposable-worker lane. Run it only when
release or destructive fault/reload evidence is explicitly required, in this
strict order with no executable edits or parallel test process:

1. `server/.venv/bin/python -m pytest tests scripts/tests -q`.
2. `server/.venv/bin/python -m pytest install/tests -q`.
3. From `server`, `uv run pytest tests -m 'not live' -q`.
4. The complete C# EditMode suite through `run_unity_tests.py`, twice back-to-back
   against the same worker, then the cleanup fault-injection and
   one/two-reload acceptance lanes.
5. Rediscover and verify the worker's final advertised port.
6. From `server`, run the default live suite with
   `UNITY_MCP_HOST=127.0.0.1 UNITY_MCP_PORT=<final-port>
   UNITY_MCP_PROJECT_PATH=<disposable-worker> uv run pytest tests/live -q`.

Each test must establish and restore its own baseline, so other sequential
orders are supported; the order above is the release evidence convention. Do
not infer that guarantee from an isolated focused pass. The final record must
contain the exact commands, counts, request/run IDs, port history, duration,
and explicit paid-lane skips.

The disposable worker exists for explicitly destructive fault and domain-reload
lanes, not for routine focused or full-suite execution.

## GitHub Actions CI

Continuous integration runs EditMode tests on GitHub Actions across three
platforms (Linux, macOS, Windows) in parallel. The workflow fires on push to
feature branches (`feat/**`, `feature/**`, `fix/**`) and master when paths include
`unity-plugin/**`, `unity-test-project/**`, or the workflow file itself; it also
runs on `workflow_dispatch` (manual trigger) and pull requests to master.

### Workflow behavior

Each platform matrix job:

1. Checks out the repository.
2. Caches UPM packages (`packages-lock.json`-keyed) to speed up Editor setup.
3. Runs Unity `6000.0.65f1` in headless mode (`-batchmode -nographics`).
4. Executes the full EditMode test suite (`-runTests -testPlatform EditMode`).
5. Captures NUnit results to `artifacts/editmode-results.xml`.
6. Reports results via `dorny/test-reporter@v1` (NUnit format).
7. Publishes a job summary with pass/fail/skip table per platform.
8. Uploads test results and (on failure) the Unity Editor log as artifacts.

Parallel execution across Linux, macOS, and Windows serves two purposes: earlier
feedback on platform-specific issues and faster overall CI duration. The build
target per platform (`StandaloneLinux64`, `StandaloneOSX`, `StandaloneWindows64`)
ensures any platform-dependent compilation errors surface immediately.

### Test attribute: RequiresGraphicsDevice

Tests that depend on GPU functionality are marked with
`[RequiresGraphicsDevice]`. This NUnit custom attribute implements
`IApplyToTest` and checks `SystemInfo.graphicsDeviceType`:

- If graphics device type is `Null` (headless mode), the test is skipped with
  reason "Requires graphics device (skipped in headless mode)".
- If a graphics device is available, the test runs normally.

This allows the same test suite to run in both interactive Editor (with GPU) and
headless CI (no GPU) without duplicating test code.

### Test attribute: SkipOnWindows

Tests with known platform-specific failures on Windows are marked with
`[SkipOnWindows("reason")]`. This NUnit custom attribute checks
`Application.platform == RuntimePlatform.WindowsEditor`:

- If running on Windows, the test is skipped with the provided reason.
- On macOS and Linux, the test runs normally.

Use this attribute for tests with path-separator issues, shell differences, or
subprocess behavior that differs on Windows and require a separate fix. The
reason is customizable; default is "Known Windows platform incompatibility —
fix tracked separately".

### CI results and reporting

Job summaries are generated in Python and appended to the GitHub job summary
markdown. Each summary includes:

```
## ✅ EditMode Tests — Linux
| Passed | Failed | Skipped | Total |
|:------:|:------:|:-------:|:-----:|
| **1234** | **0** | **18** | **1252** |
```

Failed tests trigger artifact uploads for the full NUnit XML (`editmode-results.xml`)
and the Editor log (`unity.log`) for diagnosis.

NUnit test results are parsed and reported as check runs via the standard GitHub
Checks API. Failed assertions appear as annotations on the commit.

### Diagram: CI matrix

```
feat/* push
    ↓
[CI Triggered]
    ↓
├─ Linux (ubuntu-latest)      ─→ UPM cache → Setup → Tests (EditMode)
├─ macOS (macos-latest)       ─→ UPM cache → Setup → Tests (EditMode)
└─ Windows (windows-2022)     ─→ UPM cache → Setup → Tests (EditMode)
    ↓
All pass → ✅ checks pass
Any fail → ❌ check fail + artifacts uploaded
```

Historical release evidence, superseded by later executable test-runner changes:
**PASSED on 2026-08-03** against disposable worker
`/private/tmp/unity-mcp-worker-final4-20260802`, Unity `6000.0.65f1`, built-in
UTF `1.6.0`, and frozen executable snapshot
`ae78d331a91ee2cfac2e015d575c61ffdbb9f8bf79abe06f1a1a6d07f9cd7145`
(1619 files).

- Python unit, strictly sequential: repository `288 passed` in 42.55 s;
  installer `75 passed, 1 skipped` in 1.04 s (the platform-specific `pwsh`
  check); server non-live `5045 passed, 287 deselected` in 216.26 s.
- Full C# EditMode run 1: request
  `standalone-9d870d938a3c4c4e971a44bb4c69f779`, run
  `run-3ed238ce8e1e4284ac4fc7de0c7a2b5d`, UTF
  `18715ae2-eae7-4f45-a11e-61315563e359`; 7136/7136 terminal,
  7118 passed, 18 explicit worker-only skips, zero failed, inconclusive,
  missing, unexpected, or conflicting, cleanup complete, 102.75 s.
- Full C# EditMode run 2: request
  `standalone-4eb85d23039e409d8d9406db09a099fd`, run
  `run-e9ca6070951e432498f30894e4d1fdbd`, UTF
  `3668914c-3b90-486c-8979-0bae8c2655a4`; identical 7136/7136 and
  7118/18 counts, all error counters zero, cleanup complete, 105.93 s.
  Both runs used build fingerprint
  `sha256=4c9d4382931456851ed15c57df7c124c736de87c8dee0cb0d04d414246be338f;assemblies=24;utf=1.6.0`.
- Fault acceptance: sync, async, and setup fault runs each produced exactly one
  attributed failed leaf without `RunError`; their three immediately following
  canaries passed and verified sentinel and scene cleanup. Run IDs were
  `run-a4dc8085c49a44f8ad23783e3423bb0a` / `run-fa0bed87286f4d3585f5634c37264389`,
  `run-e6759000591e4dd9b14283ccab9cd28c` / `run-feb72d63b9c34a029f07d33addd8c9df`,
  and `run-bb01f81f7de64ee9a20c0da755dfcbfa` /
  `run-e3abf8cc42fc48deb9de1b8a4ad72a43`.
- Reload acceptance: request `domain-reload-1-216f866c90a44bc8b89d544ac92a6e44`
  preserved run `run-f0815bbd682d4c03a0e42bca67aeb7d2` across
  `57131 -> 63226`; request `domain-reload-2-b0f6558ddb8a409fbf52a0c419c8dab9`
  preserved run `run-afd7253be5574884a5a03c666f0aa49b` across
  `63226 -> 63241 -> 63243`. Each reconciled exactly 3/3 leaves with the
  expected two or three observer generations.
- Default Python live, strictly last: `278 passed, 9 skipped` in 636.70 s.
  All 287 collected nodes were accounted for; the nine skips are exactly the
  five backend and four visual-diff tests marked paid `live_cli`.
- Final recovery: worker port `57131` was rediscovered and project-verified;
  Unity was in EditMode with only `Assets/Scenes/GridTest.unity`, no dirty
  scene, no `MCPChatWindow`, no run-owned live asset below the empty parent
  directory, no nonterminal durable run, and no compile error. The executable
  snapshot and source/worker package hashes remained unchanged through all gates.
