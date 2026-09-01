// P0-50: proves the SourcePatchHost gate inside AssetDatabaseHelper.WriteText
// covers BOTH raw direct dispatch and batch dispatch with the single call
// site — BatchHelper.Execute reaches the exact same WriteText via
// CommandRouter.ExecuteCommand("asset", ...), so there is nothing batch-side
// left to duplicate. See §3.2/§6 P0-50 in
// Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchCsWriteGateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/SourcePatchCsWriteGate";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SourcePatchHost.ResetForTests);
        }

        private static string Abs(string relPath) => Path.GetFullPath(relPath);

        [TestCase(SourcePatchState.OnReady)]
        [TestCase(SourcePatchState.Busy)]
        [TestCase(SourcePatchState.Disabling)]
        [TestCase(SourcePatchState.Recovery)]
        public void WriteText_RawDirect_CsPath_NonOffState_RejectsBeforeFileWrite(SourcePatchState state)
        {
            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/raw-blocked.cs";
            AssetHelper.EnsureDirectory(path);

            SourcePatchHost.CurrentState = state;
            Assert.Throws<System.InvalidOperationException>(() =>
                AssetDatabaseHelper.Execute("write_text",
                    $"{{\"path\":\"{path}\",\"content\":\"x\"}}"));

            Assert.IsFalse(File.Exists(Abs(path)));
        }

        [TestCase(SourcePatchState.OnReady)]
        [TestCase(SourcePatchState.Busy)]
        [TestCase(SourcePatchState.Disabling)]
        [TestCase(SourcePatchState.Recovery)]
        public void WriteText_ViaBatch_CsPath_NonOffState_RejectsBeforeFileWrite(SourcePatchState state)
        {
            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/batch-blocked.cs";
            AssetHelper.EnsureDirectory(path);

            SourcePatchHost.CurrentState = state;
            var result = BatchHelper.Execute(
                $"asset action=write_text path=\"{path}\" content=\"x\"", "stop");

            Assert.IsTrue(BatchHelper.HasErrors(result), result);
            Assert.IsFalse(File.Exists(Abs(path)));
        }

        [TestCase(SourcePatchState.Off)]
        [TestCase(SourcePatchState.Unavailable)]
        public void WriteText_CsPath_OffOrUnavailable_StillWritesLikeBaseline(SourcePatchState state)
        {
            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/off-allowed.cs";
            AssetHelper.EnsureDirectory(path);

            SourcePatchHost.CurrentState = state;
            var result = AssetDatabaseHelper.Execute("write_text",
                $"{{\"path\":\"{path}\",\"content\":\"testdata\"}}");

            StringAssert.StartsWith("ok:write", result);
            var bytes = File.ReadAllBytes(Abs(path));
            // Same pre-existing BOM quirk frozen by P0-30 — the gate must not
            // change legacy OFF bytes at all.
            Assert.AreEqual(0xEF, bytes[0]);
            Assert.AreEqual(0xBB, bytes[1]);
            Assert.AreEqual(0xBF, bytes[2]);
        }

        [TestCase(SourcePatchState.OnReady)]
        [TestCase(SourcePatchState.Busy)]
        public void WriteText_NonCsPath_NeverGated_EvenWhileArmed(SourcePatchState state)
        {
            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/unaffected.txt";
            AssetHelper.EnsureDirectory(path);

            SourcePatchHost.CurrentState = state;
            var result = AssetDatabaseHelper.Execute("write_text",
                $"{{\"path\":\"{path}\",\"content\":\"testdata\"}}");

            StringAssert.StartsWith("ok:write", result);
            Assert.AreEqual("testdata", File.ReadAllText(Abs(path), System.Text.Encoding.UTF8));
        }
    }
}
