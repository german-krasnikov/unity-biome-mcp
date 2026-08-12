// T2.3b: IToolCardRenderer for Bash tool calls.
//
// Two-pass rendering:
//   Pass 1 (ArgsJson available, before result): command header + output placeholder.
//          "bash-rendered" is set LAST (after content) — MARKER LAST rule.
//   Pass 2 (result arrived): fill output container with lines.
//          "bash-output-populated" on the container is set LAST inside try/catch
//          so an exception does NOT block retry on the next OnUpdate call.
//
// Exit code: rec.IsOk=false → "bash--error" CSS class on chip (red border via USS).
// Truncation: ResultText.Length >= 2000 (T0.1 threshold) → "bash-truncated" indicator.
// 20-line limit: more than 20 lines → "bash-show-more" button reveals the rest.
using System;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class BashCard : IToolCardRenderer
    {
        private const int    VisibleLineLimit = 20;
        private const int    MaxCommandLen    = 80;
        private const int    TruncationLen    = 2000; // T0.1: raised from 200
        private const string RenderedClass    = "bash-rendered";
        private const string OutputPopulated  = "bash-output-populated";

        // Test seam: when non-null, thrown at start of BuildOutput to simulate failure.
        // Register cleanup via UnityMcpTestBase.RegisterCleanup in tests.
        internal static Exception _outputBuildException = null;

        static BashCard()
        {
            var inst = new BashCard();
            ToolCardRendererRegistry.Register("Bash", inst);
        }

        public void OnStart(VisualElement chip, ToolCallRecord rec) { }

        public void OnUpdate(VisualElement chip, ToolCallRecord rec)
        {
            if (rec.ArgsJson == null) return; // chip-creation call — no args yet

            // Pass 1: render command header (idempotent via RenderedClass)
            if (!chip.ClassListContains(RenderedClass))
            {
                var args = BashArgsParser.Parse(rec.ArgsJson);
                BuildCommandHeader(chip, args);
                chip.AddToClassList(RenderedClass); // MARKER LAST — after all content
            }

            if (!rec.HasResult) return;

            // Pass 2: fill output container (idempotent via OutputPopulated)
            var outputContainer = chip.Q("bash-output");
            if (outputContainer == null || outputContainer.ClassListContains(OutputPopulated)) return;

            try
            {
                if (_outputBuildException != null) throw _outputBuildException;
                BuildOutput(outputContainer, chip, rec);
                outputContainer.AddToClassList(OutputPopulated); // MARKER LAST
            }
            catch
            {
                // Don't set OutputPopulated — next OnUpdate can retry
            }
        }

        private static void BuildCommandHeader(VisualElement chip, BashArgs args)
        {
            if (!string.IsNullOrEmpty(args.Description))
            {
                var desc = new Label(args.Description);
                desc.AddToClassList("bash-description");
                chip.Add(desc);
            }

            var cmd        = args.IsValid ? args.Command : "";
            var displayCmd = cmd.Length > MaxCommandLen
                ? "$ " + cmd.Substring(0, MaxCommandLen) + "…"
                : "$ " + cmd;
            var cmdLabel = new Label(displayCmd);
            cmdLabel.AddToClassList("bash-command");
            chip.Add(cmdLabel);

            var output = new VisualElement();
            output.name = "bash-output";
            chip.Add(output);
        }

        private static void BuildOutput(VisualElement container, VisualElement chip, ToolCallRecord rec)
        {
            if (!rec.IsOk)
                chip.EnableInClassList("bash--error", true);

            var text  = rec.ResultText ?? "";
            var lines = text.Split('\n');

            // Trim trailing empty entry produced by a trailing newline
            int count = lines.Length;
            if (count > 0 && lines[count - 1].Length == 0) count--;

            int visible = count < VisibleLineLimit ? count : VisibleLineLimit;
            for (int i = 0; i < visible; i++)
            {
                var lbl = new Label(lines[i]);
                lbl.AddToClassList("bash-output-line");
                container.Add(lbl);
            }

            if (count > VisibleLineLimit)
                AppendShowMore(container, lines, count);

            if (IsLikelyTruncated(rec.ResultText))
            {
                var ellipsis = new Label("…");
                ellipsis.AddToClassList("bash-truncated");
                container.Add(ellipsis);
            }
        }

        private static void AppendShowMore(VisualElement container, string[] lines, int count)
        {
            var remaining = count - VisibleLineLimit;
            var showMore  = new Label("▼ " + remaining + " more lines…");
            showMore.AddToClassList("bash-show-more");
            var capturedLines = lines;
            var capturedCount = count;
            showMore.RegisterCallback<ClickEvent>(_ =>
            {
                container.Remove(showMore);
                for (int i = VisibleLineLimit; i < capturedCount; i++)
                {
                    var lbl = new Label(capturedLines[i]);
                    lbl.AddToClassList("bash-output-line");
                    container.Add(lbl);
                }
            });
            container.Add(showMore);
        }

        private static bool IsLikelyTruncated(string text) =>
            text != null && text.Length >= TruncationLen;
    }
}
