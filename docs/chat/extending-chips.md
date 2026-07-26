# Extending Chip Kinds in In-Unity Chat

This guide shows how to add custom chip kinds to the in-Unity agent chat, enabling third-party plugins to define domain-specific object types with custom display, AI payload formatting, and navigation.

## Quick Start

### 1. Create an Assembly Definition

Create a new `.asmdef` in your plugin folder:

```json
{
  "name": "MyPlugin.Chat",
  "references": ["UnityMCP.Editor.Chat"],
  "autoReferenced": false
}
```

**Key points:**
- Reference `UnityMCP.Editor.Chat` (which provides `IChipKindProvider` + `ChipKindRegistry`)
- Set `autoReferenced: false` to avoid interfering with other assemblies

### 2. Implement IChipKindProvider

```csharp
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace MyPlugin.Chat
{
    [InitializeOnLoad]
    internal sealed class CustomAssetChipProvider : IChipKindProvider
    {
        // Static ctor runs on domain-load
        static CustomAssetChipProvider() => ChipKindRegistry.Register(new CustomAssetChipProvider());

        // Unique identifier for this kind
        public string Key => "custom_asset";

        // Lower numbers = checked first during detection
        // Built-ins use 40–700 (Asset fallback = int.MaxValue); plugin overrides use <40, extensions use >700
        public int Priority => 900;

        // Return true if this provider handles the object
        public bool CanHandle(Object obj, string assetPath)
        {
            // Example: detect custom asset type by extension
            return !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".myasset");
        }

        // Create a chip for the object (called once per drag-drop)
        public ChipData Create(Object obj, string assetPath)
        {
            return new ChipData(
                kindKey: Key,
                path: assetPath,
                displayName: obj.name,
                instanceID: 0  // Assets don't have instance IDs
            );
        }

        // Icon displayed in the chip pill
        // Use EditorGUIUtility.IconContent(name).image names
        public string IconName => "d_Prefab Icon";  // Any Unity icon name works

        // Hex color for the pill background and response tag
        public string HexColor => "#ff9500";

        // Default depth when user hasn't configured one in settings
        // Options: "none", "path", "summary", "full"
        // "none" = omit from AI context entirely
        // "path" = just the file path (token-efficient)
        // "summary" = path + 3 top-level properties (common)
        // "full" = path + all serialized state (expensive)
        public string DefaultDepth => "path";

        // File extensions that should be recognized as bare-path references of this kind
        // in assistant responses (e.g., "img.png" -> [image:img/png]).
        // Return System.Array.Empty<string>() if this kind has no bare-path form.
        public string[] BarePathExtensions => new[] { ".myasset" };

        // Format the AI-facing payload
        // Called with resolved summary (if depth includes it)
        // Return empty string to omit from context entirely
        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            // ctx.Depth tells us what the user configured (or DefaultDepth if unconfigured)
            // ctx.ResolvedSummary (if depth="summary"|"full") has pre-resolved component list

            if (ctx.Depth == "none")
                return "";  // Omit entirely

            if (ctx.Depth == "path")
                return $"[{Key}:{chip.Path}]";

            if (ctx.Depth == "summary" || ctx.Depth == "full")
            {
                // Include summary if available
                var summary = !string.IsNullOrEmpty(ctx.ResolvedSummary)
                    ? "\n" + ctx.ResolvedSummary
                    : "";
                return $"[{Key}:{chip.Path}]{summary}";
            }

            return "";
        }

        // Handle a click on a chip link in the transcript
        // reference = the path from the chip linkId (e.g., "Assets/MyAsset.myasset")
        public void Navigate(string reference)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(reference);
            if (asset == null)
            {
                Debug.LogWarning($"[Chat] Asset not found: {reference}");
                return;
            }
            AssetDatabase.OpenAsset(asset);
        }

        // Highlight/ping the referenced object when an inline preview is first shown.
        // Usually the same as Navigate, but without opening a dedicated editor window.
        public void Ping(string reference)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(reference);
            if (asset == null) return;
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        // Append custom menu items to the chip's context menu (right-click on transcript chip).
        // This allows plugins to add actions beyond the default Navigate/Ping/Copy options.
        public void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Custom Action", action =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(reference);
                if (asset != null)
                {
                    Debug.Log($"Custom action invoked on {asset.name}");
                }
            });
        }
    }
}
```

Your plugin will auto-register on domain load and appear in chip detection.

## API Reference

### IChipKindProvider Members

| Member | Type | Purpose |
|--------|------|---------|
| `Key` | `string` (property) | Unique identifier, must match `^[a-z0-9_]+$`. Used in linkId format: `chip:KEY:REF` |
| `Priority` | `int` (property) | Detection order (lower = earlier). Built-ins 40–700; Asset fallback = int.MaxValue |
| `CanHandle(obj, assetPath)` | `bool` (method) | Return true if this provider recognizes the object |
| `Create(obj, assetPath)` | `ChipData` (method) | Construct a `ChipData` for drag-drop and context |
| `IconName` | `string` (property) | EditorGUIUtility.IconContent key (e.g., `"d_Prefab Icon"`) |
| `HexColor` | `string` (property) | RGB hex color for pill, e.g. `"#4a9eff"` |
| `DefaultDepth` | `string` (property) | Fallback context depth: `"none"`, `"path"`, `"summary"`, or `"full"` |
| `BarePathExtensions` | `string[]` (property) | Extensions recognized as bare-path refs in responses (e.g., `{ ".png" }`) |
| `FormatPayload(chip, ctx)` | `string` (method) | Render AI-facing bracket text. Return `""` to omit. |
| `Navigate(reference)` | `void` (method) | Handle click on a chip link (e.g., open asset, select object) |
| `Ping(reference)` | `void` (method) | Highlight/ping object when inline preview first shown |
| `AppendContextMenuItems(menu, reference)` | `void` (method) | Add custom context menu items to chip (e.g., "Custom Action"). Called on right-click in transcript. |

### ChipKindRegistry Public API

```csharp
public static class ChipKindRegistry
{
    // Register a provider (from [InitializeOnLoad])
    public static bool Register(IChipKindProvider p);

    // Unregister by key (rarely used; mainly for testing)
    public static bool Unregister(string key);

    // Find first provider that CanHandle(obj, assetPath) — used in drag-drop detection
    public static IChipKindProvider Resolve(Object obj, string assetPath);

    // Look up by exact key (used for reload-recovery)
    public static IChipKindProvider ForKey(string key);

    // Current version counter (increments on register/unregister)
    public static int Version { get; }

    // All registered keys in priority order
    public static IReadOnlyList<string> AllKeys { get; }
}
```

### ChipData & ChipPayloadContext

```csharp
public readonly struct ChipData
{
    public readonly string KindKey;          // e.g., "custom_asset"
    public readonly string Path;             // File path or object reference
    public readonly string DisplayName;      // Shown in the pill UI
    public readonly int    InstanceID;       // 0 for assets, >0 for scene objects
    public readonly GlobalObjectId GlobalObjectId; // Stable cross-session identity (scene objects)
}

public readonly struct ChipPayloadContext
{
    public readonly string Depth;              // "none" | "path" | "summary" | "full"
    public readonly string ResolvedSummary;    // Pre-resolved component list (if depth includes it)
}
```

## Priority Convention

Use priority to control detection order:

- **<40:** Plugin overrides a built-in (e.g., provide a better detector for a specific asset type)
- **40–700:** Built-in kinds: annotated_screenshot=40, image=50, hierarchy=100, region=120, component=125, field=130, folder=150, scene=200, script=300, prefab=400, model=450, material=500, audio=550, texture=600, so=700, asset=int.MaxValue
- **>700:** Plugin extensions (new kinds not overlapping built-ins)

Example: If you want to extend asset detection, use `Priority = 900`. If you want to override the built-in script handler, use `Priority = 250`.

## Depth Configuration

Users can override `DefaultDepth` per kind via the **F9 Settings Form** (per-backend chip config dropdown). If no user override, your `DefaultDepth` is used.

Custom providers always use their `DefaultDepth`. Built-in kinds have per-kind dropdowns in settings.

## Reload Survival

When Unity domain-reloads:

1. `PendingTurnState` serializes the in-flight chips' `KindKeys[]` to disk
2. On resume, `ChipKindRegistry.ForKey(kindKey)` re-binds each chip
3. If a provider isn't yet registered (e.g., plugin delayed initialization), fallback is automatic re-detection via `Resolve()`

No manual work needed — reload survival is automatic.

## Link Format & Navigation

Chips are serialized as linkIds in the format: `chip:KEY:REF`

- `KEY` = your `IChipKindProvider.Key`
- `REF` = the `ChipData.Path` (or a custom reference string)

When a user clicks a chip link in the transcript, your `Navigate(reference)` is called with the `REF` portion. Use it to open files, select objects, or run domain-specific actions.

**Example:** For a custom code-snippet chip:
```csharp
public void Navigate(string reference)
{
    var snippet = CodeSnippetDatabase.Get(reference);
    if (snippet != null)
        CodeEditor.Open(snippet.FilePath, snippet.LineNumber);
}
```

## Testing

Use `ChipKindRegistry.ResetToBuiltIns()` in `[SetUp]` to clear registered plugins between test cases:

```csharp
[Test]
public void CustomProvider_CanHandle_CustomAsset()
{
    ChipKindRegistry.ResetToBuiltIns();
    var provider = new CustomAssetChipProvider();
    ChipKindRegistry.Register(provider);

    Assert.IsTrue(provider.CanHandle(null, "Assets/MyAsset.myasset"));
    Assert.IsFalse(provider.CanHandle(null, "Assets/Texture.png"));
}
```

(This method is available only in test assemblies via `#if UNITY_INCLUDE_TESTS`.)

## Full Example: Domain-Specific Configuration Chip

```csharp
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace MyGame.Chat
{
    [InitializeOnLoad]
    internal sealed class GameConfigChipProvider : IChipKindProvider
    {
        static GameConfigChipProvider() => ChipKindRegistry.Register(new GameConfigChipProvider());

        public string Key => "game_config";
        public int Priority => 850;
        public string IconName => "d_Settings";
        public string HexColor => "#fbbf24";
        public string DefaultDepth => "summary";

        public bool CanHandle(Object obj, string assetPath)
            => obj is ScriptableObject && assetPath.Contains("GameConfig");

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            if (ctx.Depth == "none") return "";
            if (ctx.Depth == "path") return $"[{Key}:{chip.Path}]";

            var header = $"[{Key}:{chip.Path}]";
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(chip.Path);
            if (config == null) return header;

            var details = $"\ndifficultyLevel={config.difficultyLevel}\n" +
                         $"maxPlayers={config.maxPlayers}\n" +
                         $"enableAI={config.enableAI}";
            return header + details;
        }

        public void Navigate(string reference)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameConfig>(reference);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        public void Ping(string reference) => Navigate(reference);

        public void AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string reference)
        {
            menu.AppendAction("Open Config", _ => Navigate(reference));
        }
    }
}
```

## Troubleshooting

**"Duplicate key 'X' — keeping first registration"**

Two providers registered with the same `Key`. Check for name collisions across plugins. The registry keeps the first one registered and logs a warning.

**Plugin not appearing in chips**

1. Ensure `[InitializeOnLoad]` static ctor calls `ChipKindRegistry.Register(this)` exactly once
2. Check Console for warnings from ChipKindRegistry
3. Verify `CanHandle()` logic is correct

**Chip not navigating**

If clicking a chip doesn't open anything:
1. Check your `Navigate(reference)` for errors (add Debug.Log to debug)
2. Verify the reference format matches what `Create()` produces
3. Add Debug.LogWarning in Navigate when object is not found
