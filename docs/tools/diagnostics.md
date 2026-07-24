# Diagnostics & Connection Tools

Troubleshoot connection issues, inspect errors, and monitor compilation. Essential for debugging when commands hang or fail.

## doctor

Comprehensive health check with optional auto-fix.

**Parameters:**
- `fix` (bool, default=false) — Auto-clean stale port files and retry connection

**Checks (5 total):**

| Check | Tests | Auto-fix? |
|-------|-------|-----------|
| `python_version` | Python ≥ 3.10 | ❌ Install Python 3.10+ |
| `port_file` | ~/.unity-biome-mcp/ports/*.port exist + PIDs alive | ✅ Remove stale files |
| `lockfile` | ~/.unity-biome-mcp/*.lock holds live PID | ✅ Clean stale files |
| `tcp_connection` | 127.0.0.1:port reachable + responds | ⚠️ Reconnect attempt only |
| `unity_state` | Editor.log accessible + recent activity | ⚠️ Diagnose compile/reload wedge |

**Output:**

```
✅ All checks passed
  Python: 3.12.1 ✓
  Port file: ~/.unity-biome-mcp/ports/1234.port (PID 1234 alive) ✓
  Lockfile: ~/.unity-biome-mcp/1234.lock ✓
  TCP connection: port 9500 ✓
  Unity state: compile clean ✓
```

**On Error:**

```
❌ 1 check failed
  Python: 3.12.1 ✓
  Port file: FAIL — stale PID 9999 (dead)
  Lockfile: ✓
  TCP connection: FAIL — cannot reach 127.0.0.1:9500
  Unity state: ⚠️ compiling (8.2s elapsed)
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

Check if C# compilation has errors. Gates test execution.

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

**Use Cases:**
- Gate playtest execution (never run tests if compile fails)
- Catch typos before write cycles
- Verify clean state before pushing code

---

## get_console

Read Unity Console output (errors, warnings, logs).

**Parameters:**
- `level` (string, optional) — "error" | "warning" | "log" (default: all). Note: "error" catches **Error logs only**. For comprehensive problem detection (including Exception and Assert, where most C# runtime crashes land), use `level="Error,Exception,Assert"` per the PROBLEM_LEVELS convention.
- `count` (int, default=10) — Number of lines to return
- `first` (int, default=0) — If > 0, return first N from init buffer + last (count-first) from ring

**Output:** Console lines with timestamps.

**Format:**
```
[12:34:56] ERROR: NullReferenceException: Object reference not set to an instance
  at UnityEngine.Transform.get_position()
[12:34:57] ERROR: ...
[12:35:01] WARNING: Mesh 'PlayerMesh' was not created
[12:35:02] LOG: Game started
```

**Example:**

```python
# All console output
console = await get_console()

# Error logs only (excludes Exception/Assert)
errors = await get_console(level="error")

# All problem types (Error + Exception + Assert)
problems = await get_console(level="Error,Exception,Assert")

# Last 10 lines
recent = await get_console(count=10)
```

**Use Cases:**
- Check for runtime exceptions after playtest
- Verify "compile clean" state before tests
- Debug why commands hung (check for infinite loops)

---

## await_compile

Block until C# compilation and domain reload finish.

**Parameters:**
- `timeout` (float, default=60.0) — Max seconds to wait

**Returns:**
- `"compile clean (X.Xs)"` — Success after N seconds
- `"compile clean (sync)"` — Via epoch tracking
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
await run_tests(mode="EditMode")
```

---

## find_references

Locate all usages of a C# symbol (Category: `advanced`).

**Parameters:**
- `symbol` (string) — Name to search (required)
- `kind` (string, optional) — Disambiguator: "class" | "field" | "method" | "property" | "param" | "local" | "namespace"
- `scope` (string, optional) — Assembly name (empty = all)

**Output Format:**
```
SYMBOL: MoveTo
  Assets/Scripts/PlayerController.cs:15:10
  Assets/Scripts/GameManager.cs:42:15
  Assets/Editor/Tests.cs:8:5
```

**Responses:**
- `SYMBOL: X` + file:line:col list
- `AMBIGUOUS [kind=class, kind=method, ...]` — Need kind parameter
- `NOT FOUND [candidates: X, Y, Z]` — Typo? Suggestions provided
- `[ROSLYN UNAVAILABLE]` — Phase B C# not yet loaded

**Example:**

```python
# Find all usages of MoveTo method
refs = await find_references("MoveTo", kind="method")

# Find class definition
refs = await find_references("PlayerController", kind="class")

# Rename safety check
refs = await find_references("Health")
# → returns all references to verify rename impact
```

**Use Cases:**
- Verify rename scope before refactoring
- Find all callers of a method
- Audit unused symbols

---

## compile_preflight

Validate C# code without recompiling (fast syntax check).

**Parameters:**
- `file_path` (string) — Assets-relative path (e.g., "Assets/Scripts/Player.cs")
- `new_content` (string) — Full file content to validate

**Output:**
- `"OK preflight (143ms)"` — No errors
- `"ERR preflight"` + error list — Diagnostics found

**Errors listed as:**
```
error CS0103 at line 15: The name 'Health' does not exist in the current context
error CS0246 at line 8: Type 'PlayerController' not found
```

**Example:**

```python
new_code = """public class Player : MonoBehaviour {
    public void Move(float speed) { 
        transform.position += Vector3.forward * speed;
    }
}"""

result = await compile_preflight("Assets/Scripts/Player.cs", new_code)
# → "OK preflight (156ms)"  (can now safely write)
# → "ERR preflight\nerror CS0103 at line 5: ..." (fix first)
```

**Time Savings:** 200ms preflight catch ~50% of errors before 30s write cycle.

---

## semantic_at

Get symbol/type info at a code position (Category: `advanced`).

**Parameters:**
- `file_path` (string) — Assets-relative path
- `line` (int) — 1-based line number
- `col` (int) — 1-based column number

**Output:**
```
kind: method
name: MoveTo
signature: public void MoveTo(Vector3 position)
namespace: UnityMCP.Editor
decl: Assets/Scripts/PlayerController.cs:15:5
```

**Example:**

```python
info = await semantic_at("Assets/Scripts/Player.cs", 15, 10)
# → Returns type info at line 15, column 10
```

---

## sync_unity

Force synchronization and re-probe of Unity state.

**Parameters:** None

**Example:**

```python
await sync_unity()
```

---

## list_connections

Show current TCP connection status.

**Parameters:** None

**Output:** Single line with port and state.

**Example:**

```python
status = await list_connections()
# → "port 9500 (connected)"
# → "port 9500 (disconnected)"
```

---

## reconnect_unity

Explicitly reconnect to Unity via TCP.

**Parameters:**
- `port` (int, default=0) — Port to connect to (0 = auto-discover)

**Port discovery waterfall:**
1. Explicit `port` param (if > 0)
2. `UNITY_MCP_PORT` env var
3. First live `.port` file in `~/.unity-biome-mcp/ports/`
4. Default: 9500

**Example:**

```python
# Auto-discover port and reconnect
await reconnect_unity()

# Manual port
await reconnect_unity(port=9501)

# Multi-instance discovery
import os
ports = []
for f in os.listdir(os.path.expanduser("~/.unity-biome-mcp/ports")):
    try:
        ports.append(int(open(f).read().split("\n")[0]))
    except: pass
print(f"Available ports: {ports}")
```

---

## diagnose

Lightweight non-blocking diagnostics.

**Parameters:** None

**Returns:** Quick summary of connection status, compile state, plugin health.

---

## Troubleshooting Decision Tree

```
Commands hanging or timing out?
├─ Run: doctor()
├─ If errors: doctor(fix=True)
├─ Check console: get_console(level="Error,Exception,Assert")  # Problem-levels convention
├─ Check compile: get_compile_errors()
├─ If compiling: await_compile(timeout=30)
├─ If disconnected: reconnect_unity()
└─ Still broken? try: doctor(fix=True), then reconnect_unity()
```

## Common Issues & Fixes

| Issue | Check | Fix |
|-------|-------|-----|
| "Commands hang after 30s" | `get_compile_errors()` | Wait for compile: `await_compile()` |
| "Connection refused" | `list_connections()` | Restart Unity or `reconnect_unity()` |
| "ROSLYN UNAVAILABLE" | Try fallback tools | Use grep + Read tool instead of find_references |
| "Tests fail but compile clean" | Check for stale DLL | Bump package.json version + force reload |
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

# 4. Run tests
await run_tests(mode="EditMode")

# 5. Poll results
import asyncio
for i in range(24):
    result = await get_test_results()
    if result not in ("pending", "none"):
        print(f"Tests: {result}")
        break
    await asyncio.sleep(5)
```

---

**See also:** [Getting Started Troubleshooting](../getting-started/index.md) for common connection issues.
