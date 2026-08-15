using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    // Partial class: UI Toolkit read tools for Phase 1 (inspect + lint).
    // Mutations (uitk_element write path) are added in Phase 2.
    public static partial class UIHelper
    {
        // Shared serializer — holds VERefTable across calls.
        // ResetRefTable() is called inside Serialize() on every invocation.
        private static readonly UIElementSerializer _uitkSerializer = new UIElementSerializer();

        // Called by ExecInspectUITK.
        // path="scene" → lists all UIDocuments in open scenes.
        // path=null → same as path="scene" (list all).
        public static string InspectUITK(string path, int depth, string selector,
                                         string filter, bool includeInternal, bool showStyle)
        {
            if (string.IsNullOrEmpty(path) || path == "scene")
                return ListAllUIDocuments();

            UIDocument doc;
            try { doc = ResolveUIDocument(path); }
            catch (Exception ex) { return ex.Message; }   // err: already formatted

            return _uitkSerializer.Serialize(doc.rootVisualElement, depth, selector,
                                             filter, includeInternal, showStyle);
        }

        // Called by ExecLintUITK (delegates to UILinter).
        public static string LintUITK(string path, bool fix) =>
            UILinter.LintUITK(path, fix);

        // ── Private helpers ──────────────────────────────────────────────────────

        private static string ListAllUIDocuments()
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            if (docs.Length == 0) return "no UIDocument found in open scenes";
            var sb = new StringBuilder();
            foreach (var doc in docs)
            {
                var goPath  = ComponentSerializer.GetPath(doc.gameObject);
                var nullHint = doc.rootVisualElement == null
                    ? " (null — Edit Mode without RunInEditMode)"
                    : "";
                sb.AppendLine($"{goPath} [UIDocument]{nullHint}");
            }
            return sb.ToString().TrimEnd();
        }

        // Resolves path → UIDocument. Throws with "err:"-prefixed message on failure.
        private static UIDocument ResolveUIDocument(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new ArgumentException($"err: path not found: {path}");

            var doc = go.GetComponent<UIDocument>();
            if (doc == null)
                throw new ArgumentException($"err: no UIDocument component on {path}");

            if (doc.rootVisualElement == null)
                throw new InvalidOperationException(
                    $"err: UIDocument.rootVisualElement is null in Edit Mode. " +
                    $"Enable RunInEditMode on the UIDocument, or enter Play Mode, then retry.");

            return doc;
        }
    }
}
