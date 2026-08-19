using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    // Compat layer for UIDocument (Unity 6.0) and PanelRenderer (Unity 6.4+).
    // #if guards are contained here; all callers use the clean API below.
    internal static class UIPanelHost
    {
#if UNITY_6000_4_OR_NEWER
        private static readonly PropertyInfo _rootProp =
            typeof(PanelRenderer).GetProperty("rootVisualElement",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#endif

        // Returns the VisualElement root for the first UI host found on go.
        // Throws ArgumentException (no host) or InvalidOperationException (ambiguous / null root).
        // All messages start with "err:" for string-return callers.
        internal static VisualElement ResolveRoot(GameObject go)
        {
            var doc = go.GetComponent<UIDocument>();
#if UNITY_6000_4_OR_NEWER
            var renderer = go.GetComponent<PanelRenderer>();
            if (doc != null && renderer != null)
                throw new InvalidOperationException(
                    $"err: '{go.name}' has both UIDocument and PanelRenderer — ambiguous host. Remove one first.");
            if (renderer != null)
            {
                if (_rootProp == null)
                    throw new InvalidOperationException(
                        "err: PanelRenderer.rootVisualElement inaccessible via reflection. " +
                        "Unity version may have changed the internal API.");
                var root = _rootProp.GetValue(renderer) as VisualElement;
                if (root == null)
                    throw new InvalidOperationException(
                        "err: PanelRenderer.rootVisualElement is null. " +
                        "Activate the panel or enter Play Mode.");
                return root;
            }
#endif
            if (doc != null)
            {
                if (doc.rootVisualElement == null)
                    throw new InvalidOperationException(
                        "err: UIDocument.rootVisualElement is null in Edit Mode. " +
                        "Enable RunInEditMode or enter Play Mode.");
                return doc.rootVisualElement;
            }
            throw new ArgumentException(
                $"err: no UIDocument or PanelRenderer on '{go.name}'. " +
                "Add a UI host component first.");
        }

        // Non-throwing version — returns null on any failure.
        internal static VisualElement TryResolveRoot(GameObject go)
        {
            try { return ResolveRoot(go); }
            catch { return null; }
        }

        // True if the GameObject has any UI host component.
        internal static bool HasHost(GameObject go)
            => go.GetComponent<UIDocument>() != null
#if UNITY_6000_4_OR_NEWER
               || go.GetComponent<PanelRenderer>() != null
#endif
               ;

        // Human-readable label for the host type present on the GameObject.
        internal static string HostLabel(GameObject go)
        {
            if (go.GetComponent<UIDocument>() != null) return "[UIDocument]";
#if UNITY_6000_4_OR_NEWER
            if (go.GetComponent<PanelRenderer>() != null) return "[PanelRenderer]";
#endif
            return "[no UI host]";
        }

        // Adds the correct UI host component for the current Unity version.
        // Returns "UIDocument" or "PanelRenderer[<warn line>]" — caller prepends "ok: ... added to".
        internal static string CreateHost(GameObject go, VisualTreeAsset vta, PanelSettings ps, int sortingOrder)
        {
#if UNITY_6000_4_OR_NEWER
            var renderer = Undo.AddComponent<PanelRenderer>(go);
            renderer.visualTreeAsset = vta;
            renderer.panelSettings = ps;
            EditorUtility.SetDirty(renderer);
            var orderHint = sortingOrder != 0
                ? "\nwarn:sorting_order ignored with PanelRenderer — use PanelSettings.sortingOrder"
                : "";
            return $"PanelRenderer{orderHint}";
#else
            var doc = Undo.AddComponent<UIDocument>(go);
            doc.visualTreeAsset = vta;
            doc.panelSettings = ps;
            doc.sortingOrder = sortingOrder;
            EditorUtility.SetDirty(doc);
            return "UIDocument";
#endif
        }

        // Iterates all UI host components in open scenes across both types.
        // rootNull=true means the visual root is unavailable (Edit Mode / panel inactive).
        internal static IEnumerable<(GameObject go, string label, bool rootNull)> FindAllHosts()
        {
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                yield return (doc.gameObject, "[UIDocument]", doc.rootVisualElement == null);
#if UNITY_6000_4_OR_NEWER
            foreach (var r in UnityEngine.Object.FindObjectsByType<PanelRenderer>(FindObjectsSortMode.None))
            {
                bool rootNull = true;
                if (_rootProp != null)
                {
                    var root = _rootProp.GetValue(r) as VisualElement;
                    rootNull = root == null;
                }
                yield return (r.gameObject, "[PanelRenderer]", rootNull);
            }
#endif
        }
    }
}
