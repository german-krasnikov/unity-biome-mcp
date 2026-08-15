using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    // Bridge between PlaytestRunner and UI Toolkit VisualElements.
    // Phase 1: read-only (ReadValue + IsElementEnabled).
    // Phase 2 adds: SimulateClick, FillText, FocusElement.
    // ONLY caller: PlaytestRunner.cs and PlaytestRunner.Steps.cs.
    internal static class UIElementHelper
    {
        // Called from PlaytestRunner.ReadValue when comp starts with "UIDocument|".
        // selector: element name, .class, TypeName, or ~refid.
        // field:    text | value | visible | display | enabledSelf | enabledInHierarchy | class | width | height.
        internal static string ReadValue(GameObject go, string selector, string field)
        {
            var doc = go.GetComponent<UIDocument>();
            if (doc == null) return $"err: no UIDocument on {go.name}";
            if (doc.rootVisualElement == null)
                return "err: rootVisualElement is null (Edit Mode without RunInEditMode)";

            var el = FindElement(doc.rootVisualElement, selector);
            if (el == null)
                return $"err: no element matches '{selector}' in {go.name}. " +
                       $"Call inspect_uitk(path=\"{ComponentSerializer.GetPath(go)}\") to list available names.";

            return ReadProperty(el, field);
        }

        internal static bool IsElementEnabled(UIDocument doc, string selector)
        {
            if (doc?.rootVisualElement == null) return false;
            var el = FindElement(doc.rootVisualElement, selector);
            return el?.enabledInHierarchy ?? false;
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private static VisualElement FindElement(VisualElement root, string selector)
        {
            if (string.IsNullOrEmpty(selector)) return root;
            if (selector.StartsWith(".")) return root.Q(className: selector.Substring(1));
            if (selector.StartsWith("#")) return root.Q(selector.Substring(1));
            return root.Q(selector) ??
                   root.Query().Where(e => e.GetType().Name == selector).First();
        }

        private static string ReadProperty(VisualElement el, string field) => field switch
        {
            "text"               => (el as TextElement)?.text ?? "",
            "value"              => ReadValue(el),
            "visible"            => (el.resolvedStyle.visibility == Visibility.Visible).ToString().ToLowerInvariant(),
            "display"            => el.resolvedStyle.display == DisplayStyle.Flex ? "flex" : "none",
            "enabledSelf"        => el.enabledSelf.ToString().ToLowerInvariant(),
            "enabledInHierarchy" => el.enabledInHierarchy.ToString().ToLowerInvariant(),
            "class"              => string.Join(" ", el.GetClasses()),
            "width"              => el.layout.width.ToString("F1"),
            "height"             => el.layout.height.ToString("F1"),
            _ => $"err: unknown field '{field}'. Valid: text value visible display enabledSelf enabledInHierarchy class width height",
        };

        private static string ReadValue(VisualElement el) => el switch
        {
            Slider s    => s.value.ToString("F3"),
            Toggle t    => t.value.ToString().ToLowerInvariant(),
            TextField f => f.value,
            _           => (el as TextElement)?.text ?? "",
        };
    }
}
