// Chip methods extracted to a partial to keep MCPChatWindow.cs under 200 lines.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        internal delegate void ChipInserter(Object obj, string path, string name);

        private static void OnDragUpdated(DragUpdatedEvent e)
        {
            if (DragAndDrop.objectReferences.Length > 0 || DragAndDrop.paths.Length > 0)
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            DragAndDrop.AcceptDrag();
            var selected = Selection.activeGameObject;
            var handled = new HashSet<string>();
            foreach (var obj in DragAndDrop.objectReferences)
                ProcessDraggedObject(obj, selected, InsertInlineChip, handledPaths: handled);
            // Always process external paths (Finder drag); deduplicate via handled set
            foreach (var path in DragAndDrop.paths)
                if (!handled.Contains(path))
                    ProcessExternalPath(path, InsertInlineChip);
        }

        internal delegate bool HasComponentFn(GameObject go, System.Type type);

        // F26: extracted for testability — injectable selectedGO + ChipInserter delegate
        // hasComponent is injectable for tests where FromMonoBehaviour can't bind test-assembly types
        // handledPaths: collects inserted asset paths for dedup with ProcessExternalPath
        internal static void ProcessDraggedObject(Object obj, GameObject selectedGO, ChipInserter insert,
            HasComponentFn hasComponent = null, HashSet<string> handledPaths = null)
        {
            if (obj == null) return;
            hasComponent ??= (go, t) => go.GetComponent(t) != null;

            if (obj is GameObject go && !AssetDatabase.Contains(go))
            {
                insert(go, ComponentSerializer.GetPath(go), go.name);
                return;
            }

            // Component drag from Inspector: insert GO chip + optional MonoScript chip
            if (obj is Component comp)
            {
                var compGO = comp.gameObject;
                insert(compGO, ComponentSerializer.GetPath(compGO), compGO.name);
                if (comp is MonoBehaviour mb)
                {
                    var script = MonoScript.FromMonoBehaviour(mb);
                    if (script != null)
                    {
                        var scriptPath = AssetDatabase.GetAssetPath(script);
                        if (!string.IsNullOrEmpty(scriptPath))
                        {
                            insert(script, scriptPath, script.name);
                            handledPaths?.Add(scriptPath);
                        }
                    }
                }
                return;
            }

            // Accept all DefaultAsset with a valid path (folders + .md, .txt, .json, etc.)
            if (obj is DefaultAsset)
            {
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    insert(obj, assetPath, obj.name);
                    handledPaths?.Add(assetPath);
                }
                return;
            }

            if (obj is MonoScript ms)
            {
                var assetPath = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(assetPath)) return;
                var cls = ms.GetClass();
                if (selectedGO != null && !AssetDatabase.Contains(selectedGO)
                    && cls != null && typeof(UnityEngine.Component).IsAssignableFrom(cls)
                    && hasComponent(selectedGO, cls))
                {
                    insert(selectedGO, ComponentSerializer.GetPath(selectedGO), selectedGO.name);
                }
                insert(ms, assetPath, ms.name);
                handledPaths?.Add(assetPath);
                return;
            }

            if (AssetDatabase.Contains(obj) && ChatChipPolicy.IsAllowedAssetType(obj.GetType()))
            {
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var prov = ChipKindRegistry.Resolve(obj, assetPath);
                    if (prov != null)
                    {
                        var chip = prov.Create(obj, assetPath);
                        insert(obj, chip.Path, chip.DisplayName);
                    }
                    handledPaths?.Add(assetPath);
                }
            }
            else if (!(obj is GameObject))
                Debug.LogWarning($"{BiomeLabel.Tag} {obj.GetType().Name} not supported as a context chip");
        }

        // F29: external path handler (Finder drag) — internal static for testability.
        // Image files (png/jpg/…) are copied to Library/MCPChat/Attachments/ before inserting.
        internal static void ProcessExternalPath(string path, ChipInserter insert)
        {
            if (string.IsNullOrEmpty(path)) return;
            var name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path;
            var ext = System.IO.Path.GetExtension(path);
            if (IsImageExtension(ext))
            {
                var dest = ImageAttachmentStore.ImportFile(path);
                // C3: if ImportFile returns null (oversize, invalid magic, error) — abort, don't leak path
                if (dest == null) return;
                insert(null, dest, name);
            }
            else
            {
                insert(null, path, name);
            }
        }

        /// <summary>Returns true for image file extensions (case-insensitive).</summary>
        internal static bool IsImageExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            switch (ext.ToLowerInvariant())
            {
                case ".png": case ".jpg": case ".jpeg":
                case ".bmp": case ".gif": case ".webp":
                case ".tiff": case ".tif":
                    return true;
                default:
                    return false;
            }
        }
    }
}
