# Extend Chat Context Chips

Implement a chip provider when a Unity Editor extension needs a project object
to appear in MCP Chat with custom detection, context formatting, color, and
navigation. Use the built-in asset chip when a custom kind adds no behavior.

## 1. Reference the Chat API

Add an Editor-only assembly definition to the extension:

```json
{
  "name": "MyPlugin.Chat",
  "references": ["UnityMCP.Editor.Chat.CLI"],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

The public extension points are `IChipKindProvider`, `ChipKindRegistry`,
`ChipData`, and `ChipPayloadContext` in the `UnityMCP.Editor.Chat` namespace.

## 2. Implement and Register a Provider

This example recognizes a custom asset extension. It uses an empty `ObjectId`
because project assets are identified by path.

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace MyPlugin.Chat
{
    [InitializeOnLoad]
    public sealed class CustomAssetChipProvider : IChipKindProvider
    {
        static CustomAssetChipProvider()
        {
            ChipKindRegistry.Register(new CustomAssetChipProvider());
        }

        public string Key => "custom_asset";

        // Lower values are checked first. This runs after the ordinary
        // asset-specific providers but before the generic asset fallback.
        public int Priority => 900;

        public string IconName => "d_DefaultAsset Icon";
        public string HexColor => "#ff9500";
        public string DefaultDepth => "path";
        public string[] BarePathExtensions => new[] { ".myasset" };

        public bool CanHandle(Object obj, string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.EndsWith(
                    ".myasset",
                    StringComparison.OrdinalIgnoreCase);
        }

        public ChipData Create(Object obj, string assetPath)
        {
            var displayName = obj != null
                ? obj.name
                : Path.GetFileNameWithoutExtension(assetPath);

            return new ChipData(
                kindKey: Key,
                path: assetPath,
                displayName: displayName,
                objectId: "");
        }

        public string FormatPayload(ChipData chip, ChipPayloadContext context)
        {
            if (context.Depth == "none")
                return "";

            var payload = $"[{Key}:{chip.Path}]";
            if ((context.Depth == "summary" || context.Depth == "full")
                && !string.IsNullOrEmpty(context.ResolvedSummary))
            {
                payload += "\n" + context.ResolvedSummary;
            }
            return payload;
        }

        public void Navigate(string reference)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(reference);
            if (asset == null)
            {
                Debug.LogWarning($"Custom asset not found: {reference}");
                return;
            }
            AssetDatabase.OpenAsset(asset);
        }

        public void Ping(string reference)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(reference);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public void AppendContextMenuItems(
            DropdownMenu menu,
            string reference)
        {
            menu.AppendAction("Open", _ => Navigate(reference));
            menu.AppendAction("Ping in Project", _ => Ping(reference));
        }
    }
}
```

Registration is keep-first by key. `Register` returns `false` and logs a
warning for a duplicate or invalid key; it does not replace the existing
provider.

## Provider Contract

| Member | Contract |
|---|---|
| `Key` | Unique lowercase key matching `^[a-z0-9_]+$` |
| `Priority` | Detection order; lower values run first |
| `CanHandle` | Return `true` only for objects or paths owned by this provider |
| `Create` | Return the kind, reference path, display label, and string object ID |
| `IconName` | Unity `EditorGUIUtility.IconContent` key |
| `HexColor` | Six-digit RGB color such as `#ff9500` |
| `DefaultDepth` | `none`, `path`, `summary`, or `full` |
| `BarePathExtensions` | File extensions eligible for bare-path links in responses |
| `FormatPayload` | Build the model-facing context; return `""` to omit it |
| `Navigate` | Handle a normal click on a chip link |
| `Ping` | Highlight the reference without requiring a dedicated editor |
| `AppendContextMenuItems` | Add provider-specific right-click actions |

### Priority

`ChipKindRegistry.Resolve` checks providers in ascending priority order and
returns the first match. Equal priorities retain registration order. There is
no reserved numeric range for third-party providers: choose a value relative
to the providers that the extension intentionally precedes or follows. The
generic built-in asset fallback uses `int.MaxValue`.

### `ChipData` Identity

The current public shape is:

```csharp
public readonly struct ChipData
{
    public readonly string KindKey;
    public readonly string Path;
    public readonly string DisplayName;
    public readonly string ObjectId;
    public readonly GlobalObjectId GlobalObjectId;
}
```

`ObjectId` is a string, not an `InstanceID` field. Use `""` for path-backed
assets. Scene-object providers may supply the tool-facing object reference and
an optional `GlobalObjectId`; prefer the built-in hierarchy provider unless a
custom scene identity contract is required.

### Context Depth

Users can override depth and color for every registered kind under
**MCP > Settings > Chat Settings > Context Chips**. `DefaultDepth` applies until
an override is saved.

- `none` omits the chip from model context.
- `path` sends only the compact reference.
- `summary` and `full` can append `ResolvedSummary` when the core resolver
  provides one.

Keep `FormatPayload` deterministic and compact. Do not read files or mutate
Unity state from it.

## Registry API

```csharp
bool registered = ChipKindRegistry.Register(provider);
bool removed = ChipKindRegistry.Unregister("custom_asset");
IChipKindProvider match = ChipKindRegistry.Resolve(obj, assetPath);
IChipKindProvider exact = ChipKindRegistry.ForKey("custom_asset");
int version = ChipKindRegistry.Version;
IReadOnlyList<string> keys = ChipKindRegistry.AllKeys;
```

`Version` increments after a successful register or unregister. `AllKeys`
follows detection order.

## Links and Reload Recovery

Transcript chip links use `chip:KEY:REFERENCE`. The registry passes the
`REFERENCE` segment to `Navigate`, `Ping`, and context-menu callbacks.

During an Editor domain reload, Chat stores each pending chip's key and
rebinds it with `ForKey`. If that provider is absent, payload resolution uses
the generic persisted key/path fallback; it does not rerun object detection.
A provider should therefore register from a stable `[InitializeOnLoad]` entry
point and keep its key unchanged across releases.

## Test Without Internal Hooks

Test the provider directly and use the public registry for integration checks.
Do not depend on package-internal test helpers from an extension test assembly.

```csharp
[Test]
public void CustomAssetProvider_CreatesPathBackedChip()
{
    var provider = new CustomAssetChipProvider();

    Assert.IsTrue(provider.CanHandle(null, "Assets/Data/Level.myasset"));
    Assert.IsFalse(provider.CanHandle(null, "Assets/Data/Level.asset"));

    var chip = provider.Create(null, "Assets/Data/Level.myasset");
    Assert.AreEqual("custom_asset", chip.KindKey);
    Assert.AreEqual("", chip.ObjectId);
}
```

Use a unique test key if a test registers a temporary provider, and unregister
that key in `finally` so it cannot leak into another test.

## Troubleshooting

| Symptom | Check |
|---|---|
| Registration returns `false` | Validate `Key` and look for an earlier provider with the same key |
| Dragged object uses another chip kind | Compare `Priority` and narrow both providers' `CanHandle` conditions |
| Bare path is not linked | Include the extension in `BarePathExtensions`, with or without a leading dot |
| Context is missing | Check the saved depth; `none` intentionally omits the payload |
| Link does nothing | Confirm that `Create.Path` and the reference expected by `Navigate` use the same format |
