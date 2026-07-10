// NUnit unit tests for AliasExpander — zero AssetDatabase dependency via _tableOverride.
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AliasExpanderTests
    {
        [SetUp]
        public void Setup() => AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["julia"]  = "/World/Characters/Julia",
            ["max_hp"] = "100",
        };

        [TearDown]
        public void Teardown() => AliasExpander._tableOverride = null;

        [Test]
        public void ExpandJson_NoDollar_ReturnsUnchanged()
        {
            const string input = "{\"path\":\"/World/Characters/Julia\",\"type\":\"MeshRenderer\"}";
            Assert.AreEqual(input, AliasExpander.ExpandJson(input));
        }

        [Test]
        public void ExpandJson_KnownAlias_Replaces()
        {
            var result = AliasExpander.ExpandJson("{\"path\":\"$julia\",\"type\":\"MeshRenderer\"}");
            StringAssert.Contains("/World/Characters/Julia", result);
            StringAssert.DoesNotContain("$julia", result);
        }

        [Test]
        public void ExpandJson_UnknownAlias_LeavesIntact()
        {
            var result = AliasExpander.ExpandJson("{\"path\":\"$unknown\"}");
            StringAssert.Contains("$unknown", result);
        }

        [Test]
        public void ExpandJson_MultipleAliases_AllReplaced()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["player"] = "/Player",
                ["enemy"]  = "/Enemy",
            };
            var result = AliasExpander.ExpandJson("{\"src\":\"$player\",\"dst\":\"$enemy\"}");
            StringAssert.Contains("/Player", result);
            StringAssert.Contains("/Enemy", result);
            StringAssert.DoesNotContain("$player", result);
            StringAssert.DoesNotContain("$enemy", result);
        }

        [Test]
        public void ExpandJson_ConstAlias_ReplacesWithLiteral()
        {
            var result = AliasExpander.ExpandJson("{\"value\":\"$max_hp\"}");
            StringAssert.Contains("100", result);
            StringAssert.DoesNotContain("$max_hp", result);
        }

        [Test]
        public void ExpandText_KnownAlias_Replaces()
        {
            var result = AliasExpander.ExpandText("get_component path=$julia type=MeshRenderer");
            StringAssert.Contains("/World/Characters/Julia", result);
            StringAssert.DoesNotContain("$julia", result);
        }

        [Test]
        public void ExpandJson_Null_ReturnsNull()
        {
            Assert.IsNull(AliasExpander.ExpandJson(null));
        }

        [Test]
        public void ExpandJson_Empty_ReturnsEmpty()
        {
            Assert.AreEqual("", AliasExpander.ExpandJson(""));
        }

        [Test]
        public void ExpandJson_PathWithBackslash_EscapedInJson()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["winpath"] = @"C:\Users\foo",
            };
            var result = AliasExpander.ExpandJson("{\"path\":\"$winpath\"}");
            // Backslash must be double-escaped inside JSON string
            StringAssert.Contains(@"C:\\Users\\foo", result);
        }

        [Test]
        public void ExpandText_EmptyTable_ReturnsUnchanged()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            const string input = "get_component path=$julia";
            Assert.AreEqual(input, AliasExpander.ExpandText(input));
        }

        [Test]
        public void ExpandJson_ValueWithDoubleQuote_EscapedInJson()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["greeting"] = "say \"hello\"",
            };
            var result = AliasExpander.ExpandJson("{\"msg\":\"$greeting\"}");
            StringAssert.Contains("say \\\"hello\\\"", result);
        }

        // P3: Bare name without $ is NOT expanded
        [Test]
        public void ExpandText_BareWord_NotExpanded()
        {
            // "julia" is in the table but bare word (no $) must never expand
            var result = AliasExpander.ExpandText("get_component path=julia type=MeshRenderer");
            StringAssert.Contains("path=julia", result);
        }

        // P4: $alias embedded inside a longer path string expands correctly
        [Test]
        public void ExpandText_EmbeddedAlias_Expands()
        {
            var result = AliasExpander.ExpandText("get_hierarchy root=/Scenes/$julia");
            StringAssert.Contains("/Scenes//World/Characters/Julia", result);
            StringAssert.DoesNotContain("$julia", result);
        }

        // P4: $alias embedded in JSON value expands with proper JSON escaping
        [Test]
        public void ExpandJson_EmbeddedAlias_Expands()
        {
            var result = AliasExpander.ExpandJson("{\"root\":\"/Scenes/$julia\"}");
            StringAssert.Contains("/Scenes//World/Characters/Julia", result);
            StringAssert.DoesNotContain("$julia", result);
        }

        // P4: Multiple $aliases in one value — all expand
        [Test]
        public void ExpandText_TwoAliasesInOneValue_BothExpand()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = "X",
                ["b"] = "Y",
            };
            var result = AliasExpander.ExpandText("cmd val=$a-$b");
            Assert.AreEqual("cmd val=X-Y", result);
        }

        // ── Pipe-path tests (regression for GetTable pipe truncation bug) ───

        [Test]
        public void ExpandJson_QueriesWithPipe_PreservesFullPipe()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["counter"] = "/Zone/Counter|ZoneComp|Remaining",
            };
            var result = AliasExpander.ExpandJson("{\"queries\":\"$counter\"}");
            StringAssert.Contains("/Zone/Counter|ZoneComp|Remaining", result);
            StringAssert.DoesNotContain("$counter", result);
        }

        [Test]
        public void ExpandJson_CommaSeparatedPipeAliases_BothExpand()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = "/Obj1|C1|f1",
                ["b"] = "/Obj2|C2|f2",
            };
            var result = AliasExpander.ExpandJson("{\"queries\":\"$a,$b\"}");
            StringAssert.Contains("/Obj1|C1|f1", result);
            StringAssert.Contains("/Obj2|C2|f2", result);
        }

        [Test]
        public void ExpandText_BatchWithPipeAlias_PreservesPipe()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["q"] = "/Path|Comp|Field",
            };
            var result = AliasExpander.ExpandText("query_state queries=$q");
            StringAssert.Contains("/Path|Comp|Field", result);
            StringAssert.DoesNotContain("$q", result);
        }

        [Test]
        public void ExpandJson_PathOnlyAlias_NoTrailingPipes()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["player"] = "/Player",
            };
            var result = AliasExpander.ExpandJson("{\"path\":\"$player\"}");
            StringAssert.Contains("/Player", result);
            StringAssert.DoesNotContain("|", result);
        }

        [Test]
        public void ExpandJson_PipeValueWithParentheses_Preserved()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["eggs"] = "/Storage|CargoVDemo|Count(false)",
            };
            var result = AliasExpander.ExpandJson("{\"queries\":\"$eggs\"}");
            StringAssert.Contains("Count(false)", result);
            StringAssert.DoesNotContain("$eggs", result);
        }

        [Test]
        public void ExpandJson_AdjacentAliases_BothExpand()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = "X",
                ["b"] = "Y",
            };
            // $a$b — SigilRegex matches $a then $b separately ($ is the boundary)
            var result = AliasExpander.ExpandJson("{\"v\":\"$a$b\"}");
            StringAssert.Contains("XY", result);
            StringAssert.DoesNotContain("$a", result);
            StringAssert.DoesNotContain("$b", result);
        }

        [Test]
        public void ExpandJson_PartialPipe_CompOnly_NoField()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["x"] = "/Path|Comp",
            };
            var result = AliasExpander.ExpandJson("{\"queries\":\"$x\"}");
            StringAssert.Contains("/Path|Comp", result);
            StringAssert.DoesNotContain("$x", result);
        }
    }
}
