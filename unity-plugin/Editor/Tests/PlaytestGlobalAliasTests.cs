// NUnit tests for PlaytestConfig alias injection into PlaytestRunner script assembly.
// Tests run in EditMode (no PlayMode dependency) by exercising FormatVALBlock + Parse.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestGlobalAliasTests : SceneTestBase
    {
        private PlaytestConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<PlaytestConfig>();
            RegisterCleanup(() =>
            {
                if (_config) Object.DestroyImmediate(_config);
            });
        }

        // 1. Config alias expands in ASSERT step when injected via cfgBlock
        [Test]
        public void Run_WithConfigAliases_InjectsVALBlock()
        {
            _config.aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "player", type = AliasType.ValPath, path = "/Player" }
            };
            var cfgBlock = PlaytestAliasHelpers.FormatVALBlock(_config.aliases);
            var result = PlaytestParser.Parse(cfgBlock + "\nASSERT $player|Health|hp == 100");
            Assert.AreEqual("/Player|Health|hp", result.Steps[0].Query);
        }

        // 2. Script VAL after cfgBlock overrides config alias (last-write-wins in CollectVals)
        [Test]
        public void Run_IncludeOverridesConfigAlias()
        {
            _config.aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "player", type = AliasType.ValPath, path = "/ConfigPlayer" }
            };
            var cfgBlock = PlaytestAliasHelpers.FormatVALBlock(_config.aliases);
            // Re-declaring the same alias later simulates INCLUDE override behaviour
            var script = cfgBlock + "\nVAL $player /ScriptPlayer\nASSERT $player|Health|hp == 100";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual("/ScriptPlayer|Health|hp", result.Steps[0].Query);
        }

        // 3. Empty alias list produces empty block (no extra lines injected)
        [Test]
        public void Run_NoAliases_NoInjection()
        {
            _config.aliases = new List<QueryAlias>();
            var cfgBlock = PlaytestAliasHelpers.FormatVALBlock(_config.aliases);
            Assert.AreEqual("", cfgBlock);
        }

        // 4. VarRuntime alias is NOT emitted as VAL (it produces VAR, not a static alias)
        [Test]
        public void Run_VarRuntimeSkipped()
        {
            _config.aliases = new List<QueryAlias>
            {
                new QueryAlias
                {
                    alias = "hp",
                    type = AliasType.VarRuntime,
                    path = "/Player",
                    component = "Health",
                    field = "hp"
                }
            };
            var cfgBlock = PlaytestAliasHelpers.FormatVALBlock(_config.aliases);
            StringAssert.DoesNotContain("VAL $hp", cfgBlock);
            StringAssert.Contains("VAR $hp", cfgBlock);
        }

        // ── Pipe path regression tests ──────────────────────────────────────

        // 8. Config alias with comp+field emits full pipe in VAL block
        [Test]
        public void Run_WithConfigAliases_CompField_InjectsFullPipe()
        {
            _config.aliases = new List<QueryAlias>
            {
                new QueryAlias
                {
                    alias = "counter",
                    type = AliasType.ValPath,
                    path = "/Zone/Counter",
                    component = "ZoneComp",
                    field = "Remaining"
                }
            };
            var cfgBlock = PlaytestAliasHelpers.FormatVALBlock(_config.aliases);
            StringAssert.Contains("/Zone/Counter|ZoneComp|Remaining", cfgBlock);
        }

        // 9. FormatVALBlock output round-trips through CollectVals preserving full pipe
        [Test]
        public void FormatVALBlock_RoundTrips_ThroughCollectVals()
        {
            _config.aliases = new List<QueryAlias>
            {
                new QueryAlias
                {
                    alias = "counter",
                    type = AliasType.ValPath,
                    path = "/Zone/Counter",
                    component = "ZoneComp",
                    field = "Remaining"
                }
            };
            var block = PlaytestAliasHelpers.FormatVALBlock(_config.aliases);
            var vals = PlaytestParser.CollectVals(block.Split('\n'));
            Assert.IsTrue(vals.TryGetValue("counter", out var value));
            Assert.AreEqual("/Zone/Counter|ZoneComp|Remaining", value);
        }

        // 10. FormatLine for ValPath with comp+field outputs full pipe path
        [Test]
        public void FormatLine_ValPath_WithCompField_OutputsFullPipe()
        {
            var a = new QueryAlias
            {
                alias = "counter",
                type = AliasType.ValPath,
                path = "/Zone/Counter",
                component = "ZoneComp",
                field = "Remaining"
            };
            var line = PlaytestAliasHelpers.FormatLine(a);
            Assert.AreEqual("VAL $counter /Zone/Counter|ZoneComp|Remaining", line);
        }

        // 11. FormatLine for ValPath with path only — no trailing pipes
        [Test]
        public void FormatLine_ValPath_PathOnly_NoTrailingPipes()
        {
            var a = new QueryAlias { alias = "player", type = AliasType.ValPath, path = "/Player" };
            var line = PlaytestAliasHelpers.FormatLine(a);
            Assert.AreEqual("VAL $player /Player", line);
            StringAssert.DoesNotContain("|", line);
        }

        // 12. FormatLine for VarRuntime emits VAR keyword
        [Test]
        public void FormatLine_VarRuntime_EmitsVAR()
        {
            var a = new QueryAlias
            {
                alias = "hp",
                type = AliasType.VarRuntime,
                path = "/Player",
                component = "Health",
                field = "hp"
            };
            var line = PlaytestAliasHelpers.FormatLine(a);
            StringAssert.StartsWith("VAR ", line);
            StringAssert.Contains("$hp", line);
        }

        // 13. GetTable() value matches BuildAliasSection value for same alias (consistency)
        [Test]
        public void GetTable_Matches_BuildAliasSection()
        {
            var alias = new QueryAlias
            {
                alias = "counter",
                type = AliasType.ValPath,
                path = "/Zone/Counter",
                component = "ZoneComp",
                field = "Remaining"
            };
            _config.aliases = new List<QueryAlias> { alias };
            var savedOverride = AliasExpander._tableOverride;
            RegisterCleanup(() => AliasExpander._tableOverride = savedOverride);
            AliasExpander._tableOverride = new Dictionary<string, string>
            {
                { "counter", "/Zone/Counter|ZoneComp|Remaining" }
            };

            // What GetTable() builds (tested via ExpandText)
            var expanded = AliasExpander.ExpandText("$counter");

            // What BuildAliasSection emits: "--- ALIASES ---\ncounter=<value>\n---"
            var section = CommandRouter.BuildAliasSection(_config);
            Assert.IsNotNull(section, "BuildAliasSection returned null");
            var lines = section.Split('\n');
            // Find the "counter=..." line
            string sectionValue = null;
            foreach (var l in lines)
            {
                if (l.StartsWith("counter=")) { sectionValue = l.Substring("counter=".Length); break; }
            }
            Assert.IsNotNull(sectionValue, "counter= line not found in BuildAliasSection output");

            // Both must agree on the full pipe path
            Assert.AreEqual(sectionValue, expanded,
                "GetTable value differs from BuildAliasSection — pipe truncation bug");
        }
    }
}
