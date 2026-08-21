// Chip provider for scene hierarchy GameObjects.
// Uses transient EntityId, GlobalObjectId, and HierarchyResolver for navigation.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class HierarchyChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Hierarchy;
        public int    Priority => 100;
        public string IconName => "d_UnityEditor.SceneHierarchyWindow";
        public string HexColor => "#4a9eff";
        public string DefaultDepth => "path";
        public string[] BarePathExtensions => System.Array.Empty<string>();

        public bool CanHandle(Object obj, string assetPath)
            => obj is GameObject go && !AssetDatabase.Contains(go);

        public ChipData Create(Object obj, string assetPath)
        {
            var go = (GameObject)obj;
            var path = ComponentSerializer.GetPath(go);
            var goid = GlobalObjectId.GetGlobalObjectIdSlow(go);
            return new ChipData(Key, path, FormatHierarchyDisplay(path, go.name),
                RefManager.Assign(go), goid);
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
            // HierarchySerializer emits process-local &base62 references. Resolve that
            // canonical shape directly; a stale compact ref must not become a path/name.
            if (!string.IsNullOrEmpty(reference) && reference[0] == WirePrefix.Ref &&
                RefManager.IsRef(reference))
                return RefManager.Resolve(reference);

            var href = HierarchyReference.Parse(reference);
            var resolver = new HierarchyResolver();
            return resolver.Resolve(href);
        }
    }
}
