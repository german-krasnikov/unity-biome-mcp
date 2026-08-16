# NUnit Test Tools

Run Unity Test Framework EditMode or PlayMode tests with durable request and run
identity. For a normal interactive MCP session, use one `run_tests_wait` call. The
lower-level tools exist for protocol recovery and non-blocking integrations.

Repository maintainers use the standalone `run_unity_tests.py` worker for
this repository's C# acceptance runs; the tools on this page are for an open
consumer project connected through MCP.

## Preferred workflow: `run_tests_wait` {#run_tests_wait}

`run_tests_wait` dispatches one run, keeps its `request_id` and `run_id`, resolves a
lost start acknowledgment, and polls until the exact run reaches terminal state.

```python
import json

raw = await run_tests_wait(
    mode="EditMode",
    filter="HealthTests|DamageTests",
    timeout=300,
)

if not raw.startswith("{"):
    raise RuntimeError(raw)  # BLOCKED, TIMEOUT, or PROTOCOL-ERROR

run = json.loads(raw)
if run["state"] != "terminal" or run["outcome"] != "passed":
    raise RuntimeError(raw)
```

Parameters:

- `mode` is `EditMode` (default) or `PlayMode`.
- `filter` is an optional pipe-separated list of test class/filter names.
- `timeout` defaults to 900 seconds.
- `poll_interval` defaults to five seconds.
- `request_id` is optional. Supply a stable ID when an external workflow must
  correlate or safely resume the same dispatch intent.

The return value is text. A successful terminal result is compact JSON; failures
to establish or observe a valid run use explicit `BLOCKED`, `TIMEOUT`, or
`PROTOCOL-ERROR` records. A timeout is observational: the Unity run may still be
active.

When both modes matter, use a passing EditMode run as the fast gate before starting
PlayMode tests. `run_tests_wait` handles domain reload and transport recovery; a
manual MCP reconnect between the modes is not part of the workflow.

## Completion evidence

Accept a run only when its durable snapshot proves all of the following:

- `state` and `lifecycle` are `terminal`;
- `is_terminal`, `execution_finished`, and `cleanup_complete` are true;
- `outcome` is `passed`;
- the snapshot's mode and filter match the request;
- its `issues` list contains no blocking infrastructure problem.

Do not treat a timeout, a progress message, or the newest entry in a run list as
evidence for the run you requested.

## Non-blocking dispatch: `run_tests` {#run_tests}

Use `run_tests` only when the caller intentionally owns polling and recovery.

```python
ack = await run_tests(
    mode="EditMode",
    filter="HealthTests",
    request_id="health-edit-001",
)
```

A normal acknowledgment is:

```text
tests-started|request_id=health-edit-001|run_id=<id>|utf_guid=<id>|state=dispatched
```

If dispatch may have happened but the acknowledgment was lost, the tool returns:

```text
START-UNKNOWN|request_id=health-edit-001|reason=<reason>
```

Do not submit a new request ID in that case. Resolve the existing intent.

## Recover a lost start

### `resolve_test_request`

```python
status = await resolve_test_request(request_id="health-edit-001")
```

This reads the durable request record without launching another run. A correlated
record includes its `run_id` and current state. Poll that exact ID with
`get_test_run`.

### `get_test_run`

```python
import json

raw = await get_test_run(run_id="<run-id>")
snapshot = json.loads(raw)
```

`get_test_run` returns a JSON string, not an already-decoded Python dictionary.
Keep polling only the `run_id` bound to the request you dispatched.

### Manual recovery sequence

```python
import asyncio
import json

status = await resolve_test_request(request_id="health-edit-001")
# Parse run_id from the correlated pipe-delimited status.
run_id = next(part.split("=", 1)[1] for part in status.split("|")
              if part.startswith("run_id="))

while True:
    raw = await get_test_run(run_id=run_id)
    snapshot = json.loads(raw)
    if snapshot.get("state") == "terminal":
        break
    await asyncio.sleep(5)
```

## Cancel a run {#cancel_test_run}

`cancel_test_run(run_id)` requests cancellation; it does not make the run terminal
immediately. Continue polling the same run until its terminal snapshot reports the
final outcome and cleanup evidence.

```python
await cancel_test_run(run_id="<run-id>")
```

## Discovery and diagnostic facades

### `list_test_runs`

Returns recent durable snapshots as JSON, newest first. It is useful for diagnosis,
not for replacing a lost `run_id` with “latest.” `limit` is clamped to 1–100.

### `get_test_count`

Test discovery is asynchronous. The first call can return `discovering`; a later
call returns a record such as:

```text
742|edit=610|play=132
```

An incoherent build can instead return an `unavailable|...` record. A cached count
is discovery information only and never certifies execution.

### `get_test_results` and `get_test_progress`

These are legacy convenience facades. When using them for diagnosis, always pass
`run_id`. Use `get_test_run` for acceptance because it includes lifecycle,
correlation, and cleanup evidence.

## Troubleshooting

| Result | Next action |
|---|---|
| `BLOCKED: ...` | Fix the reported compile/domain state, then start a new intentional run |
| `START-UNKNOWN` | Call `resolve_test_request` with the same `request_id` |
| `TIMEOUT|...|run_id=...` | Poll that `run_id`; do not assume the run stopped |
| `PROTOCOL-ERROR` | Preserve the full record and inspect the correlated run; do not accept it |
| Terminal `failed` | Read counts and `issues`, fix the tests, then dispatch a new run |
| Terminal `incomplete` or `dispatch_failed` | Treat as infrastructure failure, not a test pass |

See [Reliable Test Execution](../testing-reliability.md) for the lifecycle model and
[Diagnostics](diagnostics.md) for compile and domain recovery.
