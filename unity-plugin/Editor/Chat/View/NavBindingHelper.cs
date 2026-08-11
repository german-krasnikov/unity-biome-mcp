// Navigation dispatcher for NavTarget. Wires click handlers onto VisualElements.
// Dispatches through ChipKindRegistry; script+line uses FileLineNavigator.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Attaches navigation behavior to a VisualElement and dispatches Navigate calls.
    /// Phase 1.2 — does not require existenceService; added in a later phase if needed.
    /// </summary>
    internal static class NavBindingHelper
    {
        /// <summary>
        /// Wire a click handler on element that calls Navigate(target).
        /// No-op when element is null or target.IsEmpty.
        /// </summary>
        internal static void Attach(VisualElement element, NavTarget target)
        {
            if (element == null || target.IsEmpty) return;
            element.RegisterCallback<ClickEvent>(_ => Navigate(target));
        }

        /// <summary>
        /// Dispatch navigation for target.
        /// - script kind + Line > 0 → FileLineNavigator.OpenAtLine
        /// - all other kinds → ChipKindRegistry.ForKey(KindKey)?.Navigate(Reference)
        /// - empty target or unknown kind → no-op
        /// </summary>
        internal static void Navigate(NavTarget target)
        {
            if (target.IsEmpty) return;

            if (target.KindKey == ChipKindKeys.Script && target.Line > 0)
            {
                FileLineNavigator.OpenAtLine(target.Reference, target.Line);
                return;
            }

            ChipKindRegistry.ForKey(target.KindKey)?.Navigate(target.Reference);
        }
    }
}
