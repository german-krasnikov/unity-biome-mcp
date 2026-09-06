using NUnit.Framework;

namespace UnityMCP.Playtest.Core.PureTests
{
    // A15 (PR-07, Gap 7): ParseResult/PlaytestStep are public, fully mutable containers --
    // nothing prevented two runs sharing one parsed instance from corrupting each other's
    // plan. PreparedPlaytest.From() copies the *container* (List -> read-only view) so a
    // caller mutating the original ParseResult after preparing a run cannot affect it.
    // This targets exactly the container-sharing bug this PR fixes -- it does not deep-copy
    // each PlaytestStep's own mutable fields or shared arrays (ShallowClone's documented
    // "shared by reference by design" scope), which stays a separate, out-of-scope concern
    // per the architecture plan's own risk note.
    [TestFixture]
    public class PreparedPlaytestTests
    {
        private const string Script = "ASSERT /Obj|Comp|field == 1\nWAIT 0.1\nASSERT_CONSOLE_CLEAN";

        [Test]
        public void From_MutatingSourceParseResultStepsAfterPrepare_DoesNotAffectPrepared()
        {
            var parsed = PlaytestParser.Parse(Script);
            var originalRawLines = parsed.Steps.ConvertAll(s => s.RawLine);

            var prepared = PreparedPlaytest.From(parsed);

            // Simulate a second run reusing the same ParseResult and mutating its container
            // (insert/remove/reorder) -- prepared must be unaffected because From() copied
            // the container, not just a reference to it.
            parsed.Steps.Clear();
            parsed.Steps.Add(new PlaytestStep { Type = StepType.Wait, RawLine = "INJECTED" });

            Assert.AreEqual(originalRawLines.Count, prepared.Steps.Count);
            for (int i = 0; i < originalRawLines.Count; i++)
                Assert.AreEqual(originalRawLines[i], prepared.Steps[i].RawLine);
        }

        [Test]
        public void From_TwoPreparedViewsFromSameParseResult_AreIndependentAndOrderIrrelevant()
        {
            // One prepared scenario, two independent "runs" (represented here by two
            // PreparedPlaytest views taken from the same parse) -- run order must not
            // matter because neither view can mutate the other's container.
            var parsed = PlaytestParser.Parse(Script);
            var runA = PreparedPlaytest.From(parsed);
            var runB = PreparedPlaytest.From(parsed);

            Assert.AreEqual(runA.Steps.Count, runB.Steps.Count);
            for (int i = 0; i < runA.Steps.Count; i++)
                Assert.AreEqual(runA.Steps[i].RawLine, runB.Steps[i].RawLine);

            // The exposed view must not be the mutable backing list itself.
            var asList = (System.Collections.Generic.IList<PlaytestStep>)runA.Steps;
            Assert.IsTrue(asList.IsReadOnly);
        }

        [Test]
        public void From_NoSetupOrTeardownBlocks_StayNull()
        {
            var parsed = PlaytestParser.Parse(Script); // no SETUP/TEARDOWN block

            var prepared = PreparedPlaytest.From(parsed);

            Assert.IsNull(prepared.SetupSteps);
            Assert.IsNull(prepared.TeardownSteps);
        }

        [Test]
        public void From_SetupAndTeardownPresent_CopiedIntoPrepared()
        {
            const string script =
                "SETUP\nWAIT 0.1\nSETUP_END\n" +
                "ASSERT_CONSOLE_CLEAN\n" +
                "TEARDOWN\nLOG done\nTEARDOWN_END";
            var parsed = PlaytestParser.Parse(script);

            var prepared = PreparedPlaytest.From(parsed);

            Assert.AreEqual(1, prepared.SetupSteps.Count);
            Assert.AreEqual(1, prepared.TeardownSteps.Count);
        }

        [Test]
        public void From_ExposesSameHeaderReference()
        {
            var parsed = PlaytestParser.Parse(Script);

            var prepared = PreparedPlaytest.From(parsed);

            Assert.AreSame(parsed.Header, prepared.Header);
        }
    }
}
