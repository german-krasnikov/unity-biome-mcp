// P0-30: freezes the legacy OFF .cs write route end-to-end, INCLUDING the real
// AssetDatabase import that schedules a genuine Unity compile. Deliberately
// isolated in its own fixture (separate from AssetHelperTests) so that a
// class/fixture-scoped --filter run of the OTHER new P0-30 characterization
// tests never sweeps this one in — NUnit's [Explicit] (BiomeWorkerOnly) is
// excluded from unfiltered/category runs, but IS included by a filter that
// names its own containing class.
//
// NOT executed during P0-30 itself: triggering a real compile mid-session
// risks corrupting the full EditMode suite run mandated by this task. Live
// .cs-compile proof is owned by P0-70/P0-80's disposable-worker product
// cycles (see Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md
// §6). This test exists so that proof has somewhere to run when the time
// comes — do not delete it to "clean up" an unused-looking fixture.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SourcePatchLegacyCsWriteExplicitTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/SourcePatchCsBoundary";

        [Test]
        [UnityMCP.Editor.Testing.BiomeWorkerOnly(
            "writes a real .cs file under Assets/ and lets AssetDatabase.ImportAsset " +
            "schedule a genuine Unity recompile; must run alone in a disposable worker, " +
            "never as part of a class/category-filtered or full-suite pass")]
        public void WriteText_CsExtension_LegacyRouteWritesAndSchedulesCompile()
        {
            TrackOwnedAsset(TempFolder);
            AssetHelper.EnsureDirectory(TempFolder + "/Probe.cs");

            var content = "// P0-30 legacy .cs write probe — safe to compile, does nothing.\n";
            var result = AssetDatabaseHelper.Execute("write_text",
                $"{{\"path\":\"{TempFolder}/Probe.cs\",\"content\":\"{content.Replace("\n", "\\n")}\"}}");

            StringAssert.StartsWith("ok:write", result);
            var abs = System.IO.Path.GetFullPath(TempFolder + "/Probe.cs");
            Assert.IsTrue(System.IO.File.Exists(abs));
            var bytes = System.IO.File.ReadAllBytes(abs);
            Assert.AreEqual(0xEF, bytes[0]);
            Assert.AreEqual(0xBB, bytes[1]);
            Assert.AreEqual(0xBF, bytes[2]);
        }
    }
}
