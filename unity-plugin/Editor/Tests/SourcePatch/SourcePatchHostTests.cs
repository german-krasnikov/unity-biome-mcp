// P0-50: unit-level coverage for SourcePatchHost, the one main-assembly
// integration seam (§3.1/§6). CurrentState is a settable seam — no live path
// can reach anything but Unavailable yet (no provider registration/P0-60, no
// mutation_mode command/P0-70), so every non-Off/Unavailable state here is
// forced directly. See Plans/HotReload/V2/FSR-MVP-CLEAN/
// 04-PARETO-COMPLETION-HANDOFF.md.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchHostTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/SourcePatchHost";

        [SetUp]
        public void SetUp()
        {
            // Mandatory: reset the shared static BEFORE any test sets it, so a
            // Busy/Recovery leak from one test can never bleed into the next.
            RegisterCleanup(SourcePatchHost.ResetForTests);
        }

        private static string Abs(string relPath) => Path.GetFullPath(relPath);

        [TestCase(SourcePatchState.Unavailable, ExpectedResult = false)]
        [TestCase(SourcePatchState.Off, ExpectedResult = false)]
        [TestCase(SourcePatchState.OnReady, ExpectedResult = true)]
        [TestCase(SourcePatchState.Busy, ExpectedResult = true)]
        [TestCase(SourcePatchState.Disabling, ExpectedResult = true)]
        [TestCase(SourcePatchState.Recovery, ExpectedResult = true)]
        public bool GuardLegacyCsWrite_CsPath_ThrowsExactlyForNonOffUnavailableStates(SourcePatchState state)
        {
            SourcePatchHost.CurrentState = state;
            try
            {
                SourcePatchHost.GuardLegacyCsWrite("Assets/Foo.cs");
                return false; // no throw
            }
            catch (System.InvalidOperationException)
            {
                return true; // threw
            }
        }

        [TestCase(SourcePatchState.Unavailable)]
        [TestCase(SourcePatchState.Off)]
        [TestCase(SourcePatchState.OnReady)]
        [TestCase(SourcePatchState.Busy)]
        [TestCase(SourcePatchState.Disabling)]
        [TestCase(SourcePatchState.Recovery)]
        public void GuardLegacyCsWrite_NonCsPath_NeverThrows_ForAnyState(SourcePatchState state)
        {
            SourcePatchHost.CurrentState = state;
            Assert.DoesNotThrow(() => SourcePatchHost.GuardLegacyCsWrite("Assets/Foo.txt"));
        }

        [Test]
        public void GuardLegacyCsWrite_Throws_MentionsStateAndPreEffectRejection()
        {
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            var ex = Assert.Throws<System.InvalidOperationException>(
                () => SourcePatchHost.GuardLegacyCsWrite("Assets/Foo.cs"));
            StringAssert.Contains("OnReady", ex.Message);
            StringAssert.Contains("source patch active — legacy .cs write rejected pre-effect", ex.Message);
        }

        [TestCase(SourcePatchState.Unavailable)]
        [TestCase(SourcePatchState.Off)]
        public void WriteText_OffOrUnavailable_DelegatesToLegacyWriterExactlyOnce(SourcePatchState state)
        {
            TrackOwnedAsset(TempFolder);
            AssetHelper.EnsureDirectory(TempFolder + "/legacy.txt");
            AssetHelper.EnsureDirectory(TempFolder + "/viahost.txt");

            var legacyResult = AssetDatabaseHelper.Execute("write_text",
                $"{{\"path\":\"{TempFolder}/legacy.txt\",\"content\":\"parity-check\"}}");

            SourcePatchHost.CurrentState = state;
            var hostResult = SourcePatchHost.WriteText(
                $"{{\"path\":\"{TempFolder}/viahost.txt\",\"content\":\"parity-check\"}}");

            // Single call-site, no delegation counter available — assert both the
            // exact response text and the on-disk effect match the direct legacy
            // call byte-for-byte (sufficient proof of "exactly one delegation").
            var legacyBytes = File.ReadAllBytes(Abs(TempFolder + "/legacy.txt"));
            var hostBytes = File.ReadAllBytes(Abs(TempFolder + "/viahost.txt"));
            CollectionAssert.AreEqual(legacyBytes, hostBytes);
            Assert.AreEqual(
                legacyResult.Replace("legacy.txt", "viahost.txt"),
                hostResult,
                "source_patch_write's OFF/Unavailable delegation must produce the exact same response as calling the legacy writer directly.");
        }

        [TestCase(SourcePatchState.OnReady)]
        [TestCase(SourcePatchState.Busy)]
        [TestCase(SourcePatchState.Disabling)]
        [TestCase(SourcePatchState.Recovery)]
        public void WriteText_NonOffUnavailableState_ThrowsAndNeverWritesFile(SourcePatchState state)
        {
            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/never-written.cs";
            AssetHelper.EnsureDirectory(path);

            SourcePatchHost.CurrentState = state;
            Assert.Throws<System.InvalidOperationException>(
                () => SourcePatchHost.WriteText($"{{\"path\":\"{path}\",\"content\":\"x\"}}"));

            Assert.IsFalse(File.Exists(Abs(path)));
        }
    }
}
