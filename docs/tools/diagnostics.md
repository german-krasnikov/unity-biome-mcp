# Diagnostics & Connection Tools

Troubleshoot connection issues, inspect errors, and monitor compilation. Essential for debugging when commands hang or fail.

## doctor

Comprehensive health check with optional auto-fix.

**Parameters:**
- `fix` (bool, default=false) — Auto-clean stale port files and retry connection

**Checks (5 total):**

| Check | Tests | Auto-fix? |
|-------|-------|-----------|
| `python_version` | Python >= 3.10 | No (install Python 3.10+) |
| `port_file` | ~/.unity-biome-mcp/ports/*.port exist + PIDs alive | Yes (remove stale files) |
| `lockfile` | `~/.unity-biome-mcp/server-*.lock` contains no dead-PID entries | Yes (clean stale files) |
| `tcp_connection` | 127.0.0.1:port reachable + responds | Reconnect attempt only |
| `unity_state` | Unity responds to the TCP `diagnose` command without a compile/reload wedge | No |

**Output:**

```
All checks passed
  Python: 3.12.1
  Port file: ~/.unity-biome-mcp/ports/1234.port (PID 1234 alive)
  Lockfile: no stale server lockfiles
  TCP connection: port 9500
  Unity state: compile clean
```

**Example:**

```python
# Diagnosis only
result = await doctor()

# Auto-fix stale files + retry
result = await doctor(fix=True)
```

**When to use:** Before every session, or if commands hang/timeout.

---

## get_compile_errors

Check if C# compilation has errors. Gates test execution. Uses corroboration between TCP response and Editor.log for reliability.

**Parameters:** None

**Output:** List of compiler errors with file:line:col, or "compile clean".

**Format:**
```
error CS0103 at Assets/Scripts/Player.cs:15:10: The name 'Health' does not exist
error CS0246 at Assets/Scripts/Player.cs:8:5: Type 'Enemy' not found
```

**Example:**

```python
# Check compile status
errors = await get_compile_errors()
if "error CS" in errors:
    print(f"Compile errors: {errors}")
else:
    print("Compile clean — ready for tests")
```

---

## get_console

Read Unity Console output (errors, warnings, logs).

**Parameters:**
- `count` (int, default=10) — Number of lines to return
- `level` (string, optional) — "error" | "warning" | "log" (default: all). For comprehensive problem detection (including Exception and Assert), use `level="Error,Exception,Assert"`
- `first` (int, default=0) — If > 0, return first N from init buffer + last (count-first) from ring
- `keyword` (string, optional) — Case-insensitive substring filter
- `count_only` (bool, default=false) — Return number of matches as string instead of log lines
- `since` (float, optional) — Only logs from last N seconds

**Output:** Console lines with timestamps.

**Example:**

```python
# All console output
console = await get_console()

# Error logs only (excludes Exception/Assert)
errors = await get_console(level="error")

# All problem types (Error + Exception + Assert)
problems = await get_console(level="Error,Exception,Assert")

# Search for specific keyword
hits = await get_console(keyword="NullReference", count=50)

# Count errors without returning them
error_count = await get_console(level="error", count_only=True)

# Recent logs only (last 30 seconds)
recent = await get_console(since=30.0)
```

---

## console_mark

Create a console watermark. Pure Python, no TCP call.

**Parameters:**
- `label` (string, default="") — Optional label for the mark

**Returns:** `mark_id` string encoding current timestamp. Pass to `get_console_since()` to retrieve only logs after this point.

**Example:**

```python
# Mark before an operation
mark = await console_mark(label="before_test")

# ... perform operations ...

# Get only new logs since the mark
new_logs = await get_console_since(mark_id=mark)
```

---

## get_console_since

Console entries after a watermark created by `console_mark()`.

**Parameters:**
- `mark_id` (string) — String from `console_mark()` or bare float timestamp
- `level` (string, optional) — Filter (e.g. `"error,exception,assert"`)
- `count` (int, default=500) — Max entries to return
- `keyword` (string, optional) — Case-insensitive substring filter
- `count_only` (bool, default=false) — Return match count as string

**Example:**

```python
mark = await console_mark()
# ... do something ...
errors = await get_console_since(mark_id=mark, level="error")
```

---

## recompile

Trigger Unity to reimport C# scripts. Returns immediately.

**Parameters:** None

**Returns:** Acknowledgment. Use `await_compile` to block until compilation finishes.

**Example:**

```python
await recompile()
result = await await_compile(timeout=30)
```

---

## await_compile

Block until C# compilation and domain reload finish.

**Parameters:**
- `timeout` (float, default=60.0) — Max seconds to wait. `timeout=0` for immediate check without polling.

**Returns:**
- `"compile clean (X.Xs)"` — Success after N seconds
- `"compile clean (sync)"` — Via epoch tracking
- `"compile clean (no IL change)"` — Compiled but no IL delta
- `"error CS0103: ..."` — Compilation failed with errors
- `"timeout after 60s — compile still in progress"` — Timeout

**Example:**

```python
# Wait up to 30s for compile
result = await await_compile(timeout=30.0)
if "clean" in result:
    print("Ready for tests")
else:
    print(f"Compile status: {result}")
```

**Workflow:**

```python
# After writing .cs files
await write_file(...)
result = await await_compile(timeout=30)
if "clean" not in result:
    return  # Abort, don't run tests
result = await run_tests_wait(mode="EditMode")
# Accept only its reconciled terminal snapshot; TIMEOUT is nonterminal.
```

This wrapper is for focused consumer-project verification. When developing Unity
Biome MCP itself, run repository and disposable-worker C# tests with
`python3 run_unity_tests.py`; release evidence never comes from an ad hoc MCP
poll loop.

---

## compile_preflight

Validate C# code without recompiling (fast Roslyn syntax check).

**Parameters:**
- `file_path` (string) — Assets-relative path (e.g., "Assets/Scripts/Player.cs")
- `new_content` (string) — Full file content to validate

**Output:**
- `"OK preflight (143ms)"` — No errors
- `"ERR preflight"` + error list — Diagnostics found

**Example:**

```python
new_code = """public class Player : MonoBehaviour {
    public void Move(float speed) { 
        transform.position += Vector3.forward * speed;
    }
}"""

result = await compile_preflight("Assets/Scripts/Player.cs", new_code)
# -> "OK preflight (156ms)"  (can now safely write)
# -> "ERR preflight\nerror CS0103 at line 5: ..." (fix first)
```

Preflight can reject syntax and reference errors before writing a file and
triggering a Unity compile cycle.

---

## execute_code

Execute C# code in the Unity Editor via Roslyn without creating a persistent
script asset. Bare statements are auto-wrapped in a static class.

**Security:** Scanning depends on the selected security level. The default **AllowAll** level does not block these APIs; **Standard** and **Strict** apply progressively stronger checks. See the [Code Execution Guide](../features/code-execution.md#security-levels).

**Parameters:**
- `code` (string) — C# code to execute (bare statements, no class wrapper needed)
- `undo_label` (string, default="execute_code") — Label for Unity Undo group

**Output:** Return value from the executed code, or error message.

**Example:**

```python
# Create a GameObject
result = await execute_code('var go = new GameObject("Test"); return go.name;')

# Query scene state
result = await execute_code('return FindObjectOfType<Camera>().orthographic.ToString();')

# Modify component values
result = await execute_code("""
var rb = GameObject.Find("Player").GetComponent<Rigidbody>();
rb.mass = 5f;
return $"mass={rb.mass}";
""")
```

---

## diagnose

Lightweight non-blocking diagnostics. Reads Unity compile/reload fact-signals atomically and returns a single typed verdict.

**Parameters:**
- `prev_mvid` (string, default="") — MVID from before a sync operation. Enables `STALE-DOMAIN` detection when provided.
- `expected_compile` (bool, default=true) — Set to `false` for cache-hit/will_compile=false probes to prevent false `STALE-DOMAIN` on legitimately-frozen MVID.

**Verdicts:**

| Verdict | Meaning |
|---------|---------|
| `CLEAN-LIVE` | All signals green, MVID determined, no errors |
| `FAIL:<CS>` | Compile errors found (CS code or 'unknown') |
| `STALE-DOMAIN` | MVID unchanged after intended recompile |
| `WEDGE-ENGINE` | iscompiling=true + cn_active=false + stamp_frozen |
| `WEDGE-STATE` | sync_state=compiling but compile=idle |
| `BUILD-FAILED-WEDGE` | Log shows failed reload + guard keeps rejecting |
| `STALE-CACHE` | Disk-fixed CS error not yet reimported |
| `TESTS-INVISIBLE` | Tests dll unknown(missing) |
| `REBUILDING` | All dlls missing, mid-rebuild |
| `NO-OP` | idle-never, idle-stale, or MVID frozen (no compile expected) |
| `UNKNOWN` | Connection error or undetermined stamp |

**Example:**

```python
# Standalone probe
verdict = await diagnose()

# After sync with MVID tracking
verdict = await diagnose(prev_mvid="abc123", expected_compile=True)
```

---

## sync_unity

Unified Unity reload: trigger Refresh (+ optional Resolve), wait for new code to be live.

**Parameters:**
- `resolve` (bool, default=false) — Call Client.Resolve() first (use after package.json change)
- `bump` (bool, default=false) — Atomically increment plugin patch version before sync; implies `resolve=True`. Circuit-breaker: one bump per session.
- `timeout` (float, default=session timeout) — Max seconds to wait for convergence

**Returns:** `"sync clean"` / compile errors / timeout message / `"REIMPORT-NEEDED"`.

**Example:**

```python
# Basic sync after code changes
result = await sync_unity()

# After package.json change
result = await sync_unity(resolve=True)

# Force version bump + resolve + sync
result = await sync_unity(bump=True)
```

---

## alias_status

Check alias table health: loaded/empty/stale, sources, and total alias count.

**Parameters:** None

**Returns:** Status of the alias expander cache.

**Example:**

```python
status = await alias_status()
```

---

## mcp_status

Compact MCP status: scene, dirty, play/compile state, port, alias count.

**Parameters:** None

**Returns:** One-line status summary.

**Example:**

```python
status = await mcp_status()
```

---

## release_smoke

Run release readiness checks: status, aliases, compile. Returns PASS/FAIL summary.

**Parameters:** None

**Returns:** `PASS` or `FAIL` header + per-check lines.

**Example:**

```python
result = await release_smoke()
# -> "PASS\nstatus: ok\naliases: ok\ncompile: ok"
```

---

## list_connections

Show current TCP connection status.

**Parameters:** None

**Output:** Single line with port and state.

**Example:**

```python
status = await list_connections()
# -> "port 9500 (connected)"
# -> "port 9500 (disconnected)"
```

---

## reconnect_unity

Explicitly reconnect to Unity via TCP.

**Parameters:**
- `port` (int, default=0) — Port to connect to (0 = auto-discover)

**Port discovery waterfall:**
1. Explicit `port` param (if > 0)
2. `UNITY_MCP_PORT` env var
3. Live `.port` file whose recorded project path best matches `UNITY_MCP_PROJECT_DIR`, `CLAUDE_PROJECT_DIR`, or the current working directory
4. Newest live `.port` file
5. Default: 9500

**Example:**

```python
# Auto-discover port and reconnect
await reconnect_unity()

# Manual port
await reconnect_unity(port=9501)
```

---

## Troubleshooting Decision Tree

```
Commands hanging or timing out?
+-- Run: doctor()
+-- If errors: doctor(fix=True)
+-- Check console: get_console(level="Error,Exception,Assert")
+-- Check compile: get_compile_errors()
+-- If compiling: await_compile(timeout=30)
+-- Diagnose: diagnose()  # typed verdict
+-- If disconnected: reconnect_unity()
+-- Still broken? doctor(fix=True), then reconnect_unity()
```

## Common Issues & Fixes

| Issue | Check | Fix |
|-------|-------|-----|
| "Commands hang after 30s" | `get_compile_errors()` | Wait for compile: `await_compile()` |
| "Connection refused" | `list_connections()` | Restart Unity or `reconnect_unity()` |
| "Tests fail but compile clean" | `diagnose()` | Check verdict; if STALE-DOMAIN: `sync_unity(bump=True)` |
| "Reconnect spam (9+ attempts)" | `doctor(fix=True)` | Clean stale port files |
| "Wrong port when multi-instance" | `ls ~/.unity-biome-mcp/ports/` | Set explicitly: `export UNITY_MCP_PORT=9501` |

## Connection Diagnostics Workflow

```python
# 1. Start session
result = await doctor()
if "failed" in result:
    await doctor(fix=True)

# 2. Before tests
errors = await get_compile_errors()
if "error CS" in errors:
    print(f"Cannot test: {errors}")
    exit(1)

# 3. Gate on compile
compile_result = await await_compile(timeout=30)
if "clean" not in compile_result:
    print(f"Compile failed: {compile_result}")
    exit(1)

# 4. Run tests with correlated request/run identity
result = await run_tests_wait(mode="EditMode")
print(f"Tests: {result}")
```

For Unity Biome MCP repository/full-suite verification, replace the final MCP
call with the standalone durable `run_unity_tests.py` command documented in
[Testing Reliability](../testing-reliability.md).

---

**See also:** [Getting Started Troubleshooting](../getting-started/index.md) for common connection issues.
