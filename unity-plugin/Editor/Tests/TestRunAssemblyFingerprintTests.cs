// TDD -- A24: TestRunAssemblyFingerprint.HashFile caches by (path, mtime, size) so an
// unchanged compiled tree performs 0 SHA-256 computations on a repeat Capture().
using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunAssemblyFingerprintTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly TimeSpan MtimeShift = TimeSpan.FromMinutes(5);

        [Test]
        public void HashFile_UnchangedMtimeAndSize_ReturnsCachedValueWithoutRehashing()
        {
            using var scope = new TempDirScope("mcp_fingerprint_cache");
            var path = Path.Combine(scope.Path, "unchanged.bin");
            File.WriteAllText(path, "content-a");
            var original = TestRunAssemblyFingerprint.HashFileImpl;
            var calls = 0;
            try
            {
                TestRunAssemblyFingerprint.HashFileImpl = p => { calls++; return original(p); };

                var first = TestRunAssemblyFingerprint.HashFile(path);
                var second = TestRunAssemblyFingerprint.HashFile(path);

                Assert.AreEqual(1, calls,
                    "unchanged (path,mtime,size) must reuse the cached hash, not rehash");
                Assert.AreEqual(first, second);
            }
            finally { TestRunAssemblyFingerprint.HashFileImpl = original; }
        }

        [Test]
        public void HashFile_ChangedSizeSameMtime_RehashesFile()
        {
            // A literal, coarse UTC constant -- not a value round-tripped through a
            // prior File.GetLastWriteTimeUtc read. SetLastWriteTimeUtc does not
            // preserve sub-microsecond precision exactly on every filesystem (macOS
            // APFS observed truncating it), so re-applying the same read-back value
            // can silently land on a *different* actual mtime the second time.
            // Applying the same literal constant to both writes converges on the
            // same truncated on-disk value both times.
            var fixedMtime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            using var scope = new TempDirScope("mcp_fingerprint_cache");
            var path = Path.Combine(scope.Path, "size-changed.bin");
            File.WriteAllText(path, "short");
            File.SetLastWriteTimeUtc(path, fixedMtime);
            var original = TestRunAssemblyFingerprint.HashFileImpl;
            var calls = 0;
            try
            {
                TestRunAssemblyFingerprint.HashFileImpl = p => { calls++; return original(p); };
                TestRunAssemblyFingerprint.HashFile(path);

                File.WriteAllText(path, "a much longer replacement body");
                File.SetLastWriteTimeUtc(path, fixedMtime); // same literal mtime, different size

                TestRunAssemblyFingerprint.HashFile(path);

                Assert.AreEqual(2, calls,
                    "same mtime but changed size must force a rehash -- mtime alone is unsafe");
            }
            finally { TestRunAssemblyFingerprint.HashFileImpl = original; }
        }

        [Test]
        public void HashFile_ChangedMtimeSameSize_RehashesFile()
        {
            using var scope = new TempDirScope("mcp_fingerprint_cache");
            var path = Path.Combine(scope.Path, "mtime-changed.bin");
            File.WriteAllText(path, "AAAAAAAAAA");
            var original = TestRunAssemblyFingerprint.HashFileImpl;
            var calls = 0;
            try
            {
                TestRunAssemblyFingerprint.HashFileImpl = p => { calls++; return original(p); };
                TestRunAssemblyFingerprint.HashFile(path);

                File.WriteAllText(path, "BBBBBBBBBB"); // same length, different content
                File.SetLastWriteTimeUtc(path,
                    File.GetLastWriteTimeUtc(path) + MtimeShift); // force a distinct mtime

                TestRunAssemblyFingerprint.HashFile(path);

                Assert.AreEqual(2, calls,
                    "same size but changed mtime must force a rehash");
            }
            finally { TestRunAssemblyFingerprint.HashFileImpl = original; }
        }

        // Acceptance (A24): a second Capture() over the real, unchanged compiled tree
        // performs 0 new hash computations -- proven via the seam counter, not by
        // inspecting HashFile results alone.
        [Test]
        public void Capture_CalledTwiceOnUnchangedTree_PerformsZeroAdditionalHashComputations()
        {
            var original = TestRunAssemblyFingerprint.HashFileImpl;
            var calls = 0;
            try
            {
                TestRunAssemblyFingerprint.HashFileImpl = p => { calls++; return original(p); };

                var first = TestRunBuildFingerprintProbe.Capture();
                Assert.IsTrue(first.IsCoherent, first.Error);
                var callsAfterFirst = calls;

                var second = TestRunBuildFingerprintProbe.Capture();

                Assert.IsTrue(second.IsCoherent, second.Error);
                Assert.AreEqual(callsAfterFirst, calls,
                    "a second Capture() over an unchanged tree must perform 0 new hash computations");
            }
            finally { TestRunAssemblyFingerprint.HashFileImpl = original; }
        }
    }
}
