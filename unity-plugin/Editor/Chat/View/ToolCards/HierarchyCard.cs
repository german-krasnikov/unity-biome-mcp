// T2.2: IToolCardRenderer for get_hierarchy.
// Parses the text-tree result, renders the first 20 nodes with depth-indent
// and click-to-select navigation, then reveals the rest on demand.
//
// MARKER LAST rule (from team review): hierarchy-rendered is set AFTER all DOM
// mutations so a thrown exception in rendering does not permanently block
// re-rendering on the next OnUpdate call.
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class HierarchyCard : IToolCardRenderer
    {
        private const int    VisibleNodeLimit = 20;
        private const int    IndentPxPerDepth = 12;
        private const string RenderedClass    = "hierarchy-rendered";

        static HierarchyCard()
        {
            var inst = new HierarchyCard();
            ToolCardRendererRegistry.Register("get_hierarchy", inst);
        }

        public void OnStart(VisualElement chip, ToolCallRecord rec) { }

        public void OnUpdate(VisualElement chip, ToolCallRecord rec)
        {
            if (rec.ResultText == null) return;                // result not arrived yet
            if (chip.ClassListContains(RenderedClass)) return; // idempotency guard

            if (rec.ResultText == "NO_CHANGE")
            {
                chip.AddToClassList(RenderedClass);
                return;
            }

            var nodes = HierarchyResultParser.Parse(rec.ResultText);
            if (nodes.Length == 0)
            {
                chip.AddToClassList(RenderedClass);
                return;
            }

            RenderNodes(chip, nodes);
            chip.AddToClassList(RenderedClass); // LAST — after successful rendering
        }

        private static void RenderNodes(VisualElement chip, HierarchyNode[] nodes)
        {
            int visible = nodes.Length < VisibleNodeLimit ? nodes.Length : VisibleNodeLimit;

            for (int i = 0; i < visible; i++)
                chip.Add(MakeNodeRow(nodes[i]));

            if (nodes.Length > VisibleNodeLimit)
            {
                var remaining = nodes.Length - VisibleNodeLimit;
                var showMore  = new Label("▼ " + remaining + " more objects…");
                showMore.AddToClassList("hierarchy-show-more");
                var capturedNodes = nodes;
                showMore.RegisterCallback<ClickEvent>(_ =>
                {
                    chip.Remove(showMore);
                    AppendRemainingNodes(chip, capturedNodes, VisibleNodeLimit);
                });
                chip.Add(showMore);
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
