# Reliable Unity test runs

Unity test execution can cross compilation and domain-reload boundaries. Unity
Biome MCP therefore gives each NUnit request a durable `request_id` and each
actual run a `run_id`. Keep those identities together and accept a result only
when the exact run reaches a reconciled terminal state.

This page is for users running tests in their own Unity project. Repository
fixture design and CI policy live in the contributor documentation.

## Preferred workflow

For an interactive MCP session, use `run_tests_wait`:

```python
result = await run_tests_wait(
    mode="EditMode",
    filter="Game.Tests.InventoryTests",
    timeout=300,
)
```

The tool performs a preflight, dispatches once, follows the exact run through
reloads, and returns either terminal JSON or an explicit `BLOCKED`, `TIMEOUT`,
or `PROTOCOL-ERROR` result.

Before accepting success, require all of the following in terminal JSON:

- `state` and `lifecycle` are `terminal`;
- `outcome` is `passed`;
- expected, completed, and unique terminal counts agree;
- `cleanup_complete` and the execution-boundary flags are true;
- `issues` contains no infrastructure error.

A client-side timeout is observational. It does not cancel the Unity run and it
is not completion evidence.

## Nonblocking workflow

Use `run_tests` only when the caller needs to do its own polling:

```python
ack = await run_tests(
    mode="EditMode",
    filter="Game.Tests.InventoryTests",
    request_id="inventory-tests-2026-08-16",
)
```

A normal acknowledgment contains the same `request_id` plus a `run_id`. Poll
that run directly:

```python
snapshot = await get_test_run(run_id="<run-id-from-ack>")
```

Do not use an implicit “latest run” as acceptance evidence. Another client can
start a run while you are waiting.

## Recover an uncertain start

If the connection drops after Unity may have accepted the request,
`run_tests` returns `START-UNKNOWN` with the original `request_id`. Do not create
a replacement ID and do not dispatch a second run. Resolve the original intent:

```python
resolved = await resolve_test_request(
    request_id="inventory-tests-2026-08-16"
)
```

Possible outcomes:

- a correlated run exists: use its `run_id` and continue polling;
- the intent is still prepared: retry with the **same** `request_id` so the
  server can complete the one durable dispatch;
- the identity is bound to a different mode or filter: stop and choose a new
  ID for the different request;
- the response cannot be correlated: treat it as a protocol failure, not a
  passing test.

## Cancel safely

Cancellation is asynchronous:

```python
await cancel_test_run(run_id="<run-id>")
snapshot = await get_test_run(run_id="<same-run-id>")
```

Continue polling until the same run is terminal. A cancellation acknowledgment
only means the request was received.

## Choose the right test layer

Run focused EditMode tests first. Use PlayMode when behavior needs the player
loop, scene runtime state, physics, rendering, or coroutines. Use the
[Playtest DSL](features/playtest.md) for deterministic gameplay assertions that
do not need an NUnit fixture.

When C# changed, run `sync_unity` before testing. A stale domain can report old
tests or old code even when the source file on disk is correct.

## Diagnose failures

| Result | Meaning | Next action |
|---|---|---|
| `BLOCKED` | Preflight or Unity state rejected dispatch | Fix the reported compile/domain condition, then retry |
| `START-UNKNOWN` | Dispatch may have occurred but its ACK was lost | Resolve the same `request_id` |
| `TIMEOUT` | Caller stopped waiting; run may continue | Poll the returned `run_id` or cancel it |
| `PROTOCOL-ERROR` | Identity or terminal invariants do not reconcile | Preserve the snapshot and inspect logs; do not accept results |
| terminal `failed` | Tests completed with failures | Inspect the exact run's failure records |
| terminal `incomplete` or `invalid` | Unity could not produce complete evidence | Inspect `issues`, console, and Editor log |

Use [Testing Tools](tools/tests.md) for the individual tool contracts and
[Diagnostics](tools/diagnostics.md) for compilation, console, and domain
recovery. `list_test_runs` is useful for diagnosis, but the `run_id` returned by
your own dispatch remains the authoritative identity.

## Standalone runner

Repository automation and other environments that should not depend on an MCP
client poll loop can use the included durable runner:

```bash
python3 run_unity_tests.py EditMode --project /absolute/path/to/UnityProject
```

It follows the same one-dispatch identity rule. Use `--filter` for a focused
fixture and `--json` for machine-readable output. The runner must target an
already open project with the Unity Biome MCP plugin active.
