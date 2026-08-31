// P0-70: real port implementations backing SourcePatchCoordinator (§6).
// Exercised against the real Unity APIs they wrap; no fakes here — the fakes
// in SourcePatchCoordinatorTests.cs already cover the coordinator's own
// sequencing.
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchUnityPortsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/SourcePatchPorts";

        private static string Abs(string relPath) => Path.GetFullPath(relPath);

        [Test]
        public void BytesPort_WriteThenRead_RoundTripsExactBytes()
        {
            // Deliberately .txt, not .cs: the port itself is extension-agnostic
            // (only SourcePatchRequest.TryCreate enforces .cs). Write() is raw
            // File.WriteAllBytes only — it no longer calls
            // AssetDatabase.ImportAsset at all (§6 P0-70 fix, P0-80 Cycle A).
            // .txt remains the right choice here regardless: it keeps this
            // round-trip test decoupled from any AssetDatabase import noise a
            // real .cs path could still trigger elsewhere in a shared batch run.
            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/roundtrip.txt";
            AssetHelper.EnsureDirectory(path);
            var port = new UnitySourcePatchBytesPort();
            var content = System.Text.Encoding.UTF8.GetBytes("port write\n");

            port.Write(path, content);
            var readBack = port.Read(path);

            Assert.IsTrue(File.Exists(Abs(path)));
            CollectionAssert.AreEqual(content, readBack);
        }

        // Product bug found in P0-80 Cycle A: source_patch_write stably returned
        // "outcome is uncertain; entering Recovery" on a live ON apply even
        // though the FSR provider itself succeeded (diagnostic probe proved
        // DynamicAssemblyCompiler.Compile -> AssemblyChangesLoader applied=True,
        // failureReason=null). Root cause: this port's own Write() called
        // AssetDatabase.ImportAsset on the .cs file BEFORE provider.Apply ran,
        // which synchronously flips EditorApplication.isCompiling and requests
        // real Unity script compilation (Editor.log: "[ScriptCompilation]
        // Requested script compilation because: Assetdatabase observed changes
        // in script compilation related files") — SyncHelperCompileEvidencePort
        // then correctly (from its own narrow view) detects that self-inflicted
        // violation and the coordinator branches into Recovery. §3.2 requires
        // a "raw full-file source update" — Unity must only see the change via
        // the OFF-path sync, never via an ON-path import.
        [Test]
        public void BytesPort_Write_CsPath_NeverRequestsUnityScriptCompilation()
        {
            // Deliberately does not assert on the whole-project
            // EditorApplication.isCompiling flag: a sibling P0-30 legacy test
            // in the same filtered batch (e.g. SourcePatchCsWriteGateTests)
            // can legitimately be mid-compile from its own real .cs write at
            // the same time, and waiting for that to settle here previously
            // proved actively dangerous — an actual Domain Reload firing
            // while this test's own wait loop is subscribed to
            // EditorApplication.update corrupted the whole UTF run's terminal
            // evidence. The two assertions below are causally tied to THIS
            // port's own path/assembly and are immune to that ambient noise:
            // GetMainAssetTypeAtPath only reflects what happened to this exact
            // file, and the Domain stamp only tracks UnityMCP.* assemblies —
            // a scratch TestsTemp file (outside every asmdef) never touches it.
            TrackOwnedAsset(TempFolder);
            var path = TempFolder + "/never-compiled.cs";
            AssetHelper.EnsureDirectory(path);
            var port = new UnitySourcePatchBytesPort();
            var stampBefore = SyncHelper.CurrentDomainStamp;

            port.Write(path, System.Text.Encoding.UTF8.GetBytes("// ON-path write must stay import-free\n"));

            Assert.IsNull(AssetDatabase.GetMainAssetTypeAtPath(path),
                "the file must stay unknown to AssetDatabase until the next real sync, never registered as a MonoScript " +
                "— ON-path bytes-port writes must never call AssetDatabase.ImportAsset on a .cs file (§3.2 raw full-file source update)");
            Assert.AreEqual(stampBefore, SyncHelper.CurrentDomainStamp, "stable Domain stamp — zero compile occurred");
        }

        [Test]
        public void LeasePort_AcquireThenDispose_DoesNotThrow()
        {
            var port = new UnityAutoRefreshLeasePort();

            var lease = port.AcquireLease();

            Assert.IsNotNull(lease);
            Assert.DoesNotThrow(() => lease.Dispose());
        }

        [Test]
        public void LeasePort_DoubleDispose_IsIdempotentAndDoesNotThrow()
        {
            var port = new UnityAutoRefreshLeasePort();
            var lease = port.AcquireLease();
            lease.Dispose();

            // Clarification 2: "releases owned holds once" — a second Dispose
            // must be a no-op, never a second Allow/Unlock pair and never a throw.
            Assert.DoesNotThrow(() => lease.Dispose());
        }

        [Test]
        public void LeasePort_ReleaseThenReacquire_DoesNotThrow()
        {
            // No public Editor API exposes the internal AutoRefresh disallow
            // ref-count for a direct readback, and AssetDatabase.Refresh() is
            // banned from test source (AI/testing.md). This is the closest
            // available proxy for "auto-refresh is really allowed again after
            // dispose": a well-released pair tolerates an immediate second
            // acquire/release cycle without hanging or throwing.
            var port = new UnityAutoRefreshLeasePort();
            port.AcquireLease().Dispose();

            var second = port.AcquireLease();

            Assert.DoesNotThrow(() => second.Dispose());
        }

        [Test]
        public void CompileEvidencePort_MatchesRealAmbientCompilingAndStampState()
        {
            // Not a hardcoded "must be true": in a large batch run,
            // EditorApplication.isCompiling can genuinely and non-transiently
            // read true for reasons unrelated to this port (e.g. this
            // fixture's own AssetHelperTests neighbors doing real asset
            // import work), reproducible in the full filtered batch and
            // absent when this class runs alone — a bounded settle-wait did
            // not clear it. Asserting a fixed expectation would make the test
            // itself flaky on ambient state it does not own. Instead this
            // recomputes the exact same formula the port uses, independently,
            // at the call site: it still catches a real regression in the
            // port's wiring (e.g. a dropped/negated condition) without ever
            // depending on what the ambient value happens to be.
            var stampBefore = SyncHelper.CurrentDomainStamp;
            var port = new SyncHelperCompileEvidencePort();
            var request = MakeRequest();

            var expected = !EditorApplication.isCompiling && SyncHelper.CurrentDomainStamp == stampBefore;
            Assert.AreEqual(expected, port.ConfirmApplied(request));
        }

        [Test]
        public void CompileEvidencePort_StampChangedSinceConstruction_ReturnsFalse()
        {
            var originalStamp = SyncHelper.CurrentDomainStamp;
            RegisterCleanup(() => SyncHelper.OverrideDomainStampForTest(originalStamp));

            var port = new SyncHelperCompileEvidencePort();
            SyncHelper.OverrideDomainStampForTest(originalStamp + "-changed");
            var request = MakeRequest();

            Assert.IsFalse(port.ConfirmApplied(request));
        }

        private static UnityMCP.Editor.SourcePatch.SourcePatchRequest MakeRequest()
        {
            UnityMCP.Editor.SourcePatch.SourcePatchRequest.TryCreate(
                "Assets/Fake.cs",
                System.Text.Encoding.UTF8.GetBytes("before"),
                System.Text.Encoding.UTF8.GetBytes("after"),
                out var request);
            return request;
        }
    }
}
