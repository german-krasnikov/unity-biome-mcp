using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // What a slot of the table takes as a value, and how it is called in a message. The expectations are those of the reader of the SURFACE in JavaScript
    // (/^-?\d+(\.\d+)?$/, /^\d+$/, new RegExp("^(?:" + expression + ")$"), the words of an enum), found by running it on every value below, not read off SlotCheck.
    public class SlotCheckTests
    {
        private static string Chr(int code) => ((char)code).ToString();

        private static Element Slot(SlotType type, string arg = null) => new Element(ElementKind.Slot, "x", type, arg);

        private static IEnumerable<Element> Walk(IEnumerable<Element> elements) =>
            elements.SelectMany(e => e.Kind == ElementKind.Group ? Walk(e.Items) : new[] { e });

        // The slot `name` of the form `construct` of the real table: the cases are about the expressions the table holds, not about a copy of them.
        private static Element Table(string construct, string name) =>
            Walk(PlaytestGrammar.Forms.Single(f => f.Construct == construct).Elements).Single(e => e.Kind == ElementKind.Slot && e.Text == name);

        private static bool Takes(Element slot, string raw) => SlotCheck.Accepts(slot, raw);

        // What a slot gets wrong among the values that it must take and the values that it must refuse, one line each.
        private static List<string> Offenders(string name, Element slot, string[] take, string[] refuse) =>
            take.Where(raw => !Takes(slot, raw)).Select(raw => name + " must take [" + raw + "]")
                .Concat(refuse.Where(raw => Takes(slot, raw)).Select(raw => name + " must refuse [" + raw + "]")).ToList();

        private static readonly (string Construct, string Slot, string[] Take, string[] Refuse)[] Expressions =
        {
            ("PT_SET", "path", new[] { "/Player", "\"/My Object\"", "$hud|submitBtn", "/[Box]/X", "\"\"", "\"unclosed", "x\"y", "]", "/a]b" },
                new[] { "/[Open", "/[a]/[b", "[", "/x[y]z[w", "ab\n" }),
            ("PT_WAIT_UNTIL_FLAG", "flag", new[] { "$x", "!$x", "$" }, new[] { "x$", "!x", "!!$x", "$x\n" }),
            ("PT_MOVE", "to", new[] { "1,2,3", "(1,2,3)", "(1,2,3", "1,2,3)", "-1.5e3,+2,3", "@/Enemy.position", "$p", "\"a b\"", "..,.,." },
                new[] { "1,2", "1,2,3,4", "@", "$", "x,y,z" }),
            ("PT_ASSERT_CONSERVED", "expected", new[] { "CONSTANT", "-1.5", "7", "007" }, new[] { "1.", ".5", "constant", "CONSTANTS", "-", Chr(0x0663) }),
            ("PT_MCP_INTO", "var", new[] { "$tree", "$_x1" }, new[] { "$1x", "$", "tree", "$a" + Chr(0x00E9), "$a b" }),
            ("PT_VAR", "name", new[] { "$hp", "hp", "_a" }, new[] { "$", "$1a", "a-b", "a" + Chr(0x00E9) }),
            ("PT_VAR", "query", new[] { "@/Player|Health|hp", "@||", "@a|b|c|d" }, new[] { "@NotAPipePath", "@a|b", "/Player|Health|hp" }),
            ("PT_ASSERT_FLAG", "flag", new[] { "$x", "/x", "#x", "(x)", "!(x)", "$" }, new[] { "()", "x", "!" }),
            ("PT_ASSERT", "op", new[] { "<", "<=", ">", ">=", "==", "!=", "contains" }, new[] { "=", "=>", "<>", "Contains", "contain", "<==" }),
            ("PT_CAPTURE_FRAMES", "count", new[] { "2", "9", "10", "99", "100" }, new[] { "1", "0", "01", "10x" }),
            // PT-8/Q22: the playtest SURFACE now says CASEFOLD, so an enum word folds ASCII
            // case too — "tRue" and "increased" are case-folds of listed words, not refusals.
            ("PT_SET_ACTIVE", "active", new[] { "true", "False", "TRUE", "tRue", "1", "0" }, new[] { "yes", "2", "01", "tru" }),
            ("PT_ASSERT_CAPTURED", "mode", new[] { "INCREASED", "INCREASED_BY", "UNCHANGED", "increased" }, new[] { "INCREASED_", "INCREASE", "INCREASED|UNCHANGED" }),
        };

        [Test]
        public void Accepts_TheSlotsOfTheTable_ReadAsTheReaderInJavaScriptDoes()
        {
            var wrong = new List<string>();
            foreach (var (construct, name, take, refuse) in Expressions)
                wrong.AddRange(Offenders(construct + "." + name, Table(construct, name), take, refuse));
            Assert.That(wrong, Is.Empty);
        }

        [TestCase("")]
        [TestCase("a")]
        [TestCase("a  b \"c d\"")]
        public void Accepts_Text_TakesAnyString(string raw) => Assert.That(Takes(Slot(SlotType.Text), raw), Is.True);

        [TestCase("-1")]
        [TestCase("0")]
        [TestCase("0.5")]
        [TestCase("10")]
        [TestCase("-0.25")]
        [TestCase("007")]
        [TestCase("1.0")]
        public void Accepts_Number_TakesDigitsWithASignAndAFraction(string raw) => Assert.That(Takes(Slot(SlotType.Number), raw), Is.True);

        [TestCase("")]
        [TestCase(".5")]
        [TestCase("5.")]
        [TestCase("+1")]
        [TestCase("1e3")]
        [TestCase("1,5")]
        [TestCase("-")]
        [TestCase("--1")]
        [TestCase("1.2.3")]
        [TestCase("1 ")]
        [TestCase(" 1")]
        [TestCase("1\n")]
        [TestCase("-.5")]
        [TestCase("0.")]
        public void Accepts_Number_RefusesWhatTheNumberOfJavaScriptRefuses(string raw) => Assert.That(Takes(Slot(SlotType.Number), raw), Is.False);

        [TestCase("0")]
        [TestCase("10")]
        [TestCase("007")]
        public void Accepts_UInt_TakesDigits(string raw) => Assert.That(Takes(Slot(SlotType.UInt), raw), Is.True);

        [TestCase("")]
        [TestCase("-1")]
        [TestCase("+1")]
        [TestCase("0.5")]
        [TestCase("1e3")]
        [TestCase("1 ")]
        [TestCase("1\n")]
        public void Accepts_UInt_RefusesAnythingElse(string raw) => Assert.That(Takes(Slot(SlotType.UInt), raw), Is.False);

        // \d of .NET reads the digits of every script; the number of the grammar is ASCII, as the one of JavaScript is.
        [TestCase(0x0663)]
        [TestCase(0x0967)]
        [TestCase(0xFF11)]
        public void Accepts_NumberAndUInt_RefuseADigitOfAnotherScript(int code)
        {
            Assert.That(Takes(Slot(SlotType.Number), Chr(code)), Is.False);
            Assert.That(Takes(Slot(SlotType.Number), "1." + Chr(code)), Is.False);
            Assert.That(Takes(Slot(SlotType.UInt), Chr(code)), Is.False);
        }

        // PT-8/Q22: CaseFold is true file-wide (the one generated table, playtest, says
        // CASEFOLD), so "STRIP" is now a case-fold of a listed word, not a refusal.
        [Test]
        public void Accepts_Enum_TakesOneWordOfTheList_CaseFolded()
        {
            Assert.That(Offenders("enum", Slot(SlotType.Enum, "strip|list|Strip"), new[] { "strip", "list", "Strip", "STRIP" },
                new[] { "stri", "stripx", "strip|list", "", " strip", "lis", "Strip|" }), Is.Empty);
        }

        [Test]
        public void Accepts_Match_NeedsTheWholeToken()
        {
            Assert.That(Offenders("ab+", Slot(SlotType.Match, "ab+"), new[] { "ab", "abbb" }, new[] { "", "a", "xab", "abx", "ab ab" }), Is.Empty);
        }

        // .NET lets $ match before a final line feed and JavaScript does not: a match shorter than the token is not the token.
        [Test]
        public void Accepts_Match_RefusesATokenThatTheExpressionReadsOnlyUpToAFinalLineFeed() =>
            Assert.That(Takes(Slot(SlotType.Match, "\\S+"), "ab\n"), Is.False);

        // \w and \d of JavaScript are ASCII; without RegexOptions.ECMAScript .NET reads the letters and the digits of every script.
        [Test]
        public void Accepts_Match_ReadsWordAndDigitClassesAsAscii()
        {
            Assert.That(Takes(Slot(SlotType.Match, "\\w+"), "ab" + Chr(0x00E9)), Is.False);
            Assert.That(Takes(Slot(SlotType.Match, "\\d+"), Chr(0x0663)), Is.False);
            Assert.That(Takes(Slot(SlotType.Match, "\\w+\\d"), "ab1"), Is.True);
        }

        [Test]
        public void Accepts_Match_AnExpressionThatRunsOutOfTime_IsNotAccepted_AndDoesNotThrow()
        {
            var slot = Slot(SlotType.Match, "(a+)+$");
            var watch = Stopwatch.StartNew();
            var accepted = true;
            Assert.DoesNotThrow(() => accepted = Takes(slot, new string('a', 40) + "b"));
            Assert.That(accepted, Is.False);
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5000), "the limit of a match is a quarter of a second");
        }

        // The expression of a path is quadratic on a long run of [ (R-H3 of the plan): nobody types it, but a line of a script is data.
        [Test]
        public void Accepts_APath_OnALongRunOfOpenBrackets_ReturnsInTime_AndDoesNotThrow()
        {
            var watch = Stopwatch.StartNew();
            Assert.DoesNotThrow(() => Takes(Table("PT_SET", "path"), new string('[', 30000) + "]"));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5000));
        }

        [Test]
        public void Accepts_Match_ManyThreadsFillingTheCache_AgreeWithOne()
        {
            var expressions = Enumerable.Range(1, 40).Select(i => new string('a', i) + "+").ToArray();   // a+, aa+, aaa+ ...: they take 1, 2, 3 ... a or more
            var taken = new bool[expressions.Length * 50];
            Parallel.For(0, taken.Length, i => taken[i] = Takes(Slot(SlotType.Match, expressions[i % expressions.Length]), new string('a', 20)));
            Assert.That(taken.Select((t, i) => t == (i % expressions.Length + 1 <= 20)), Has.All.True);
        }

        [Test]
        public void Accepts_NoValue_IsNeverAccepted()
        {
            foreach (SlotType type in Enum.GetValues(typeof(SlotType)))
                Assert.That(Takes(Slot(type, "a|b"), null), Is.False, type.ToString());
        }

        [Test]
        public void Describe_NamesWhatTheSlotTakes_AsTheReaderInJavaScriptDoes()
        {
            Assert.That(SlotCheck.Describe(Table("PT_WAIT", "seconds")), Is.EqualTo("a number"));
            Assert.That(SlotCheck.Describe(Table("PT_INVOKE_REPEAT", "count")), Is.EqualTo("a uint"));
            Assert.That(SlotCheck.Describe(Table("PT_LOG", "message")), Is.EqualTo("a text"));
            Assert.That(SlotCheck.Describe(Table("PT_ASSERT_CTA", "state")), Is.EqualTo("one of VISIBLE|CLICKABLE"));
            Assert.That(SlotCheck.Describe(Table("PT_ASSERT", "op")), Is.EqualTo("like [<>]=?|==|!=|contains"));
            Assert.That(SlotCheck.Describe(Slot(SlotType.Match, "FROM|ON")), Is.EqualTo("FROM or ON"));
            Assert.That(SlotCheck.Describe(Slot(SlotType.Match, "FROM")), Is.EqualTo("FROM"));
            Assert.That(SlotCheck.Describe(Slot(SlotType.Match, "1|2")), Is.EqualTo("like 1|2"));
        }
    }
}
