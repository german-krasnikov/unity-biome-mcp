# Code Intelligence Tools

Roslyn-backed C# preflight, execution, and compile-state tools. The current
public surface in `server/src/unity_mcp/tools/code_intel.py` is
`compile_preflight`, `await_compile`, and `serialized_field_rename_audit`.

`find_references` and `semantic_at` were removed because no Unity handler was
ever shipped for them. Use repository source search for code symbols;
`search_scene` searches Unity scene objects, not C# source.

## compile_preflight(file_path, new_content)

**Purpose:** Validate C# WITHOUT writing/recompiling (Roslyn) — catches typos in ~200ms vs 30s Unity cycle.

**Parameters:**
- `file_path`: Assets-relative (e.g., "Assets/Scripts/Player.cs")
- `new_content`: Full file content (string)

**Output Format:**
```
OK preflight (143ms)
```

**On Error:**
```
ERR preflight
error CS0103 at line 15: The name 'Health' does not exist in the current context
error CS0246 at line 8: Type 'PlayerController' not found
```

**Responses:**
- `OK preflight (Xms)` (no errors)
- `ERR preflight` + error list (all diagnostics printed)
- `[ROSLYN UNAVAILABLE]`

**Use Case:** Before Write tool to catch obvious bugs, then write once (saves iteration).

**Timeout:** 15s.

**Example:**
```python
new_code = """public class Player : MonoBehaviour {
    public void Move(float speed) { 
        transform.position += Vector3.forward * speed;
    }
}"""
result = await compile_preflight("Assets/Scripts/Player.cs", new_code)
# → OK preflight (156ms)
```

Preflight can also append Unity-specific `WARN:` hints for unsupported serialized
dictionaries, interface/abstract serialized fields, and likely field renames
without `FormerlySerializedAs`.

## serialized_field_rename_audit(type, old_field, new_field, include=...)

**Purpose:** Check whether a serialized field rename is ready to finish after
the new assembly is live. The default `include` value is
`"prefabs,scenes,scriptable_objects"`; pass a comma-separated subset to limit
the scan.

```python
await serialized_field_rename_audit(
    type="PlayerStats",
    old_field="health",
    new_field="currentHealth",
)
```

The response reports `has_formerly_serialized_as`, matching stale assets,
`safe_to_remove_attribute`, and recommended actions. Results are capped at 100
asset paths. Scene scanning matches the serialized field name without a type
filter, so treat scene matches as conservative candidates and inspect them
before changing the attribute.

## execute_code(code, undo_label="execute_code")

**Purpose:** Compile and execute inline C# in the Unity Editor. Bare statements
are wrapped automatically, and the operation participates in an Undo group.
The active security level controls source scanning; it is not an OS sandbox.

**Parameters:**
- `code`: C# code snippet to execute (string, required)
- `undo_label`: Undo group label (optional)

**Output Format:**
```
OK: Operation complete
```

**On Error:**
```
Security [Standard]: blocked pattern 'System.Reflection.Emit'. Only UnityEngine/UnityEditor APIs allowed.
```

**Responses:**
- `OK: Operation complete` (code executed successfully)
- `Security: blocked pattern 'X'...` (security check failed; pattern blocked)
- Other runtime errors from code execution

**Use Case:** Execute trusted editor automation when a purpose-built MCP tool is
not available.

**Timeout:** The tool metadata supplies a 60-second outer deadline.

**Example:**
```python
result = await execute_code("""
    var player = GameObject.Find("Player");
    player.SetActive(true);
""")
# → OK: Operation complete
```

### Security Levels

`CodeExecutor.SecurityScan` reads the setting from `MCPSettings.GetSecurityLevel()`:

| Level | Behavior |
|---|---|
| `AllowAll` | Current default. Skips source-pattern scanning. This is trusted local code execution, not a sandbox. |
| `Standard` | Blocks process, file, network, dynamic-code, unsafe reflection, editor-exit, and related patterns, including reflective `GetValue`/`SetValue`/`Invoke`. |
| `Strict` | Applies Standard and additionally blocks field/property reflection lookup. |

Standard and Strict strip comments, normalize whitespace, compare patterns case-insensitively, and reject `extern` and `unsafe` as whole words. Error responses name the active level and may include a safer Unity API suggestion. The exact policy is authoritative in `unity-plugin/Editor/CodeExecutor.cs`; user-facing configuration is documented in `docs/features/code-execution.md`.

Changing the setting does not turn `execute_code` into process isolation. Treat every call as a scene/code mutation, provide a meaningful `undo_label`, and use purpose-built tools when one exists.

---

## Compile Status & Await

### Immediate status check

`compile_status` is an internal TCP command used by `await_compile`; it is not a public MCP tool. For one non-polling check, call:

```python
await await_compile(timeout=0)
```

This returns `"still compiling"` for an active compile/reload or the corroborated compile result for terminal states.

### await_compile(timeout=60.0)

**Purpose:** Block until compile + reload finish, return errors (if any).

**Timeout Semantics:**
- `timeout=0` → single check, no loop (immediate return)
- `timeout=60` → poll every 1s, up to 60s

**Output:**
```
compile clean (8.2s)
```

**On Error:**
```
error CS0103: The name 'Projectile' does not exist...
```

**Special Cases:**
- Epoch-aware: Tracks sync_status epoch to detect stale domain (MVID unchanged after recompile)
- Domain reload: Transparently retries on ConnectionError
- Fallback: Uses compile_status if sync_status unavailable

**Returns:**
- `compile clean (Xs)` (no errors)
- `compile clean (sync)` (via epoch tracking)
- `STALE-DOMAIN: stamp unchanged after reload` (MVID not updated)
- `compile failed (...) + error list`
- `timeout after Xs — compile still in progress`

**Timeout:** 60s default (increase for large projects or network latency).

**Example:**
```python
# After writing .cs files:
result = await await_compile(timeout=30.0)
if "clean" in result:
    print("Ready to test")
else:
    print(f"Compile errors: {result}")
```

## Compile Workflow Diagram

```
[Prepare complete .cs content in memory]
    ↓
[compile_preflight (fast check)]  ← ~200ms, catch typos
    ↓
[Write to disk]
    ↓
[sync_unity (refresh, compile wait, and recovery ladder)]
    ↓
[Cross-check get_console / Editor.log when needed]
    ↓
[Run tests / continue]
```

Preflight can reject syntax and reference errors before writing a file and
triggering a Unity compile cycle.

## Common Patterns

| Pattern | Tool | Why |
|---------|------|-----|
| Find all usages of method X | Repository source search | Rename safety; `search_scene` is not a source-code search |
| Validate .cs before write | compile_preflight(file_path, new_content) | 200ms vs 30s cycle |
| Reload after script edit | sync_unity | Waits until the new assembly is live or returns an actionable stop verdict |
| Check compile once | await_compile(timeout=0) | Public immediate status path |

## Errors & Recovery

| Error | Cause | Fix |
|-------|-------|-----|
| "[ROSLYN UNAVAILABLE: ...]" | Roslyn assemblies could not be loaded | Inspect the detailed loader error and the plugin's Roslyn package state |
| "timeout after 60s" | Very large project or network lag | Increase timeout; check get_compile_errors for actual status |
| "STALE-DOMAIN: stamp unchanged" | MVID not updated after reload | Unity stalled; see `AI/reload-reference.md` for reload constraints |
| "CS0246: Type not found" | preflight error in new code | Check import statements; verify assembly references |

## Invocation Order

**Recommended sequence for feature implementation:**

1. Read source (understand current code)
2. Prepare the complete new file content in memory
3. `compile_preflight(file_path, new_content)` before writing
4. If preflight fails, fix the in-memory content and retry
5. Write to disk
6. Run `sync_unity` and require a clean result
7. If the result is unexpected, run `diagnose`, then inspect `get_console` and
   `Editor.log`
8. Run the focused test filter

Do not run Unity tests between a `.cs` edit and a successful `sync_unity`; that
can execute the previous DLL.

---

**Related:** `AI/architecture.md` (Roslyn workspace setup),
`AI/reload-reference.md` (reload constraints), and `CONTRIBUTING.md` (current
compile/test commands).
