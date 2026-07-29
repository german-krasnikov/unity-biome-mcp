# Code Intelligence Tools

Roslyn-based C# code analysis: fast symbol lookup, preflight compilation checks, semantic queries, and compile status monitoring.

**Phase A (current):** Python tool wrappers. **Phase B (deferred):** C# Roslyn implementation in Unity Editor.

**Pre-Phase-B behavior:** Tools raise ToolError ("Command not registered") if Roslyn not available — fail-safe by design.

## ~~find_references~~ (REMOVED)

**Status:** Removed — no C# handler was ever shipped. Use `grep` or `search_scene` instead.

**Parameters:**
- `symbol`: Name to search (required)
- `kind`: Disambiguator — class|field|method|property|param|local|namespace (optional)
- `scope`: Assembly name (empty = all assemblies)

**Output Format:**
```
SYMBOL: MyClass
  Assets/Scripts/PlayerController.cs:25:10
  Assets/Scripts/GameManager.cs:40:5
```

**Responses:**
- `SYMBOL: X` + file:line:col list
- `AMBIGUOUS [kind=class, kind=method, ...]` (need kind to disambiguate)
- `NOT FOUND [candidates: X, Y, Z]` (typo? suggestions provided)
- `[ROSLYN UNAVAILABLE]` (Phase B C# not yet loaded)

**Cost:** ~200ms if warm Roslyn workspace, first call may cold-start (~1-2s).

**Example:**
```python
await find_references("MoveTo", kind="method")
# → SYMBOL: MoveTo
#     Assets/Scripts/PlayerController.cs:15:10
#     Assets/Scripts/GameManager.cs:42:15
```

**Timeout:** 10s.

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

## ~~semantic_at~~ (REMOVED)

**Status:** Removed — no C# handler was ever shipped. Use source reading instead.

**Parameters:**
- `file_path`: Assets-relative
- `line`: 1-based
- `col`: 1-based

**Output Format:**
```
kind: method
name: MoveTo
signature: public void MoveTo(Vector3 position)
namespace: UnityMCP.Editor
decl: Assets/Scripts/PlayerController.cs:15:5
members: (none for method)
```

**Responses:**
- `kind: X` + full info block
- `NO SYMBOL at file:line:col` (whitespace or comment)
- `[ROSLYN UNAVAILABLE]`

**Use Case:** Jump-to-definition, hover info, refactoring decisions.

**Timeout:** 10s (cold Roslyn workspace may need longer).

**Example:**
```python
await semantic_at("Assets/Scripts/Player.cs", 15, 10)
# → kind: class
#   name: PlayerController
#   namespace: UnityMCP.Editor.Game
#   decl: Assets/Scripts/PlayerController.cs:10:5
```

## execute_code(code, undo_label="")

**Purpose:** Safely execute inline C# scripts in the Unity Editor with sandbox protection against code generation attacks.

**Parameters:**
- `code`: C# code snippet to execute (string, required)
- `undo_label`: Undo group label (optional)

**Output Format:**
```
OK: Operation complete
```

**On Error:**
```
ERR: Security: blocked pattern 'System.Reflection.Emit'. Only UnityEngine/UnityEditor APIs allowed.
```

**Responses:**
- `OK: Operation complete` (code executed successfully)
- `Security: blocked pattern 'X'...` (security check failed; pattern blocked)
- Other runtime errors from code execution

**Use Case:** Execute editor-only automation, modify scene/objects, trigger recompile, manipulate assets — within sandbox.

**Timeout:** 30s (typical execution).

**Example:**
```python
result = await execute_code("""
    var player = GameObject.Find("Player");
    player.SetActive(true);
""")
# → OK: Operation complete
```

### Security Constraints

CodeExecutor enforces **whitelist-only execution model**: all code must compile via Roslyn and pass SecurityScan checks. Three attack classes are blocked:

#### 1. **CodeDom Blocking** (Dynamic Code Generation)

**Why blocked:** CodeDom allows runtime compilation and code generation — could compile & execute arbitrary code, bypassing the scanner.

**Blocked patterns:**
- `System.CodeDom.*` (any CodeDom type)
- `System.Reflection.Emit.*` (IL generation)
- `CSharpCodeProvider`, `CodeDomProvider`, `CompileAssemblyFrom`

**What happens if triggered:** RuntimeError: `"Security: blocked pattern 'System.CodeDom'. Only UnityEngine/UnityEditor APIs allowed."`

**Blocked code example:**
```csharp
// BLOCKED — CodeDom code generation
using System.CodeDom.Compiler;
var provider = new CSharpCodeProvider();
var code = "public class X { }";
provider.CompileAssemblyFromSource(new CompilerParameters(), code);
```

**Allowed alternative:**
```csharp
// OK — use execute_code recursively via Python MCP layer (safe)
// Python side validates & executes; no dynamic compilation in C#
```

#### 2. **Reflection.Emit Blocking** (IL Injection)

**Why blocked:** Reflection.Emit builds methods at runtime via IL instructions — attacker could generate any executable bytecode.

**Blocked patterns:**
- `System.Reflection.Emit.*` namespace
- `DynamicMethod` constructor
- `ILGenerator` (IL instruction builder)
- `OpCodes.*` (IL instruction set)
- `CreateDelegate` (bind IL to delegate)

**What happens if triggered:** RuntimeError: `"Security: blocked pattern 'System.Reflection.Emit'. Only UnityEngine/UnityEditor APIs allowed."`

**Blocked code example:**
```csharp
// BLOCKED — IL generation via Reflection.Emit
var dm = new DynamicMethod("Hack", typeof(void), Type.EmptyTypes);
var il = dm.GetILGenerator();
il.Emit(OpCodes.Ldc_I4, 42);
var func = (Action)dm.CreateDelegate(typeof(Action));
func.Invoke(null);
```

**Allowed alternative:**
```csharp
// OK — invoke existing safe methods only
var obj = new MyClass();
obj.DoWork();  // safe instance call
```

#### 3. **GetRuntimeMethod + DynamicInvoke Blocking** (Reflection Tricks)

**Why blocked:** GetRuntimeMethod + DynamicInvoke can invoke hidden/private methods and bypass type safety. Combined with Emit above, could execute arbitrary code.

**Blocked patterns:**
- `GetRuntimeMethod` (bypass type safety)
- `DynamicInvoke` (invoke without signature check)
- `GetMethod()`, `GetMethods()` (reflection enumeration)
- `Type.GetType()` (dynamic type lookup)

**What happens if triggered:** RuntimeError: `"Security: blocked pattern 'GetRuntimeMethod'. Only UnityEngine/UnityEditor APIs allowed."`

**Blocked code example:**
```csharp
// BLOCKED — invoke private method via reflection
var type = Type.GetType("SomeClass");
var method = type.GetRuntimeMethod("HiddenMethod", Type.EmptyTypes);
method.Invoke(null, new object[] { });

// Also BLOCKED
var obj = GetSomeObject();
var invoke = obj.GetType().GetMethod("Foo", System.Reflection.BindingFlags.NonPublic);
invoke.DynamicInvoke();
```

**Allowed alternative:**
```csharp
// OK — call public methods directly
var obj = GetSomeObject();
obj.PublicMethod();  // safe, type-checked
```

#### 4. **Comment/Whitespace Bypass Prevention**

Scanner strips comments and normalizes whitespace to defeat obfuscation:

```csharp
// BLOCKED — comment split attempt
System. /* hiding from scanner */
Reflection.Emit.DynamicMethod

// BLOCKED — newline split
System.\
Reflection.Emit

// BLOCKED — using alias
using E = System.Reflection.Emit;
E.DynamicMethod
```

All three are caught by `StripComments()` + `Regex.Replace(@"\s+", "")` + case-insensitive IndexOf.

#### Summary Table

| Attack Class | Blocked | Detection | Error Message |
|---|---|---|---|
| CodeDom | `System.CodeDom*`, `CSharpCodeProvider` | StringMatch in SecurityScan | "blocked pattern 'System.CodeDom'" |
| Reflection.Emit | `System.Reflection.Emit*`, `DynamicMethod`, `OpCodes` | StringMatch in SecurityScan | "blocked pattern 'System.Reflection.Emit'" |
| GetRuntimeMethod | `GetRuntimeMethod`, `DynamicInvoke`, `GetMethod(` | StringMatch in SecurityScan | "blocked pattern 'GetRuntimeMethod'" |
| Obfuscation | Comments, whitespace, using-aliases | StripComments + Dense regex | Caught before scan |

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
[force_refresh → wait 15s → diagnose]
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
| Find all usages of method X | grep / search_scene | Rename safety |
| Validate .cs before write | compile_preflight(file_path, new_content) | 200ms vs 30s cycle |
| Reload after script edit | force_refresh → diagnose | Confirms the new assembly loaded |
| Check compile once | await_compile(timeout=0) | Public immediate status path |

## Errors & Recovery

| Error | Cause | Fix |
|-------|-------|-----|
| "[ROSLYN UNAVAILABLE]" | Phase B C# not loaded | Write only after source review, then use the reload-recovery workflow |
| "AMBIGUOUS [kind=...]" | Symbol name matches multiple types | Retry with kind parameter (e.g., kind="method") |
| "timeout after 60s" | Very large project or network lag | Increase timeout; check get_compile_errors for actual status |
| "STALE-DOMAIN: stamp unchanged" | MVID not updated after reload | Unity stalled; see .claude/skills/reload-recovery.md for T-ladder |
| "CS0246: Type not found" | preflight error in new code | Check import statements; verify assembly references |

## Invocation Order

**Recommended sequence for feature implementation:**

1. Read source (understand current code)
2. Prepare the complete new file content in memory
3. `compile_preflight(file_path, new_content)` before writing
4. If preflight fails, fix the in-memory content and retry
5. Write to disk
6. `force_refresh`, wait 15 seconds, then run `diagnose`
7. If diagnostics disagree with Editor behavior, inspect `get_console`, then
   `Editor.log`
8. Run the focused test filter

Do not run Unity tests between a `.cs` edit and `force_refresh`; that can execute
the previous DLL.

---

**Related:** `AI/architecture.md` (Roslyn workspace setup), `.claude/skills/reload-recovery.md` (T-ladder for stale domain), `CLAUDE.md` § compile workflow.
