using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    // Partial class: UI Toolkit tools.
    // Session 9: attach_uitk — add UIDocument component to a GameObject.
    public static partial class UIHelper
    {
        // Called by ExecAttachUITK.
        // path: scene path to target GameObject.
        // uxmlPath: Assets/ path to .uxml VisualTreeAsset (optional).
        // panelSettings: Assets/ path to PanelSettings asset (optional).
        // sortingOrder: UIDocument.sortingOrder.
        public static string AttachUITK(string path, string uxmlPath, string panelSettings, int sortingOrder)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                return $"err: path not found: {path}";

            if (go.GetComponent<UIDocument>() != null)
                return $"err: {path} already has UIDocument. Remove it first or use inspect_uitk.";

            Undo.AddComponent<UIDocument>(go);
            var doc = go.GetComponent<UIDocument>();

            if (!string.IsNullOrEmpty(uxmlPath))
            {
                var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                if (vta == null)
                    return $"err: uxml not found at {uxmlPath}. Use uitk_file to create it first.";
                doc.visualTreeAsset = vta;
            }

            if (!string.IsNullOrEmpty(panelSettings))
            {
                var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettings);
                if (ps == null)
                    return $"err: PanelSettings not found at {panelSettings}";
                doc.panelSettings = ps;
            }

            doc.sortingOrder = sortingOrder;

            var uxmlHint = !string.IsNullOrEmpty(uxmlPath) ? $" (vta={uxmlPath})" : " (no vta)";
            return $"ok: UIDocument added to {path}{uxmlHint}";
        }
    }
}
