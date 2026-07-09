// TDD: BuildAliasSection typed behavior — ValPath/ValConst/VarRuntime variants.
// Uses BuildAliasSection(config) overload to avoid FindAssets race with project configs.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GetAliasesTypedTests
    {
        static PlaytestConfig MakeConfig(List<QueryAlias> aliases)
        {
            var config = ScriptableObject.CreateInstance<PlaytestConfig>();
            config.aliases = aliases;
            return config;
        }

        // ── ValPath: unchanged behavior ────────────────────────────────────────

        [Test]
        public void BuildAliasSection_ValPath_EmitsNameEqualsPathPipeCompPipeField()
        {
            var config = MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp", type = AliasType.ValPath,
                    path = "/Player", component = "Health", field = "currentHp" }
            });
            var result = CommandRouter.BuildAliasSection(config);
            StringAssert.Contains("hp=/Player|Health|currentHp", result);
        }

        [Test]
        public void BuildAliasSection_ValPath_ExplicitType_SameAsDefault()
        {
            var typed = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp", type = AliasType.ValPath,
                    path = "/Player", component = "HP", field = "health" }
            }));
            var legacy = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp", path = "/Player", component = "HP", field = "health" }
            }));
            Assert.AreEqual(legacy, typed);
        }

        // ── ValConst ──────────────────────────────────────────────────────────

        [Test]
        public void BuildAliasSection_ValConst_EmitsNameEqualsConstValue()
        {
            var result = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "speed", type = AliasType.ValConst, constValue = "5.5" }
            }));
            StringAssert.Contains("speed=5.5", result);
        }

        [Test]
        public void BuildAliasSection_ValConst_NoPipes()
        {
            var result = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "speed", type = AliasType.ValConst, constValue = "5.5" }
            }));
            foreach (var line in result.Split('\n'))
                if (line.StartsWith("speed="))
                    StringAssert.DoesNotContain("|", line);
        }

        [Test]
        public void BuildAliasSection_ValConst_PathFieldsNotEmitted()
        {
            var result = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "x", type = AliasType.ValConst,
                    constValue = "42", path = "/Player", component = "Health", field = "hp" }
            }));
            StringAssert.Contains("x=42", result);
            StringAssert.DoesNotContain("/Player", result);
        }

        // ── VarRuntime: skipped ────────────────────────────────────────────────

        [Test]
        public void BuildAliasSection_VarRuntime_Skipped()
        {
            var result = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "pos", type = AliasType.VarRuntime,
                    path = "/Enemy", component = "Transform", field = "position" }
            }));
            Assert.IsNull(result);
        }

        [Test]
        public void BuildAliasSection_VarRuntime_AbsentFromOutput()
        {
            var result = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp",  type = AliasType.ValPath,
                    path = "/Player", component = "Health", field = "hp" },
                new QueryAlias { alias = "pos", type = AliasType.VarRuntime,
                    path = "/Enemy", component = "Transform", field = "position" }
            }));
            Assert.IsNotNull(result);
            StringAssert.Contains("hp=", result);
            StringAssert.DoesNotContain("pos=", result);
            StringAssert.DoesNotContain("@", result);
        }

        // ── Mixed types ────────────────────────────────────────────────────────

        [Test]
        public void BuildAliasSection_MixedTypes_OnlyValPathAndValConst()
        {
            var result = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp",    type = AliasType.ValPath,
                    path = "/P", component = "H", field = "h" },
                new QueryAlias { alias = "speed",  type = AliasType.ValConst, constValue = "5.5" },
                new QueryAlias { alias = "pos",    type = AliasType.VarRuntime,
                    path = "/E", component = "T", field = "p" }
            }));
            Assert.IsNotNull(result);
            int aliasLineCount = 0;
            foreach (var line in result.Split('\n'))
                if (!line.StartsWith("---") && line.Length > 0 && line.Contains("="))
                    aliasLineCount++;
            Assert.AreEqual(2, aliasLineCount, "ValPath + ValConst only; VarRuntime skipped");
        }

        // ── Serialization round-trip ───────────────────────────────────────────

        [Test]
        public void BuildAliasSection_LegacyAlias_NoTypeField_TreatedAsValPath()
        {
            var result = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp", path = "/Player", component = "HP", field = "health" }
            }));
            StringAssert.Contains("hp=/Player|HP|health", result);
        }

        // ── Empty / null guards ───────────────────────────────────────────────

        [Test]
        public void BuildAliasSection_EmptyList_ReturnsNull()
        {
            Assert.IsNull(CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>())));
        }

        [Test]
        public void BuildAliasSection_AllVarRuntime_ReturnsNull()
        {
            var result = CommandRouter.BuildAliasSection(MakeConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "a", type = AliasType.VarRuntime, path = "/X", component = "C", field = "f" },
                new QueryAlias { alias = "b", type = AliasType.VarRuntime, path = "/Y", component = "D", field = "g" }
            }));
            Assert.IsNull(result);
        }
    }
}
