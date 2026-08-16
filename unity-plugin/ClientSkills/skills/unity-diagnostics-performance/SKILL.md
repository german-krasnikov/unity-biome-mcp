---
name: unity-diagnostics-performance
description: Use when diagnosing compile or domain state, console errors, runtime objects, scene health, frame time, memory, rendering, or profiling regressions.
---

# Unity Diagnostics And Performance

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Diagnose from the cheapest
deterministic signal to the most expensive probe. Enable `VERIFY`, `RUNTIME`,
or `MEDIA` only when needed.

## Diagnostic Ladder

1. `mcp_status` for connection and mode.
2. `await_compile(timeout=0)` for compile state and corroborated errors.
3. `console_mark`, reproduce once, then `get_console_since(mark_id=...)`.
4. Targeted hierarchy or component inspection.
5. `scan_scene`, `scene_health`, or domain-specific diagnostics.
6. `verify_after_change(...)` when a completed change needs compile checks plus
   explicitly requested console, NUnit, or playtest gates.
7. Runtime snapshot, watch, frame statistics, or profile in Play Mode.
8. Screenshot only for a visual symptom.

```text
console_mark()
# reproduce once
get_console_since(mark_id="<exact returned token>")
```

```text
runtime_snapshot(type="FeatureController", name="Subject", compress=True)
watch(
  action="add",
  path="/Subject",
  component="FeatureController",
  field="state",
  condition="== Error",
  trigger_action="log"
)
get_watches()
watch(action="clear")
```

## Performance

- Establish the scenario and baseline before profiling.
- Use `get_frame_stats` for a compact snapshot, then `profile` for a bounded
  capture when the snapshot indicates a problem.
- Compare like-for-like Editor state, camera, scene, and time scale.
- Separate CPU, allocation, rendering, memory, and loading claims.
- Report measured values and capture conditions, not generic budgets.

## Specialized Probes

| Symptom | Prefer |
|---|---|
| Animator state or transition | `debug_animator` |
| Rigidbody or collider behavior | `debug_physics` |
| Navigation or pathfinding | `navmesh_query` |
| Rendering or overdraw | `render_analyze` |
| Many runtime fields | `query_state` or `runtime_snapshot` |
| One changing field | a bounded `watch`, then clear it |
| Serialized field rename safety | `serialized_field_rename_audit` |
| Scene integrity check | `scene_health(focus="...")` with options: `all`, `hierarchy`, `naming`, `duplicates`, `origins`, `missing`, `empty`, or `disabled` |

Resolve schemas for gated probes in one request and enable only their category.

`verify_after_change` always waits for compilation and reads compile errors. It
checks the console only when `mark_id` is provided, runs NUnit only when
`run_tests_mode` is provided, and runs playtests only when `playtests` is
provided. It does not validate object references, scan the scene, or capture a
screenshot; add those probes explicitly when the claim requires them.

## Rules

- Do not retry an identical failing call without new evidence.
- Pass the exact console token through the `mark_id` argument; there is no
  `mark` argument.
- Runtime snapshots require `type`. Watches require `action`; `field` is
  required for `action="add"` but not for `action="clear"`.
- Stop watches and bounded profiling sessions after use.
- Keep exact stack traces and compiler diagnostics.
- A visual symptom can guide diagnosis but cannot replace state evidence.
