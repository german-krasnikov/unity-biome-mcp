// T2.2: IToolCardRenderer for get_hierarchy.
// Parses the text-tree result, renders the first 20 nodes with depth-indent
// and click-to-select navigation, then reveals the rest on demand.
//
// T2.5: Extends ToolCardBase. The base enforces marker-last and retry-on-throw.
// ShowMoreButton.Append replaces the inline show-more construction.
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class HierarchyCard : ToolCardBase
    {
        private const int VisibleNodeLimit = 20;
        private const int IndentPxPerDepth = 12;

        static HierarchyCard()
        {
            ToolCardRendererRegistry.Register("get_hierarchy", new HierarchyCard());
        }

        internal HierarchyCard() : base("hierarchy-rendered") { }

        protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
        {
            if (rec.ResultText == null) return false; // result not arrived yet

            if (rec.ResultText == "NO_CHANGE") return true;

            var nodes = HierarchyResultParser.Parse(rec.ResultText);
            if (nodes.Length == 0) return true;

            RenderNodes(chip, nodes);
            return true;
        }

        private static void RenderNodes(VisualElement chip, HierarchyNode[] nodes)
        {
            int visible = nodes.Length < VisibleNodeLimit ? nodes.Length : VisibleNodeLimit;
            for (int i = 0; i < visible; i++)
                chip.Add(MakeNodeRow(nodes[i]));

            if (nodes.Length > VisibleNodeLimit)
            {
                var remaining     = nodes.Length - VisibleNodeLimit;
                var capturedNodes = nodes;
                ShowMoreButton.Append(chip, "hierarchy-show-more",
                    "▼ " + remaining + " more objects…",
                    () => AppendRemainingNodes(chip, capturedNodes, VisibleNodeLimit));
            }
        }

        private static void AppendRemainingNodes(VisualElement chip, HierarchyNode[] nodes, int from)
        {
            for (int i = from; i < nodes.Length; i++)
                chip.Add(MakeNodeRow(nodes[i]));
        }

        private static VisualElement MakeNodeRow(HierarchyNode node)
        {
            var row = new VisualElement();

            if (node.IsSceneHeader)
            {
                row.AddToClassList("hierarchy-scene-header");
                row.Add(new Label(node.SceneName));
                return row;
            }

            row.AddToClassList("hierarchy-node");
            row.style.paddingLeft = node.Depth * IndentPxPerDepth;

            if (node.IsInactive)
                row.AddToClassList("hierarchy-inactive");

            row.Add(new Label(node.Name));

            if (node.HiddenCount > 0)
            {
                var hidden = new Label("+" + node.HiddenCount);
                hidden.AddToClassList("hierarchy-hidden-count");
                row.Add(hidden);
            }

            if (!string.IsNullOrEmpty(node.HexRef))
                NavBindingHelper.Attach(row, new NavTarget(ChipKindKeys.Hierarchy, node.HexRef));
            else
                row.AddToClassList("hierarchy-node--no-nav");

            return row;
        }
    }
}
