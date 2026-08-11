// 10 built-in IChipKindProvider implementations.
// All internal — registered by ChipKindRegistry.EnsureBuiltIns().
// AssetChipProviderBase: shared FormatPayload + Navigate(PingAsset) + DefaultDepth + Create.
// HierarchyChipProvider: fully custom (transient EntityId, GlobalObjectId, HierarchyResolver).
// Preview building moved to ChipPreviewBridge (temporary) until the preview registry (Dev 2).
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat
{
    internal abstract class AssetChipProviderBase : IChipKindProvider
    {
        // Set by AssetViewerFactory [InitializeOnLoad]. Returns true if viewer handled the path.
        // Same seam pattern as ChipPillFactory.AddToContextAction.
        internal static Func<string, bool> ViewerLauncher;

        public abstract string Key      { get; }
        public abstract int    Priority { get; }
        public abstract string IconName { get; }
        public abstract string HexColor { get; }
        public virtual  string DefaultDepth => "path";
        public virtual  string[] BarePathExtensions => Array.Empty<string>();

        public abstract bool CanHandle(Object obj, string assetPath);

        public virtual ChipData Create(Object obj, string assetPath)
        {
            var name = obj != null ? obj.name : Path.GetFileNameWithoutExtension(assetPath);
            return new ChipData(Key, assetPath, name, 0);
        }

        public virtual string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public virtual void Navigate(string reference)
        {
            var handled = ViewerLauncher?.Invoke(reference) == true;
            var obj = AssetDatabase.LoadAssetAtPath<Object>(reference);
            if (obj == null)
            {
                if (!handled)
                    Debug.LogWarning($"{BiomeLabel.Tag} Asset not found: " + reference);
                return;
            }
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        public virtual void Ping(string reference)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(reference);
            if (obj == null) return;
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        public virtual void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Ping in Project", _ => Ping(reference));
            menu.AppendAction("Open",            _ => Navigate(reference));
        }
    }

    internal sealed class HierarchyChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Hierarchy;
        public int    Priority => 100;
        public string IconName => "d_UnityEditor.SceneHierarchyWindow";
        public string HexColor => "#4a9eff";
        public string DefaultDepth => "path";
        public string[] BarePathExtensions => Array.Empty<string>();

        public bool CanHandle(Object obj, string assetPath)
            => obj is GameObject go && !AssetDatabase.Contains(go);

        public ChipData Create(Object obj, string assetPath)
        {
            var go = (GameObject)obj;
            var path = ComponentSerializer.GetPath(go);
            var goid = GlobalObjectId.GetGlobalObjectIdSlow(go);
            return new ChipData(Key, path, FormatHierarchyDisplay(path, go.name),
                TransientObjectId.GetHexRef(go), goid);
        }

        internal static string FormatHierarchyDisplay(string path, string leafName)
        {
            var sep = path.IndexOf(":/", System.StringComparison.Ordinal);
            return sep >= 0 ? "[" + path.Substring(0, sep) + "] " + leafName : leafName;
        }

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            var bracket = ChipContextResolver.FormatChipRef(Key, chip.Path, chip.ObjectId);
            if (chip.GlobalObjectId.targetObjectId != 0)
                bracket = bracket.Insert(bracket.Length - 1, $"@{chip.GlobalObjectId}");
            return ctx.Depth == "none" ? "" :
                   (ctx.Depth == "summary" || ctx.Depth == "full") && !string.IsNullOrEmpty(ctx.ResolvedSummary)
                       ? bracket + "\n" + ctx.ResolvedSummary
                       : bracket;
        }

        public void Navigate(string reference)
        {
            var go = Resolve(reference);
            if (go == null) { Debug.LogWarning($"{BiomeLabel.Tag} Reference stale: " + reference); return; }
            EditorGUIUtility.PingObject(go);
            Selection.activeObject = go;
        }

        public void Ping(string reference)
        {
            var go = Resolve(reference);
            if (go == null) return;
            EditorGUIUtility.PingObject(go);
            Selection.activeObject = go;
        }

        public void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Select in Hierarchy", _ => Navigate(reference));
            menu.AppendAction("Frame in Scene View",  _ =>
            {
                Navigate(reference);
                if (UnityEditor.SceneView.lastActiveSceneView != null)
                    UnityEditor.SceneView.lastActiveSceneView.FrameSelected();
            });
        }

        static GameObject Resolve(string reference)
        {
            var href = HierarchyReference.Parse(reference);
            var resolver = new HierarchyResolver();
            return resolver.Resolve(href);
        }
    }

    internal sealed class SceneChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Scene;
        public override int    Priority => 200;
        public override string IconName => "d_SceneAsset Icon";
        public override string HexColor => "#c084fc";

        public override bool CanHandle(Object obj, string assetPath)
            => obj != null && !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".unity");

        // chip.Path = scene name ("MyScene"), not the full asset path.
        public override ChipData Create(Object obj, string assetPath)
        {
            // obj null: direct-call path only — CanHandle requires non-null for registry calls
            var name = obj != null ? obj.name : Path.GetFileNameWithoutExtension(assetPath);
            return new ChipData(Key, name, name, 0);
        }

        public override void Navigate(string reference)
        {
            var obj = LoadScene(reference);
            if (obj == null)
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Scene not found: {reference}");
                return;
            }
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        public override void Ping(string reference)
        {
            var obj = LoadScene(reference);
            if (obj != null) { EditorGUIUtility.PingObject(obj); Selection.activeObject = obj; }
        }

        // Full path or .unity extension → direct load; name-only → exact-name lookup.
        private static Object LoadScene(string reference)
        {
            if (reference.Contains("/") || reference.EndsWith(".unity"))
                return AssetDatabase.LoadAssetAtPath<Object>(reference);
            var path = FindScenePathByExactName(reference);
            return path != null ? AssetDatabase.LoadAssetAtPath<Object>(path) : null;
        }

        // FindAssets uses substring search; filter by exact filename to avoid "Level" matching "Level1".
        internal static string FindScenePathByExactName(string sceneName)
        {
            var guids = AssetDatabase.FindAssets("t:Scene " + sceneName);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                    return path;
            }
            return null;
        }
    }

    internal sealed class ScriptChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Script;
        public override int    Priority => 300;
        public override string IconName => "d_cs Script Icon";
        public override string HexColor => "#4ade80";

        public override bool CanHandle(Object obj, string assetPath) => obj is MonoScript;

        public override void Navigate(string reference)
        {
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(reference);
            if (ms == null) { Debug.LogWarning($"{BiomeLabel.Tag} Script not found: " + reference); return; }
            AssetDatabase.OpenAsset(ms);
        }

        public override void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Ping in Project", _ => Ping(reference));
            menu.AppendAction("Open in IDE",     _ => Navigate(reference));
        }
    }

    internal sealed class PrefabChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Prefab;
        public override int    Priority => 400;
        public override string IconName => "d_Prefab Icon";
        public override string HexColor => "#60a5fa";

        // C9/C10: Navigate uses base.Navigate → ViewerLauncher (wired by AssetViewerFactory [InitializeOnLoad]).
        // .prefab registered in AssetViewerFactory.RegisterBuiltIns → PrefabViewerWindow.ViewerAdapter.
        // No static OnNavigate field — eliminates multi-window nulling race (C10).

        public override bool CanHandle(Object obj, string assetPath)
            => obj != null && !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");
    }

    internal sealed class MaterialChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Material;
        public override int    Priority => 500;
        public override string IconName => "d_Material Icon";
        public override string HexColor => "#f97316";

        public override bool CanHandle(Object obj, string assetPath) => obj is Material;
    }

    internal sealed class TextureChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Texture;
        public override int    Priority => 600;
        public override string IconName => "d_Texture Icon";
        public override string HexColor => "#facc15";

        public override bool CanHandle(Object obj, string assetPath) => obj is Texture;
    }

    internal sealed class SOChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.ScriptableObject;
        public override int    Priority => 700;
        public override string IconName => "d_ScriptableObject Icon";
        public override string HexColor => "#fb7185";

        public override bool CanHandle(Object obj, string assetPath) => obj is ScriptableObject;
    }

    internal sealed class FolderChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Folder;
        public override int    Priority => 150;
        public override string IconName => "d_Folder Icon";
        public override string HexColor => "#a78bfa";

        public override bool CanHandle(Object obj, string assetPath)
            => obj is DefaultAsset && !string.IsNullOrEmpty(assetPath)
               && AssetDatabase.IsValidFolder(assetPath);

        public override void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Open in Project", _ => Navigate(reference));
        }
    }

    internal sealed class AssetChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Asset;
        public override int    Priority => int.MaxValue;
        public override string IconName => "d_DefaultAsset Icon";
        public override string HexColor => "#94a3b8";

        public override bool CanHandle(Object obj, string assetPath) => true; // fallback

        public override ChipData Create(Object obj, string assetPath)
        {
            var path = string.IsNullOrEmpty(assetPath) ? (obj != null ? obj.name : "") : assetPath;
            return new ChipData(Key, path, obj != null ? obj.name : path, 0);
        }
    }

    internal sealed class ModelChipProvider : AssetChipProviderBase
    {
        private static readonly string[] _exts = { ".fbx", ".obj", ".blend", ".dae" };

        public override string Key      => ChipKindKeys.Model;
        public override int    Priority => 450;
        public override string IconName => "d_Mesh Icon";
        public override string HexColor => "#34d399";

        public override bool CanHandle(Object obj, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            var matches = false;
            foreach (var e in _exts) if (ext == e) { matches = true; break; }
            if (!matches) return false;
            // Only claim the root model GameObject (or path-only detection where obj is null).
            // A Mesh sub-asset of an FBX should fall back to the generic Asset provider.
            return obj == null || obj is GameObject;
        }
    }

    internal sealed class AudioChipProvider : AssetChipProviderBase
    {
        private static readonly string[] _exts = { ".wav", ".mp3", ".ogg", ".aiff" };

        public override string Key      => ChipKindKeys.Audio;
        public override int    Priority => 550;
        public override string IconName => "d_AudioClip Icon";
        public override string HexColor => "#a78bfa";

        public override bool CanHandle(Object obj, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            foreach (var e in _exts) if (ext == e) return true;
            return false;
        }
    }

    /// <summary>
    /// Chip kind for external image files dropped from Finder or pasted from clipboard.
    /// Priority 50 — beats all asset providers (they need non-null obj).
    /// CanHandle: obj must be null AND path must have an image extension.
    /// FormatPayload returns "" — images are sent as binary image_url blocks, not text refs.
    /// </summary>
    internal sealed class ImageChipProvider : IChipKindProvider
    {
        private static readonly string[] _exts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff", ".tif" };

        // Seam: View assembly wires this to ImageViewerWindow.Show at [InitializeOnLoad].
        internal static Action<string> ImageFallbackViewer;

        public string Key        => ChipKindKeys.Image;
        public int    Priority   => 50;
        public string IconName   => "d_Texture Icon";
        public string HexColor   => "#f472b6";
        public string DefaultDepth => "path";
        public string[] BarePathExtensions => _exts;

        public bool CanHandle(Object obj, string assetPath)
        {
            if (obj != null || string.IsNullOrEmpty(assetPath)) return false;
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            foreach (var e in _exts) if (ext == e) return true;
            return false;
        }

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, Path.GetFileName(assetPath), 0);

        // Images go as binary image_url blocks — no text bracket needed.
        public string FormatPayload(ChipData chip, ChipPayloadContext ctx) => "";

        public void Navigate(string reference)
        {
            if (AssetChipProviderBase.ViewerLauncher?.Invoke(reference) == true) return;
            ImageFallbackViewer?.Invoke(reference);
        }

        public void Ping(string reference)
        {
            // Images have no project asset to ping; reuse the viewer fallback.
            Navigate(reference);
        }

        public void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Ping in Project", _ => Ping(reference));
            menu.AppendAction("Open in Viewer",  _ => Navigate(reference));
        }
    }
}
