# Code Execution Guide

Execute C# scripts directly in the Unity Editor.

## Overview

`execute_code()` runs arbitrary C# code in the Editor. With the default **AllowAll** level it has full access to Unity APIs; **Standard** and **Strict** scan for blocked patterns before execution. Use it for complex mutations that don't fit simple tool parameters.

## Basic Usage

```python
# Simple script
await execute_code("""
var player = GameObject.Find("Player");
player.SetActive(false);
""")

# With return value
result = await execute_code("""
var enemies = FindObjectsOfType<Enemy>();
return enemies.Length.ToString();
""")
# → "3"
```

## Editor API Access

At the **AllowAll** level, code can access:

| API | Example |
|-----|---------|
| GameObject API | `GameObject.Find()`, `Instantiate()` |
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
| **AllowAll** | **Yes** | Skips all pattern scanning. No restrictions beyond compilation and timeout. |
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
- Using guards: `using System.Diagnostics`, `using System.IO`, `using System.Net`, `using System.Reflection`
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

## Undo Integration

```python
await execute_code("""
Undo.RecordObject(player, "custom edit");
player.health = 50;
""", undo_label="set health")
```

`undo_label` names the Unity Undo group. The wrapper does not automatically make
arbitrary C# reversible: the script must use `Undo.RecordObject`,
`Undo.RegisterCreatedObjectUndo`, or another appropriate Unity Undo API before
each mutation. File, package, and external-process side effects are not covered.

## Error Handling

Compilation errors stop execution:

```python
# This fails (Player not defined)
await execute_code("Player.SetActive(false)")
# → ERROR: The name 'Player' does not exist in the current context
```

**Solution:** Use GameObject.Find() or instantiate from prefabs:

```python
# Correct
await execute_code('GameObject.Find("Player").SetActive(false);')
```

## Common Patterns

**Batch property changes:**
```python
await execute_code("""
var objects = FindObjectsOfType<MyComponent>();
foreach (var obj in objects) {
    obj.health = 100;
}
""")
```

**Prefab instantiation:**
```python
result = await execute_code("""
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
var instance = Instantiate(prefab, new Vector3(5, 0, 0), Quaternion.identity);
instance.name = "Enemy_1";
return instance.GetInstanceID().ToString();
""")
```

**Query and modify:**
```python
await execute_code("""
var colliders = FindObjectsOfType<Collider>();
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
return obj.transform.position.ToString();
""")
# → "(5.0, 0.0, 3.0)"
```

Return an explicit string when the output format matters. Complex objects are
not JSON-serialized automatically.

## Play Mode Notes

`execute_code()` is callable in both Edit and Play Mode. Changes made in Play
Mode are runtime changes and normally disappear when Play Mode stops. Prefer a
runtime-specific tool or the Playtest DSL for gameplay state.

```python
# Edit Mode: full access
await execute_code("GameObject.Find('Player').SetActive(false)")

# Play Mode: structured runtime method preferred
await invoke_method("/Player", "PlayerController", "SetHealth", args="50")  # runtime reflection
```

## Timeout & Performance

- **MCP request timeout:** 60 seconds
- **Editor impact:** execution is synchronous in the Unity Editor; avoid long or
  unbounded work
- **Compilation:** First call warm (~500ms); cached after

```python
# Keep the work bounded and return a compact result.
result = await execute_code("""
var count = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
return $"colliders={count}";
""")
```

---

**See also:** [Object Tools](../tools/objects.md) for scene editing APIs, [Batch Reference](../tools/batch.md) for batching strategies.
