---
name: csharp-unity
description: C# Unity Editor API patterns for the Unity Biome MCP plugin. Load when writing or reviewing C# Editor code in unity-plugin/Editor/. Covers: InitializeOnLoad lifecycle, HierarchyChanged event, SerializedObject update/apply pattern, main-thread dispatch via ConcurrentQueue, EditorPrefs persistence across domain reloads, CancellationTokenSource safety on Mono (ObjectDisposedException), shutdown guard (_shuttingDown volatile), InitializeOnEnterPlayMode, EditorWindow setup, and a quick-reference table of common Unity Editor pitfalls and their fixes.
user-invocable: false
---

# C# Unity Editor API

## InitializeOnLoad

```csharp
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MCPServer  // namespace UnityMCP.Editor
{
    static MCPServer()
    {
        // Вызывается при: загрузке Unity, компиляции, Play Mode
        EditorApplication.update += ProcessMainThreadQueue;
        EditorApplication.quitting += Stop;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
    }
}
```

## HierarchyChanged Event

```csharp
[InitializeOnLoad]
public static class VersionTracker  // thread-safe
{
    private static int _version = 0;

    static VersionTracker()
    {
        EditorApplication.hierarchyChanged += IncrementVersion;
        Undo.undoRedoPerformed += IncrementVersion;
    }

    public static int Version => System.Threading.Volatile.Read(ref _version);
    private static void IncrementVersion() => System.Threading.Interlocked.Increment(ref _version);
}
```

## SerializedObject (правильно)

```csharp
// Паттерн: Update → Modify → Apply
serializedObject.UpdateIfRequiredOrScript();

var prop = serializedObject.FindProperty("speed");
prop.floatValue = 10f;

serializedObject.ApplyModifiedProperties(); // Undo работает автоматом
```

## Main Thread Dispatch

```csharp
using System.Collections.Concurrent;

public static class MainThreadDispatcher
{
    private static readonly ConcurrentQueue<Action> _queue = new();

    static MainThreadDispatcher()
    {
        EditorApplication.update += ProcessQueue;
    }

    public static void Enqueue(Action action) => _queue.Enqueue(action);

    private static void ProcessQueue()
    {
        while (_queue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}

// Из background thread:
MainThreadDispatcher.Enqueue(() => Selection.activeObject = obj);
```

## Альтернатива: delayCall

```csharp
// Для одноразовых действий
EditorApplication.delayCall += () => {
    // Выполнится на main thread
    Selection.activeGameObject = someObject;
};
```

## Domain Reload Survival

### EditorPrefs Persistence (actual project pattern)

```csharp
// MCPSettings is an EditorWindow, persists via EditorPrefs
public class MCPSettings : EditorWindow
{
    private const string KeyPrefix = "UnityMCP_Tool_";

    public static bool IsToolEnabled(string toolName) =>
        EditorPrefs.GetBool(KeyPrefix + toolName, true);

    // Port from env var or default 9500
    private static int GetPort() =>
        int.TryParse(Environment.GetEnvironmentVariable("UNITY_MCP_PORT"), out var p) ? p : 9500;
}
```

### InitializeOnEnterPlayMode (disabled domain reload)

```csharp
public class StatefulClass
{
    private static int s_Counter = 0;

    [InitializeOnEnterPlayMode]
    static void Reset(EnterPlayModeOptions options)
    {
        if (options.HasFlag(EnterPlayModeOptions.DisableDomainReload))
            s_Counter = 0; // Сброс вручную
    }
}
```

## EditorWindow

```csharp
public class MCPWindow : EditorWindow
{
    [SerializeField] private string _savedState; // Выживет domain reload

    [MenuItem("Window/MCP Server")]
    public static void ShowWindow() => GetWindow<MCPWindow>("MCP");

    private void OnEnable()
    {
        // Восстановление после domain reload
    }

    private void OnHierarchyChange()
    {
        Repaint();
    }
}
```

## Shutdown Guard (_shuttingDown volatile)

`ProcessMainThreadQueue` MUST check `_shuttingDown` before executing — otherwise commands run
during Unity shutdown/domain reload when Editor API is in undefined state:

```csharp
private static volatile bool _shuttingDown;

private static void ProcessMainThreadQueue()
{
    if (_shuttingDown) return;  // REQUIRED — skip execution during shutdown
    while (_mainThreadQueue.TryDequeue(out var action))
    {
        try { action(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}

// Also guard enqueued lambdas themselves:
_mainThreadQueue.Enqueue(() =>
{
    if (_shuttingDown || cmdTimeout.IsCancellationRequested) return;
    CommandRouter.ProcessAsync(json, tcs);
});
```

## CTS + Domain Reload Safety (Cycle 13)

**Root cause of production crash:** `CreateLinkedTokenSource` registers a callback on the parent
token. If the child CTS is already cancelled/disposed before the parent calls `.Cancel()`, Mono
throws `ObjectDisposedException` from within the callback. Always wrap BOTH cancels:

```csharp
// BAD — Mono throws ObjectDisposedException if _clientCts already cancelled
_clientCts.Cancel();
_cts.Cancel();

// GOOD — both wrapped independently
try { _clientCts?.Cancel(); } catch { }
try { _cts?.Cancel(); } catch { }
try { _clientCts?.Dispose(); } catch { }
_clientCts = null;
try { _cts?.Dispose(); } catch { }
_cts = null;
```

Capture token locally BEFORE the async loop — `_cts` becomes null after `Stop()`:

```csharp
_cts = new CancellationTokenSource();
var token = _cts.Token; // local copy — safe from null after Stop()/OnBeforeReload()
while (!token.IsCancellationRequested) { ... }
```

## Типичные ошибки

| Ошибка | Решение |
|--------|---------|
| `MissingReferenceException` | Проверять `obj != null` |
| GUI вне OnGUI | `EditorApplication.delayCall` |
| Static field потерян | `[InitializeOnLoad]` |
| Static НЕ сбросился (Play Mode) | `[InitializeOnEnterPlayMode]` |
| Static НЕ сбросился (domain reload) | `[InitializeOnLoadMethod]` + reset method |
| TCP callback не работает | `ConcurrentQueue` + dispatch |
| CTS dispose crash (Mono) | `try { cts?.Cancel(); } catch { }` для ОБОИХ CTS |
| Commands run during shutdown | `if (_shuttingDown) return;` в ProcessMainThreadQueue |
| Listener stale после Stop | `_listener = null` после `_listener?.Stop()` |

## See Also

- `.claude/skills/testing-tdd.md` — NUnit patterns, EditMode/PlayMode, temp assets
- `.claude/skills/unity-debugging.md` — diagnosis workflows, console reading
- `.claude/skills/unity-components.md` — component setup, references

## Sources

- [InitializeOnLoad](https://docs.unity3d.com/ScriptReference/InitializeOnLoadAttribute.html)
- [EditorApplication.hierarchyChanged](https://docs.unity3d.com/ScriptReference/EditorApplication-hierarchyChanged.html)
- [SerializedObject](https://docs.unity3d.com/ScriptReference/SerializedObject.html)
- [Domain Reloading](https://docs.unity3d.com/Manual/domain-reloading.html)
