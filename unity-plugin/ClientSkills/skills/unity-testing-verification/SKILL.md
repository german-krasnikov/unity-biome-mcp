---
name: unity-testing-verification
description: Use when running Unity tests, authoring or linting playtest DSL, verifying Play Mode behavior, or making gameplay claims from evidence.
---

# Unity Testing And Verification

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Evidence must match the
claim. Data proves behavior; images prove appearance.

Before authoring or reviewing C#, pytest, EditMode, or PlayMode tests, read and
apply [test-authoring.md](references/test-authoring.md). It is the canonical
policy for Unity 6000.0.65f1, the Editor's built-in UTF 1.6.0, common fixture
isolation, Task-first asynchrony with bounded Editor-update and runtime-frame operations, a
strict coroutine ban covering lifecycle and helper methods, disposable workers,
owned `EditorWindow` instances that cannot alias user windows, and correlated
MCP run results.

## Choose The Test

| Goal | Use |
|---|---|
| Biome repository or disposable-worker NUnit | `python3 run_unity_tests.py EditMode --project <project> [--filter <filter>] --timeout 1800 --json` |
| Consumer-project EditMode or PlayMode NUnit | One correlated `run_tests_wait` call; direct `run_tests` plus `get_test_run` is low-level nonblocking/recovery only |
| Deterministic Python live gate | Project-pinned `pytest tests/live`; require `UNITY_MCP_PROJECT_PATH`, and keep `live_cli` skipped unless `UNITY_MCP_RUN_LIVE_CLI=1` explicitly enables the paid external lane |
| One repeatable runtime scenario | `lint_playtest`, then `run_playtest` |
| Several `.playtest` files | `run_playtest_suite` |
| One bounded runtime condition | `wait_until` after enabling `RUNTIME` |
| Trigger one public runtime action | `invoke_method` |
| Set one runtime-only field | `set_property` (works in both modes; invoke for runtime-only mutations) |
| Read several runtime values | `query_state` |
| Visual motion or stability | frame capture plus a separate behavioral assertion |

Pre-existing Unity or SceneView preview scenes are not runner-owned and must not
be closed. Register every fixture-created preview immediately with
`CreateOwnedPreviewScene()`; common-base cleanup then
closes only that owned scene.

For an ordinary repository run, `<project>` is the already-open canonical test
project. Do not launch an additional Unity process. A disposable project is
required only by the destructive fault-injection and domain-reload lanes.

## Playtest Workflow

1. Define the claim, action, observable field, expected change, timeout, and
   cleanup before writing DSL.
2. Create or update a descriptive file under `Assets/Playtests/`.
3. Resolve all referenced aliases, paths, and component fields in one
   `resolve_scene_refs(refs="...")` call, then run both
   `lint_playtest(path=...)` and `lint_scene_refs(path=...)`.
4. Enter Play Mode explicitly unless the suite uses `auto_play=True`.
5. Use `TIMESCALE 5` as the default fast profile. Keep `TIMESCALE 1` for tests
   whose claim depends on real-time duration, frame pacing, animation timing,
   or physics stability.
6. Run the saved file by `path`; use inline `script` only for disposable,
   single-purpose diagnostics.
7. Run with bounded waits and exact assertions.
8. Inspect every failure line. Treat `ERR`, `FAIL`, `TIMEOUT`, and `BLOCKED` as
   failure even if an aggregate line is optimistic.
9. Restore `TIMESCALE 1` and stop Play Mode in cleanup after a tool error.

```text
resolve_scene_refs(refs="$player,/UI/Submit,t:GameManager")
lint_playtest(path="Assets/Playtests/state-change.playtest")
lint_scene_refs(path="Assets/Playtests/state-change.playtest")
run_playtest(
  path="Assets/Playtests/state-change.playtest",
  timeout=120,
  abort_on_fail=True
)
```

`run_playtest` defaults to 120 seconds and requires Play Mode.
`run_playtest_suite` defaults to `auto_play=False`.

For a maintained suite, lint all saved artifacts before running them:

```text
lint_playtest_suite(pattern="Assets/Playtests/*.playtest")
run_playtest_suite(
  pattern="Assets/Playtests/*.playtest",
  auto_play=True,
  restart_between=True,
  stop_after=True
)
```

Treat the suite matrix as coordination output, not sole acceptance evidence.
When an individual report is missing or ambiguous, rerun that saved file with
`run_playtest(path=...)` and keep its exact result.

For path syntax with special characters, use backslash escaping (`\/` for literal
`/`, `\\` for literal `\`) or bracket protection (`[Zone A/Zone B]` protects
embedded slashes without escaping).

Keep reusable aliases in a `.defs` artifact. Run `validate_playtest_aliases`
against `PlaytestConfig.asset` before synchronization; choose one direction
explicitly with `sync_playtest_aliases_from_defs` or
`export_playtest_aliases_to_defs`. Never synchronize merely to silence a diff.

## Correlated Test Recovery

Do not hand-roll the low-level polling protocol for an ordinary test run. The
standalone repository runner and the consumer-facing `run_tests_wait` wrapper
already preserve correlation across transport loss and domain reload.

Resolve an uncertain start with the original `request_id`. Only a correlated
`state=prepared` intent may call `run_tests` again, once, with the identical
mode/filter/request payload and the same assigned `run_id`. After `dispatched`,
observe or cancel that exact run; never dispatch another logical run.

## Release Test Order

After executable files are frozen, run gates sequentially with no edits or
parallel test process: repository Python unit tests, server unit tests excluding
`live`, the complete durable C# EditMode suite twice back-to-back plus fault and
one/two-reload acceptance, final-port rediscovery, then deterministic Python
live tests. The exact commands and evidence requirements are in
[test-authoring.md](references/test-authoring.md). Run repository C# gates with
`run_unity_tests.py`, not an ad hoc MCP poll loop. `live_cli` remains a separate
paid opt-in lane.

## References

- Read [test-authoring.md](references/test-authoring.md) before creating,
  changing, or reviewing any test.
- Read [playtest-dsl.md](references/playtest-dsl.md) when authoring or reviewing
  DSL.
- Read [evidence.md](references/evidence.md) before reporting gameplay or visual
  verification.

## Stop Conditions

- The scene is not in the expected initial state.
- A required observable cannot be queried deterministically.
- A fixed delay is the only proposed synchronization.
- Test output has been summarized without exact failure evidence.
- A suite summary is the only evidence for a behavioral claim.
- Cleanup did not restore time scale, Play Mode, or the scenario baseline.
- A run result is not correlated to its `request_id`, `run_id`, and `utf_guid`.
- A Unity fixture bypasses `UnityMcpTestBase`, or a Python live test has no
  `unity_state_owner` fixture.
- Test source contains `[UnityTest]`, `[UnitySetUp]`, `[UnityTearDown]`, an
  `IEnumerator` helper, or `AssetDatabase.Refresh()` anywhere.
- A fixture restores `SyncHelper.Ops` manually instead of installing its mock
  through `SyncHelper.OverrideOpsForTest` and relying on the common base.
- A fixture manually clears or restores `ReloadGuard`, `CommandRegistry`,
  plugin/provider registries, `UpdateChecker`, relay state, the domain stamp, or
  `LogAssert` state already owned by `UnityMcpTestBase`.
- A test writes `EditorPrefs` directly instead of using typed base ownership, or
  exercises a production preference writer without `ProtectEditorPref*`.
- An EditorWindow test uses `GetWindow<T>` or `GetWindowWithRect<T>` instead of
  `CreateOwnedEditorWindow<T>`; MCP Chat tests additionally reject production
  `ShowWindow`.
- A persistence test writes live `PortFileManager` storage/static state, or
  endpoint code treats a configured port as truth while a listener is bound.
- A reload callback is not an exact `Passed` boundary in the expected control
  phase, or its control record was not archived after completion.
- Unity C# code introduces any `#if UNITY_6000_*_OR_NEWER` compatibility branch.
