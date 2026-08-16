// T2.2: IToolCardRenderer for get_hierarchy.
// Parses the text-tree result, renders the first 20 entries with depth-indent
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
        private const int VisibleEntryLimit = 20;
        private const int IndentPxPerDepth = 12;
        private const int ToolResultCharacterLimit = 2000;

        static HierarchyCard()
        {
            ToolCardRendererRegistry.Register("get_hierarchy", new HierarchyCard());
        }

        internal HierarchyCard() : base("hierarchy-rendered") { }

        protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
        {
            if (rec.ResultText == null) return false; // result not arrived yet

            if (rec.ResultText == "NO_CHANGE") return true;

            if (IsSummaryRequest(rec.ArgsJson))
            {
                if (!string.IsNullOrWhiteSpace(rec.ResultText))
                    RenderSummary(chip, rec.ResultText);
                return true;
            }

            var parseText = RemovePossiblyTruncatedFinalLine(rec.ResultText);
            var nodes = HierarchyResultParser.Parse(parseText,
                parseComponents: IsTrueArg(rec.ArgsJson, "components"));
            if (nodes.Length == 0) return true;

            RenderNodes(chip, nodes);
            return true;
        }

        private static bool IsSummaryRequest(string argsJson) =>
            IsTrueArg(argsJson, "summary");

        private static bool IsTrueArg(string argsJson, string key) =>
            JsonHelper.ExtractString(argsJson, key) == "true";

        private static string RemovePossiblyTruncatedFinalLine(string resultText)
        {
            // stream_transform caps tool results at 2000 characters without a marker.
            // At that boundary only newline-terminated rows are safe: a partial base62
            // token can still be syntactically valid and alias another live reference.
            if (resultText.Length < ToolResultCharacterLimit ||
                resultText[resultText.Length - 1] == '\n')
                return resultText;

            int finalNewline = resultText.LastIndexOf('\n');
            return finalNewline < 0 ? "" : resultText.Substring(0, finalNewline + 1);
        }

        private static void RenderSummary(VisualElement chip, string resultText)
        {
            var summary = ChatLabel.Selectable(resultText.TrimEnd());
            summary.AddToClassList("hierarchy-summary");
            chip.Add(summary);
        }

        private static void RenderNodes(VisualElement chip, HierarchyNode[] nodes)
        {
            int visible = nodes.Length < VisibleEntryLimit ? nodes.Length : VisibleEntryLimit;
            for (int i = 0; i < visible; i++)
                chip.Add(MakeNodeRow(nodes[i]));

            if (nodes.Length > VisibleEntryLimit)
            {
                var remaining     = nodes.Length - VisibleEntryLimit;
                var capturedNodes = nodes;
                ShowMoreButton.Append(chip, "hierarchy-show-more",
                    "▼ " + remaining + " more entries…",
                    () => AppendRemainingNodes(chip, capturedNodes, VisibleEntryLimit));
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

            if (!string.IsNullOrEmpty(node.Reference))
                NavBindingHelper.Attach(row, new NavTarget(ChipKindKeys.Hierarchy, node.Reference));
            else
                row.AddToClassList("hierarchy-node--no-nav");

            return row;
        }
    }
}
