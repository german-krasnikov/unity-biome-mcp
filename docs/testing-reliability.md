# Reliable Unity test runs

Choose the smallest test layer that proves the behavior, then use the durable
runner that matches the caller. Exact request/run identity, lifecycle fields,
terminal evidence, cancellation, and polling contracts are owned by
[NUnit Test Tools](tools/tests.md).

## Choose a workflow

| Need | Workflow |
|---|---|
| Normal interactive NUnit run | [`run_tests_wait`](tools/tests.md#run_tests_wait) |
| Integration that owns polling | [`run_tests`](tools/tests.md#run_tests), then poll its exact `run_id` |
| Deterministic gameplay assertion without an NUnit fixture | [Playtest DSL](features/playtest.md) |
| Repository automation without an MCP-client poll loop | [Standalone runner](#standalone-runner) |

Run focused EditMode tests first. Use PlayMode only when behavior needs the
player loop, runtime scene state, physics, rendering, or coroutines. After a C#
change, call `sync_unity` and require a clean result before trusting any test.

## Preferred interactive run

```python
result = await run_tests_wait(
    mode="EditMode",
    filter="Game.Tests.InventoryTests",
    timeout=300,
)
```

`run_tests_wait` owns preflight, one durable dispatch, reload recovery, and
polling. Accept only the exact terminal evidence described in
[Completion evidence](tools/tests.md#completion-evidence); a timeout is not a
pass and does not cancel the Unity run.

## Recover from a reported result

| Result | Action |
|---|---|
| `BLOCKED` | Fix the reported compile or domain condition, then retry intentionally |
| `START-UNKNOWN` | Resolve the same `request_id`; do not create a second dispatch |
| `TIMEOUT` | Poll or cancel the returned `run_id`; do not assume the run stopped |
| `PROTOCOL-ERROR` | Preserve the record and inspect the correlated run; never accept it as a pass |
| terminal `failed` | Inspect that run's failure records, fix them, then start a new run |

The canonical recovery sequence and cancellation semantics are in
[Recover a lost start](tools/tests.md#recover-a-lost-start) and
[Cancel a run](tools/tests.md#cancel_test_run). Use
[Diagnostics](tools/diagnostics.md) for compile, console, and domain failures.

## Standalone runner

Automation that should not implement the MCP polling loop can use the included
durable runner from the repository root:

```bash
server/.venv/bin/python run_unity_tests.py EditMode \
  --project /absolute/path/to/UnityProject
```

On Windows, use `server\.venv\Scripts\python.exe`. Add `--filter` for a focused
fixture and `--json` for machine-readable output. The target project must
already be open with Unity Biome MCP active; the runner verifies the responding
endpoint belongs to that project.
