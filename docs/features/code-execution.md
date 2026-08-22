# Code Execution Guide

Execute C# scripts directly in the Unity Editor.

## Overview

`execute_code()` runs arbitrary C# code in the Editor. The default **AllowAll**
level skips the source-pattern scan; **Standard** and **Strict** scan for blocked
patterns before execution. Use it for complex mutations that do not fit simple
tool parameters.

## Basic Usage

```python
# Simple script
await execute_code("""
var player = GameObject.Find("Player");
if (player != null) player.SetActive(false);
""")

# With return value
result = await execute_code("""
var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
return lights.Length.ToString();
""")
# → number of Light components in the loaded scenes
```

## Editor API Access

At the **AllowAll** level, code can access:

| API | Example |
|-----|---------|
| GameObject API | `GameObject.Find()`, `Object.Instantiate()` |
| Transform | `transform.position`, `SetParent()` |
| Physics | `Physics.Raycast()`, `Physics2D.OverlapArea()` |
| Components | `GetComponent<>()`, `AddComponent<>()` |
| AssetDatabase | `AssetDatabase.LoadAssetAtPath()` |
| EditorApplication | Scene queries, serialization |
| Debug | `Debug.Log()`, `Debug.DrawRay()` |

## Security Levels

Code execution uses a three-tier security system. The active level is set via the **Security Level** dropdown in **MCP → Settings** (Unity Editor).

| Level | Default? | Behavior |
|-------|----------|----------|
| **AllowAll** | **Yes** | Skips the source-pattern scan; compilation, timeout, read-only, and command guards still apply. |
| **Standard** | No | Moderate scanning — blocks filesystem, network, reflection, process, and editor-exit patterns. |
| **Strict** | No | Densest scanning — everything in Standard plus `GetField()`, `GetProperty()`, `GetFields()`, `GetProperties()`. |

> **Important:** The default level is **AllowAll**, which bypasses all security scanning. If you need protection against accidental destructive calls, switch to **Standard** or **Strict** in **MCP → Settings**.

### Blocked Patterns (Standard and Strict only)

These patterns are **not checked** when the level is AllowAll.

**Tier 1 — blocked in Standard and Strict:**
- Filesystem: `System.IO.File`, `System.IO.Directory`, `System.IO.Stream`, `FileStream`, `StreamWriter`, `StreamReader`, `System.IO.Path`, `FileUtil.`
- Network: `System.Net.`, `WebClient`, `HttpClient`
- Process/Environment: `System.Diagnostics.Process`, `Environment.Exit`, `Environment.SetEnvironmentVariable`, `Environment.GetEnvironmentVariable`
- Reflection/Dynamic: `Assembly.Load`, `AppDomain`, `DllImport`, `System.Reflection.Assembly`, `Type.GetType`, `.GetMethod(`, `GetRuntimeMethod`, `DynamicInvoke`, `Activator`, `System.Linq.Expressions.Expression`, `GetMethods(`, `CreateDelegate`, `GetTypes(`, `GetMembers(`, `GetConstructors(`, `.Assembly`, `System.Reflection.Emit`, `DynamicMethod`, `ILGenerator`, `OpCodes`, `CSharpCodeProvider`, `CodeDomProvider`, `CompileAssemblyFrom`, `InvokeMember(`
- Threading: `System.Threading`, `System.Runtime.InteropServices`
- Editor-exit: `EditorApplication.Exit`, `Application.Quit`, `Environment.FailFast`, `EditorApplication.isPlaying`, `EditorApplication.isPaused`
- Editor-destructive: `AssetDatabase.ExportPackage`, `AssetDatabase.ImportPackage`, `EditorApplication.OpenProject`, `ProjectWindowUtil`
- Namespace guards: `using System.Diagnostics`, `using System.IO`, `using System.Net`, `using System.Reflection`, and aliases assigned from those namespaces (for example, `IO = System.IO`)
- Keywords (word-boundary): `extern`, `unsafe`

**Tier 2 — blocked in Standard and Strict:**
- `.GetValue(`, `.SetValue(`, `.Invoke(`

**Tier 3 — blocked in Strict only (additional):**
- `GetField(`, `GetProperty(`, `GetFields(`, `GetProperties(`

When a pattern is blocked, the error message includes a suggestion where available (e.g., "Use `SerializedObject.FindProperty()` instead of `GetField(`").

## Code Quality of Life Features

The code wrapper automatically handles three common patterns:

**Void snippets → object return:**
```python
# Before: bare `return;` would fail (CS0161: void method must return)
await execute_code("""
Debug.Log("done");
return;
""")
# After: `return;` is auto-replaced with `return null;` (compiles successfully)
```

**Using directives auto-hoisted:**
```python
await execute_code("""
using System.Text;
var sb = new StringBuilder();
sb.Append("hello");
return sb.ToString();
""")
# Using statements automatically moved above the class wrapper, no manual hoist needed.
```

**UnityEngine.Object alias auto-injected:**
```python
await execute_code("""
var list = new List<Object>();  // Resolves to UnityEngine.Object
""")
# `using Object = UnityEngine.Object;` is auto-included in every script.
```

## Type Persistence (Mutation Mode)

When **Mutation Mode** is enabled, use `persist_as` to store compiled types for
reuse across multiple `execute_code` calls. This eliminates per-call compilation
overhead:

```python
# Call 1: compile and store type "Handler"
await execute_code(
    code="""
    public class Handler {
        public static void Execute() { Debug.Log("handler executed"); }
        public static int Value { get; set; } = 42;
    }
    """,
    persist_as="Handler"
)
# → returns "compile clean"

# Call 2: Handler is available; recompile only this code (100ms vs 5s)
result = await execute_code("return Handler.Value.ToString();")
# → "42"

# Call 3: execute Handler without any new code
result = await execute_code("Handler.Execute(); return null;")
# → "null"

# Clear types when done
await clear_held_types()
```

**Without `persist_as`:** Each call compiles fresh (full domain reload, 15-30s).

**With `persist_as`:** First call is normal speed; subsequent calls see the persisted
types and compile much faster (100-300ms). Use this pattern for rapid iteration
during development.

`persist_as` is ignored when Mutation Mode is disabled.

## Undo Integration

```python
await execute_code("""
var player = GameObject.Find("Player");
if (player == null) return "Player not found";
Undo.RecordObject(player.transform, "move player");
player.transform.position = Vector3.zero;
return "Player moved";
""", undo_label="move player")
```

`undo_label` names the Unity Undo group. The wrapper does not automatically make
arbitrary C# reversible: the script must use `Undo.RecordObject`,
`Undo.RegisterCreatedObjectUndo`, or another appropriate Unity Undo API before
each mutation. File, package, and external-process side effects are not covered.

## Error Handling

Compilation errors stop execution:

```python
# This fails (Player not defined)
await execute_code("Player.SetActive(false);")
# → ERROR: The name 'Player' does not exist in the current context
```

**Solution:** Use GameObject.Find() or instantiate from prefabs:

```python
# Correct
await execute_code("""
var player = GameObject.Find("Player");
if (player == null) return "Player not found";
player.SetActive(false);
return "Player disabled";
""")
```

## Common Patterns

**Batch property changes:**
```python
await execute_code("""
var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
Undo.RecordObjects(lights, "clamp light intensity");
foreach (var light in lights) {
    light.intensity = Mathf.Max(0f, light.intensity);
}
return $"checked={lights.Length}";
""")
```

**Prefab instantiation:**
```python
# Project-specific asset path: replace it with a prefab that exists.
result = await execute_code("""
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
if (prefab == null) return "Prefab not found";
var instance = Object.Instantiate(prefab, new Vector3(5, 0, 0), Quaternion.identity);
Undo.RegisterCreatedObjectUndo(instance, "instantiate enemy prefab");
instance.name = "Enemy_1";
return instance.GetInstanceID().ToString();
""")
```

**Query and modify:**
```python
await execute_code("""
var colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
Undo.RecordObjects(colliders, "enable colliders");
foreach (var col in colliders) {
    if (!col.enabled) col.enabled = true;
}
""")
```

## Return Values

The static wrapper returns an object and the bridge sends its `ToString()` value
(`null` becomes the string `null`):

```python
result = await execute_code("""
var obj = GameObject.Find("Player");
return obj == null ? "Player not found" : obj.transform.position.ToString();
""")
# → "(5.0, 0.0, 3.0)" when Player exists
```

Return an explicit string when the output format matters. Complex objects are
not JSON-serialized automatically.

## Play Mode Notes

`execute_code()` is callable in both Edit and Play Mode. Runtime scene and object
state normally disappears when Play Mode stops. AssetDatabase operations,
filesystem writes, package changes, and project settings can persist, so Play
Mode is not a rollback boundary. Prefer a runtime-specific tool or the Playtest
DSL for gameplay state.

```python
# Edit Mode: full access
await execute_code('var player = GameObject.Find("Player"); if (player != null) player.SetActive(false);')

# Play Mode: structured runtime method preferred; names below are project-specific.
await invoke_method("/Player", "PlayerController", "SetHealth", args="50")  # runtime reflection
```

## Timeout & Performance

- **MCP request timeout:** 60 seconds
- **Editor impact:** execution is synchronous in the Unity Editor; avoid long or
  unbounded work
- **Compilation:** the Roslyn compiler load is reused after warm-up, but every
  submitted snippet is compiled separately

```python
# Keep the work bounded and return a compact result.
result = await execute_code("""
var count = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
return $"colliders={count}";
""")
```

---

**See also:** [Object Tools](../tools/objects.md) for scene editing APIs, [Batch Reference](../tools/batch.md) for batching strategies.
