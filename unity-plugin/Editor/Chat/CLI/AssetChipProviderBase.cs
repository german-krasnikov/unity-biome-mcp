// Shared abstract base for asset-backed chip providers.
// AssetChipProviderBase: FormatPayload, Navigate (PingAsset), Ping, AppendContextMenuItems.
// ViewerLauncher seam: wired by AssetViewerFactory [InitializeOnLoad].
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
}
