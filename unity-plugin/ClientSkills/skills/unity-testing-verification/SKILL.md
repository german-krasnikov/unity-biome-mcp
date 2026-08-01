---
name: unity-testing-verification
description: Use when running Unity tests, authoring or linting playtest DSL, verifying Play Mode behavior, or making gameplay claims from evidence.
---

# Unity Testing And Verification

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Evidence must match the
claim. Data proves behavior; images prove appearance.

## Choose The Test

| Goal | Use |
|---|---|
| EditMode or PlayMode NUnit | `run_tests_wait(mode=..., filter=..., timeout=...)` |
| One repeatable runtime scenario | `lint_playtest`, then `run_playtest` |
| Several `.playtest` files | `run_playtest_suite` |
| One bounded runtime condition | `wait_until` after enabling `RUNTIME` |
| Trigger one public runtime action | `invoke_method` |
| Set one runtime-only field | `set_property` (works in both modes; use `set_runtime_property` only if avoiding Edit Mode) |
| Read several runtime values | `query_state` |
| Visual motion or stability | frame capture plus a separate behavioral assertion |

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
lint_playtest_suite(paths="Assets/Playtests/*.playtest")
run_playtest_suite(
  paths="Assets/Playtests/*.playtest",
  auto_play=True,
  restart_between=True,
  stop_after=True
)
```

Treat the suite matrix as coordination output, not sole acceptance evidence.
When an individual report is missing or ambiguous, rerun that saved file with
`run_playtest(path=...)` and keep its exact result.

Keep reusable aliases in a `.defs` artifact. Run `validate_playtest_aliases`
against `PlaytestConfig.asset` before synchronization; choose one direction
explicitly with `sync_playtest_aliases_from_defs` or
`export_playtest_aliases_to_defs`. Never synchronize merely to silence a diff.

## References

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
