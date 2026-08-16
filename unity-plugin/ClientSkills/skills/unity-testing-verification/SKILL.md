---
name: unity-testing-verification
description: Use when running Unity tests, authoring or linting playtest DSL, verifying Play Mode behavior, or reporting gameplay and visual claims from evidence.
---

# Unity Testing And Verification

Read `.claude/skills/unity-mcp-operations/SKILL.md` once if it is not already
loaded. Evidence must match the claim: data proves behavior; images prove
appearance.

## Choose The Smallest Test

| Goal | Use |
|---|---|
| Focused EditMode or PlayMode NUnit run | One correlated `run_tests_wait(...)` call |
| Explicit nonblocking dispatch or recovery | `run_tests`, then the exact `get_test_run(run_id=...)` |
| One repeatable runtime scenario | `lint_playtest`, then `run_playtest` |
| Several saved scenarios | `lint_playtest_suite`, then `run_playtest_suite` |
| One bounded runtime condition | `wait_until` after enabling `RUNTIME` |
| Invoke one public runtime action | `invoke_method` |
| Read several runtime values | `query_state` |
| Inspect appearance | Screenshot or frame capture plus separate state evidence when behavior matters |

Follow the consumer project's own test-framework version, fixture hierarchy,
and cleanup conventions. Keep every test independent, use bounded waits, restore
state it owns, and retain the exact failure output. Do not import Unity Biome
MCP's repository fixtures or CI commands into another project.

Use `set_property` for supported serialized Edit Mode changes. Do not assume it
can mutate arbitrary Play Mode state; expose a bounded public action and call
`invoke_method`, or express the action in Playtest DSL.

## NUnit Workflow

Use one blocking wrapper call for an ordinary run:

```text
run_tests_wait(
  mode="EditMode",
  filter="InventoryTests",
  timeout=300
)
```

Accept a verdict only from the terminal result returned for that logical run.
Keep failing test names, expected and actual values, and stack traces. A caller
timeout, disconnect, or partial result is not a pass.

Direct `run_tests` is low-level dispatch, not completion. Preserve its
`request_id`, `run_id`, and `utf_guid`. On `START-UNKNOWN`, resolve the original
request instead of starting another run. Once a run is dispatched, observe or
cancel that exact `run_id`; never create a replacement logical run. Do not
hand-roll this polling protocol for an ordinary test run.

## Playtest Workflow

1. Define the initial state, action, observable value, expected change, timeout,
   and cleanup.
2. Save a descriptively named scenario under `Assets/Playtests/`.
3. Resolve referenced aliases, paths, and fields with `resolve_scene_refs`.
4. Run `lint_playtest(path=...)` and `lint_scene_refs(path=...)`.
5. Enter Play Mode unless a suite uses `auto_play=True`.
6. Run the saved file by `path`; reserve inline `script` for a disposable probe.
7. Treat every `ERR`, `FAIL`, `TIMEOUT`, or `BLOCKED` line as failure.
8. Restore time scale and stop Play Mode after success or tool error.

```text
resolve_scene_refs(refs="$player,/HUD,t:GameManager")
lint_playtest(path="Assets/Playtests/inventory-open.playtest")
lint_scene_refs(path="Assets/Playtests/inventory-open.playtest")
run_playtest(
  path="Assets/Playtests/inventory-open.playtest",
  timeout=120,
  abort_on_fail=True
)
```

Use `TIMESCALE 5` for ordinary state-transition scenarios and restore
`TIMESCALE 1` in cleanup. Keep `TIMESCALE 1` throughout when the claim depends
on real-time duration, frame pacing, animation timing, or physics stability.

For several files, both linting and execution use `pattern`:

```text
lint_playtest_suite(pattern="Assets/Playtests/*.playtest")
run_playtest_suite(
  pattern="Assets/Playtests/*.playtest",
  auto_play=True,
  restart_between=True,
  stop_after=True
)
```

The suite matrix is coordination output. If an individual result is missing or
ambiguous, rerun that saved file and retain its exact report.
Accept a suite only when it reports `passed == total > 0` with no failure
signal. Treat an empty match or an unconfirmed Play Mode start/restart as a
failed suite.

## UI Interaction

Use ordinary hierarchy paths for uGUI and the
`GameObject|UIDocument|element-name` form for UI Toolkit:

```text
CLICK /Canvas/StartButton
CLICK /HUD|UIDocument|submit-button
FILL /HUD|UIDocument|player-name Player1
FOCUS /HUD|UIDocument|player-name
```

`FILL` and `FOCUS` support UI Toolkit fields through `UIDocument` addressing.
Inspect the live tree when an element name is uncertain.

## Aliases And Reuse

Keep reusable `VAL` definitions and stable macros in a `.defs` artifact. Run
`validate_playtest_aliases` before synchronizing `PlaytestConfig.asset`. Choose
one direction explicitly with `sync_playtest_aliases_from_defs` or
`export_playtest_aliases_to_defs`; do not synchronize merely to hide a diff.

For paths containing literal slashes, use `\/`, `\\`, or bracket protection as
supported by the parser. Lint after changing either a scenario or its included
definitions.

## References

- Read [playtest-dsl.md](references/playtest-dsl.md) when authoring or reviewing
  DSL.
- Read [evidence.md](references/evidence.md) before reporting gameplay or visual
  verification.

## Stop Conditions

- The scene is not in the declared initial state.
- A required observable cannot be queried deterministically.
- A guessed delay is the only synchronization mechanism.
- Test output lost the exact failure details.
- A suite summary or screenshot is the only evidence for a behavioral claim.
- Cleanup did not restore time scale, Play Mode, or scenario-owned state.
- An NUnit result is not correlated to the dispatched logical run.
