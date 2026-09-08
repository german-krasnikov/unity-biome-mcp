// Actual-content fingerprint regression checks, including metadata-preserving edits.
using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunAssemblyFingerprintTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly TimeSpan MtimeShift = TimeSpan.FromMinutes(5);

        [Test]
        public void HashFile_UnchangedBytes_HasStableDigestAfterRehash()
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

                Assert.AreEqual(2, calls,
                    "file metadata is not a content certificate");
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

        [Test]
        public void HashFile_SameSizeSameMtimeRewrite_ChangesDigest()
        {
            using var scope = new TempDirScope("mcp_fingerprint_same_metadata");
            var path = Path.Combine(scope.Path, "source.cs");
            var fixedTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.WriteAllText(path, "return 101;");
            File.SetLastWriteTimeUtc(path, fixedTime);
            var before = TestRunAssemblyFingerprint.HashFile(path);
            File.WriteAllText(path, "return 202;");
            File.SetLastWriteTimeUtc(path, fixedTime);
            Assert.AreNotEqual(before, TestRunAssemblyFingerprint.HashFile(path));
        }

        // Stable results still require re-reading current file content.
        [Test]
        public void Capture_CalledTwiceOnUnchangedTree_RevalidatesBytesWithStableFingerprint()
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
                Assert.Greater(calls, callsAfterFirst, "each capture must inspect current bytes");
                Assert.AreEqual(first.Fingerprint, second.Fingerprint);
            }
            finally { TestRunAssemblyFingerprint.HashFileImpl = original; }
        }
        [TestCase("Added.cs", "undiscovered-source")]
        [TestCase("Added.asmdef", "undiscovered-asmdef")]
        [TestCase("Added.asmref", "asmref-coverage")]
        public void RawMembershipOutsideCompilerInventory_IsExplicitlyUnknown(string file, string reason)
        {
            using var scope = new TempDirScope("mcp_freshness_membership");
            File.WriteAllText(Path.Combine(scope.Path, file), "{}");
            Assert.That(AssemblyFreshnessInventory.FindUndiscovered(scope.Path,
                new HashSet<string>(), new HashSet<string>()), Is.EqualTo(reason));
        }

        [Test]
        public void KnownNestedAssemblySourcesDoNotUseFilenameAsAssemblyName()
        {
            using var scope = new TempDirScope("mcp_freshness_nested");
            var nested = Directory.CreateDirectory(Path.Combine(scope.Path, "Chat", "Tests")).FullName;
            var definition = Path.Combine(nested, "FilenameDiffers.asmdef");
            var source = Path.Combine(nested, "Test.cs");
            File.WriteAllText(definition, "{\"name\":\"UnityMCP.Chat.Tests\"}");
            File.WriteAllText(source, "// compiler-reported source");
            Assert.That(AssemblyFreshnessInventory.FindUndiscovered(scope.Path,
                new HashSet<string> { source }, new HashSet<string> { definition }), Is.Empty);
        }
    }
}
