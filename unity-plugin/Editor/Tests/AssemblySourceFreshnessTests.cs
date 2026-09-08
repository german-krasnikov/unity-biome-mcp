using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class AssemblySourceFreshnessTests
#if UNITY_EDITOR
        : UnityMCP.Editor.Testing.UnityMcpTestBase
#endif
    {
        private sealed class Artifact : IDisposable
        {
            internal readonly string Root = Path.Combine(Path.GetTempPath(), "biome_pdb_" + Guid.NewGuid().ToString("N"));
            internal readonly string Output;
            internal readonly string Source;
            internal Artifact([CallerFilePath] string sourcePath = "")
            {
                Directory.CreateDirectory(Root);
                Output = Path.Combine(Root, "fixture.dll");
                Source = Path.Combine(Root, "source.cs");
                var assembly = typeof(AssemblySourceFreshnessTests).Assembly.Location;
                File.Copy(assembly, Output);
                File.Copy(Path.ChangeExtension(assembly, ".pdb"), Path.ChangeExtension(Output, ".pdb"));
                File.Copy(ResolveOriginal(sourcePath), Source);
            }
            internal string Resolve(string path) => path.Replace('\\', '/').EndsWith("/AssemblySourceFreshnessTests.cs", StringComparison.Ordinal)
                ? Source : ResolveOriginal(path);
            private static string ResolveOriginal(string path)
            {
#if UNITY_EDITOR
                return TestRuns.TestRunAssemblyFingerprint.ResolveSourcePath(
                    Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..")), path);
#else
                return Path.GetFullPath(path);
#endif
            }
            internal AssemblySourceObservation Inspect(string limitation = "") =>
                AssemblySourceFreshness.Inspect(Output, new[] { Source }, Resolve, limitation);
            public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
        }

        [Test]
        public void ActualCompilerPdb_OriginalSourceIsMatchedWithoutCompileReceipt()
        {
            using var artifact = new Artifact();
            var result = artifact.Inspect();
            Assert.That(result.PdbPaired, Is.True, result.Limitation);
            Assert.That(result.CheckedSources, Is.EqualTo(1));
            Assert.That(result.Token, Is.EqualTo("fresh"), result.Mismatch);
        }

        [Test]
        public void ActualCompilerPdb_SameSizeSameMtimeChangedSourceIsStaleBeforeRun()
        {
            using var artifact = new Artifact();
            var time = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(artifact.Source, time);
            Assert.That(artifact.Inspect().Token, Is.EqualTo("fresh"));
            var bytes = File.ReadAllBytes(artifact.Source);
            bytes[0] ^= 1;
            File.WriteAllBytes(artifact.Source, bytes);
            File.SetLastWriteTimeUtc(artifact.Source, time);
            var result = artifact.Inspect();
            Assert.That(result.Token, Is.EqualTo("stale"));
            Assert.That(result.Mismatch, Does.Contain("checksum differs"));
            Assert.That(result.MismatchedCompilerSource, Is.EqualTo(artifact.Source));
        }

        [Test]
        public void UnownedPdbDocumentMismatchCannotAuthorizeSourceImport()
        {
            using var artifact = new Artifact();
            var bytes = File.ReadAllBytes(artifact.Source); bytes[0] ^= 1;
            File.WriteAllBytes(artifact.Source, bytes);
            var result = AssemblySourceFreshness.Inspect(artifact.Output, Array.Empty<string>(), artifact.Resolve);
            Assert.That(result.Token, Is.EqualTo("stale"));
            Assert.That(result.MismatchedCompilerSource, Is.Empty);
        }

        [TestCase("Assets/Test.cs", "Assets/Test.cs")]
        [TestCase("AssetsNear/Test.cs", "")]
        [TestCase("LocalPackage/Editor/Test.cs", "Packages/com.test/Editor/Test.cs")]
        [TestCase("LocalPackageNear/Editor/Test.cs", "")]
        public void CompilerPhysicalSourceMapsOnlyWithinExactAssetRoots(string relative, string expected)
        {
            using var artifact = new Artifact();
            var roots = new Dictionary<string, string> { ["Packages/com.test"] = Path.Combine(artifact.Root, "LocalPackage") };
            var mapped = AssemblySourceFreshness.SourceAssetPath(Path.Combine(artifact.Root, relative), artifact.Root, roots);
            Assert.That(mapped, Is.EqualTo(expected));
        }

        [Test]
        public void UnknownSourceEvidenceCannotSelectAnAsset()
        {
            using var artifact = new Artifact();
            File.Delete(Path.ChangeExtension(artifact.Output, ".pdb"));
            Assert.That(artifact.Inspect().MismatchedCompilerSource, Is.Empty);
            Assert.That(AssemblySourceFreshness.SourceAssetPath("", artifact.Root, new Dictionary<string, string>()), Is.Empty);
        }

        [Test]
        public void MetadataOnlyTouch_IsStillMatchedToPdb()
        {
            using var artifact = new Artifact();
            File.SetLastWriteTimeUtc(artifact.Source, DateTime.UtcNow.AddMinutes(5));
            Assert.That(artifact.Inspect().Token, Is.EqualTo("fresh"));
        }

        [Test]
        public void ForeignPairedPdbIdentityCannotBeAccepted()
        {
            using var artifact = new Artifact();
            // A real foreign compiler output, not a fabricated invalid header.
            File.Copy(typeof(AssemblySourceFreshness).Assembly.Location, artifact.Output, true);
#if !UNITY_EDITOR
            // In the linked pure lane production and tests share one assembly.
            // Cecil rewrites its MVID so the original compiler PDB no longer pairs.
            using (var module = Mono.Cecil.ModuleDefinition.ReadModule(artifact.Output, new Mono.Cecil.ReaderParameters { InMemory = true }))
            {
                module.Mvid = Guid.NewGuid();
                module.Write(artifact.Output);
            }
#endif
            var result = artifact.Inspect();
            Assert.That(result.Token, Is.Not.EqualTo("fresh"));
            Assert.That(result.PdbPaired, Is.False);
            Assert.That(result.MismatchedCompilerSource, Is.Empty);
        }

        [Test]
        public void MissingPdbIsLimitedCoverage_NotStaleOrProvenFresh()
        {
            using var artifact = new Artifact();
            File.Delete(Path.ChangeExtension(artifact.Output, ".pdb"));
            Assert.That(artifact.Inspect().Token, Is.EqualTo("unknown(missing-pdb)"));
        }

        [Test]
        public void UndiscoveredMembershipPreventsKnownFreshEvenWhenOldDocumentsMatch()
        {
            using var artifact = new Artifact();
            Assert.That(artifact.Inspect("undiscovered-source").Token, Is.EqualTo("unknown(undiscovered-source)"));
        }

        [Test]
        public void SourceWithoutDocumentIsExplicitPartialCoverage()
        {
            using var artifact = new Artifact();
            var uncovered = Path.Combine(artifact.Root, "new-source.cs");
            File.WriteAllText(uncovered, "// no compiler document");
            var result = AssemblySourceFreshness.Inspect(artifact.Output, new[] { artifact.Source, uncovered }, artifact.Resolve);
            Assert.That(result.Token, Is.EqualTo("unknown(partial-source-coverage)"));
            Assert.That(result.CheckedSources, Is.EqualTo(1));
        }
    }
}
