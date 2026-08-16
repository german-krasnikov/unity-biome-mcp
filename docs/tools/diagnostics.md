# Diagnostics and recovery

Use these tools to distinguish a connection problem from compilation, console,
scene-reference, or test failures. Start with the narrowest read-only check and
escalate only when its evidence points to a recovery action.

Exact arguments and defaults live in the
[generated tool schema](../tools-schema/index.md). Connection and orchestration
tools are also indexed in [System tools](system.md).

## Check installation and connection health

### `doctor` {#doctor}

`doctor()` runs five independent checks: Python version, Unity port files,
server lock files, TCP reachability, and Unity's compile/reload state.

```python
report = await doctor()
```

A report begins with a summary such as `Unity Biome MCP Doctor — 5/5 checks
passed` and then lists each check. A failed TCP or Unity-state check is evidence
only; `doctor` does not start Unity, reconnect the MCP session, reimport assets,
or retry the failed operation.

Use repair mode only when the report identifies stale discovery files:

```python
report = await doctor(fix=True)
```

`fix=True` may delete stale `.port` files whose Unity PIDs are dead and stale
`server-*.lock` files. It does not change live entries. Because this is a local
filesystem mutation, it is blocked when `UNITY_MCP_READ_ONLY=1`.

### `list_connections` and `reconnect_unity` {#reconnect_unity}

Inspect the current Python-to-Unity transport before reconnecting:

```python
status = await list_connections()
# port 9500 | tcp:connected | stdio:alive
```

Reconnect with automatic live-port discovery, or choose a known port when
several Unity Editors are running:

```python
result = await reconnect_unity()
result = await reconnect_unity(port=9501)
```

A successful manual reconnect refreshes the Unity capability catalog and resets
the session's category enablement. Re-enable any deferred category needed by the
next task. A failed reconnect leaves the previous session enablement intact.

## Verify a C# edit

### `compile_preflight` {#compile_preflight}

Before writing a complete C# file, ask Unity's Roslyn workspace to check the
proposed content without touching disk or starting a Unity compile:

```python
candidate = """using UnityEngine;

public sealed class Spinner : MonoBehaviour
{
    public float DegreesPerSecond = 90f;

    private void Update() =>
        transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f);
}
"""

preflight = await compile_preflight(
    file_path="Assets/Scripts/Spinner.cs",
    new_content=candidate,
)
```

`OK preflight` means the workspace found no reported diagnostics. `ERR
preflight` includes diagnostics to fix before writing. `[ROSLYN UNAVAILABLE]`
means this fast check could not run; it is not proof that the file will compile.
Preflight does not validate runtime behavior, serialized data, or the final
on-disk project after other files change.

### `sync_unity` {#sync_unity}

After writing a `.cs` or assembly-definition file, use `sync_unity` as the
normal synchronization boundary:

```python
sync = await sync_unity(timeout=120)
```

It asks Unity to refresh, follows compilation and domain reload, reconnects
across the reload when possible, and returns only after the new code is live or
a terminal error/recovery verdict is available. Do not replace it with a fixed
sleep.

Use `resolve=True` after intentionally changing package metadata:

```python
sync = await sync_unity(resolve=True, timeout=180)
```

`bump=True` edits `unity-plugin/package.json`, implies package resolution, and is
limited to one use per connection session. It belongs to an explicit package
release workflow, not routine recovery from a stale domain.

Treat compile errors, `STOP:`, `REIMPORT-NEEDED`, and other non-clean verdicts as
failed synchronization. Follow the reported action; do not continue to tests on
the assumption that the new assembly loaded.

### `recompile` and `await_compile` {#recompile}

`recompile()` is a narrower, idempotent request to reimport C# scripts. It
returns immediately. If compilation has already been requested, use
`await_compile()` to observe completion:

```python
await recompile()
compile_result = await await_compile(timeout=60)
```

`await_compile(timeout=0)` performs one immediate check. Neither tool provides
the full refresh, domain-reload recovery, and live-code guarantee of
`sync_unity`; prefer `sync_unity` after an actual source edit.

### `get_compile_errors` {#get_compile_errors}

Use `get_compile_errors()` for the durable C# error list:

```python
errors = await get_compile_errors()
```

The server corroborates Unity's typed response with `Editor.log`, so compilation
errors are not lost when the Unity Console is cleared. An empty or clean result
does not prove that a newly edited assembly was reloaded; pair it with a
successful `sync_unity` result.

### `diagnose` {#diagnose}

`diagnose()` takes a read-only snapshot of compile, reload, assembly-stamp, log,
and test-assembly signals and reduces them to a typed verdict:

```python
verdict = await diagnose()
```

`CLEAN-LIVE` is the positive verdict. `FAIL:<CS>`, `STALE-DOMAIN`,
`WEDGE-ENGINE`, `WEDGE-STATE`, `BUILD-FAILED-WEDGE`, `STALE-CACHE`,
`TESTS-INVISIBLE`, and `REBUILDING` identify specific failure states. `NO-OP`
and `UNKNOWN` are not evidence that an intended compile completed.

For an assembly-stamp comparison, pass the pre-change MVID and whether a compile
was expected:

```python
verdict = await diagnose(prev_mvid="<before-mvid>", expected_compile=True)
```

Prefer the recovery verdict returned by `sync_unity` during a normal edit. Use
`diagnose` when that workflow stalls or when you need to classify existing
state without triggering work.

### `serialized_field_rename_audit` {#serialized_field_rename_audit}

Before removing `[FormerlySerializedAs]`, inspect assets that may still contain
the old field name:

```python
report = await serialized_field_rename_audit(
    type="Game.PlayerStats",
    old_field="health",
    new_field="hitPoints",
)
```

The report covers the requested prefab, scene, and ScriptableObject scopes and
suggests migration actions. It does not modify assets.

## Isolate new console problems

### `get_console` {#get_console}

Read recent Unity Console entries with a narrow level, keyword, count, or time
window:

```python
problems = await get_console(
    level="Error,Exception,Assert",
    keyword="PlayerController",
    count=50,
)
```

Use `get_compile_errors` for C# compilation failures. `get_console` is intended
for runtime and Editor log evidence; a finite result window is not proof that
older problems never occurred.

### `console_mark` and `get_console_since` {#console_mark}

Create a watermark immediately before the operation under test, then read only
later entries:

```python
mark = await console_mark(label="before-equip")
# Run the bounded operation under test here.
new_problems = await get_console_since(
    mark_id=mark,
    level="Error,Exception,Assert",
)
```

`console_mark` records a timestamp in the Python process; it does not clear or
snapshot Unity's ring buffer. If `get_console_since` reports a ring-overflow
warning, the evidence window is incomplete. Repeat the bounded operation with a
fresh mark instead of treating the absence of returned errors as a pass.

<span id="get_console_since"></span>

## Run the additive verification gate

### `verify_after_change` {#verify_after_change}

`verify_after_change` runs enabled gates in order and stops at the first failure:

1. wait for any current compile;
2. read durable compile errors;
3. inspect console entries after `mark_id`, when provided;
4. run correlated Unity tests, when `run_tests_mode` is provided;
5. run a `.playtest` suite, when `playtests` is provided.

```python
mark = await console_mark(label="before-change")
# Perform the bounded change here.

result = await verify_after_change(
    mark_id=mark,
    run_tests_mode="EditMode",
    test_filter="InventoryTests",
    playtests="Playtests/inventory/*.playtest",
    timeout=300,
    restart_between=True,
)
```

The test and playtest gates can change Editor/runtime state. A playtest suite
stops Play Mode when it finishes. With `restart_between=True`, verification
first stops Play Mode, enters it automatically for the first file, and restarts
it between later files. With the default `False`, enter Play Mode before the
playtest gate.

`timeout` limits the compile wait (capped at 120 seconds) and the Unity test
gate; it is not one wall-clock deadline for the entire workflow and does not set
the per-playtest timeout. `changed_files` is informational context and does not
select a gate.

This verifier is additive, not atomic. It does not create a checkpoint, roll
back failures, validate scene references, or save the scene. For a guarded scene
mutation with atomic batch behavior and save gating, use
[`scene_change_plan` and `apply_scene_change`](scene.md#apply-a-guarded-scene-change).

### `get_changeset` {#get_changeset}

After a mutation sequence, `get_changeset()` summarizes operations observed by
the current Python server session:

```python
changes = await get_changeset()
```

It is review evidence, not a filesystem diff or an Undo transaction. Reconnects
and operations outside the coordinator can limit what it contains.

## Validate scene and serialized references

### `scene_health` {#scene_health}

Run a broad hierarchy audit, or narrow it to a supported focus:

```python
all_findings = await scene_health()
naming = await scene_health(focus="naming")
missing = await scene_health(focus="missing")
```

Results are tagged `CRITICAL`, `WARNING`, `INFO`, or `OK`. Treat them as findings
to inspect, not an automatic repair plan.

### `validate_references` {#validate_references}

Inspect serialized `ObjectReference` fields below a scene path:

```python
summary = await validate_references(path="/Player", depth=3)
details = await validate_references(
    path="/Player",
    depth=5,
    verbose=True,
    ignore_optional=True,
)
```

The default output emphasizes broken and missing references. `verbose=True`
also includes valid fields; `ignore_optional=True` skips fields marked optional.

### `resolve_scene_refs` {#resolve_scene_refs}

Resolve project aliases, hierarchy paths, and component-type selectors without
mutating the scene:

```python
resolved = await resolve_scene_refs(
    refs="$player,/Enemies/Boss,t:Camera",
    fields="health,maxHealth",
)
```

Each token returns `OK`, `MISS`, or `AMB`. Resolve ambiguity before using the
reference in a mutation.

### `lint_scene_refs` {#lint_scene_refs}

Lint either one project-relative `.playtest` file or an inline DSL/batch snippet:

```python
file_report = await lint_scene_refs(path="Playtests/combat.playtest")
snippet_report = await lint_scene_refs(
    snippet="ASSERT /Player|Health|hp == 100",
)
```

`path` and `snippet` are mutually exclusive. The linter checks unresolved or
embedded aliases, missing objects, and ambiguous names without executing the
input.

## Recovery order

| Symptom | First evidence | Next action |
|---|---|---|
| No Unity connection | `list_connections()` then `doctor()` | Start/focus the intended Unity Editor, then `reconnect_unity()` |
| Stale port or lock files | `doctor()` identifies them | `doctor(fix=True)`, then reconnect if needed |
| Source edit not live | `sync_unity()` result | Follow its terminal verdict; use `diagnose()` only for deeper classification |
| Compile failed | `get_compile_errors()` | Fix the reported source, then run `sync_unity()` again |
| New runtime/Editor error | fresh `console_mark()` window | Reproduce once, inspect `get_console_since()`, then fix the first relevant error |
| Missing or ambiguous scene target | `resolve_scene_refs()` or `lint_scene_refs()` | Correct the path/alias before mutation |
| Broad scene-quality concern | `scene_health()` and focused validators | Inspect findings, apply a bounded fix, then read back state |

Do not use repeated reconnects, package-version bumps, or long sleeps as a
generic recovery loop. Each changes state without explaining the underlying
failure.

<span id="scan_scene"></span>

For `scan_scene`, collider diagnostics, trigger spacing, and other scene-wide
spatial checks, see [Spatial tools](spatial.md#scene-wide-analysis).
