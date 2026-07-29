# Feature: Serializers

## Overview

Serialization of Unity data into compact text format:
- **HierarchySerializer**: scene → compact text tree
- **ComponentSerializer**: components → key-value (90% vs JSON)
- **ConsoleCapture**: logs → formatted text
- **ScreenshotCapture**: window → base64 PNG

## Architecture (for Architect)

### Output Format (default, components=false)

**Single scene:**
```
Main Camera $a
Directional Light $b
GameManager $c
├─ UIRoot $d
│  ├─ HealthBar $e
│  └─ PauseMenu $f !
Player $g
├─ Body $h
└─ WeaponSlot $i
   └─ Sword $j
```

**Multiple scenes (with headers):**
```
[MainScene]
Main Camera $a
Directional Light $b
GameManager $c
├─ UIRoot $d

[AdditiveScene]
Player $e
├─ Body $f
└─ WeaponSlot $g
   └─ Sword $h
```

### Output Format (components=true)

**Single scene:**
```
Main Camera [Camera,AudioListener] $a
Directional Light [Light] $b
GameManager [GameManager,AudioSource] $c
├─ UIRoot [Canvas,CanvasScaler] $d
│  ├─ HealthBar [Image,HealthBarUI] $e
│  └─ PauseMenu [CanvasGroup] $f !
Player [Rigidbody,PlayerController] $g
├─ Body [SkinnedMeshRenderer] $h
└─ WeaponSlot [] $i
   └─ Sword [MeshFilter,MeleeWeapon] $j
```

**Multiple scenes:**
```
[MainScene]
Main Camera [Camera,AudioListener] $a
Directional Light [Light] $b

[AdditiveScene]
Player [Rigidbody,PlayerController] $c
├─ Body [SkinnedMeshRenderer] $d
```

### Format Rules

| Element | Format | Example |
|---------|--------|---------|
| Scene header (multi-scene) | `[SceneName]` (single scene = no headers) | `[MainScene]` |
| Duplicate scene names | disambiguate with parent dir | `[Scene (Assets/Scenes)]` |
| Unsaved scenes | mark as (unsaved) | `[Scene (unsaved)]` |
| Name | plain text | `Main Camera` |
| Short ref | `$a`-`$zz` (702 slots via RefManager) | `$a`, `$ab` |
| Components | `[Type1,Type2]` (only when `components=true`) | `[Camera,AudioListener]` |
| Transform | OMITTED (100% have it) | - |
| Inactive | `!` suffix | `PauseMenu ... !` |
| Depth-truncated | `+N` descendant count | `WeaponSlot $i +3` |
| Tree chars | `├─ └─ │` | Unicode box drawing |
| MAX_NODES | 3000, truncates with message | `... truncated at 3000 nodes` |
| Sibling truncation | when MAX_NODES hit mid-children | `... +5 siblings` |

### Compression Mode (compress=True)

Groups consecutive slot_N/point_N sequences into compact summaries:
- `WeaponSlot $i [5 slots]` instead of listing all 5 children
- Reduces token count for large hierarchies (~60–100 tokens for 50-object scene)
- Usage: `get_hierarchy(compress=True)`

### Summary Mode (summary=True)

Root-only overview with per-scene node counts. Useful for quick navigation.
- Output: `[MainScene] 1200 nodes, [AdditiveScene] 45 nodes`
- Token cost: 60–100 tokens
- Usage: `get_hierarchy(summary=True)`

### Scene Filter (scene="SceneName")

Isolates serialization to a single scene in multi-scene projects.
- Usage: `get_hierarchy(scene="MainScene")`
- Omit to serialize all loaded scenes

### Token Budget

- 50 objects: ~350 tokens (vs ~4000 JSON)
- Compact text mode omits repeated structure and default values

## Implementation Notes (for Developer)

### HierarchySerializer.cs

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    public static class HierarchySerializer
    {
        public const int MAX_NODES = 3000;

        public static string Serialize(int depth = 99, string root = null, string filter = null, bool components = false)
        {
            var sb = new StringBuilder();
            int nodeCount = 0;

            if (string.IsNullOrEmpty(root))
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                {
                    SerializeObject(sb, stage.prefabContentsRoot, depth, 0, new List<bool>(), true, filter, ref nodeCount, components);
                }
                else
                {
                    var scenes = GetAllLoadedSceneRoots();
                    bool multi = scenes.Count > 1;
                    foreach (var (name, roots) in scenes)
                    {
                        if (nodeCount >= MAX_NODES) break;
                        if (multi)
                        {
                            int headerPos = sb.Length;
                            sb.AppendLine($"[{name}]");
                            int beforeCount = nodeCount;
                            for (int i = 0; i < roots.Length && nodeCount < MAX_NODES; i++)
                                SerializeObject(sb, roots[i], depth, 0, new List<bool>(), i == roots.Length - 1, filter, ref nodeCount, components);
                            if (nodeCount == beforeCount) sb.Length = headerPos; // remove phantom header
                        }
                        else
                        {
                            for (int i = 0; i < roots.Length && nodeCount < MAX_NODES; i++)
                                SerializeObject(sb, roots[i], depth, 0, new List<bool>(), i == roots.Length - 1, filter, ref nodeCount, components);
                        }
                    }
                }
            }
            else
            {
                var roots = GetSubtreeRoots(root);
                for (int i = 0; i < roots.Length && nodeCount < MAX_NODES; i++)
                    SerializeObject(sb, roots[i], depth, 0, new List<bool>(), i == roots.Length - 1, filter, ref nodeCount, components);
            }

            if (nodeCount >= MAX_NODES)
                sb.AppendLine($"... truncated at {MAX_NODES} nodes. Use filter/root/depth to narrow.");

            return sb.ToString();
        }

        // Key per-node logic (simplified):
        // 1. AppendIndent(sb, parentIsLast, isLast)  — parent continuation lines + connector
        // 2. sb.Append(go.name)
        // 3. if (components) → sb.Append(" [Type1,Type2]")  — Transform omitted, no space after comma
        // 4. sb.Append(' ').Append(RefManager.Assign(go))   — short ref ($a-$zz) instead of instance ID
        // 5. if (!go.activeSelf) sb.Append(" !")
        // 6. if (depth-truncated && has children) sb.Append(" +").Append(descendantCount)
        // 7. Recurse children; at MAX_NODES mid-children → "... +N siblings"

        // Multi-scene support: GetAllLoadedSceneRoots() returns all loaded scenes
        // When 2+ scenes loaded: emit [SceneName] headers; duplicate names disambiguate with parent dir
        // When 1 scene: no headers (zero overhead)
        // Phantom header removal: if multi-scene but scene had no matching objects, remove the header line

        // Incremental cache: SerializeIncremental() returns "NO_CHANGE" if hierarchy unchanged
        // Summary mode: SerializeSummary() returns compact root-level overview + scene stats
        // Subtree: SerializeSubtree(go, depth) for single object
        // Helper: GetAllLoadedSceneRoots() → List<(name, roots[])> with dedup logic

        internal static List<(string name, GameObject[] roots)> GetAllLoadedSceneRoots()
        {
            var raw = new List<(Scene scene, GameObject[] roots)>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                // Exclude DontDestroyOnLoad virtual scene (runtime only, has no path)
                if (!s.isLoaded || s.name == "DontDestroyOnLoad") continue;
                raw.Add((s, s.GetRootGameObjects()));
            }
            if (raw.Count == 0)
            {
                var active = SceneManager.GetActiveScene();
                return new List<(string, GameObject[])> { (active.name, active.GetRootGameObjects()) };
            }
            // Detect duplicate names — disambiguate with directory path
            var nameCount = new Dictionary<string, int>();
            foreach (var (s, _) in raw)
            {
                nameCount.TryGetValue(s.name, out var c);
                nameCount[s.name] = c + 1;
            }
            var result = new List<(string, GameObject[])>();
            foreach (var (s, roots) in raw)
            {
                string label;
                if (nameCount[s.name] > 1)
                    label = string.IsNullOrEmpty(s.path)
                        ? $"{s.name} (unsaved)"
                        : $"{s.name} ({System.IO.Path.GetDirectoryName(s.path)})";
                else
                    label = s.name;
                result.Add((label, roots));
            }
            return result;
        }

        private static GameObject[] GetSubtreeRoots(string rootPath)
        {
            var root = ComponentSerializer.FindObject(rootPath);
            return root != null ? new[] { root } : new GameObject[0];
        }
    }
}
```

### Version Tracking

```csharp
[InitializeOnLoad]
public static class VersionTracker
{
    private static int _version = 0;

    static VersionTracker()
    {
        EditorApplication.hierarchyChanged += IncrementVersion;
        Undo.undoRedoPerformed += IncrementVersion;
    }

    public static int Version => System.Threading.Volatile.Read(ref _version);

    private static void IncrementVersion()
    {
        System.Threading.Interlocked.Increment(ref _version);
    }
}
```

### Alias System (PlaytestConfig Integration)

`BuildAliasSection()` in `CommandRouter.ObjectHandlers.cs` reads aliases from the first `PlaytestConfig` ScriptableObject found via `AssetDatabase.FindAssets("t:PlaytestConfig")`:
- Returns `--- ALIASES ---\nname=path|comp|field\n---` block, or `null` when no config / no aliases
- Each alias line: `alias=path|component|field` (pipe-separated, no `$` prefix)

**`get_hierarchy` does NOT prepend this block** — it was removed in Wave 3. Python `strip_alias_block()` in `middleware_alias.py` is a safety net only (strips if C# re-introduces it).

**`get_aliases` TCP command** (read-only, `allowedDuringCompile=true`): returns bare `name=value` lines via `GetAliasesText()`. No header/footer, just one alias per line. Returns `"no aliases"` when config absent or alias list empty. Used by Python middleware to seed `_alias_cache` on demand without triggering a hierarchy call.

```
# get_aliases response example
hp=Player/Health|HealthComponent|m_HP
speed=/Player|Rigidbody|m_Velocity
```

### Incremental Cache

`SerializeIncremental()` stores last serialized result. Returns `"NO_CHANGE"` if hierarchy is identical.
`ResetIncrementalCache()` clears stored state.
No Python-side cache — all calls go to Unity via `bridge.send("get_hierarchy", {...})`.

### ScenePathParser Struct (v0.31.0+)

Multi-scene path parsing utility: handles `"SceneName:/"` prefix extraction.
- Prevents hallucination of hand-built scene paths
- Used internally by ComponentSerializer + HierarchySerializer
- Public static method: `ScenePathParser.ExtractSceneName(path) → (sceneName, path)`

### RefManager Ring (702 Slots)

Ephemeral short references ($a–$zz) valid within a single scene serialization.
- Slot allocation: wraps around at 702 (eviction on wrap)
- Prune() on refresh: clears all slots on hierarchy change
- Thread-safe: lock-based access
- Useful for quick object reference in LLM prompts

## Code Locations

**C#:**
- `unity-plugin/Editor/HierarchySerializer.cs` — Scene → text tree (MAX_NODES=3000, summary mode, incremental cache, $refs)
- `unity-plugin/Editor/ComponentSerializer.cs` — Component → key-value + ObjectReference + UnityEvent
- `unity-plugin/Editor/VersionTracker.cs` — Version tracking
- `unity-plugin/Editor/ConsoleCapture.cs` — Logs → text (orchestrates bounded ring buffer + SessionState-persisted problem entries)
- `unity-plugin/Editor/ScreenshotCapture.cs` — Camera modes: default, overview, overview_game, multi_view
- `unity-plugin/Editor/MultiViewCapture.cs` — 4-panel grid (Front, Left, Top, Isometric)
- `unity-plugin/Editor/MultiViewOverlay.cs` — Overlay rendering for multi-view
- `unity-plugin/Editor/OverlayDrawer.cs` — Drawing utilities
- `unity-plugin/Editor/RefManager.cs` — Short refs ($a-$zz, 702 slots)

## ComponentSerializer (427 lines)

### Output Format

```
name: Player
active: true
tag: Player
layer: Default
---
[Transform]
m_LocalPosition: (1.2, 3.4, 5.6)
m_LocalRotation: (0, 0.7, 0, 0.7)
m_LocalScale: (1, 1, 1)
---
[Rigidbody]
m_Mass: 1
m_Drag: 0
m_UseGravity: true
```

**Features:**
- Uses `SerializedProperty` API — serializes all visible fields via `prop.name: value`
- Separator `: ` (colon-space), sections separated by `---`
- Component headers: `[TypeName]`
- Handles all built-in types: int, float, bool, string, Vector3, Color, Enum, ObjectReference, etc.
- **Float format**: ToString("G4") — 4 significant figures (for example, 1.234 or 0.005678)
- Null reference → `null`
- Arrays → element-per-line with `[i]` indices
- Skips internal Unity properties (m_Script, m_ObjectHideFlags, etc.)
- Compact key-value output instead of repeated JSON structure

### Usage

```csharp
string text = ComponentSerializer.Serialize("/Player", "Rigidbody");
```

## ConsoleCapture + Console Buffers (Issue 27)

**Files (3-file split for domain-reload survival):**
- `ConsoleCapture.cs` (183 lines) — Public query API + wiring between ring buffer and problem persistence
- `ConsoleRingBuffer.cs` (141 lines) — Bounded in-memory log capture: init phase (50 entries, 5s window) + fixed-size ring (450 entries)
- `ConsoleProblemPersistence.cs` (144 lines) — SessionState-persisted problem-type entries (Error/Exception/Assert) across domain reload

### Output Format

```
[Log] 14:30:22.123 Log message here
[Warning] 14:30:22.456 Warning message
[Error] 14:30:22.789 Error message
[Exception] 14:30:23.100 Unhandled exception
[Assert] 14:30:23.200 Assertion failed
```

**Features:**
- Captures Unity debug logs via `Application.logMessageReceived`
- Dual-buffer capture:
  - **Init buffer** (ConsoleRingBuffer): 50 entries, 5-second post-load window — captures initial setup spam
  - **Ring buffer** (ConsoleRingBuffer): 450 entries, wraps on overflow
  - **SessionState persistence** (ConsoleProblemPersistence): stores Error/Exception/Assert entries across domain reload (FIFO cap: 20 entries)
- Problem-level convention: `PROBLEM_LEVELS = Error,Exception,Assert` (per `console_levels.py`) — not just Error alone
  - Unhandled C# exceptions arrive as `LogType.Exception`, not Error
  - Assertions fail as `LogType.Assert`
- `count=-1` returns all; `first=N` returns first N from init + last from ring
- `GetErrorsSince(DateTime since)` for post-playtest problem checks (filters by PROBLEM_LEVELS)
- Thread-safe (lock-based)

### Usage

```csharp
var logs = ConsoleCapture.GetLogs(count: 20, level: "Error,Exception,Assert");
var problems = ConsoleCapture.GetErrorsSince(startTime, maxCount: 5); // catches all problem types
ConsoleCapture.Clear();
```

## ScreenshotCapture

### Output Format

Base64-encoded PNG or file path.

**Features:**
- Camera modes: `default`, `overview`, `overview_game`, `multi_view`
- Custom width/height, supersample (1-4x)
- `multi_view`: 4-panel grid (Front, Left, Top, Isometric) of target object
  - Params: path, cellSize, custom angles, zoom, offset, fixed_size, highlight
  - Returns file path + optional manifest (for highlight markers)
- `overview`: Top-down scene capture
- `highlight`: Draw markers on specific objects
- Saves to file or returns base64

## TDD Scenarios (for Developer)

### Implemented Tests (all passing)

**HierarchySerializer** (Unity plugin, no Python tests for C# code):
- Serialize empty scene → empty string
- Serialize single object → correct format
- Components only shown when `components=true`
- Omit Transform from component list
- Short refs via RefManager (`$a`-`$zz`)
- Mark inactive objects with `!`
- Depth limiting works + `+N` descendant count suffix
- MAX_NODES=3000 truncation + sibling truncation
- Tree characters render correctly `├─ └─ │`
- Incremental cache returns `NO_CHANGE` when unchanged
- Summary mode (`SerializeSummary`)
- Version tracking increments on hierarchy change (thread-safe)
- **Multi-scene support (new, v0.23.23):**
  - Single scene: no scene headers emitted
  - Multiple loaded scenes: `[SceneName]` headers for each
  - Duplicate scene names: disambiguate with parent directory path
  - Unsaved scenes: mark as `(unsaved)` instead of path
  - Phantom header removal: if scene matches filter but has no objects, remove header
  - Root param overrides multi-scene (single object subtree, no headers)
  - `GetAllLoadedSceneRoots()` excludes DontDestroyOnLoad virtual scene
  - SerializeSummary works with multi-scene + per-scene node counts

**ComponentSerializer**:
- Serialize built-in types (Vector3, Color, Quaternion)
- Handle null references
- Array serialization
- Nested object handling

**ConsoleCapture**:
- Capture debug logs
- Filter by level
- Limit to last N entries
- Preserve timestamps

**ScreenshotCapture**:
- Render viewport to texture
- Export as PNG
- Support custom dimensions

## Review Checklist (for Reviewer)

- [ ] Transform always omitted from component list
- [ ] Tree chars render correctly
- [ ] Short refs ($a-$zz) via RefManager, not instance IDs
- [ ] Inactive objects marked with `!`
- [ ] Components only shown when `components=true`
- [ ] Depth limiting works + `+N` descendant suffix
- [ ] MAX_NODES=3000 truncation works
- [ ] Version increments on hierarchy change (thread-safe)
- [ ] Prefab stage detection in Serialize
- [ ] **Multi-scene checks (v0.23.23):**
  - [ ] Single scene: no `[SceneName]` headers
  - [ ] Multiple scenes: headers present for each scene
  - [ ] Duplicate names: dir path or `(unsaved)` appended to label
  - [ ] Phantom headers: removed if scene has no matching objects
  - [ ] Root param: overrides multi-scene (no headers)
  - [ ] DontDestroyOnLoad: excluded from scene list
  - [ ] Summary mode: shows per-scene node counts
- [ ] **Multi-scene API hygiene (skill: `.claude/skills/multi-scene.md`):**
  - [ ] No raw `SceneManager.sceneCount > 1` — use `SceneContext.Current.IsMulti`
  - [ ] No hand-built `sceneName + ":/" + path` — use `ComponentSerializer.GetPath(go)`
  - [ ] Scene iteration uses `SceneContext.Current.Scenes`, not raw `SceneManager.GetSceneAt(i)`
  - [ ] Each multi-scene test has a single-scene regression counterpart

## Related

- Skill: `.claude/skills/token-optimization.md`
- Knowledge: `AI/tcp-bridge.md`
