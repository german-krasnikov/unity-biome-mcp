// TDD — RED first. Alias provenance: lint authority (PlaytestParser) and
// runtime authority (AliasExpander) must agree on expansions.
// Covers: ValPath lint≡runtime, ValConst expands to literal, VarRuntime NOT
// expanded at parse time (runtime-only sigil).
// EditMode only — no scene, no AssetDatabase.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestAliasProvenanceLintTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── 6. ValPath: lint expansion equals runtime expansion ──────────────────

        [Test]
        public void ValAlias_LintResolvesToSamePathAsRuntime()
        {
            // Lint authority: PlaytestParser.Parse with inline VAL declaration
            var script = "VAL $hp /Player|Health\nASSERT $hp|hp == 10";
            var parsed = PlaytestParser.Parse(script);

            // After parse-time sigil expansion: $hp → /Player|Health
            string lintQuery = parsed.Steps[0].Query;
            Assert.AreEqual("/Player|Health|hp", lintQuery,
                "PlaytestParser must expand VAL $hp at parse (lint) time");

            // Runtime authority: AliasExpander with matching table entry
            AliasExpander._tableOverride = new Dictionary<string, string>(
                System.StringComparer.OrdinalIgnoreCase) { { "hp", "/Player|Health" } };
            RegisterCleanup(() => AliasExpander._tableOverride = null);

            string runtimeExpanded = AliasExpander.ExpandText("$hp|hp");
            Assert.AreEqual(lintQuery, runtimeExpanded,
                "AliasExpander (runtime) must produce the same expansion as PlaytestParser (lint)");
        }

        // ── 7. ValConst: lint expands to literal value, not a path ──────────────

        [Test]
        public void ValConst_LintResolvesToLiteralValue()
        {
            // VAL $threshold 100 — literal; no pipe/component syntax
            var script = "VAL $threshold 100\nASSERT $threshold == 10";
            var parsed = PlaytestParser.Parse(script);

            // $threshold → "100" (literal) so Query becomes "100", not a path
            Assert.AreEqual("100", parsed.Steps[0].Query,
                "$threshold must expand to literal '100' at parse time");
        }

        // ── 8. VarRuntime: NOT expanded at parse (lint) time ────────────────────

        [Test]
        public void VarRuntime_LintTimeExpansionIsNull()
        {
            // VAR sigil is runtime-only — the parser records it in VarDefs but does
            // not substitute $pos in ASSERT at parse time.
            var script = "VAR $pos @/Player|Transform|position\nASSERT $pos == someValue";
            var parsed = PlaytestParser.Parse(script);

            // One ASSERT step (VAR line adds no step)
            Assert.AreEqual(1, parsed.Steps.Count);
            StringAssert.Contains("$pos", parsed.Steps[0].Query,
                "$pos must stay unexpanded at parse/lint time (VarRuntime is runtime-only)");

            // VarDefs must record the raw @-query
            Assert.IsNotNull(parsed.VarDefs, "VarDefs should be non-null after VAR declaration");
            Assert.IsTrue(parsed.VarDefs.ContainsKey("pos"),
                "VarDefs must contain 'pos' from VAR $pos declaration");

            // AliasExpander excludes VarRuntime from its static table — empty table
            // leaves $pos unexpanded in ExpandText
            AliasExpander._tableOverride = new Dictionary<string, string>();
            RegisterCleanup(() => AliasExpander._tableOverride = null);

            string expanded = AliasExpander.ExpandText("$pos");
            Assert.AreEqual("$pos", expanded,
                "AliasExpander must not expand VarRuntime sigils statically");
        }
    }
}
