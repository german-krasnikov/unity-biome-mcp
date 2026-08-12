// TDD T2.3b — BashCard: IToolCardRenderer for Bash tool calls.
//
// Double-red requirement:
//   A — corrupt any Assert → test RED
//   B — remove ToolCardRendererRegistry.Register("Bash") → registration test RED
//       AND grouper-bypass test RED (Bash chips absorbed by grouper → Count < 2)
//
// Retry-marker test (OnUpdate_OutputBuildThrows_CardRetriableOnNextCall):
//   RED if "bash-output-populated" marker is moved ABOVE BuildOutput call —
//   first call sets marker before exception; second call sees marker → skips →
//   output remains empty → Assert.IsTrue(outputLines.Count > 0) FAILS.
//
// Data: real Bash output — git log, ls, grep, Cyrillic, multi-line, truncated.
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BashCardTests : UnityMcpTestBase
    {
        // ── Registration (RED B: fails when [InitializeOnLoad] register removed) ─

        [Test]
        public void BashCard_IsRegisteredForBash()
        {
            var renderer = ToolCardRendererRegistry.Resolve("Bash");
            Assert.IsNotNull(renderer,
                "BashCard must be registered for 'Bash' via [InitializeOnLoad]");
            Assert.IsInstanceOf<BashCard>(renderer,
                "Resolved renderer must be BashCard");
        }

        // ── OnStart ─────────────────────────────────────────────────────────────

        [Test]
        public void OnStart_DoesNotModifyChip()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-s", null);
            card.OnStart(chip, rec);
            Assert.AreEqual(0, chip.childCount, "OnStart must be a no-op");
        }

        // ── Pass 1: command label shown before result ────────────────────────────

        [Test]
        public void OnUpdate_NoResult_CommandLabelShown_NoOutputLines()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            // Real Bash call: check git working tree status
            var rec = new ToolCallRecord("Bash", "id-1",
                "{\"command\":\"git status\",\"description\":\"check working tree\"}",
                resultText: null);
            card.OnUpdate(chip, rec);
            var cmdLabel = chip.Q<Label>(className: "bash-command");
            Assert.IsNotNull(cmdLabel, "Command label must be present in pass 1 (before result)");
            Assert.IsTrue(cmdLabel.text.Contains("git status"),
                "Command label text must include the actual command");
            var outputLines = chip.Query<Label>(className: "bash-output-line").ToList();
            Assert.AreEqual(0, outputLines.Count,
                "No output lines before result arrives");
        }

        [Test]
        public void OnUpdate_NoResult_RenderedMarkerSet()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-2",
                "{\"command\":\"ls -la /Users/german/Work/Python\"}",
                resultText: null);
            card.OnUpdate(chip, rec);
            Assert.IsTrue(chip.ClassListContains("bash-rendered"),
                "'bash-rendered' marker must be set after pass 1");
        }

        // ── Pass 1: description shown when present ───────────────────────────────

        [Test]
        public void OnUpdate_WithDescription_DescriptionLabelPresent()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-3",
                "{\"command\":\"find /Users/german/Work -name \\\"*.cs\\\" | wc -l\",\"description\":\"count C# files in project\"}",
                resultText: null);
            card.OnUpdate(chip, rec);
            var descLabel = chip.Q<Label>(className: "bash-description");
            Assert.IsNotNull(descLabel,
                "Description label must be present when description field is provided");
            Assert.AreEqual("count C# files in project", descLabel.text);
        }

        [Test]
        public void OnUpdate_NoDescription_DescriptionLabelAbsent()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-4",
                "{\"command\":\"uv run pytest tests -q\"}",
                resultText: null);
            card.OnUpdate(chip, rec);
            var descLabel = chip.Q<Label>(className: "bash-description");
            Assert.IsNull(descLabel,
                "No description label when description field is absent");
        }

        // ── Pass 1: long command truncated for display ───────────────────────────

        [Test]
        public void OnUpdate_LongCommand_TruncatedAt80CharsWithEllipsis()
        {
            var longCmd = "find /Users/german/Work/Python/unity-biome-mcp -name \"*.cs\" -exec grep -l \"IToolCardRenderer\" {} \\;";
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-5",
                "{\"command\":\"" + longCmd.Replace("\"", "\\\"") + "\"}",
                resultText: null);
            card.OnUpdate(chip, rec);
            var cmdLabel = chip.Q<Label>(className: "bash-command");
            Assert.IsNotNull(cmdLabel);
            Assert.IsTrue(cmdLabel.text.EndsWith("…"),
                "Commands longer than 80 chars must be truncated with '…'");
        }

        // ── Pass 2: multiline output rendered ───────────────────────────────────

        [Test]
        public void OnUpdate_MultilineOutput_OutputLinesShown()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            // Real git log --oneline output (3 lines)
            var result = "185d3cfb feat(T2.2): HierarchyCard\n" +
                         "4b31c793 fix(T2.1): move rendered-marker\n" +
                         "6965d062 feat(T2.1): ScreenshotCard";
            var rec = new ToolCallRecord("Bash", "id-6",
                "{\"command\":\"git log --oneline -3\",\"description\":\"recent commits\"}",
                resultText: result, isOk: true);
            card.OnUpdate(chip, rec);
            var lines = chip.Query<Label>(className: "bash-output-line").ToList();
            Assert.AreEqual(3, lines.Count, "Each output line must be a separate label");
            Assert.AreEqual("185d3cfb feat(T2.2): HierarchyCard", lines[0].text);
        }

        // ── Pass 2: empty output ─────────────────────────────────────────────────

        [Test]
        public void OnUpdate_EmptyOutput_NoOutputLines_NoError()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-7",
                "{\"command\":\"touch /tmp/empty_test_file_biome\"}",
                resultText: "", isOk: true);
            card.OnUpdate(chip, rec);
            var lines = chip.Query<Label>(className: "bash-output-line").ToList();
            Assert.AreEqual(0, lines.Count, "Empty output must produce no line labels");
            Assert.IsFalse(chip.ClassListContains("bash--error"),
                "Success exit with empty output must not set error class");
        }

        // ── Pass 2: non-zero exit → error class on chip ──────────────────────────

        [Test]
        public void OnUpdate_NonZeroExitCode_ErrorClassOnChip()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            // ls on a nonexistent path returns non-zero
            var rec = new ToolCallRecord("Bash", "id-8",
                "{\"command\":\"ls /nonexistent/path/for/test\",\"description\":\"check path\"}",
                resultText: "ls: /nonexistent/path/for/test: No such file or directory",
                isOk: false);
            card.OnUpdate(chip, rec);
            Assert.IsTrue(chip.ClassListContains("bash--error"),
                "'bash--error' class must be set on the chip when exit code is non-zero");
        }

        // ── Pass 2: truncated output (2000-char limit from T0.1) ─────────────────

        [Test]
        public void OnUpdate_TruncatedOutput_TruncationIndicatorShown()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            // Simulate 2000-char output that was cut mid-content
            var truncated = new string('x', 1998) + "\ny";  // 2000 chars exactly
            var rec = new ToolCallRecord("Bash", "id-9",
                "{\"command\":\"cat /Users/german/Work/Python/unity-biome-mcp/server/src/unity_mcp/stream_transform.py\"}",
                resultText: truncated, isOk: true);
            card.OnUpdate(chip, rec);
            var indicator = chip.Q<Label>(className: "bash-truncated");
            Assert.IsNotNull(indicator,
                "'bash-truncated' label must appear when output is at the 2000-char limit");
        }

        [Test]
        public void OnUpdate_ShortOutput_NoTruncationIndicator()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-10",
                "{\"command\":\"echo done\"}",
                resultText: "done", isOk: true);
            card.OnUpdate(chip, rec);
            var indicator = chip.Q<Label>(className: "bash-truncated");
            Assert.IsNull(indicator,
                "No truncation indicator for short output (well under 2000 chars)");
        }

        // ── Pass 2: output without trailing newline ───────────────────────────────

        [Test]
        public void OnUpdate_OutputWithoutTrailingNewline_NoSpuriousEmptyLine()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            // grep output: no trailing newline
            var result = "server/src/unity_mcp/stream_transform.py:8:_MAX_TOOL_RESULT_LEN = 2000\nserver/src/unity_mcp/stream_transform.py:132:    text[:_MAX_TOOL_RESULT_LEN]";
            var rec  = new ToolCallRecord("Bash", "id-11",
                "{\"command\":\"grep -n \\\"_MAX_TOOL_RESULT_LEN\\\" server/src/unity_mcp/stream_transform.py\"}",
                resultText: result, isOk: true);
            card.OnUpdate(chip, rec);
            var lines = chip.Query<Label>(className: "bash-output-line").ToList();
            Assert.AreEqual(2, lines.Count,
                "Output without trailing newline must produce exactly 2 lines (no spurious empty line)");
        }

        // ── Pass 2: Cyrillic in output ────────────────────────────────────────────

        [Test]
        public void OnUpdate_CyrillicOutput_PreservedCorrectly()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-12",
                "{\"command\":\"echo \\\"Привет мир\\\"\\necho \\\"Кириллица в выводе\\\"\",\"description\":\"проверка кириллицы\"}",
                resultText: "Привет мир\nКириллица в выводе", isOk: true);
            card.OnUpdate(chip, rec);
            var lines = chip.Query<Label>(className: "bash-output-line").ToList();
            Assert.AreEqual(2, lines.Count, "Two Cyrillic lines rendered");
            Assert.AreEqual("Привет мир", lines[0].text);
            Assert.AreEqual("Кириллица в выводе", lines[1].text);
        }

        // ── 20-line truncation with show-more button ──────────────────────────────

        [Test]
        public void OnUpdate_TwentyFiveLines_Shows20LinesAndShowMoreButton()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= 25; i++)
                sb.AppendLine($"  {i,4}: line content from uv run pytest --tb=short output");
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-13",
                "{\"command\":\"uv run pytest tests -q --tb=short\",\"description\":\"run unit tests\"}",
                resultText: sb.ToString(), isOk: true);
            card.OnUpdate(chip, rec);
            var outputLines = chip.Query<Label>(className: "bash-output-line").ToList();
            var showMore    = chip.Q<Label>(className: "bash-show-more");
            Assert.AreEqual(20, outputLines.Count,
                "Exactly 20 output lines visible before 'show more'");
            Assert.IsNotNull(showMore,
                "'bash-show-more' label must appear when output has more than 20 lines");
        }

        [Test]
        public void OnUpdate_TwentyFiveLines_ShowMoreMentionsFiveMore()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= 25; i++)
                sb.AppendLine($"line {i}");
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-14",
                "{\"command\":\"find . -name \\\"*.py\\\" | wc -l\"}",
                resultText: sb.ToString(), isOk: true);
            card.OnUpdate(chip, rec);
            var showMore = chip.Q<Label>(className: "bash-show-more");
            Assert.IsNotNull(showMore, "show-more label must exist");
            Assert.IsTrue(showMore.text.Contains("5"),
                "Label text must mention '5' remaining lines");
        }

        // ── Idempotency ───────────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_CalledTwice_CommandLabelNotDuplicated()
        {
            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-15",
                "{\"command\":\"python install.py doctor\",\"description\":\"verify installation\"}",
                resultText: "All checks passed", isOk: true);
            card.OnUpdate(chip, rec);
            card.OnUpdate(chip, rec); // second call must be idempotent
            var cmdLabels = chip.Query<Label>(className: "bash-command").ToList();
            Assert.AreEqual(1, cmdLabels.Count,
                "Second OnUpdate must not duplicate command label (idempotency)");
        }

        // ── Retry after failed output build ──────────────────────────────────────
        //
        // Proves marker "bash-output-populated" is set LAST (after content):
        //   RED if the marker is moved BEFORE BuildOutput call — first call sets
        //   marker before exception; second call sees marker, skips; output stays empty.

        [Test]
        public void OnUpdate_OutputBuildThrows_CardRetriableOnNextCall()
        {
            // Inject a failure into output building
            BashCard._outputBuildException = new InvalidOperationException("simulated build failure");
            RegisterCleanup(() => { BashCard._outputBuildException = null; });

            var card = new BashCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-retry",
                "{\"command\":\"uv run pytest tests -m 'not live' -q\",\"description\":\"run unit tests\"}",
                resultText: "FAILED tests/test_something.py::test_case\n1 failed, 247 passed",
                isOk: false);

            card.OnUpdate(chip, rec); // must NOT propagate exception; output NOT populated

            var outputLines = chip.Query<Label>(className: "bash-output-line").ToList();
            Assert.AreEqual(0, outputLines.Count,
                "Failed output build must produce no line labels");

            var outputContainer = chip.Q("bash-output");
            Assert.IsFalse(outputContainer?.ClassListContains("bash-output-populated") ?? false,
                "Populated marker must NOT be set after a failed build. " +
                "Bug: marker placed before content, blocking all future retries.");

            // Clear exception, retry must succeed
            BashCard._outputBuildException = null;
            card.OnUpdate(chip, rec);

            outputLines = chip.Query<Label>(className: "bash-output-line").ToList();
            Assert.IsTrue(outputLines.Count > 0,
                "Card must render output lines on retry after exception is cleared");
            Assert.IsTrue(outputContainer?.ClassListContains("bash-output-populated") ?? false,
                "Populated marker must be set after successful render");
        }

        // ── Grouper bypass: real BashCard triggers card-chip class ─────────────────
        //
        // RED B: unregister BashCard → grouper absorbs both chips → Count < 2 → FAIL.

        [Test]
        public void TwoBashChips_BothVisibleInFeed_NotAbsorbedByGrouper()
        {
            var container  = new VisualElement();
            var registry   = ChatBlockRendererFactory.CreateDefault(null, null);
            var transcript = new ChatTranscript(container, registry);

            // Real Bash call names (same tool, two consecutive calls)
            transcript.AppendToolChip("Bash", ok: true, toolId: "bash-1");
            transcript.AppendToolChip("Bash", ok: true, toolId: "bash-2");
            transcript.FinalizeAssistant();

            var cardChips = container.Query(className: "card-chip").ToList();
            Assert.AreEqual(2, cardChips.Count,
                "Both Bash chips must bypass the grouper and appear as card-chip elements");

            var foldout = container.Q<Foldout>(className: "tool-group");
            if (foldout != null)
                Assert.IsNull(foldout.Q(className: "card-chip"),
                    "No card-chip may reside inside a collapsed tool-group foldout");
        }
    }
}
