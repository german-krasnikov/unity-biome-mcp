using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Playtest.Core.SharedTests;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The forms of the real table, read by the front. Every fate below was printed by the reader in JavaScript (matchPattern of pattern.ts, driven as uses.ts does: the forms of the first word
    // in the order of the table, the furthest failure when none reads the line), not by this code. Only verbs with no LAZY text: the end of a LAZY text is H1.1.
    public class PatternMatcherTableTests
    {
        // What the reader will do with a line: the first form of its verb that reads it wins; if none does, the furthest failure of them tells where.
        private static string Fate(string line)
        {
            var toks = PatternTokens.Split(line);
            MatchTrace furthest = null;
            foreach (var form in PlaytestGrammar.Forms.Where(f => f.Verb == toks[0].Text))
            {
                var trace = new MatchTrace();
                var bound = PatternMatcher.Match(form.Elements, toks, line, trace);
                if (bound != null) return form.Construct + (bound.Slots.Count + bound.Flags.Count > 0 ? ": " + MatchShow.Bound(bound) : "");
                if (furthest == null || trace.At > furthest.At) furthest = trace;
            }
            return furthest == null ? "no form" : "no match, " + MatchShow.Failed(furthest);
        }

        private static IEnumerable<Element> Walk(IEnumerable<Element> elements) =>
            elements.SelectMany(e => e.Kind == ElementKind.Group ? Walk(e.Items) : new[] { e });

        [TestCase("WAIT 2.5", "PT_WAIT: seconds=2.5@5")]
        [TestCase("  WAIT   2.5", "PT_WAIT: seconds=2.5@9")]
        [TestCase("wait 1", "no form")]
        [TestCase("WAIT abc", "no match, at 1 slot seconds")]
        [TestCase("  WAIT abc", "no match, at 1 slot seconds")]
        [TestCase("WAIT \"abc\"", "no match, at 1 slot seconds")]
        [TestCase("WAIT", "no match, at 1 slot seconds")]
        [TestCase("WAIT 1 2", "no match, at 2")]
        [TestCase("MOVE TO 1,2,3", "PT_MOVE: to=1,2,3@8")]
        [TestCase("MOVE Player TO @/Enemy.position + (5,0,0)", "PT_MOVE: offset=+ (5,0,0)@32; path=Player@5; to=@/Enemy.position@15")]
        [TestCase("MOVE TO TO 1,0,0", "PT_MOVE: path=TO@5; to=1,0,0@11")]
        [TestCase("MOVE /Player", "no match, at 2 word TO")]
        [TestCase("MOVE /Player TO", "no match, at 3 slot to")]
        [TestCase("MOVE TO", "no match, at 2 word TO")]
        [TestCase("CLICK /Canvas/Button", "PT_CLICK: path=/Canvas/Button@6")]
        [TestCase("CLICK /Canvas/Button WAIT 0.5", "PT_CLICK: path=/Canvas/Button@6; wait=0.5@26")]
        [TestCase("CLICK /Canvas/Button WAIT abc", "no match, at 3 slot wait")]
        [TestCase("CLICK /Canvas/Button WAIT", "no match, at 3 slot wait")]
        [TestCase("LOG", "PT_LOG")]
        [TestCase("LOG # UNPARSED: x", "PT_LOG: message=# UNPARSED: x@4")]
        [TestCase("LOG a  b \"c d\"", "PT_LOG: message=a  b \"c d\"@4")]
        [TestCase("SECTION  my   section", "PT_SECTION: name=my   section@9")]
        [TestCase("ASSERT_CTA", "PT_ASSERT_CTA")]
        [TestCase("ASSERT_CTA VISIBLE", "PT_ASSERT_CTA: state=VISIBLE@11")]
        [TestCase("ASSERT_CTA HIDDEN", "no match, at 1 slot state")]
        [TestCase("SET /A B C 1", "PT_SET: component=B@7; field=C@9; path=/A@4; value=1@11")]
        [TestCase("SET /A B C 1 2 3", "no match, at 5")]
        [TestCase("SET /A B C hello world", "no match, at 5")]
        [TestCase("SET_ACTIVE /obj true", "PT_SET_ACTIVE: active=true@16; path=/obj@11")]
        [TestCase("SET_ACTIVE /obj yes", "no match, at 2 slot active")]
        [TestCase("INVOKE /Player Health TakeDamage 42 true", "PT_INVOKE: args=42 true@33; component=Health@15; method=TakeDamage@22; path=/Player@7")]
        [TestCase("INVOKE \"\" C M", "PT_INVOKE: component=C@10; method=M@12; path=\"\"@7")]
        [TestCase("INVOKE /[My Obj]/X Comp Method", "no match, at 1 slot path")]
        [TestCase("INVOKE_REPEAT 3 /A B C x y z", "PT_INVOKE_REPEAT: args=x y z@23; component=B@19; count=3@14; method=C@21; path=/A@16")]
        [TestCase("INVOKE_REPEAT -3 /A B C", "no match, at 1 slot count")]
        [TestCase("COMPLETE_PURCHASE /btn EXPECT", "PT_COMPLETE_PURCHASE: path=/btn@18; +EXPECT")]
        [TestCase("COMPLETE_PURCHASE /btn", "PT_COMPLETE_PURCHASE: path=/btn@18")]
        [TestCase("TELEPORT /P 1,2,3 + (1,0,0)", "PT_TELEPORT: offset=+ (1,0,0)@18; path=/P@9; to=1,2,3@12")]
        [TestCase("TRACE_FLOW FROM /a TO /b FIELD f TIMEOUT 3", "PT_TRACE_FLOW: field=f@31; from=/a@16; timeout=3@41; to=/b@22")]
        [TestCase("TRACE_FLOW FROM /a TO /b", "no match, at 5 word FIELD")]
        [TestCase("VAR $hp @/Player|Health|hp", "PT_VAR: name=$hp@4; query=@/Player|Health|hp@8")]
        [TestCase("VAR $hp", "no match, at 2 slot query")]
        [TestCase("VAR $bad @NotAPipePath", "no match, at 2 slot query")]
        [TestCase("ASSERT_ONE_ACTIVE /a /b /c d", "PT_ASSERT_ONE_ACTIVE: more=/c d@24; path1=/a@18; path2=/b@21")]
        [TestCase("ASSERT_ONE_ACTIVE /a", "no match, at 2 slot path2")]
        [TestCase("ASSERT_NEAR /a /b 0.5", "PT_ASSERT_NEAR: path1=/a@12; path2=/b@15; threshold=0.5@18")]
        [TestCase("ASSERT_NEAR /a /b x", "no match, at 3 slot threshold")]
        [TestCase("SWEEP_PATH /P DWELL 0.4", "PT_SWEEP_PATH: dwell=0.4@20; path=/P@11")]
        [TestCase("SWEEP_PATH /Player 0.4", "no match, at 2 word DWELL")]
        [TestCase("CAPTURE_FRAMES 1 INTERVAL 0.1", "no match, at 1 slot count")]
        [TestCase("CAPTURE_FRAMES 4 INTERVAL 0.1 CAMERA main MODE list LABEL x", "PT_CAPTURE_FRAMES: camera=main@37; count=4@15; interval=0.1@26; label=x@58; mode=list@47")]
        [TestCase("CAPTURE_FRAMES 4 INTERVAL 0.1 MODE list CAMERA main LABEL x", "no match, at 6 word LABEL")]
        [TestCase("SIMULATE bot DURATION 5 TIMESCALE 2 TARGET /t FREQUENCY 10", "PT_SIMULATE: duration=5@22; frequency=10@56; name=bot@9; target=/t@43; timescale=2@34")]
        [TestCase("SIMULATE bot DURATION", "no match, at 3 slot duration")]
        [TestCase("WAIT_CAPTURED a INCREASED > 5 TIMEOUT 3 OVER 2", "PT_WAIT_CAPTURED: label=a@14; mode=INCREASED@16; over=2@45; subop=>@26; timeout=3@38; value=5@28")]
        [TestCase("ASSERT_CAPTURED a INCREASED_BY > 1", "PT_ASSERT_CAPTURED: label=a@16; mode=INCREASED_BY@18; subop=>@31; value=1@33")]
        [TestCase("ASSERT_CAPTURED a INCREASED_BY >", "no match, at 4 slot value")]
        [TestCase("SETUP", "PT_SETUP")]
        [TestCase("SETUP x", "no match, at 1")]
        [TestCase("VAL $x 1 2 3", "PT_VAL: name=$x@4; value=1 2 3@7")]
        [TestCase("INCLUDE common.defs", "PT_INCLUDE: file=common.defs@8")]
        [TestCase("MONITOR", "PT_MONITOR")]
        [TestCase("MONITOR /a|C|f", "PT_MONITOR: query=/a|C|f@8")]
        [TestCase("CAPTURE hp /a|C|f", "PT_CAPTURE: label=hp@8; query=/a|C|f@11")]
        [TestCase("CAPTURE hp", "no match, at 2 slot query")]
        [TestCase("CAPTURE_MIN hp /a|C|f OVER 3", "PT_CAPTURE_MIN: name=hp@12; over=3@27; query=/a|C|f@15")]
        [TestCase("ASSERT_MIN hp > 3", "PT_ASSERT_MIN: name=hp@11; op=>@14; value=3@16")]
        [TestCase("ASSERT_MIN hp >", "no match, at 3 slot value")]
        [TestCase("DESC hello world", "PT_DESC: label=hello world@5")]
        public void Match_TheFormsOfTheTable_ReadALineAsTheReaderInJavaScriptDoes(string line, string fate) => Assert.That(Fate(line), Is.EqualTo(fate));

        // The lines of the examples of the repository for which no diagnostic is expected (dsl/constructs/playtest*.examples, the requirement examples both readers are held to) are read.
        // A block (ASSERT_BATCH ... END) is read as a whole by the reader, and a line whose verb has a LAZY text waits for H1.1; both are counted, not skipped in silence.
        [Test]
        public void Match_EveryFlatLineOfTheExamplesThatExpectNoDiagnostic_IsReadByTheFirstFormOfItsVerb()
        {
            var lazyVerbs = new HashSet<string>(PlaytestGrammar.Forms.Where(f => Walk(f.Elements).Any(e => e.Lazy)).Select(f => f.Verb));
            var blockVerbs = new HashSet<string>(PlaytestGrammar.Forms.Where(f => f.IsBlock).Select(f => f.Verb));
            int read = 0, lazy = 0, blocks = 0;
            var refused = new List<string>();
            foreach (var block in ExamplesFile.ReadAll().Where(b => b.Expect.Count == 0))
            {
                var lines = block.Source.Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#")).ToList();
                if (lines.Any(l => blockVerbs.Contains(PatternTokens.Split(l)[0].Text)))
                {
                    blocks += lines.Count;
                    continue;
                }
                foreach (var line in lines)
                {
                    if (lazyVerbs.Contains(PatternTokens.Split(line)[0].Text)) lazy++;
                    else if (Fate(line).StartsWith("no ")) refused.Add(block.FileName + ": " + line + " => " + Fate(line));
                    else read++;
                }
            }
            TestContext.WriteLine("lines read: " + read + "; waiting for LAZY: " + lazy + "; in blocks: " + blocks);
            Assert.That(refused, Is.Empty);
            Assert.That(read, Is.GreaterThan(100), "a check over (almost) no line is no check");
        }
    }
}
