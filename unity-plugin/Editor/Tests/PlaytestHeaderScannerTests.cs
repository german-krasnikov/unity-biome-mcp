// TDD: PlaytestHeaderScanner pure-logic tests — no Unity API, EditMode safe.
// Scans `# @directive` header lines out of .playtest scripts, pre-INCLUDE (B03).
using NUnit.Framework;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestHeaderScannerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Scan_NoDirectives_ReturnsDefaults()
        {
            var header = PlaytestHeaderScanner.Scan("ASSERT /Obj|Comp|field == 1\nASSERT_CONSOLE_CLEAN");

            Assert.IsFalse(header.NeedsEditmode);
            Assert.IsFalse(header.NeedsPlaymode);
            Assert.IsFalse(header.SuiteOnly);
            Assert.IsNull(header.ExpectSteps);
            Assert.IsNull(header.ExpectFailed);
            Assert.AreEqual(0, header.Tags.Count);
        }

        [Test]
        public void Scan_NeedsEditmode_SetsFlag()
        {
            var header = PlaytestHeaderScanner.Scan("# @needs editmode\nASSERT_CONSOLE_CLEAN");

            Assert.IsTrue(header.NeedsEditmode);
        }

        [Test]
        public void Scan_Tags_ParsesSpaceSeparatedList()
        {
            var header = PlaytestHeaderScanner.Scan("# @tags smoke protocol\nASSERT_CONSOLE_CLEAN");

            // Sorted for determinism (review minor) — ordinal order, not insertion order.
            CollectionAssert.AreEqual(new[] { "protocol", "smoke" }, header.Tags);
        }

        [Test]
        public void Scan_DuplicateTags_Deduplicated()
        {
            // Double-red: a naive AddRange (no dedup) would keep all 3 entries incl. the
            // repeat; an implementation that dedups but doesn't sort would be nondeterministic
            // (HashSet iteration order is not guaranteed) instead of exactly ["protocol","smoke"].
            var header = PlaytestHeaderScanner.Scan("# @tags smoke protocol smoke\nASSERT_CONSOLE_CLEAN");

            CollectionAssert.AreEqual(new[] { "protocol", "smoke" }, header.Tags);
        }

        [Test]
        public void Scan_Expect_ParsesKeyValuePairs()
        {
            var header = PlaytestHeaderScanner.Scan("# @expect steps=5 failed=1\nASSERT_CONSOLE_CLEAN");

            Assert.AreEqual(5, header.ExpectSteps);
            Assert.AreEqual(1, header.ExpectFailed);
        }

        [Test]
        public void Scan_SuiteOnly_SetsFlag()
        {
            var header = PlaytestHeaderScanner.Scan("# @suite-only\nASSERT_CONSOLE_CLEAN");

            Assert.IsTrue(header.SuiteOnly);
        }

        [Test]
        public void Scan_MultipleDirectiveLines_Merge()
        {
            // Double-red: an implementation that resets the header per directive line
            // (overwrite) instead of accumulating across lines would keep only "protocol".
            var header = PlaytestHeaderScanner.Scan("# @tags smoke\n# @tags protocol\nASSERT_CONSOLE_CLEAN");

            // Sorted for determinism (review minor) — ordinal order, not insertion order.
            CollectionAssert.AreEqual(new[] { "protocol", "smoke" }, header.Tags);
        }

        [Test]
        public void Scan_NonHeaderComment_Ignored()
        {
            // Looks like a directive (starts with a known keyword right after "#") but is
            // missing the leading "@" sigil — must not be treated as a directive line.
            var header = PlaytestHeaderScanner.Scan("# needs editmode but with no @ sigil is not a directive\nASSERT_CONSOLE_CLEAN");

            Assert.IsFalse(header.NeedsEditmode);
            Assert.IsFalse(header.SuiteOnly);
            Assert.AreEqual(0, header.Tags.Count);
        }

        [Test]
        public void Scan_UnknownDirective_Ignored()
        {
            // "@timescale-ok" is already shipped (PlaytestLinter.cs:152) — the scanner must not
            // know it, must never throw, and must never mutate any default flag (R-18: forward-compat).
            var header = PlaytestHeaderScanner.Scan("# @timescale-ok\nASSERT_CONSOLE_CLEAN");

            Assert.IsFalse(header.NeedsEditmode);
            Assert.IsFalse(header.NeedsPlaymode);
            Assert.IsFalse(header.SuiteOnly);
            Assert.IsNull(header.ExpectSteps);
            Assert.IsNull(header.ExpectFailed);
            Assert.AreEqual(0, header.Tags.Count);
        }
    }
}
