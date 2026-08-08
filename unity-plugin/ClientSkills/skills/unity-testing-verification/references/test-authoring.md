# Test Authoring Contract

This is the canonical test-authoring policy for Unity Biome MCP. Developer,
reviewer, and testing agents must apply it to every new or modified test.

The repository's canonical test project and CI worker use Unity 6000.0.65f1
with the Editor's built-in Unity Test Framework (UTF) `1.6.0`. This is also the
MCP UPM packages' Unity `6000.0` minimum. Test code must not require a newer
Editor, an embedded test-framework fork, or a second compatibility lane.

UTF 1.8 was evaluated and rejected for this project. Unity 6000.0 owns UTF as a
core package: a manifest request does not replace the actually loaded built-in
1.6 assembly, and embedding the 1.8 sources requires later-Editor internal APIs
and a different NUnit extension package. Maintaining patches for those
dependencies would make test infrastructure less reliable than the product it
verifies. Do not copy UTF 1.8-only or experimental UTF 2.0 recipes into tests.

## Non-Negotiable Rules

1. Every C# fixture executed by Unity inherits `UnityMcpTestBase` or one of its
   approved specializations: `SceneTestBase`, `SceneCleanTestBase`, or
   `MultiSceneTestBase`.
2. New asynchronous tests use `[Test] async Task`. Coroutine-based polling,
   delays, network waits, and ordinary asynchronous work must be converted to
   `Task`-based code.
3. `[UnityTest]`, `[UnitySetUp]`, and `[UnityTearDown]` are forbidden without
   exception. No test-source method, including a helper, may return
   `IEnumerator`. UTF 1.6 EditMode UI waits use the common-base
   `WaitForEditorUpdatesAsync`, never `Awaitable.NextFrameAsync`. PlayMode
   runtime frame-sensitive work may await the matching Unity `Awaitable` API
   from an `async Task`; reload and recompile scenarios use durable worker orchestration.
4. Tests never use `async void`, fire-and-forget tasks, `Thread.Sleep`,
   `.Wait()`, or `.Result`.
5. Test-owned Unity state is registered with the common ownership API. A test
   fixture must not implement its own scene reset, save-dialog workaround, or
   broad asset cleanup.
6. Reload, compilation, source mutation, package mutation, and other
   destructive tests run only in a disposable Unity worker, never in a user's
   live Editor process.
7. An MCP test run is identified by `request_id`, `run_id`, and `utf_guid`.
   Never infer completion from a disconnect, an uncorrelated latest result, or
   a client-side timeout.
8. A running listener's bound endpoint is the port truth. Configuration files
   and `PortFileManager` caches are fallback inputs, not proof of the active
   socket endpoint.
9. Tests of persistent files use injected temporary storage. They never mutate
   live discovery files, project caches, or production singleton/static state.
10. Do not add Unity-version compatibility branches such as
    `#if UNITY_6000_3_OR_NEWER` or `UNITY_6000_4_OR_NEWER` anywhere in the Unity
    C# source tree: product code, tests, fixtures, and runner infrastructure all
    implement one supported Unity 6000.0 contract.
11. Every `EditorWindow` test creates a distinct owned instance through
    `CreateOwnedEditorWindow<T>()`; it never calls `GetWindow<T>()`,
    `GetWindowWithRect<T>()`, or fixture-local `Close()`. MCP Chat tests also
    never call production `MCPChatWindow.ShowWindow()`.

## Attributes Stay Native

Use native NUnit/UTF lifecycle and discovery attributes directly: `[TestFixture]`,
`[Test]`, `[TestCase]`, `[TestCaseSource]`, `[SetUp]`, and `[TearDown]`. Do not
create or use aliases such as `[BiomeTestFixture]`, `[BiomeTest]`,
`[BiomeSetUp]`, or `[BiomeTearDown]`, even if an alias derives from the matching
NUnit attribute. Aliases create a second discovery and lifecycle surface that
must reproduce NUnit parameterization, filtering, reporting, async handling,
and UTF ordering. They also hide behavior from IDE test tooling, source guards,
and reviewers.

Custom attributes are not the abstraction for executable test business logic.
Put setup, ownership, rollback, and cleanup behavior in `UnityMcpTestBase` or an
approved specialization; keep fixture-specific preconditions in ordinary NUnit
`[SetUp]` methods and register fixture-specific restoration through the base
ownership API. This leaves NUnit/UTF as the single lifecycle engine.

Biome attributes are reserved for declarative run policy. They must not execute
setup, teardown, scene rollback, or resource cleanup. Use
`[BiomeWorkerOnly("specific reason")]` for destructive and fault-injection tests
that are safe only in a disposable worker. It inherits NUnit `[Explicit]`, so a
normal suite cannot select the test accidentally; the reason is mandatory.
`UnityMcpTestBase` also verifies the marker before derived setup or the test body,
so an exact selection in an active user Editor fails before mutation. Do not use
`[OneTimeSetUp]` or `[OneTimeTearDown]` in a worker-only fixture because those
hooks precede the per-test base guard.
Keep an NUnit `[Category]` as a separate grouping/filter concern when a lane
needs one.

## C# Fixture Contract

Import `UnityMCP.Editor.Testing` and inherit the common base even for a pure
logic fixture. This gives native NUnit/UTF one auditable lifecycle contract and
one cleanup owner.

```csharp
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor.Testing;

public sealed class SerializerTests : UnityMcpTestBase
{
    [Test]
    public async Task SerializeAsync_ReturnsExpectedPayload()
    {
        var result = await LoadAndSerializeAsync();
        Assert.That(result, Is.EqualTo("expected"));
    }
}
```

Choose the narrowest specialization:

| Fixture | Use |
|---|---|
| `UnityMcpTestBase` | Pure logic or explicitly owned non-scene Unity state |
| `SceneTestBase` | A test that opens, creates, or mutates a scene |
| `SceneCleanTestBase` | Scene tests that must also detect leaked root objects |
| `MultiSceneTestBase` | Additive or multi-scene behavior |

`UnityMcpTestBase` owns the public non-virtual `[SetUp]`
`BeginUnityMcpIsolation()` and `[TearDown]` `EndUnityMcpIsolation()` methods.
Fixtures must not hide, call, or attempt to replace them. There is no second
executable attribute or command hook responsible for cleanup.

The effective order is:

1. NUnit/UTF runs base setup and starts ownership tracking.
2. Derived setup establishes preconditions.
3. The test body runs.
4. UTF runs teardown levels from the derived fixture toward the base.
5. The common base teardown performs registered cleanup exactly once after all
   derived teardown work.

On the canonical UTF 1.6 runner, if derived setup fails after base setup,
applicable base teardown and ownership cleanup still run. The disposable
fault-injection lane continuously verifies setup failure, test failure, and
throwing derived teardown cases. New fixture cleanup belongs in
`RegisterCleanup` or `OnBeforeIsolationCleanup`; existing local `[TearDown]`
methods run before the common base teardown and may be retained while they are
migrated, provided they neither reset scenes nor suppress cleanup failures.
New reusable specializations use the protected hooks, not another competing
finalizer. `SceneCleanTestBase` is the approved legacy exception: its internal
`[TearDown]` performs leak assertions and invokes only the managed scene reset.
Ordinary fixtures must not copy that implementation.

A process crash or domain reload can bypass both managed paths. Tests that can
cause either condition belong in a disposable worker whose run-level sandbox
provides recovery and records an incomplete run.

Use an ordinary derived `[SetUp]` only to establish the test precondition. Do
not add a fixture `[TearDown]` for owned Unity resources. Register ownership as
soon as each resource is acquired:

```csharp
public sealed class InventoryViewTests : SceneCleanTestBase
{
    [Test]
    public void Rebuild_CreatesExpectedRows()
    {
        var root = TrackOwnedObject(new GameObject("InventoryTestRoot"));
        var settings = ScriptableObject.CreateInstance<ViewSettings>();
        TrackOwnedObject(settings);

        // Exercise the SUT and assert behavior.
    }
}
```

For `EditorWindow` tests, never use `GetWindow<T>()` to acquire the subject: it
may return a window that existed before the test and make the test mutate or
close user state. Create a new owned instance instead:

```csharp
var window = CreateOwnedEditorWindow<MCPChatWindow>();
```

Headless UI Toolkit tests invoke the window's deterministic UI construction
seam without `Show()`; interactive screenshot tests may call `Show()` on that
owned instance. Do not call `Close()` in fixture teardown. The base destroys
the instance after derived teardown, including setup/body/teardown failures.

The protected ownership surface is:

- `RegisterCleanup(Action)` for a specific synchronous restoration action;
- `TrackOwnedObject<T>(T)` for `UnityEngine.Object` instances;
- `CreateOwnedEditorWindow<T>()` for a new window that cannot alias a pre-test
  Editor window;
- `TrackOwnedScene(Scene)` for a scene opened by the test;
- `CreateOwnedPreviewScene()` for an atomically registered preview scene;
- `TrackOwnedAsset(string)` for an exact path under `Assets/TestsTemp`;
- typed `SetEditorPref*` / `DeleteEditorPref*` helpers for direct preference
  mutations, and `ProtectEditorPref*` before production code mutates a key.

Pre-existing Unity or SceneView preview scenes are outside runner ownership and
must never be closed by a fixture. The base snapshots `previewSceneCount` before
each test. A fixture-created preview scene uses the atomic common factory:

```csharp
var previewScene = CreateOwnedPreviewScene();
```

The common base retains and closes that exact scene during cleanup. Direct
`NewPreviewScene()` calls in tests are forbidden. Unity 6000.0 cannot enumerate a
lost preview handle. The durable run count is checked before every test body, so
a preview leaked across domain reload fail-stops the next test instead of being
accepted as its baseline. Count drift is a hard isolation failure; the framework
never guesses which ambient preview to close.

Registered actions execute once in reverse order. Register restoration
immediately after a mutation, not at the end of the test. `TrackOwnedAsset`
rejects paths outside `Assets/TestsTemp`; never weaken that guard.

Never call `EditorPrefs.Set*`, `DeleteKey`, or `DeleteAll` from a test. The typed
base helpers snapshot `HasKey` and the exact typed value on first ownership, then
restore it even when derived setup, the test body, another cleanup action, or
derived teardown fails. A preference key has one type per test; conflicting
typed ownership is an error. `DeleteAll` is forbidden without exception.

The base also snapshots and restores `SyncHelper.Ops` around every test. Install
a sync mock with `SyncHelper.OverrideOpsForTest(mock)` in derived setup or the
test body and let the base restore the exact prior instance. Do not keep a
private copy of `SyncHelper.Ops`, assign it directly, or add a `[TearDown]` just
to restore the mock. Manual restoration is skipped when setup or teardown
faults and makes later fixtures order-dependent. A mock left behind before the
next base setup is repaired and reported as an isolation violation.

The base also owns the exact before/after transaction for these extensible or
process-global surfaces:

- `ReloadGuard` operations, lock balance, watchdogs, and persistence path;
- `LogAssert.ignoreFailingMessages` and the current domain stamp;
- `CommandRegistry` and the enabled-tool cache;
- settings, toolbar, panel, chip, and plugin registries;
- the chat settings connection event;
- `UpdateChecker`, `RelaySpawner`, and `RelaySpawnState` test state.

Use the subsystem's supported test API inside the test. Do not take another
fixture-local snapshot, call a broad production `Clear`/`Reset`, stop the live
relay, clear the production dispatcher queue, rewrite MCP state files, or add a
`[TearDown]` to restore one of these surfaces. The common base restores the
exact prior state even when another cleanup stage fails.

`OnBeforeIsolationCleanup()` is the only general-purpose local cleanup hook.
Override it only when ownership registration cannot express the cleanup. The
`PrepareForOwnershipCleanup()` and `PerformFinalIsolationCleanup()` hooks are
for reusable base-class specializations, not ordinary fixtures. Cleanup errors
must fail the test; never catch and discard them.

## Scene And Asset Safety

The runner refuses to start a controlled test run when a non-owned open scene
is dirty. Tests do not resolve that condition by saving user work or clearing
dirty flags.

Forbidden in ordinary fixtures:

- saving the active/user scene, `SaveOpenScenes`, or save-dialog APIs;
- reflection or internal APIs that clear a scene's dirty flag;
- direct `Undo.ClearAll()` or ad-hoc `NewScene()` teardown recipes;
- `AssetDatabase.Refresh()` anywhere in test source, including a test body or
  helper; source/import refresh belongs to disposable-worker orchestration;
- deleting `Assets`, `Assets/TestsTemp`, or any wildcard/global path;
- `AssetDatabase.DeleteAsset` for an unregistered path;
- best-effort cleanup that logs or swallows an error and still passes.

Create test assets only below `Assets/TestsTemp`, register every exact path with
`TrackOwnedAsset`, and let a scene specialization order scene unloading before
asset deletion. Production assets are immutable test inputs.

Persistence tests must call a path-injected core with a unique temporary root.
For example, test `PortFileManager` runtime persistence through
`SaveRuntimePortsCore`, and stale-file cleanup through the directory overload.
Do not call production `SavePorts`, `SaveRuntimePorts`, `WritePortFile`, or
`DeletePortFile` from a unit test, and do not reset cached production ports.
Those APIs address the running Editor and the user's discovery directory.

Production endpoint code must report the actual bound
`TcpListener.LocalEndpoint` while a listener is running. A configured or cached
port is valid only before binding or as a stopped-listener fallback. This keeps
the live endpoint correct even when persistence tests run before connection
tests.

## Task-First Asynchrony

Coroutine to Task conversion is mandatory for new or modified asynchronous
tests. This includes polling a predicate, waiting for a socket, awaiting a
process, and waiting for elapsed wall-clock time.

```csharp
[Test]
public async Task ConnectAsync_CompletesWithinDeadline()
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await client.ConnectAsync(timeout.Token);
    Assert.That(client.IsConnected, Is.True);
}
```

All waits are bounded and cancellation-aware. Every created task is awaited
before the fixture ends. Do not start work whose lifetime can escape the test.

`Task.Delay` is a wall-clock timer, not Unity frame synchronization. Use it
only for bounded external backoff with a cancellation token. UTF 1.6 does not
reliably advance `Awaitable.NextFrameAsync` in EditMode. Editor UI tests await
the bounded helper owned by `UnityMcpTestBase`:

```csharp
[Test]
public async Task Window_RepaintsAfterStateChange()
{
    window.Repaint();
    await WaitForEditorUpdatesAsync(2);
    Assert.That(window.State, Is.EqualTo(expected));
}
```

When a PlayMode assertion requires an actual runtime frame, await the matching
Unity API from the Task:

```csharp
[Test]
public async Task Body_FallsDuringFixedUpdate()
{
    await Awaitable.FixedUpdateAsync();
    Assert.That(body.position.y, Is.LessThan(startY));
}
```

Do not mechanically replace `yield return null` with `Task.Delay`. Select the
clock that owns the behavior: `WaitForEditorUpdatesAsync` for EditMode Editor
ticks and Unity `Awaitable` for PlayMode runtime frames. Domain reload and
recompile tests cannot depend on an in-domain task surviving the boundary.
Drive them through the durable worker protocol and resume by exact
`request_id`/`run_id` evidence.

Do not use `Assert.ThrowsAsync` or `Assert.DoesNotThrowAsync` in Unity tests.
UTF 1.6 can synchronously block the main thread inside those wrappers. Capture
the exception with ordinary `try`/`await`/`catch` and assert it afterwards.

## Disposable Worker Boundary

A test belongs in the disposable worker suite when it can reload the domain,
restart or recompile the Editor, write source/assembly definitions, mutate
`Packages` or project settings, refresh imported source, manipulate global UTF
callbacks, start or stop a process-global MCP/reload listener, or intentionally
crash/hang a subsystem.

Worker tests use a disposable project copy with no user scenes and no uncommitted
assets. They must not be mixed into a normal EditMode/PlayMode run. A worker
timeout kills that worker and records an incomplete run; it never converts the
run to passed.

Run cleanup fault acceptance with the required destructive-worker acknowledgement:

```bash
python3 scripts/run_unity_fault_injection.py \
  --project /private/tmp/unity-mcp-worker \
  --confirm-disposable-worker
```

Prepare domain-reload acceptance while the worker is stopped, then launch and
compile the worker before the second command:

```bash
python3 scripts/run_unity_domain_reload_acceptance.py \
  --project /private/tmp/unity-mcp-worker \
  --prepare-only \
  --confirm-disposable-worker

python3 scripts/run_unity_domain_reload_acceptance.py \
  --project /private/tmp/unity-mcp-worker \
  --scenario all \
  --confirm-disposable-worker
```

## Test Runner Lanes

For Unity Biome MCP repository tests and every disposable-worker run, use the
standalone durable runner. It owns project identity, endpoint rediscovery,
lost-ACK recovery, and terminal reconciliation. Ordinary repository tests reuse
the already-open canonical test project and do not launch another Editor:

```bash
python3 run_unity_tests.py EditMode \
  --project /absolute/path/to/unity-test-project \
  --filter UnityMCP.Editor.Tests.ExampleTests \
  --timeout 1800 \
  --json
```

Omit `--filter` for a complete suite. Create a disposable worker only for the
destructive fault/reload lanes documented above. For an ordinary consumer
project where the repository runner is unavailable, use one correlated
`run_tests_wait(mode="EditMode", filter="...")` call. Agents must not recreate
the polling state machine themselves.

## Low-Level MCP Run Protocol

The direct protocol is reserved for callers that explicitly need nonblocking
dispatch or recovery. `run_tests` is dispatch, not completion. A successful call
immediately returns:

```text
tests-started|request_id=<request>|run_id=<run>|utf_guid=<guid>|state=dispatched
```

Persist all three identifiers. Poll `get_test_run(run_id=...)` for that exact
run. Ordinary agents use `run_tests_wait` because it preserves the same
correlation internally.

If dispatch returns `START-UNKNOWN`, call
`resolve_test_request(request_id=...)` with the same request ID. A correlated
`state=prepared` record is a persisted intent that UTF has not yet received;
continue it once by calling `run_tests` with the identical request ID, mode and
filter, and require the same already assigned `run_id`. This is recovery of one
logical run. For `dispatched`, `running`, `finalizing`, or `terminal`, never call
`run_tests` again. A client timeout is nonterminal: continue polling the same
`run_id`, explicitly cancel it with `cancel_test_run(run_id=...)`, or report it
as still running.

A structured `ok=false` response with a positive numeric `retry` field means
the verified worker is temporarily initializing. Before an ACK, wait and retry
the same request identity. After `run_id` is known, resolve that same request,
require the same run, and resume `get_test_run`; never dispatch again. Other
errors are terminal client failures. On port rediscovery, prefer the newest
advertised endpoint for the expected canonical project and verify project
identity again before every call.

Only a reconciled terminal snapshot is evidence. `incomplete`, `invalid`,
`cancelled`, `dispatch_failed`, missing expected tests, unexpected tests, or
conflicting terminal events can never be reported as passed. Legacy
`get_test_results`/`get_test_progress` calls must include `run_id`; an
uncorrelated "latest" value is diagnostic only.

Domain reload acceptance advances only after the exact boundary leaf is
`Passed` in the expected control phase. It must retain the same request/run,
reconcile one terminal attempt for every expected leaf across observer
generations, and archive the control record after the final phase. A stale,
duplicate, failed, skipped, or out-of-phase callback must not request reload.

## Python Live-Unity Tests

Python cannot inherit the C# base class. Every live-Unity pytest uses the shared
per-test autouse `unity_state_owner` fixture, which:

1. records exact scene, object identity/hierarchy, asset, mode, and time-scale
   state within the owned live-test project;
2. registers restoration before yielding control to the test;
3. restores in `finally`, using stable identity rather than object name alone,
   and reloads the canonical owned scene to roll back component/property edits;
4. verifies the restored state; and
5. fails on cleanup errors instead of swallowing them.

Do not substitute a session-wide orphan sweep, scene save, or name-prefix
deletion for ownership. A live test that cannot express exact ownership must
use a disposable worker/project fixture.

Python async tests use `async def` and await every operation. Use
`monkeypatch.setattr` for module state so pytest restores it automatically.
Never use `except Exception: pass` in a fixture.

The deterministic live lane is fail-closed. Do not call `pytest.skip()` when a
required Unity object, UI element, PlayMode transition, reconnect, fixture, or
tool is unavailable. Fail the test or fixture. Only a module explicitly marked
`live_cli` may skip because its paid external dependency was not enabled.
Recovery must prove the exact project-pinned bridge handshake and a successful
command; an open TCP port is not recovery evidence.

Tests marked `live_cli` call external paid CLIs/APIs. The deterministic live
gate leaves them skipped by default. Run them only in the explicit opt-in lane
with `UNITY_MCP_RUN_LIVE_CLI=1`, valid credentials, and an explicit cost/network
expectation. Never use an external quota or billing failure to judge the core
runner or state-owner stability.

## Test Categorization (Markers & Categories)

Use Python markers and C# categories to declare test requirements, cost, safety,
and inclusion in each CI lane. Each test belongs to exactly one tier.

### Python Markers

Register new markers in `server/pyproject.toml` under `[tool.pytest.ini_options]
markers`. Apply via module-level `pytestmark` (preferred) or per-function
`@pytest.mark.name` (only when fixtures need different markers).

**Key markers:**

- `live` — requires running Unity Editor with MCP plugin on TCP. Skip by default
  in `pytest -m "not live"`. CI: self-hosted conformance lane only.
- `live_cli` — paid external CLI/API call (e.g., Claude, Cursor). Skip by
  default; opt-in with `UNITY_MCP_RUN_LIVE_CLI=1`. CI: never.
- `monkey` — stress/chaos tests: connection storms, protocol torture, process
  leaks. CI: Python gate runs `pytest -m "not monkey"`, so Python monkey tests
  are skipped in standard CI. C# stress tests run in standard CI (see
  `[Category(TestCategories.Stress)]` below).
- `conformance` — MCP conformance gate tests portable across any Unity+MCP
  endpoint. Always combined with `live`: `pytest.mark.live,
  pytest.mark.conformance`. CI: self-hosted.
- `cross_project` — requires two running Unity editors on different ports.
  Always combined with `live`. CI: self-hosted dual-worker lane.
- `slow` — tests taking >5s. Exempt from the `--timeout=30` default via
  `pytest-timeout`. Example: reload stability, property-based.
- `perf` — performance benchmarks. Not run in standard CI. CI: nightly.
- `asyncio` — auto-registered by `pytest-asyncio`; explicit here for
  `--strict-markers` compliance.

**Module-level pytestmark (preferred):**

```python
# Apply the same marker to all tests in the file
pytestmark = pytest.mark.monkey

# Combine markers when a test spans multiple tiers
pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]
```

**DO/DON'T:**

```python
# DO: Module-level pytestmark
pytestmark = pytest.mark.monkey

# DON'T: Per-function decorator on every test (verbose and maintainability risk)
@pytest.mark.monkey  # WRONG: use module-level pytestmark instead
def TestSomething():
    pass
```

```python
# DO: Conformance tests with all required markers
pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]

# DON'T: Missing conformance marker
pytestmark = [pytest.mark.live, pytest.mark.asyncio(loop_scope="session")]  # WRONG: missing conformance
```

```python
# DO: Slow tests marked explicitly
pytestmark = pytest.mark.slow  # tests that take >5s

# DON'T: Unlabeled slow tests (breaks --timeout=30 default)
# No marker → 30s timeout kills the test
```

### C# Categories

Declare test categorization via NUnit `[Category(TestCategories.Constant)]`.
Constants are defined in `UnityMCP.Editor.Testing.TestCategories` static class:

```csharp
public static class TestCategories
{
    public const string Stress = "Stress";
    public const string RequiresGraphics = "RequiresGraphics";
    public const string FaultInjection = "FaultInjection";
    public const string LiveCLI = "LiveCLI";
    public const string InteractiveVisual = "InteractiveVisual";
    public const string Perf = "Perf";
    public const string WorkerOnly = "WorkerOnly";
}
```

**Key categories:**

- `Stress` — chaos/monkey tests (connection storms, fault tolerance). Run in
  standard C# CI; Python equivalent is excluded.
- `RequiresGraphics` — tests requiring a GPU/display device. Applied via
  `[RequiresGraphicsDevice]` attribute; inheriting fixtures auto-get the
  category. Skipped in headless CI lanes.
- `FaultInjection` — intentional faults: crashes, hangs, domain-reload
  scenarios. Disposable-worker only.
- `LiveCLI` — external paid API calls. Not run in standard CI.
- `InteractiveVisual` — interactive screenshot tests. Require display; nightly
  only.
- `Perf` — performance benchmarks. Nightly only.
- `WorkerOnly` — destructive tests allowed only in a disposable worker. Applied
  via `[BiomeWorkerOnly("reason")]` attribute; inheriting fixtures auto-get the
  category.

**Apply at class level (preferred) for all tests in the fixture:**

```csharp
[TestFixture]
[Category(TestCategories.Stress)]
public class RelayMonkeyTests : UnityMcpTestBase
{
    [Test]
    public void Test_Something() { ... }
}
```

**DRY pattern — apply to custom attributes, not individual fixtures:**

Many categories are inherently attached to a custom attribute: if you use
`[RequiresGraphicsDevice]` or `[BiomeWorkerOnly(...)]`, the category is
automatically applied through the attribute definition. Do not duplicate the
`[Category(...)]` on the fixture itself.

```csharp
// DO: Category on the attribute (DRY — all users inherit)
[Category(TestCategories.RequiresGraphics)]
public sealed class RequiresGraphicsDeviceAttribute : NUnitAttribute, IApplyToTest { ... }

// THEN: Use the attribute without repeating the category
[TestFixture, RequiresGraphicsDevice]
public class ScreenshotTests : UnityMcpTestBase { ... }  // Gets RequiresGraphics category automatically
```

```csharp
// DON'T: Category on both attribute AND fixture (duplication)
[Category(TestCategories.RequiresGraphics)]
public sealed class RequiresGraphicsDeviceAttribute : NUnitAttribute, IApplyToTest { ... }

[TestFixture, RequiresGraphicsDevice]
[Category(TestCategories.RequiresGraphics)]  // WRONG: already on the attribute
public class ScreenshotTests : UnityMcpTestBase { ... }
```

**Use constants, not string literals:**

```csharp
// DO
[Category(TestCategories.Stress)]

// DON'T: String literals (typos not caught at compile time)
[Category("Stress")]  // WRONG
```

### Test Selection Guide

When adding a new test, ask yourself in order:

1. **Does it need Unity running?** → `pytest.mark.live` (Python) or leave
   uncategorized (C#)
2. **Does it mutate source, reload the domain, or intentionally crash?** →
   `[BiomeWorkerOnly("reason")]` (C#) — automatically gets `WorkerOnly` category
3. **Is it a stress/chaos test?** → `pytest.mark.monkey` (Python) or
   `[Category(TestCategories.Stress)]` (C#)
4. **Does it need a GPU or interactive display?** → `[RequiresGraphicsDevice]`
   (C#) — automatically gets `RequiresGraphics` category
5. **Does it call a paid external API?** → `pytest.mark.live_cli` (Python) or
   `[Category(TestCategories.LiveCLI)]` (C#)
6. **Is it a perf benchmark?** → `pytest.mark.perf` (Python) or
   `[Category(TestCategories.Perf)]` (C#)
7. **Does it take >5 seconds?** → `pytest.mark.slow` (Python); ensure cleanup is
   efficient and doesn't pile up wall-clock delays
8. **None of the above?** → No marker needed (default = unit test, runs
   everywhere, no cost)

### CI Lane Inclusion

| Tier | Python | C# | Standard CI | Nightly |
|------|--------|----|----|---------|
| Unit (no marker) | ✓ | ✓ | Yes | Yes |
| Slow (`slow`) | ✓ | — | Yes | Yes |
| Live (`live`) | ✓ | — | Self-hosted | Yes |
| Conformance (`live` + `conformance`) | ✓ | — | Self-hosted | Yes |
| Cross-project (`live` + `cross_project`) | ✓ | — | Self-hosted dual | Yes |
| Stress (`monkey` / `Stress`) | ✗ Py | ✓ C# | Yes (C# only) | Yes |
| Live-CLI (`live_cli` / `LiveCLI`) | ✓ | ✓ | No | No |
| Interactive (`InteractiveVisual`) | — | ✓ | No | Yes |
| Worker-only (`WorkerOnly`) | — | ✓ | No | Yes |
| Fault injection (`FaultInjection`) | — | ✓ | No | Yes |
| Perf (`perf` / `Perf`) | ✓ | ✓ | No | Yes |
| GPU-dependent (`RequiresGraphics`) | — | ✓ | No | Yes |

## Sequential Acceptance Lanes

Ordinary repository acceptance uses the already-open canonical
`unity-test-project`: freeze executable files, run Python unit, one complete C#
EditMode suite through `run_unity_tests.py`, then Python live. Do not launch an
extra Editor.

Only a formal release or explicitly destructive fault/reload request uses the
disposable-worker qualification below. Run exactly one gate at a time, with no
edits or parallel test process:

1. From the repository root:
   `server/.venv/bin/python -m pytest tests scripts/tests -q`.
2. From the repository root:
   `server/.venv/bin/python -m pytest install/tests -q`.
3. From `server`: `uv run pytest tests -m 'not live' -q`.
4. Run the complete C# EditMode suite twice back-to-back through the durable
   `run_unity_tests.py` runner against the same disposable worker, then run
   cleanup fault injection and both domain-reload scenarios.
5. Rediscover and verify the final worker port.
6. From `server`: `UNITY_MCP_HOST=127.0.0.1
   UNITY_MCP_PORT=<final-port> UNITY_MCP_PROJECT_PATH=<disposable-worker>
   uv run pytest tests/live -q`.

Keep exact counts, request/run IDs, duration, port history, and default
`live_cli` skips. Focused passes are development evidence, not release evidence.

## Reviewer Gate

Reject the change when any answer is no:

- Does every Unity fixture inherit the common base or an approved specialization?
- Is all acquired Unity state registered immediately and exactly?
- Do EditorWindow tests create owned instances instead of using `GetWindow` or
  a production `ShowWindow` lookup, and leave destruction to the common base?
- Can cleanup still run after derived setup or test-body failure?
- Are cleanup failures observable as failures?
- Can every required Python live test only pass or fail, with skips restricted
  to the explicit paid `live_cli` lane?
- Is every ordinary async test an `async Task` with bounded waits?
- Are `[UnityTest]`, `[UnitySetUp]`, `[UnityTearDown]`, and all `IEnumerator`
  methods absent from test source?
- Do EditMode ticks use `WaitForEditorUpdatesAsync` and PlayMode frames use the
  matching Unity `Awaitable` API instead of wall-clock delays?
- Are global seams such as `SyncHelper.Ops` installed through the supported
  override and restored by the common base rather than fixture teardown?
- Are automatically owned reload, registry, update, relay, domain-stamp, and
  log-policy surfaces free of fixture-local broad reset/restoration?
- Is `AssetDatabase.Refresh()` absent from every test-source method?
- Do persistence tests use injected temporary storage without touching the live
  endpoint, discovery files, or production cached state?
- Is the entire Unity C# change free of newer-Unity
  `#if UNITY_6000_*_OR_NEWER` compatibility branches?
- Are destructive/reload tests isolated to a disposable worker?
- Does test evidence name the exact `run_id` and a reconciled terminal state?
- Would the test remain independent under arbitrary ordering and repetition?
- For a release verdict, was the frozen sequential gate followed without edits
  or parallel test execution?

## Official References

- [Unity 6 Test Framework manual](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html)
- [UTF 1.6 changelog](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/changelog/CHANGELOG.html)
- [Unity Awaitable](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.html)
