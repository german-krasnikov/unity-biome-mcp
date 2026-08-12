// T-4.1: IToolCardRenderer for Edit / Write / MultiEdit tool calls.
// Renders an inline diff block with Myers-diff for small files,
// two-block (was/now) for files > 80 lines.
// Idempotency guard: chip must NOT have "diff-rendered" class on entry.
using System;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class CodeEditDiffRenderer : ToolCardBase
    {
        // Test seam: when non-null, thrown inside TryBuildContent BEFORE RenderEdits.
        // GREEN: ToolCardBase catches the exception, marker ("diff-rendered") is NOT set → retry allowed.
        // Double-red B: revert to IToolCardRenderer and move marker before seam → frozen card.
        internal static System.Exception _renderEditsException = null;

        static CodeEditDiffRenderer()
        {
            var inst = new CodeEditDiffRenderer();
            ToolCardRendererRegistry.Register("Edit",      inst);
            ToolCardRendererRegistry.Register("Write",     inst);
            ToolCardRendererRegistry.Register("MultiEdit", inst);
        }

        internal CodeEditDiffRenderer() : base("diff-rendered") { }

        protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
        {
            if (string.IsNullOrEmpty(rec.ArgsJson)) return false; // T1.1
            var args = CodeEditArgsParser.Parse(rec.ArgsJson);
            if (!args.IsValid) return false;
            if (_renderEditsException != null) throw _renderEditsException; // test seam BEFORE content
            RenderEdits(chip, args);
            return true;
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        // Collapse into a Foldout when there are this many edits or more.
        // At ≥5 edits eager rendering risks exceeding the frame budget.
        private const int CollapseThreshold = 5;

        private static void RenderEdits(VisualElement chip, CodeEditArgs args)
        {
            var header = new Label("✎ " + args.FilePath);
            header.AddToClassList("diff-header");
            NavBindingHelper.Attach(header, new NavTarget(ChipKindKeys.Script, args.FilePath));
            chip.Add(header);

            var block = new VisualElement();
            block.AddToClassList("code-diff-block");
            chip.Add(block);

            if (args.Edits != null && args.Edits.Length >= CollapseThreshold)
            {
                RenderCollapsed(block, args.Edits);
            }
            else if (args.Edits != null && args.Edits.Length > 0)
            {
                foreach (var edit in args.Edits)
                    RenderOnePair(block, edit.OldString, edit.NewString);
            }
            else if (args.OldString != null || args.NewString != null)
            {
                RenderOnePair(block, args.OldString, args.NewString);
            }
            else if (args.Content != null)
            {
                foreach (var line in SplitLines(args.Content))
                    block.Add(MakeLine(line, "diff-add"));
            }
        }

        private static void RenderCollapsed(VisualElement container, CodeEditEdit[] edits)
        {
            var foldout = new Foldout { text = $"{edits.Length} edits", value = false };
            foldout.AddToClassList("diff-edits-foldout");
            var built = false;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue || built) return;
                built = true;
                foreach (var edit in edits)
                    RenderOnePair(foldout.contentContainer, edit.OldString, edit.NewString);
            });
            container.Add(foldout);
        }

        private static void RenderOnePair(VisualElement container, string oldStr, string newStr)
        {
            var result = TextDiffEngine.Compute(oldStr ?? "", newStr ?? "");
            if (result.IsLargeFile)
            {
                RenderTwoBlock(container, oldStr, newStr);
                return;
            }

            foreach (var line in result.Lines)
            {
                var css = line.Kind == DiffLineKind.Added   ? "diff-add"
                        : line.Kind == DiffLineKind.Removed ? "diff-remove"
                        : "diff-context";
                container.Add(MakeLine(line.Text, css));
            }
        }

        private static void RenderTwoBlock(VisualElement container, string oldStr, string newStr)
        {
            if (oldStr != null)
                foreach (var line in SplitLines(oldStr))
                    container.Add(MakeLine(line, "diff-remove"));

            container.Add(MakeLine("─── was / now ───", "diff-context"));

            if (newStr != null)
                foreach (var line in SplitLines(newStr))
                    container.Add(MakeLine(line, "diff-add"));
        }

        private static Label MakeLine(string text, string cssClass)
        {
            var lbl = new Label(text);
            lbl.AddToClassList(cssClass);
            return lbl;
        }

        private static string[] SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
            return text.Replace("\r\n", "\n").Split('\n');
        }
    }
}
