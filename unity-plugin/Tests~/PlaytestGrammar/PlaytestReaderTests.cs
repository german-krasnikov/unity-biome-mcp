using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Playtest.Core.SharedTests;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using Form = UnityMCP.Playtest.Core.PlaytestGrammar.Form;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;
using static UnityMCP.Playtest.Core.GrammarTests.MatchShow;
using static UnityMCP.Playtest.Core.GrammarTests.SyntheticForms;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The reader of a command: the form of a line, the order of the forms, the diagnostic of a line no form reads, and the blocks. The expectations are what the reader in JavaScript says
    // (readSurface of the SURFACE playtest, the lines of a script numbered from 1), printed for these scripts, not read off this code: the construct and the slots of a use with their columns,
    // the code, the line, the column and the text of a diagnostic. The positions the examples of the repository hold it to: RowsTests (dsl/playtest/reader.rows).
    public class PlaytestReaderTests
    {
        [Test]
        public void Read_ALine_GivesTheFormTheBindingsAndWhereItWas()
        {
            var lines = new[] { "LOG before", "  WAIT   2.5", "LOG after" };
            var i = 1;
            var diags = new List<PlaytestDiagnostic>();
            var use = PlaytestReader.Read(lines, new[] { 1, 2, 3 }, ref i, diags);
            Assert.That(use.Form.Construct, Is.EqualTo("PT_WAIT"));
            Assert.That(MatchShow.Bound(use.Head), Is.EqualTo("seconds=2.5@9"));
            Assert.That(use.Items, Is.Empty);
            Assert.That(new[] { use.Index, use.EndIndex, use.Col }, Is.EqualTo(new[] { 1, 1, 2 }), "index, end index and the 0-based column of the first token");
            Assert.That(use.Text, Is.EqualTo("WAIT   2.5"), "the line as written, without the blanks around it");
            Assert.That(i, Is.EqualTo(2), "the cursor is past the line");
            Assert.That(diags, Is.Empty);
        }

        [Test]
        public void Read_AScript_ReadsCommandAfterCommand_AndPassesBlankLinesAndComments()
        {
            var r = ReaderHarness.Read(new[] { "LOG a", "", "# note", "  ", "\t", "LOG b #c" });
            Assert.That(Uses(r), Is.EqualTo("PT_LOG 1-1: message=a@4 | PT_LOG 6-6: message=b #c@4"));
            Assert.That(r.Diagnostics, Is.Empty);
        }

        [Test]
        public void Read_ABlankLineOrAComment_IsNoCommand_AndMovesOn_WithNothingSaid()
        {
            foreach (var line in new[] { "", "   ", "\t", "# note", "   # note", "#" })
            {
                var i = 0;
                var diags = new List<PlaytestDiagnostic>();
                Assert.That(PlaytestReader.Read(new[] { line }, new[] { 1 }, ref i, diags), Is.Null, line);
                Assert.That(i, Is.EqualTo(1), line);
                Assert.That(diags, Is.Empty, line);
            }
        }

        [Test]
        public void Read_ALineNoFormReads_IsNull_ItIsSaid_AndTheCursorMovesOn()
        {
            var lines = new[] { "WAIT abc", "LOG x" };
            var i = 0;
            var diags = new List<PlaytestDiagnostic>();
            Assert.That(PlaytestReader.Read(lines, new[] { 1, 2 }, ref i, diags), Is.Null);
            Assert.That(i, Is.EqualTo(1));
            Assert.That(diags.Select(ReaderHarness.Say), Is.EqualTo(new[] { "PT_NUMBER@1:6 WAIT: \"abc\" is not a number (seconds)" }));
        }

        // The line numbers are those of the source of each line (a line of an INCLUDE or a CALL is not in step with its index); the index is the position in `lines`.
        [Test]
        public void Read_SaysTheLineNumberItIsGiven_AndTheIndexInTheLines()
        {
            var lines = new[] { "LOG a", "WAIT abc", "ASSERT_BATCH", "LOG hi", "END" };
            var r = ReaderHarness.Read(lines, new[] { 10, 20, 31, 32, 33 });
            Assert.That(r.Diagnostics.Select(d => d.Line + "/" + d.Index + "/" + d.Construct), Is.EqualTo(new[] { "20/1/PT_WAIT", "32/3/" }));
        }

        [Test]
        public void Read_TheFirstFormOfTheVerbThatReadsTheLineWins()
        {
            Assert.That(Uses(ReaderHarness.Read("MCP get_hierarchy depth=2 INTO $tree")), Is.EqualTo("PT_MCP_INTO 1-1: args=depth=2@18; command=get_hierarchy@4; var=$tree@31"));
            Assert.That(Uses(ReaderHarness.Read("MCP run_sql query=\"insert INTO $table\"")), Is.EqualTo("PT_MCP 1-1: args=query=\"insert INTO $table\"@12; command=run_sql@4"));
            Assert.That(Uses(ReaderHarness.Read("ASSERT $x")), Is.EqualTo("PT_ASSERT_FLAG 1-1: flag=$x@7"));
            Assert.That(Uses(ReaderHarness.Read("ASSERT $x == 1")), Is.EqualTo("PT_ASSERT 1-1: op===@10; query=$x@7; value=1@13"));
            Assert.That(Uses(ReaderHarness.Read("WAIT_UNTIL $door_open")), Is.EqualTo("PT_WAIT_UNTIL_FLAG 1-1: flag=$door_open@11"));
        }

        [Test]
        public void Read_AVerbIsTakenAsWritten()
        {
            Assert.That(Said(ReaderHarness.Read("wait 1")), Is.EqualTo("PT_UNKNOWN_KEYWORD@1:1 unknown keyword wait"));
            Assert.That(Said(ReaderHarness.Read("Wait 1")), Is.EqualTo("PT_UNKNOWN_KEYWORD@1:1 unknown keyword Wait"));
            Assert.That(Said(ReaderHarness.Read("  FOOBAR_CMD arg1")), Is.EqualTo("PT_UNKNOWN_KEYWORD@1:3 unknown keyword FOOBAR_CMD"));
            Assert.That(Said(ReaderHarness.Read(new[] { "LOG ok", "ASERT $x == 1" })), Is.EqualTo("PT_UNKNOWN_KEYWORD@2:1 unknown keyword ASERT"));
            Assert.That(Said(ReaderHarness.Read("END")), Is.EqualTo("PT_UNKNOWN_KEYWORD@1:1 unknown keyword END"), "an END with no block is no command");
        }

        // Of the forms of a verb that fail, the one that got furthest says what is wrong; of two that fail at one token, the first (the order of the table).
        [Test]
        public void Read_OfTheFormsThatFail_TheOneThatGotFurthestSaysWhatIsWrong_AndOfATieTheFirst()
        {
            var tie = ReaderHarness.Read("ASSERT");
            Assert.That(Said(tie), Is.EqualTo("PT_SYNTAX@1:1 ASSERT: the line ends early"));
            Assert.That(tie.Diagnostics[0].Construct, Is.EqualTo("PT_ASSERT"), "PT_ASSERT_FLAG fails at the same token and comes second");
            var furthest = ReaderHarness.Read("ASSERT /x|C|f == 1 TIMEOUT abc");
            Assert.That(Said(furthest), Is.EqualTo("PT_NUMBER@1:28 ASSERT: \"abc\" is not a number (timeout)"));
            Assert.That(furthest.Diagnostics[0].Construct, Is.EqualTo("PT_ASSERT"));
            Assert.That(ReaderHarness.Read("MCP").Diagnostics[0].Construct, Is.EqualTo("PT_MCP_INTO"), "both forms of MCP fail at the command; the first is named");
        }

        // Ways of a verb made by hand: the table has no verb of which a later form gets further than the first, so this is where "the furthest" is told from "the first".
        private static PlaytestReader.Way WayOf(string construct, params Element[] elements) => new PlaytestReader.Way(new Form(construct, elements, new Element[0][], false), elements);

        private static string Taken(string line, params PlaytestReader.Way[] ways)
        {
            var diags = new List<PlaytestDiagnostic>();
            var form = PlaytestReader.Take(ways, PatternTokens.Split(line), line, 0, 1, diags, out _);
            return form != null ? "read by " + form.Construct : string.Join(" | ", diags.Select(d => d.Construct + " " + ReaderHarness.Say(d)));
        }

        [Test]
        public void Take_OfWaysThatAllFail_TheOneThatGotFurthestSaysWhatIsWrong_WhereverItIs()
        {
            var shortWay = WayOf("PT_V1", W("V"), S("a", SlotType.Number));
            var longWay = WayOf("PT_V2", W("V"), W("x"), S("b", SlotType.Number), S("c", SlotType.Number));
            Assert.That(Taken("V x 2", shortWay, longWay), Is.EqualTo("PT_V2 PT_SYNTAX@1:5 V x: the line ends early"));
            Assert.That(Taken("V x 2", longWay, shortWay), Is.EqualTo("PT_V2 PT_SYNTAX@1:5 V x: the line ends early"));
        }

        [Test]
        public void Take_OfWaysThatFailAtOneToken_TheFirstSaysWhatIsWrong()
        {
            var one = WayOf("PT_V1", W("V"), S("a", SlotType.Number));
            var other = WayOf("PT_V2", W("V"), S("b", SlotType.Number), S("c"));
            Assert.That(Taken("V x", one, other), Is.EqualTo("PT_V1 PT_SYNTAX@1:3 V: \"x\" is not a number (a)"));
            Assert.That(Taken("V x", other, one), Is.EqualTo("PT_V2 PT_SYNTAX@1:3 V: \"x\" is not a number (b)"));
        }

        [Test]
        public void Take_TheFirstWayThatReadsTheLineWins_AndWithNoWayTheFirstWordIsUnknown()
        {
            var anything = WayOf("PT_V1", W("V"), S("t"));
            var number = WayOf("PT_V2", W("V"), S("n", SlotType.Number));
            Assert.That(Taken("V 1", anything, number), Is.EqualTo("read by PT_V1"));
            Assert.That(Taken("V 1", number, anything), Is.EqualTo("read by PT_V2"));
            Assert.That(Taken("wait 1"), Is.EqualTo(" PT_UNKNOWN_KEYWORD@1:1 unknown keyword wait"));
        }

        // The places the plan names. `ASSERT /A|B|c TIMEOUT 5` ends where the operator is due, and the diagnostic is at the last token; so is `ASSERT /x|C|f ==`, where the value is due.
        [TestCase("ASSERT /A|B|c TIMEOUT 5", "PT_SYNTAX@1:23 ASSERT: the line ends early")]
        [TestCase("ASSERT /x|C|f ==", "PT_SYNTAX@1:15 ASSERT: the line ends early")]
        [TestCase("WAIT abc", "PT_NUMBER@1:6 WAIT: \"abc\" is not a number (seconds)")]
        [TestCase("  WAIT abc", "PT_NUMBER@1:8 WAIT: \"abc\" is not a number (seconds)")]
        [TestCase("WAIT \"abc\"", "PT_SYNTAX@1:6 WAIT: \"\"abc\"\" is not a number (seconds)")]
        [TestCase("WAIT", "PT_SYNTAX@1:1 WAIT: the line ends early")]
        [TestCase("SET /A B C 1 2 3", "PT_SYNTAX@1:14 SET: unexpected \"2\"")]
        [TestCase("wait 1", "PT_UNKNOWN_KEYWORD@1:1 unknown keyword wait")]
        [TestCase("CAPTURE_FRAMES 1 INTERVAL 0.1", "PT_COUNT@1:16 CAPTURE_FRAMES: \"1\" is not like [2-9]|[1-9][0-9]+ (count)")]
        [TestCase("ASSERT /x|C|f == 1 TIMEOUT abc", "PT_NUMBER@1:28 ASSERT: \"abc\" is not a number (timeout)")]
        [TestCase("TRACE_FLOW FROM /a TO /b", "PT_SYNTAX@1:23 TRACE_FLOW FROM: the line ends early")]
        [TestCase("SET_ACTIVE /obj yes", "PT_SYNTAX@1:17 SET_ACTIVE: \"yes\" is not one of true|false|True|False|TRUE|FALSE|1|0 (active)")]
        [TestCase("MONITOR a b", "PT_SYNTAX@1:11 MONITOR: unexpected \"b\"")]
        [TestCase("ASSERT_CTA HIDDEN", "PT_SYNTAX@1:12 ASSERT_CTA: \"HIDDEN\" is not one of VISIBLE|CLICKABLE (state)")]
        [TestCase("WAIT_UNTIL q1 == v1 TIMEOUT 5 OR q2 == v2", "PT_SYNTAX@1:31 WAIT_UNTIL: unexpected \"OR\"")]
        [TestCase("VAR $hp", "PT_SYNTAX@1:5 VAR: the line ends early")]
        [TestCase("SETUP foo", "PT_SYNTAX@1:7 SETUP: unexpected \"foo\"")]
        [TestCase("INVOKE /[My Obj]/X Comp Method", "PT_SYNTAX@1:8 INVOKE: \"/[My\" is not like \"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+ (path)")]
        [TestCase("SWEEP_PATH /Player 0.4", "PT_SYNTAX@1:20 SWEEP_PATH: unexpected \"0.4\"")]
        public void Read_ALineNoFormReads_IsSaidAtTheTokenWhereItFailed_AsTheReaderInJavaScriptSaysIt(string line, string said) => Assert.That(Said(ReaderHarness.Read(line)), Is.EqualTo(said));
    }
}
