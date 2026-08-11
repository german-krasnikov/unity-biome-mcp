// T-4.2: IToolCardRenderer for the 11 object mutation tools + batch.
//
// Idempotency: CSS class "mutation-rendered" prevents double-render.
// Two-pass: first OnUpdate renders entries with "?" was-placeholders;
//           second OnUpdate (with ResultText) fills in the actual was-value in-place.
// Navigation: clicking any mutation-entry row selects the object in the Hierarchy.
using System;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class MutationDiffCard : IToolCardRenderer
    {
        private static readonly string[] ToolNames =
        {
            "set_property", "set_property_delta", "set_active",
            "create_object", "delete_object", "manage_component",
            "set_parent", "rename_object", "wire_event",
            "batch", "apply_scene_change"
        };

        static MutationDiffCard()
        {
            var inst = new MutationDiffCard();
            foreach (var name in ToolNames)
                ToolCardRendererRegistry.Register(name, inst);
        }

        public void OnStart(VisualElement chip, ToolCallRecord rec) { }

        public void OnUpdate(VisualElement chip, ToolCallRecord rec)
        {
            if (rec.ArgsJson == null) return;

            if (!chip.ClassListContains("mutation-rendered"))
            {
                var args = ObjectMutationParser.Parse(rec.Name, rec.ArgsJson);
                if (!args.IsValid) return;
                chip.AddToClassList("mutation-rendered");
                RenderEntry(chip, rec.Name, args);
            }

            // Second call brings ResultText — update was-value labels in-place.
            if (rec.HasResult)
                RefreshWasValues(chip, rec.ResultText);
        }

        // ── Rendering ─────────────────────────────────────────────────────────────

        private static void RenderEntry(VisualElement chip, string toolName, MutationArgs args)
        {
            var row = new VisualElement();
            row.AddToClassList("mutation-entry");

            // Click → select object in Hierarchy panel.
            var objectPath = args.Path ?? args.Name;
            if (!string.IsNullOrEmpty(objectPath))
                NavBindingHelper.Attach(row, new NavTarget(ChipKindKeys.Hierarchy, objectPath));

            var (prefix, mainText, addWas) = FormatEntry(toolName, args);

            var prefixLabel = new Label(prefix);
            prefixLabel.AddToClassList("mutation-prefix");
            row.Add(prefixLabel);

            var mainLabel = new Label(mainText);
            mainLabel.AddToClassList("mutation-main");
            row.Add(mainLabel);

            // Was-value placeholder: "?" until result arrives and RefreshWasValues fills it.
            if (addWas)
            {
                var wasLabel = new Label("?");
                wasLabel.AddToClassList("mutation-was");
                row.Add(wasLabel);
            }

            chip.Add(row);
        }

        private static (string prefix, string mainText, bool addWas) FormatEntry(
            string toolName, MutationArgs args)
        {
            switch (args.Kind)
            {
                case MutationKind.SetProperty:
                    var prop = args.Property ?? args.Path ?? toolName;
                    return ("~", prop + ": " + (args.Value ?? ""), true);

                case MutationKind.CreateObject:
                    return ("+", args.Name ?? args.Path ?? toolName, false);

                case MutationKind.DeleteObject:
                    return ("-", args.Path ?? toolName, false);

                case MutationKind.RenameObject:
                    return ("⟳", (args.OldName ?? "") + " → " + (args.NewName ?? ""), false);

                case MutationKind.ManageComponent:
                    return ("+", (args.Path ?? "") + " [" + (args.Name ?? "") + "]", false);

                default:
                    return ("~", args.Path ?? toolName, false);
            }
        }

        // ── Was-value update ──────────────────────────────────────────────────────

        // Replace "?" placeholders in .mutation-was labels with the actual was-value
        // extracted from the result text (e.g. "maxHealth = 150 (was 100)").
        private static void RefreshWasValues(VisualElement chip, string resultText)
        {
            var wasValue = ParseWasValue(resultText);
            if (wasValue == null) return;
            chip.Query<Label>(className: "mutation-was").ForEach(lbl =>
            {
                if (lbl.text == "?") lbl.text = wasValue;
            });
        }

        // Extracts "100" from text containing "(was 100)". Guards against truncated results.
        private static string ParseWasValue(string resultText)
        {
            if (string.IsNullOrEmpty(resultText)) return null;
            var idx = resultText.IndexOf("(was ", StringComparison.Ordinal);
            if (idx < 0) return null;
            var start = idx + 5;
            var end   = resultText.IndexOf(')', start);
            return end < 0 ? null : resultText.Substring(start, end - start);
        }
    }
}
