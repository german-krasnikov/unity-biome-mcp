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

## Undo Integration

```python
await execute_code("""
Undo.RecordObject(player, "custom edit");
player.health = 50;
""", undo_label="set health")
```

**undo_label:** Optional. Groups changes into one undo action with custom name.

**Automatic undo:** Any script changes are batched under "execute_code" label if not specified.

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
await execute_code("GameObject.Find('Player').SetActive(false)")
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

Return strings or serializable types:

```python
result = await execute_code("""
var obj = GameObject.Find("Player");
return obj.transform.position.ToString();
""")
# → "(5.0, 0.0, 3.0)"
```

**Types that serialize:**
- int, float, bool, string, Vector3, Quaternion
- Color, Bounds, Rect
- GameObject (as instance ID)

## Play Mode Notes

`execute_code()` works in both Edit and Play Mode. For structured runtime field changes, prefer `set_property()` instead of inline code—it works in both modes and handles Play Mode via reflection automatically.

```python
# Edit Mode: full access
await execute_code("GameObject.Find('Player').SetActive(false)")

# Play Mode: structured tool preferred
await set_property("/Player", "PlayerController", "Health", "50")  # transparently rerouted
```

## Timeout & Performance

- **Timeout:** Unity enforces a 25-second execution ceiling
- **Long operations:** Split into multiple calls
- **Compilation:** First call warm (~500ms); cached after

```python
# Time-consuming: split it
for i in range(100):
    await execute_code(f'/* process batch {i} */')
```

---

**See also:** [Object Tools](../tools/objects.md) for scene editing APIs, [Batch Reference](../tools/batch.md) for batching strategies.
