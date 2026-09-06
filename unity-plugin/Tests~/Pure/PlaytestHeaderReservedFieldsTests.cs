using NUnit.Framework;

namespace UnityMCP.Playtest.Core.PureTests
{
    // Gap 5 (PR-07): `@needs playmode`, `@expect steps=/failed=`, and `@suite-only` are
    // parsed by PlaytestHeaderScanner (see PlaytestHeader's own doc comments) but --
    // unlike `@needs editmode`/`@tags`, which ARE consumed (PlaytestParser.Directives.cs's
    // RejectPlayBoundVerbsUnderEditmode, PlaytestRunner's tag filtering) -- zero runner
    // enforces them today (confirmed by grep: no production reader anywhere outside the
    // scanner itself). This test pins that "parsed, reserved, not yet enforced" status so
    // a future enforcement change is a deliberate, reviewed decision rather than a silent
    // behavior shift that leaves the doc comments stale.
    [TestFixture]
    public class PlaytestHeaderReservedFieldsTests
    {
        [Test]
        public void Parse_ReservedDirectives_AreParsedButNotEnforced()
        {
            const string script =
                "# @needs playmode\n" +
                "# @expect steps=1 failed=1\n" +
                "# @suite-only\n" +
                "ASSERT_CONSOLE_CLEAN";

            var result = PlaytestParser.Parse(script);

            Assert.IsNull(result.Errors, "reserved directives must not block parse today");
            Assert.IsTrue(result.Header.NeedsPlaymode);
            Assert.IsTrue(result.Header.SuiteOnly);
            Assert.AreEqual(1, result.Header.ExpectSteps);
            Assert.AreEqual(1, result.Header.ExpectFailed);
        }
    }
}
