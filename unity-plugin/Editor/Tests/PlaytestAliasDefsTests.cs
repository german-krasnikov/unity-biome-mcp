// TDD: ParseDefsToAliases + ValidateAliases — pure string helpers, no Unity API.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestAliasDefsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── ParseDefsToAliases ────────────────────────────────────────────────

        [Test]
        public void ParseDefsToAliases_ValPath_SimpleObject()
        {
            var result = PlaytestAliasHelpers.ParseDefsToAliases("VAL $player /Player");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("player", result[0].alias);
            Assert.AreEqual(AliasType.ValPath, result[0].type);
            Assert.AreEqual("/Player", result[0].path);
            Assert.IsEmpty(result[0].component);
            Assert.IsEmpty(result[0].field);
        }

        [Test]
        public void ParseDefsToAliases_ValPath_WithComponentAndField()
        {
            var result = PlaytestAliasHelpers.ParseDefsToAliases("VAL $build_gate /Path|Comp|Field");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(AliasType.ValPath, result[0].type);
            Assert.AreEqual("/Path", result[0].path);
            Assert.AreEqual("Comp", result[0].component);
            Assert.AreEqual("Field", result[0].field);
        }

        [Test]
        public void ParseDefsToAliases_ValConst_Literal()
        {
            var result = PlaytestAliasHelpers.ParseDefsToAliases("VAL $spawn_pos -1.18,0,-5.36");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(AliasType.ValConst, result[0].type);
            Assert.AreEqual("-1.18,0,-5.36", result[0].constValue);
            Assert.AreEqual("spawn_pos", result[0].alias);
        }

        [Test]
        public void ParseDefsToAliases_VarRuntime_WithAtPath()
        {
            var result = PlaytestAliasHelpers.ParseDefsToAliases("VAR $tutorial_step @/Path|Comp|Field");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(AliasType.VarRuntime, result[0].type);
            Assert.AreEqual("tutorial_step", result[0].alias);
            Assert.AreEqual("/Path", result[0].path);
            Assert.AreEqual("Comp", result[0].component);
            Assert.AreEqual("Field", result[0].field);
        }

        [Test]
        public void ParseDefsToAliases_SkipsBlankLinesAndComments()
        {
            var input = "# comment\n\nVAL $x /Y\n# another comment";
            var result = PlaytestAliasHelpers.ParseDefsToAliases(input);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("x", result[0].alias);
        }

        [Test]
        public void ParseDefsToAliases_SkipsMacroBlocks()
        {
            var input = "VAL $a /A\nMACRO clear\nVAL $inner /B\nEND_MACRO\nVAL $b /C";
            var result = PlaytestAliasHelpers.ParseDefsToAliases(input);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("a", result[0].alias);
            Assert.AreEqual("b", result[1].alias);
        }

        [Test]
        public void ParseDefsToAliases_SkipsInclude()
        {
            var input = "INCLUDE other.defs\nVAL $x /Y";
            var result = PlaytestAliasHelpers.ParseDefsToAliases(input);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("x", result[0].alias);
        }

        [Test]
        public void ParseDefsToAliases_ThrowsOnValWithMissingValue()
        {
            Assert.Throws<ArgumentException>(() =>
                PlaytestAliasHelpers.ParseDefsToAliases("VAL $only_name"));
        }

        [Test]
        public void ParseDefsToAliases_LastDefinitionWins()
        {
            var input = "VAL $x /First\nVAL $x /Second";
            var result = PlaytestAliasHelpers.ParseDefsToAliases(input);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/Second", result[0].path);
        }

        // ── ValidateAliases ───────────────────────────────────────────────────

        [Test]
        public void ValidateAliases_InSync_ReturnsOkCount()
        {
            var list = new List<QueryAlias>
            {
                new QueryAlias { alias = "x", type = AliasType.ValPath,
                    path = "/A", component = "B", field = "C" }
            };
            var result = PlaytestAliasHelpers.ValidateAliases(list, list);
            StringAssert.StartsWith("ok:", result);
            StringAssert.Contains("1", result);
        }

        [Test]
        public void ValidateAliases_MissingInAsset_ReportsMissing()
        {
            var defs = new List<QueryAlias>
            {
                new QueryAlias { alias = "x", type = AliasType.ValPath,
                    path = "/A", component = "", field = "" }
            };
            var result = PlaytestAliasHelpers.ValidateAliases(defs, new List<QueryAlias>());
            StringAssert.Contains("missing:", result);
            StringAssert.Contains("$x", result);
        }

        [Test]
        public void ValidateAliases_ExtraInAsset_ReportsExtra()
        {
            var asset = new List<QueryAlias>
            {
                new QueryAlias { alias = "y", type = AliasType.ValPath,
                    path = "/B", component = "", field = "" }
            };
            var result = PlaytestAliasHelpers.ValidateAliases(new List<QueryAlias>(), asset);
            StringAssert.Contains("extra:", result);
            StringAssert.Contains("$y", result);
        }

        [Test]
        public void ValidateAliases_ChangedValue_ReportsChangedWithBeforeAfter()
        {
            var defs = new List<QueryAlias>
            {
                new QueryAlias { alias = "x", type = AliasType.ValPath,
                    path = "/A", component = "", field = "" }
            };
            var asset = new List<QueryAlias>
            {
                new QueryAlias { alias = "x", type = AliasType.ValPath,
                    path = "/B", component = "", field = "" }
            };
            var result = PlaytestAliasHelpers.ValidateAliases(defs, asset);
            StringAssert.Contains("changed:", result);
            StringAssert.Contains("$x", result);
            StringAssert.Contains("defs:", result);
            StringAssert.Contains("asset:", result);
        }

        // ── ExportToDefs seam: ImportAsset not Refresh ────────────────────────

        [Test]
        public void ExportToDefs_UsesImportAsset_NotGlobalRefresh()
        {
            var prev = PlaytestAliasHelpers._importAsset;
            string capturedPath = null;
            ImportAssetOptions capturedOptions = ImportAssetOptions.Default;
            int callCount = 0;

            PlaytestAliasHelpers._importAsset = (path, opts) =>
            {
                capturedPath = path;
                capturedOptions = opts;
                callCount++;
            };

            string exportedAbsPath = null;
            RegisterCleanup(() =>
            {
                PlaytestAliasHelpers._importAsset = prev;
                if (exportedAbsPath != null && File.Exists(exportedAbsPath))
                    File.Delete(exportedAbsPath);
            });

            var aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "test_seam", type = AliasType.ValConst, constValue = "1" }
            };

            exportedAbsPath = PlaytestAliasHelpers.ExportToDefs(aliases, "test_refresh_seam");

            Assert.AreEqual(1, callCount, "_importAsset seam must be called exactly once");
            Assert.IsNotNull(capturedPath);
            StringAssert.EndsWith(".defs", capturedPath);
            Assert.AreEqual(ImportAssetOptions.Default, capturedOptions);
        }

        // ── Export roundtrip ──────────────────────────────────────────────────

        [Test]
        public void FormatVALBlock_ParseDefs_Roundtrip_InSync()
        {
            var aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "player",  type = AliasType.ValPath,
                    path = "/Player", component = "", field = "" },
                new QueryAlias { alias = "pos",    type = AliasType.ValConst,
                    constValue = "-1.18,0,-5.36" },
                new QueryAlias { alias = "step",   type = AliasType.VarRuntime,
                    path = "/Tutorial", component = "TutorialScenario", field = "CurrentStep" },
            };
            var block  = PlaytestAliasHelpers.FormatVALBlock(aliases);
            var parsed = PlaytestAliasHelpers.ParseDefsToAliases(block);
            var result = PlaytestAliasHelpers.ValidateAliases(aliases, parsed);
            StringAssert.StartsWith("ok:", result);
            StringAssert.Contains("in sync", result);
        }

        // ── Sync (clear + addrange) ───────────────────────────────────────────

        [Test]
        public void SyncAliases_ClearAndReplace_ValidatesOk()
        {
            var defsAliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "x", type = AliasType.ValPath,
                    path = "/New", component = "", field = "" }
            };
            // Simulate config.aliases before sync — has old entry + extra
            var configAliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "x", type = AliasType.ValPath,
                    path = "/Old", component = "", field = "" },
                new QueryAlias { alias = "y", type = AliasType.ValPath,
                    path = "/Extra", component = "", field = "" },
            };

            // Simulate ExecSyncFromDefs core logic
            configAliases.Clear();
            configAliases.AddRange(defsAliases);

            var result = PlaytestAliasHelpers.ValidateAliases(defsAliases, configAliases);
            StringAssert.StartsWith("ok:", result);
            StringAssert.Contains("in sync", result);
        }
    }
}
