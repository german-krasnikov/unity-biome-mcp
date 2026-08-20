# Repository Test Policy

This document is the canonical test-authoring policy for Unity Biome MCP.
It applies to this repository and its disposable workers. It is not installed
into consumer projects with `ClientSkills`.

The canonical Unity test project uses Unity `6000.0.65f1` and the Editor's
built-in Unity Test Framework `1.6.0`. Product code, fixtures, and runners target
the Unity `6000.0` contract; do not add newer-Unity compatibility branches.

## C# Fixtures

Every Unity fixture inherits the narrowest supported base:

| Base | Use |
|---|---|
| `UnityMcpTestBase` | Logic or explicitly owned non-scene Unity state |
| `SceneTestBase` | Tests that open, create, or mutate a scene |
| `SceneCleanTestBase` | Scene tests that also detect leaked root objects |
| `MultiSceneTestBase` | Additive and multi-scene behavior |

**MCPFeedbackFixture** (`unity-test-project/Assets/MCPFeedbackFixture/`): Conformance test
fixture with 10 C# components (FixtureState, FixtureMover, FixtureReceiver, FixtureId, etc.),
5 EditMode tests (FastPass, FastFail, LongPass, CompileGenerationVisible, ReferenceGraphRoundTrip),
11 PlayTest DSL files, 4 suite definitions, and shared definitions. Use this fixture as the
protocol compliance baseline.

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

The repository uses two specialized test layers to verify protocol contracts and
conformance invariants:

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

## Documentation and Skill Checks

Run `python scripts/check_skills_freshness.py --strict` after changing bundled
skills or agents. Strict mode exits non-zero for findings classified as errors;
warnings still require human triage. The checker is a heuristic static guard, so
even a clean report does not prove that a workflow remains semantically current.
Review the affected instructions against the live tool and product contracts.

## Acceptance Order

Freeze executable files before a formal release gate. Run these lanes
sequentially, with no edits or parallel test process:

1. `server/.venv/bin/python -m pytest scripts/tests -q`
2. `server/.venv/bin/python -m pytest install/tests -q`
3. From `server`: `uv run pytest tests -m 'not live' -q`
4. Complete C# EditMode suite twice against one disposable worker, followed by
   cleanup fault injection and the domain-reload scenarios.
5. Rediscover and verify the final worker port.
6. From `server`, run project-pinned deterministic `tests/live` with the final
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
