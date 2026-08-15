using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
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

        // Phase 2: Called by ExecUitkElement.
        // Element addressing priority: refId (~N) → name → selector (CSS class/type).
        // A1: mutating actions in Play Mode append warn: line to ok: response.
        // A2: stale ~N ref returns err: message — re-run inspect_uitk.
        public static string UitkElement(string action, string path, string refId,
                                          string selector, string name,
                                          string value, string property, string className)
        {
            // For query action with no path, use selector/name to find across root
            UIDocument doc = null;
            VisualElement root = null;
            if (!string.IsNullOrEmpty(path))
            {
                try { doc = ResolveUIDocument(path); }
                catch (Exception ex) { return ex.Message; }
                root = doc.rootVisualElement;
            }

            // action=query: multi-element search
            if (action == "query")
            {
                if (root == null) return "err: path is required for action=query";
                return UitkQuery(root, selector ?? name);
            }

            // Resolve single element
            VisualElement ve = null;
            if (!string.IsNullOrEmpty(refId))
            {
                // A2: ~N ref path
                ve = _uitkSerializer.ResolveRef(refId);
                if (ve == null)
                    return $"err: stale ref {refId} — call inspect_uitk again to refresh refids";
            }
            else if (root != null)
            {
                var sel = selector ?? name;
                if (string.IsNullOrEmpty(sel)) return "err: selector or name is required";
                ve = UIElementSerializer.Dispatch(root, sel);
                if (ve == null)
                    return $"err: no element matching \"{sel}\" — use inspect_uitk to browse the tree";
            }
            else
            {
                return "err: path or ref is required";
            }

            string result = action switch
            {
                "get"          => UitkGet(ve, property),
                "set_style"    => UitkSetStyle(doc?.gameObject, ve, property, value),
                "add_class"    => UitkClassOp(doc?.gameObject, ve, true, className),
                "remove_class" => UitkClassOp(doc?.gameObject, ve, false, className),
                "get_style"    => UitkGetStyle(ve, property),
                "enable"       => UitkSetEnabled(doc?.gameObject, ve, true),
                "disable"      => UitkSetEnabled(doc?.gameObject, ve, false),
                _              => $"err: unknown action \"{action}\". Valid: query get set_style add_class remove_class get_style enable disable",
            };

            // A1: Play Mode mutation warning
            bool isMutation = action is "set_style" or "add_class" or "remove_class" or "enable" or "disable";
            if (isMutation && EditorApplication.isPlaying && result.StartsWith("ok:"))
                result += "\nwarn: Play Mode change — not persisted to scene asset.";

            return result;
        }

        // ── Phase 2 private helpers ──────────────────────────────────────────────

        private static string UitkQuery(VisualElement root, string selector)
        {
            if (string.IsNullOrEmpty(selector)) return "err: selector or name is required for action=query";
            var matches = new List<VisualElement>();
            if (selector.StartsWith("."))
                matches.AddRange(root.Query(className: selector.Substring(1)).ToList());
            else if (selector.StartsWith("#"))
                matches.AddRange(root.Query(selector.Substring(1)).ToList());
            else
                matches.AddRange(root.Query(selector).ToList());

            if (matches.Count == 0) return $"0 matches for \"{selector}\" — use inspect_uitk to browse the tree";
            var sb = new StringBuilder();
            sb.AppendLine($"{matches.Count} matches for \"{selector}\":");
            foreach (var m in matches)
                sb.AppendLine($"  {m.name ?? "_"} [{m.GetType().Name}] .{string.Join(" .", m.GetClasses())}");
            return sb.ToString().TrimEnd();
        }

        private static string UitkGet(VisualElement ve, string prop)
        {
            return prop switch
            {
                "text"    => (ve as TextElement)?.text ?? $"err: {ve.name} is not a TextElement",
                "value"   => UitkGetControlValue(ve) ?? $"err: {ve.name} has no value property",
                "visible" => (ve.resolvedStyle.display != DisplayStyle.None).ToString().ToLowerInvariant(),
                "name"    => ve.name ?? "",
                "enabled" => ve.enabledInHierarchy.ToString().ToLowerInvariant(),
                null      => $"err: prop is required for action=get",
                _         => $"err: unknown prop \"{prop}\". Valid: text value visible name enabled",
            };
        }

        private static string UitkGetControlValue(VisualElement ve)
        {
            if (ve is Toggle t) return t.value.ToString().ToLowerInvariant();
            if (ve is Slider s) return s.value.ToString("F2");
            if (ve is SliderInt si) return si.value.ToString();
            if (ve is TextField tf) return tf.value;
            if (ve is DropdownField dd) return dd.value;
            if (ve is ProgressBar pb) return pb.value.ToString("F2");
            if (ve is Foldout fo) return fo.value.ToString().ToLowerInvariant();
            return null;
        }

        private static string UitkSetStyle(GameObject go, VisualElement ve, string prop, string value)
        {
            if (string.IsNullOrEmpty(prop)) return "err: property is required for action=set_style";
            if (go != null) Undo.RegisterFullObjectHierarchyUndo(go, "UITK SetStyle");
            switch (prop)
            {
                case "display":
                    ve.style.display = value == "none" ? DisplayStyle.None : DisplayStyle.Flex;
                    break;
                case "opacity":
                    if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float op))
                        return $"err: invalid opacity value \"{value}\" — use 0.0–1.0";
                    ve.style.opacity = op;
                    break;
                case "visibility":
                    ve.style.visibility = value == "hidden" ? Visibility.Hidden : Visibility.Visible;
                    break;
                default:
                    return $"err: unknown style property \"{prop}\" — use uitk_file to edit USS rules";
            }
            return $"ok: set style.{prop}={value} on {ve.name}";
        }

        private static string UitkClassOp(GameObject go, VisualElement ve, bool add, string className)
        {
            if (string.IsNullOrEmpty(className))
                return $"err: class_name is required for action={(add ? "add_class" : "remove_class")}";
            if (go != null) Undo.RegisterFullObjectHierarchyUndo(go, "UITK ClassOp");
            if (add)
            {
                if (ve.ClassListContains(className))
                    return $"no-op: class \"{className}\" already present on {ve.name}";
                ve.AddToClassList(className);
                return $"ok: add_class \"{className}\" on {ve.name}";
            }
            ve.RemoveFromClassList(className);
            return $"ok: remove_class \"{className}\" on {ve.name}";
        }

        private static string UitkGetStyle(VisualElement ve, string prop)
        {
            var rs = ve.resolvedStyle;
            if (!string.IsNullOrEmpty(prop))
            {
                return prop switch
                {
                    "color"           => ColorUtility.ToHtmlStringRGBA(rs.color),
                    "backgroundColor" => ColorUtility.ToHtmlStringRGBA(rs.backgroundColor),
                    "opacity"         => rs.opacity.ToString("F2"),
                    "display"         => rs.display == DisplayStyle.Flex ? "flex" : "none",
                    "width"           => $"{rs.width:F1}px",
                    "height"          => $"{rs.height:F1}px",
                    _                 => $"err: unknown style property \"{prop}\"",
                };
            }
            var parts = new List<string>();
            if (rs.opacity < 1f) parts.Add($"opacity: {rs.opacity:F2}");
            if (rs.display == DisplayStyle.None) parts.Add("display: none");
            if (rs.backgroundColor.a > 0f) parts.Add($"backgroundColor: #{ColorUtility.ToHtmlStringRGBA(rs.backgroundColor)}");
            if (rs.width > 0f) parts.Add($"width: {rs.width:F1}px");
            if (rs.height > 0f) parts.Add($"height: {rs.height:F1}px");
            if (parts.Count == 0) return "no non-default styles";
            return $"styles: {ve.name}\ncomputed:\n  " + string.Join("\n  ", parts);
        }

        private static string UitkSetEnabled(GameObject go, VisualElement ve, bool enabled)
        {
            if (go != null) Undo.RegisterFullObjectHierarchyUndo(go, "UITK SetEnabled");
            ve.SetEnabled(enabled);
            return $"ok: {(enabled ? "enable" : "disable")} on {ve.name}";
        }

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
