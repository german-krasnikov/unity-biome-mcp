// T16: Static navigation helper — wires click-to-navigate on operation rows.
// Routes by target_type: "asset" → Script chip; all others → Hierarchy chip.
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    internal static class ChangeSetNavigation
    {
        internal static string ResolveKindKey(string targetType) =>
            targetType == "asset" ? ChipKindKeys.Script : ChipKindKeys.Hierarchy;

        internal static void Attach(VisualElement el, OperationViewModel op)
        {
            if (el == null || op == null || string.IsNullOrEmpty(op.TargetPath)) return;

            var target = new NavTarget(ResolveKindKey(op.TargetType), op.TargetPath);
            if (!target.IsEmpty)
                NavBindingHelper.Attach(el, target);
        }
    }
}
