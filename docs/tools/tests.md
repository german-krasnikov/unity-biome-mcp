# Testing Tools

Dispatch and manage NUnit tests (EditMode and PlayMode). Control test execution, monitor progress, and verify test outcomes with durable run identity for reliable automation.

## run_tests

Dispatch NUnit tests without waiting for completion. Returns immediately with a stable `run_id` for polling.

**Parameters:**
- `mode` (string, default="EditMode") — "EditMode" | "PlayMode"
- `filter` (string, optional) — Pipe-separated test class names (e.g., "HealthTest|DamageTest") for fast focused runs
- `request_id` (string, optional) — Caller-supplied stable identity; reuse it when resolving an uncertain start

**Output:**
- Success: `tests-started|request_id=...|run_id=...|utf_guid=...|state=dispatched`
- Lost ACK: `START-UNKNOWN|request_id=...|reason=...` (call `resolve_test_request()` with same `request_id`)

**Example:**

```python
# Start EditMode tests
ack = await run_tests(mode="EditMode")
# -> tests-started|request_id=abc123|run_id=xyz789|utf_guid=...|state=dispatched

# Run only specific failing tests (much faster)
ack = await run_tests(mode="EditMode", filter="HealthTest|DamageTest")
```

**Use `run_tests_wait()` for most workflows** — it polls automatically until completion.

---

## run_tests_wait

Dispatch tests and block until that exact run reaches a terminal state. The preferred entry point for consumer-project MCP sessions.

**Parameters:**
- `mode` (string, default="EditMode") — "EditMode" | "PlayMode"
- `filter` (string, optional) — Pipe-separated test class names for focused runs
- `timeout` (float, default=900.0) — Max seconds to wait
- `poll_interval` (float, default=5.0) — Seconds between status polls
- `request_id` (string, optional) — Caller-supplied stable identity

**Returns:** Reconciled terminal JSON snapshot, timeout message, protocol error, or `BLOCKED` reason.

**Example:**

```python
# Wait for all EditMode tests
result = await run_tests_wait(mode="EditMode")

# Run specific tests with shorter timeout
result = await run_tests_wait(mode="EditMode", filter="HealthTest|DamageTest", timeout=60)
```

**Workflow:**

```python
# 1. Run EditMode first (fast gate)
result = await run_tests_wait(mode="EditMode")

# 2. If pass, run PlayMode (requires MCP reconnect)
if "passed" in result:
    result = await run_tests_wait(mode="PlayMode")
```

**Note:** Repository and disposable-worker C# verification use the standalone `run_unity_tests.py` runner, not this MCP poll loop.

---

## resolve_test_request

Resolve a potentially lost start acknowledgment without launching another run. Use only if `run_tests()` returned `START-UNKNOWN`.

**Parameters:**
- `request_id` (string, required) — The `request_id` from the lost ACK or `START-UNKNOWN` response

**Returns:** `test-request|request_id=...|run_id=...|state=...` if a run exists with that request ID, or error.

**Example:**

```python
# Retry resolution if dispatch ACK was lost
status = await resolve_test_request(request_id="abc123")
# -> test-request|request_id=abc123|run_id=xyz789|state=running

# Use the resolved run_id for polling
result = await get_test_run(run_id="xyz789")
```

---

## get_test_run

Fetch the durable JSON snapshot for one exact run. **Only a reconciled `state="terminal"` snapshot is completion evidence.**

**Parameters:**
- `run_id` (string, required) — Value returned by `run_tests()`, `run_tests_wait()`, or `resolve_test_request()`

**Returns:** Complete JSON snapshot with state, outcome, manifest, test counts, and cleanup evidence.

**Output includes:**
- `state` — "prepared", "dispatched", "running", "finalizing", or "terminal"
- `outcome` — "passed", "failed", "cancelled", "incomplete", etc. (only valid when `state="terminal"`)
- `expected_count`, `passed`, `failed`, `skipped` — Test manifest and results
- `cleanup_complete`, `is_terminal` — Completion proof
- `issues` — Infrastructure errors or test problems (list of {severity, message})

**Example:**

```python
# Check exact run status
snapshot = await get_test_run(run_id="xyz789")

# Verify terminal state before accepting result
if snapshot.get("state") == "terminal":
    if snapshot.get("outcome") == "passed":
        print("All tests passed")
    else:
        print(f"Tests failed: {snapshot.get('failed')} failures")
```

---

## get_test_count

Return the total number of NUnit tests currently available in the project.

**Parameters:** None

**Returns:** Single number (string).

**Example:**

```python
count = await get_test_count()
# -> "3290"
```

---

## get_test_results

Legacy result facade. **Prefer `get_test_run()` for acceptance** — the durable snapshot includes lifecycle and cleanup evidence.

**Parameters:**
- `run_id` (string, optional) — Specific run identity (defaults to latest)

**Output:** Result summary with pass/fail counts, or "pending".

**Example:**

```python
# Legacy convenience (diagnostic only)
result = await get_test_results(run_id=run_id)
```

---

## get_test_progress

Legacy progress facade. **Prefer `get_test_run()` for acceptance.**

**Parameters:**
- `run_id` (string, optional) — Specific run identity (defaults to latest)

**Output:** Progress summary or "pending".

**Example:**

```python
# Legacy convenience (diagnostic only)
progress = await get_test_progress(run_id=run_id)
```

---

## cancel_test_run

Request cancellation of one exact run. Cancellation is asynchronous.

**Parameters:**
- `run_id` (string, required) — The run to cancel

**Returns:** Acknowledgment; keep polling `get_test_run()` until the run reaches terminal state.

**Example:**

```python
# Cancel an in-progress run
status = await cancel_test_run(run_id="xyz789")

# Poll until actually stopped
while True:
    snapshot = await get_test_run(run_id="xyz789")
    if snapshot.get("state") == "terminal":
        print(f"Cancelled with outcome: {snapshot.get('outcome')}")
        break
    await asyncio.sleep(2)
```

---

## list_test_runs

List recent durable runs, newest first. **Diagnostic aid only** — do not substitute a "latest" entry for the `run_id` you dispatched.

**Parameters:**
- `limit` (int, default=20) — Max runs to return

**Returns:** List of recent run snapshots (newest first).

**Example:**

```python
runs = await list_test_runs(limit=10)
# -> [{"run_id": "xyz789", "state": "terminal", "outcome": "passed"}, ...]
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Run all EditMode tests | run_tests_wait | `result = await run_tests_wait(mode="EditMode")` |
| Run specific failing tests | run_tests_wait + filter | `result = await run_tests_wait(mode="EditMode", filter="HealthTest")` |
| Run PlayMode after EditMode passes | run_tests_wait (two calls) | First EditMode, then PlayMode if pass |
| Recover from lost start ACK | resolve_test_request | `run_id = await resolve_test_request(request_id=...)` |
| Poll async test run manually | run_tests + get_test_run | Use `run_tests()` then `get_test_run(run_id=...)` |
| Check terminal test result | get_test_run | Verify `state="terminal"` and `outcome` field |

---

**Warnings:**

- **Start both test modes:** Never run PlayMode without first running EditMode as a gate.
- **Durable identity:** Always use the `run_id` returned by dispatch or resolved via `resolve_test_request()`. Do not poll an implicit "latest" run.
- **Terminal proof:** Only `state="terminal"` snapshots are valid completion evidence. Timeout is observational and does not mark the run complete.
- **Repository verification:** Use standalone `run_unity_tests.py` runner for Biome repository and disposable-worker C# tests, not this MCP poll loop.

---

**See also:** [Diagnostics](diagnostics.md) for compile checks before tests, [Runtime Tools](runtime.md) for PlayMode state assertions.
