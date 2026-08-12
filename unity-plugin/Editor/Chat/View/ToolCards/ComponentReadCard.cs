// T2.4: IToolCardRenderer for get_component / inspect / get_components_list.
//
// Two-pass rendering via ToolCardBase:
//   Pass 1 (TryBuildContent): ArgsJson available → primary layout.
//          Base sets "comp-read-rendered" marker LAST.
//   Pass 2 (OnAdditionalRender): result arrives →
//          get_component     → EnrichWithProperties (guarded by "comp-read-props-populated")
//          get_components_list → EnrichWithComponentsList (guarded by "comp-read-result-populated")
//          inspect           → no result enrichment (multi-object result too variable)
//
// Navigation: path pills link to Hierarchy via NavBindingHelper.
//             $HEX IDs in get_components_list are resolved via TransientObjectId.
//             #decimal IDs cannot navigate — rendered as plain labels.
//
// Layout (≈400px panel): long values truncated by Truncate(); no table alignment.
// Show-more: >20 properties guarded by ShowMoreButton.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class ComponentReadCard : ToolCardBase
    {
        private const int    MaxVisibleProps  = 20;
        private const int    MaxPathPills     = 3;
        private const int    MaxValueLen      = 80;
        private const int    MaxPathLen       = 50;
        private const string PropsPopulated   = "comp-read-props-populated";
        private const string ResultPopulated  = "comp-read-result-populated";

        static ComponentReadCard()
        {
            var inst = new ComponentReadCard();
            ToolCardRendererRegistry.Register("get_component",       inst);
            ToolCardRendererRegistry.Register("inspect",             inst);
            ToolCardRendererRegistry.Register("get_components_list", inst);
        }

        internal ComponentReadCard() : base("comp-read-rendered") { }

        // Pass 1: build primary content from argsJson.
        protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
        {
            if (rec.ArgsJson == null) return false;
            var args = ComponentReadArgsParser.Parse(rec.Name, rec.ArgsJson);
            if (!args.IsValid) return false;
            BuildPrimary(chip, args);
            return true;
        }

        // Pass 2: enrich with result data when it arrives.
        protected override void OnAdditionalRender(VisualElement chip, ToolCallRecord rec)
        {
            if (!rec.HasResult) return;
            if (rec.Name == "get_component")
                EnrichWithProperties(chip, rec.ResultText);
            else if (rec.Name == "get_components_list")
                EnrichWithComponentsList(chip, rec.ResultText);
        }

        // ── Primary rendering ──────────────────────────────────────────────────

        private static void BuildPrimary(VisualElement chip, ComponentReadArgs args)
        {
            switch (args.Kind)
            {
                case ReadToolKind.GetComponent:      BuildGetComponent(chip, args);      break;
                case ReadToolKind.Inspect:           BuildInspect(chip, args);           break;
                case ReadToolKind.GetComponentsList: BuildGetComponentsList(chip, args); break;
            }
        }

        private static void BuildGetComponent(VisualElement chip, ComponentReadArgs args)
        {
            var row = new VisualElement();
            row.AddToClassList("comp-read-entry");

            var pathLabel = new Label(Truncate(args.Path, MaxPathLen));
            pathLabel.AddToClassList("comp-read-path");
            NavBindingHelper.Attach(pathLabel, new NavTarget(ChipKindKeys.Hierarchy, args.Path));
            row.Add(pathLabel);

            if (!string.IsNullOrEmpty(args.ComponentType))
            {
                var typeLabel = new Label(args.ComponentType);
                typeLabel.AddToClassList("comp-read-type");
                row.Add(typeLabel);
            }

            chip.Add(row);
        }

        private static void BuildInspect(VisualElement chip, ComponentReadArgs args)
        {
            var row = new VisualElement();
            row.AddToClassList("comp-read-entry");

            if (args.Paths.Length <= MaxPathPills)
            {
                foreach (var path in args.Paths)
                {
                    var pathLabel = new Label(Truncate(path, MaxPathLen));
                    pathLabel.AddToClassList("comp-read-path");
                    NavBindingHelper.Attach(pathLabel, new NavTarget(ChipKindKeys.Hierarchy, path));
                    row.Add(pathLabel);
                }
            }
            else
            {
                var countLabel = new Label(args.Paths.Length + " objects");
                countLabel.AddToClassList("comp-read-count");
                row.Add(countLabel);
            }

            if (args.Components != null && args.Components.Length > 0)
            {
                var compsLabel = new Label(string.Join(", ", args.Components));
                compsLabel.AddToClassList("comp-read-type");
                row.Add(compsLabel);
            }

            chip.Add(row);
        }

        private static void BuildGetComponentsList(VisualElement chip, ComponentReadArgs args)
        {
            var row = new VisualElement();
            row.AddToClassList("comp-read-entry");

            var idLabel = new Label(args.ObjectId);
            idLabel.AddToClassList("comp-read-path");

            // $HEX IDs resolve via TransientObjectId → link to Hierarchy.
            // #decimal IDs do not; HierarchyReference.Parse cannot resolve them.
            if (args.ObjectId != null && args.ObjectId.StartsWith("$"))
                NavBindingHelper.Attach(idLabel, new NavTarget(ChipKindKeys.Hierarchy, args.ObjectId));

            row.Add(idLabel);
            chip.Add(row);
        }

        // ── Secondary: get_component properties ───────────────────────────────

        private static void EnrichWithProperties(VisualElement chip, string resultText)
        {
            if (chip.ClassListContains(PropsPopulated)) return;
            chip.AddToClassList(PropsPopulated); // claim guard before any allocations

            var propLines = ParsePropLines(resultText);
            if (propLines.Count == 0) return;

            var container = new VisualElement();
            container.AddToClassList("comp-read-props");

            int visible = propLines.Count < MaxVisibleProps ? propLines.Count : MaxVisibleProps;
            for (int i = 0; i < visible; i++)
            {
                var lbl = new Label(Truncate(propLines[i], MaxValueLen));
                lbl.AddToClassList("comp-read-prop");
                container.Add(lbl);
            }

            if (propLines.Count > MaxVisibleProps)
            {
                var capturedLines = propLines;
                ShowMoreButton.Append(container, "comp-read-show-more",
                    "▼ " + (propLines.Count - MaxVisibleProps) + " more…",
                    () =>
                    {
                        for (int i = MaxVisibleProps; i < capturedLines.Count; i++)
                        {
                            var lbl = new Label(Truncate(capturedLines[i], MaxValueLen));
                            lbl.AddToClassList("comp-read-prop");
                            container.Add(lbl);
                        }
                    });
            }

            chip.Add(container);
        }

        // ── Secondary: get_components_list component names ────────────────────

        private static void EnrichWithComponentsList(VisualElement chip, string resultText)
        {
            if (chip.ClassListContains(ResultPopulated)) return;
            chip.AddToClassList(ResultPopulated);

            if (string.IsNullOrEmpty(resultText)) return;

            // Result format: one type name per line (Transform excluded by C# side)
            var display = resultText.Trim().Replace('\n', ',').TrimEnd(',');
            var lbl = new Label(display);
            lbl.AddToClassList("comp-read-result");
            chip.Add(lbl);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<string> ParsePropLines(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length > 0)
                    result.Add(line);
            }
            return result;
        }

        private static string Truncate(string s, int maxLen) =>
            s != null && s.Length > maxLen ? s.Substring(0, maxLen) + "…" : s ?? "";
    }
}
