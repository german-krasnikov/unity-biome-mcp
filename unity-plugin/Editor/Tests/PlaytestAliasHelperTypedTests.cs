// TDD: FormatLine type dispatch + TokenSavingsEstimate typed variants.
// Pure static — no Unity API, no AssetDatabase.
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestAliasHelperTypedTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── FormatLine: ValPath ────────────────────────────────────────────────

        [Test]
        public void FormatLine_ValPath_ReturnsVALWithPipes()
        {
            var a = new QueryAlias { alias = "hp", type = AliasType.ValPath,
                path = "/Player", component = "Health", field = "currentHp" };
            Assert.AreEqual("VAL $hp /Player|Health|currentHp", PlaytestAliasHelpers.FormatLine(a));
        }

        [Test]
        public void FormatLine_ValPath_EmptyComponent_NoPipes()
        {
            var a = new QueryAlias { alias = "p", type = AliasType.ValPath,
                path = "/Player", component = "", field = "" };
            Assert.AreEqual("VAL $p /Player", PlaytestAliasHelpers.FormatLine(a));
        }

        [Test]
        public void FormatLine_ValPath_EmptyField_OnlyOnePipe()
        {
            var a = new QueryAlias { alias = "p", type = AliasType.ValPath,
                path = "/Player", component = "Health", field = "" };
            Assert.AreEqual("VAL $p /Player|Health", PlaytestAliasHelpers.FormatLine(a));
        }

        // ── FormatLine: ValConst ───────────────────────────────────────────────

        [Test]
        public void FormatLine_ValConst_ReturnsVALWithConstValue()
        {
            var a = new QueryAlias { alias = "speed", type = AliasType.ValConst, constValue = "5.5" };
            Assert.AreEqual("VAL $speed 5.5", PlaytestAliasHelpers.FormatLine(a));
        }

        [Test]
        public void FormatLine_ValConst_EmptyConstValue_EmitsBlankValue()
        {
            var a = new QueryAlias { alias = "x", type = AliasType.ValConst, constValue = "" };
            Assert.AreEqual("VAL $x ", PlaytestAliasHelpers.FormatLine(a));
        }

        [Test]
        public void FormatLine_ValConst_NullConstValue_EmitsBlank()
        {
            var a = new QueryAlias { alias = "x", type = AliasType.ValConst, constValue = null };
            Assert.DoesNotThrow(() => PlaytestAliasHelpers.FormatLine(a));
        }

        [Test]
        public void FormatLine_ValConst_PathFieldsIgnored()
        {
            var a = new QueryAlias { alias = "x", type = AliasType.ValConst,
                constValue = "42", path = "/Player", component = "Health", field = "hp" };
            var result = PlaytestAliasHelpers.FormatLine(a);
            StringAssert.DoesNotContain("/Player", result);
            StringAssert.DoesNotContain("|", result);
            Assert.AreEqual("VAL $x 42", result);
        }

        // ── FormatLine: VarRuntime ─────────────────────────────────────────────

        [Test]
        public void FormatLine_VarRuntime_ReturnsVARWithAtPrefix()
        {
            var a = new QueryAlias { alias = "pos", type = AliasType.VarRuntime,
                path = "/Enemy", component = "Transform", field = "position" };
            Assert.AreEqual("VAR $pos @/Enemy|Transform|position", PlaytestAliasHelpers.FormatLine(a));
        }

        [Test]
        public void FormatLine_VarRuntime_AlwaysEmitsAllThreePipes()
        {
            var a = new QueryAlias { alias = "x", type = AliasType.VarRuntime,
                path = "/Obj", component = "", field = "" };
            StringAssert.StartsWith("VAR $x @/Obj|", PlaytestAliasHelpers.FormatLine(a));
        }

        // ── FormatVALLine backward-compat wrapper ──────────────────────────────

        [Test]
        public void FormatVALLine_DelegatesToFormatLine_ValPath()
        {
            var a = new QueryAlias { alias = "hp", type = AliasType.ValPath,
                path = "/Player", component = "Health", field = "hp" };
            Assert.AreEqual(PlaytestAliasHelpers.FormatLine(a), PlaytestAliasHelpers.FormatVALLine(a));
        }

        [Test]
        public void FormatVALLine_DelegatesToFormatLine_ValConst()
        {
            var a = new QueryAlias { alias = "speed", type = AliasType.ValConst, constValue = "3.0" };
            Assert.AreEqual(PlaytestAliasHelpers.FormatLine(a), PlaytestAliasHelpers.FormatVALLine(a));
        }

        // ── FormatVALBlock: mixed types ────────────────────────────────────────

        [Test]
        public void FormatVALBlock_MixedTypes_CorrectKeywords()
        {
            var aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "hp",    type = AliasType.ValPath,
                    path = "/P", component = "H", field = "h" },
                new QueryAlias { alias = "speed",  type = AliasType.ValConst, constValue = "5.5" },
                new QueryAlias { alias = "pos",    type = AliasType.VarRuntime,
                    path = "/P", component = "T", field = "position" },
            };
            var block = PlaytestAliasHelpers.FormatVALBlock(aliases);
            var lines = block.Split('\n');
            Assert.AreEqual(3, lines.Length);
            StringAssert.StartsWith("VAL $hp",    lines[0]);
            StringAssert.StartsWith("VAL $speed", lines[1]);
            StringAssert.StartsWith("VAR $pos",   lines[2]);
        }

        [Test]
        public void FormatVALBlock_VarRuntime_EmitsVARNotVAL()
        {
            var aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "pos", type = AliasType.VarRuntime,
                    path = "/E", component = "T", field = "position" }
            };
            var block = PlaytestAliasHelpers.FormatVALBlock(aliases);
            StringAssert.StartsWith("VAR ", block);
            StringAssert.DoesNotContain("VAL ", block);
        }

        // ── Default type: ValPath ──────────────────────────────────────────────

        [Test]
        public void QueryAlias_DefaultType_IsValPath()
        {
            var a = new QueryAlias { alias = "hp", path = "/P", component = "H", field = "h" };
            Assert.AreEqual(AliasType.ValPath, a.type);
        }

        // ── TokenSavingsEstimate ───────────────────────────────────────────────

        [Test]
        public void Estimate_ValConst_UsesConstValueLength()
        {
            var a = new QueryAlias { alias = "sp", type = AliasType.ValConst,
                constValue = "SomeLongConstantString" };
            Assert.GreaterOrEqual(PlaytestAliasHelpers.TokenSavingsEstimate(new List<QueryAlias> { a }), 0);
        }

        [Test]
        public void Estimate_ValConst_LongValue_PositiveSavings()
        {
            var a = new QueryAlias { alias = "x", type = AliasType.ValConst,
                constValue = "/Very/Long/Path/That/Would/Save/Tokens/With/Alias" };
            Assert.Greater(PlaytestAliasHelpers.TokenSavingsEstimate(new List<QueryAlias> { a }), 0);
        }

        [Test]
        public void Estimate_VarRuntime_SameFormulaAsValPath()
        {
            var valPath = new QueryAlias { alias = "hp", type = AliasType.ValPath,
                path = "/Player", component = "Health", field = "currentHp" };
            var varRuntime = new QueryAlias { alias = "hp", type = AliasType.VarRuntime,
                path = "/Player", component = "Health", field = "currentHp" };
            var r1 = PlaytestAliasHelpers.TokenSavingsEstimate(new List<QueryAlias> { valPath });
            var r2 = PlaytestAliasHelpers.TokenSavingsEstimate(new List<QueryAlias> { varRuntime });
            Assert.AreEqual(r1, r2);
        }

        [Test]
        public void Estimate_MixedTypes_SumsCorrectly()
        {
            var aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "hp",    type = AliasType.ValPath,
                    path = "/Player", component = "Health", field = "hp" },
                new QueryAlias { alias = "speed",  type = AliasType.ValConst, constValue = "SomeConstantValue" },
                new QueryAlias { alias = "pos",    type = AliasType.VarRuntime,
                    path = "/Enemy", component = "Transform", field = "position" },
            };
            Assert.DoesNotThrow(() => PlaytestAliasHelpers.TokenSavingsEstimate(aliases));
            Assert.GreaterOrEqual(PlaytestAliasHelpers.TokenSavingsEstimate(aliases), 0);
        }

        [Test]
        public void Estimate_ValConst_EmptyConstValue_DoesNotThrow()
        {
            var a = new QueryAlias { alias = "x", type = AliasType.ValConst, constValue = "" };
            Assert.DoesNotThrow(() =>
                PlaytestAliasHelpers.TokenSavingsEstimate(new List<QueryAlias> { a }));
        }
    }
}
