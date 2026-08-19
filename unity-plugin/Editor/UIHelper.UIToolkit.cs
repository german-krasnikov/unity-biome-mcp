using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    // Partial class: UI Toolkit tools — inspect, lint, uitk_element, attach_uitk.
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
                return ListAllUIHosts();

            VisualElement root;
            try
            {
                var go = ComponentSerializer.FindObject(path);
                if (go == null) return $"err: path not found: {path}";
                root = UIPanelHost.ResolveRoot(go);
            }
            catch (Exception ex) { return ex.Message; }

            return _uitkSerializer.Serialize(root, depth, selector,
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
            GameObject hostGo = null;
            VisualElement root = null;
            if (!string.IsNullOrEmpty(path))
            {
                hostGo = ComponentSerializer.FindObject(path);
                if (hostGo == null) return $"err: path not found: {path}";
                try { root = UIPanelHost.ResolveRoot(hostGo); }
                catch (Exception ex) { return ex.Message; }
            }

            // action=query: multi-element search
            if (action == "query")
            {
                if (root == null) return "err: path is required for action=query";
                return UitkQuery(root, PreferredUitkAddress(name, selector));
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
                var sel = PreferredUitkAddress(name, selector);
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
                "set_style"    => UitkSetStyle(hostGo, ve, property, value),
                "add_class"    => UitkClassOp(hostGo, ve, true, className),
                "remove_class" => UitkClassOp(hostGo, ve, false, className),
                "get_style"    => UitkGetStyle(ve, property),
                "enable"       => UitkSetEnabled(hostGo, ve, true),
                "disable"      => UitkSetEnabled(hostGo, ve, false),
                _              => $"err: unknown action \"{action}\". Valid: query get set_style add_class remove_class get_style enable disable",
            };

            // A1: Play Mode mutation warning
            bool isMutation = action is "set_style" or "add_class" or "remove_class" or "enable" or "disable";
            if (isMutation && EditorApplication.isPlaying && result.StartsWith("ok:"))
                result += "\nwarn: Play Mode change — not persisted to scene asset.";

            return result;
        }

        // Session 9: Called by ExecAttachUITK.
        // path: scene path to target GameObject.
        // uxmlPath: Assets/ path to .uxml VisualTreeAsset (optional).
        // panelSettings: Assets/ path to PanelSettings asset (optional).
        // sortingOrder: UIDocument.sortingOrder.
        public static string AttachUITK(string path, string uxmlPath, string panelSettings, int sortingOrder)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                return $"err: path not found: {path}";

            if (UIPanelHost.HasHost(go))
                return $"err: {path} already has a UI host component. Remove it first or use inspect_uitk.";

            VisualTreeAsset vta = null;
            if (!string.IsNullOrEmpty(uxmlPath))
            {
                vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                if (vta == null)
                    return $"err: uxml not found at {uxmlPath}. Use uitk_file to create it first.";
            }

            PanelSettings settings = null;
            if (!string.IsNullOrEmpty(panelSettings))
            {
                settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettings);
                if (settings == null)
                    return $"err: PanelSettings not found at {panelSettings}";
            }

            // Resolve every supplied asset before the single scene mutation. Invalid input
            // must never leave a partially configured component behind.
            var uxmlHint = !string.IsNullOrEmpty(uxmlPath) ? $" (vta={uxmlPath})" : " (no vta)";
            var psHint = settings != null
                ? $" ps={panelSettings}"
                : " warn:panelSettings=null — UI Toolkit will not render; supply panel_settings param";

            var hostLabel = UIPanelHost.CreateHost(go, vta, settings, sortingOrder);
            return $"ok: {hostLabel} added to {path}{uxmlHint}{psHint}";
        }

        // ── Phase 2 private helpers ──────────────────────────────────────────────

        // Public tool contract: refId wins before this method is reached; when both
        // remaining fields are present, the explicit element name wins over selector.
        internal static string PreferredUitkAddress(string name, string selector) =>
            !string.IsNullOrEmpty(name) ? name : selector;

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
                    return $"err: unknown style property \"{prop}\" — " +
                           "to change inline styles use only valid Unity USS properties. " +
                           "For custom rules: call inspect_uitk to find the panel's stylesheet path, " +
                           "then uitk_file action=set-rule on that .uss file.";
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

        private static string ListAllUIHosts()
        {
            var sb = new StringBuilder();
            foreach (var (go, label, rootNull) in UIPanelHost.FindAllHosts())
            {
                var goPath = ComponentSerializer.GetPath(go);
                var nullHint = rootNull
                    ? (label == "[UIDocument]"
                        ? " (null — Edit Mode without RunInEditMode)"
                        : " (null — panel not active)")
                    : "";
                sb.AppendLine($"{goPath} {label}{nullHint}");
            }
            if (sb.Length == 0)
                return "no UI host (UIDocument or PanelRenderer) found in open scenes";
            return sb.ToString().TrimEnd();
        }
    }
}
