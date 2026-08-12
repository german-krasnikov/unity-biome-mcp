// 9 thin built-in IChipKindProvider implementations (registered via ChipKindRegistry.EnsureBuiltIns).
// Larger providers live in their own files:
//   AssetChipProviderBase.cs, HierarchyChipProvider.cs, SceneChipProvider.cs, ImageChipProvider.cs
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat
{
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

        public override void AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string reference)
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

        public override void AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string reference)
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
}
