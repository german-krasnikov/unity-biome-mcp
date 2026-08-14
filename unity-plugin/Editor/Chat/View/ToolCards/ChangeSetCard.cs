// T16: IToolCardRenderer for "get_changeset". Renders on second pass (result available).
// Collapses to Foldout when operation count exceeds CollapseThreshold.
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class ChangeSetCard : ToolCardBase
    {
        private const int CollapseThreshold = 8;

        static ChangeSetCard()
            => ToolCardRendererRegistry.Register("get_changeset", new ChangeSetCard());

        internal ChangeSetCard() : base("changeset-rendered") { }

        // No args to show on first pass — wait for result.
        protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
        {
            if (!rec.HasResult) return false;
            var vm = ChangeSetParser.Parse(rec.ResultText);
            if (vm == null) return false;
            RenderCard(chip, vm);
            return true;
        }

        private static void RenderCard(VisualElement chip, ChangeSetViewModel vm)
        {
            var header = new Label(vm.Summary);
            header.AddToClassList("changeset-header");
            chip.Add(header);

            if (vm.Operations.Length == 0) return;

            if (vm.Operations.Length > CollapseThreshold)
                RenderCollapsed(chip, vm.Operations);
            else
                foreach (var op in vm.Operations)
                    RenderOpRow(chip, op);
        }

        private static void RenderCollapsed(VisualElement container, OperationViewModel[] ops)
        {
            var foldout = new Foldout { text = $"{ops.Length} operations", value = false };
            foldout.AddToClassList("changeset-ops-foldout");
            bool built = false;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue || built) return;
                built = true;
                foreach (var op in ops)
                    RenderOpRow(foldout.contentContainer, op);
            });
            container.Add(foldout);
        }

        private static void RenderOpRow(VisualElement container, OperationViewModel op)
        {
            var row = new VisualElement();
            row.AddToClassList("changeset-op-row");

            var prefix = op.Kind == "create" ? "+" : op.Kind == "delete" ? "-" : "~";
            var text   = $"{prefix} {op.TargetPath}";
            if (op.Prop != null) text += $" {op.Prop}";

            var label = new Label(text);
            label.AddToClassList("changeset-op-label");
            row.Add(label);

            ChangeSetNavigation.Attach(row, op);
            container.Add(row);
        }
    }
}
