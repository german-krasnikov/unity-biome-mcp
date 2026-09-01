using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using static UnityMCP.Editor.Tests.RoslynLoaderTestData;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class RoslynLoaderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUpRoslynLoaderIsolation()
        {
            RegisterCleanup(RoslynLoader.BeginTestIsolation());
            RoslynLoader.ResetForTests();
        }

        [Test]
        public void SelectLoadedPair_NoRoots_ReturnsNone()
        {
            var result = RoslynLoader.SelectLoadedPair(
                Array.Empty<RoslynAssemblySnapshot>(), out var core, out var compiler);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.None));
            Assert.That(core, Is.Null);
            Assert.That(compiler, Is.Null);
        }

        [Test]
        public void SelectLoadedPair_UniqueCompatiblePair_ReturnsSameAssemblies()
        {
            var pair = CompatiblePair();

            var result = RoslynLoader.SelectLoadedPair(pair, out var core, out var compiler);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Valid));
            Assert.That(core.Assembly, Is.SameAs(CoreToken));
            Assert.That(compiler.Assembly, Is.SameAs(CompilerToken));
        }

        [Test]
        public void SelectLoadedPair_PartialPair_ReturnsInvalid()
        {
            var pair = CompatiblePair();

            var result = RoslynLoader.SelectLoadedPair(
                new[] { pair[0] }, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void SelectLoadedPair_DuplicateCore_ReturnsInvalid()
        {
            var pair = CompatiblePair();

            var result = RoslynLoader.SelectLoadedPair(
                new[] { pair[0], pair[0], pair[1] }, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void SelectLoadedPair_MixedVersions_ReturnsInvalid()
        {
            var pair = CompatiblePair(compilerVersion: new Version(4, 5, 0, 0));

            var result = RoslynLoader.SelectLoadedPair(pair, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void SelectLoadedPair_CompilerReferencesDifferentCore_ReturnsInvalid()
        {
            var pair = CompatiblePair(referenceVersion: new Version(4, 5, 0, 0));

            var result = RoslynLoader.SelectLoadedPair(pair, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void SelectLoadedPair_NonMicrosoftToken_ReturnsInvalid()
        {
            var pair = CompatiblePair(compilerToken: "b03f5f7f11d50a3a");

            var result = RoslynLoader.SelectLoadedPair(pair, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void SelectLoadedPair_NonNeutralCulture_ReturnsInvalid()
        {
            var pair = CompatiblePair(compilerCulture: "en-US");

            var result = RoslynLoader.SelectLoadedPair(pair, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void SelectLoadedPair_DifferentDirectories_ReturnsInvalid()
        {
            var pair = CompatiblePair(compilerDirectory: PairDirectory("other"));

            var result = RoslynLoader.SelectLoadedPair(pair, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void SelectLoadedPair_EmptyMvid_ReturnsInvalid()
        {
            var pair = CompatiblePair(compilerMvid: Guid.Empty);

            var result = RoslynLoader.SelectLoadedPair(pair, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void SelectLoadedPair_MissingLocation_ReturnsInvalid()
        {
            var pair = CompatiblePair();
            pair[1] = new RoslynAssemblySnapshot(
                pair[1].Assembly, pair[1].Identity, pair[1].References,
                null, pair[1].Mvid, false);

            var result = RoslynLoader.SelectLoadedPair(pair, out _, out _);

            Assert.That(result, Is.EqualTo(RoslynPairSelection.Invalid));
        }

        [Test]
        public void EnsureRoslyn_LoadedPair_AdoptsSameObjectsWithoutFallback()
        {
            var pair = CompatiblePair();
            var fallbackCalls = 0;
            var probeCalls = 0;
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => pair,
                Fallback = () =>
                {
                    Interlocked.Increment(ref fallbackCalls);
                    return PairDirectory("unused");
                },
                Probe = (_, __) =>
                {
                    Interlocked.Increment(ref probeCalls);
                    return true;
                }
            };

            var first = RoslynLoader.EnsureRoslyn();
            var second = RoslynLoader.EnsureRoslyn();

            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(fallbackCalls, Is.Zero);
            Assert.That(probeCalls, Is.EqualTo(1));
            Assert.That(RoslynLoader.RoslynCore, Is.SameAs(CoreToken));
            Assert.That(RoslynLoader.RoslynCompiler, Is.SameAs(CompilerToken));
        }

        [Test]
        public void EnsureRoslyn_AbiProbeFails_PublishesNeitherAssembly()
        {
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => CompatiblePair(),
                Probe = (_, __) => false
            };

            var result = RoslynLoader.EnsureRoslyn();

            Assert.That(result, Is.False);
            Assert.That(RoslynLoader.RoslynCore, Is.Null);
            Assert.That(RoslynLoader.RoslynCompiler, Is.Null);
        }

        [Test]
        public void EnsureRoslyn_PartialPair_DoesNotFallback()
        {
            var pair = CompatiblePair();
            var fallbackCalls = 0;
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => new[] { pair[0] },
                Fallback = () =>
                {
                    Interlocked.Increment(ref fallbackCalls);
                    return PairDirectory("unused");
                }
            };

            var result = RoslynLoader.EnsureRoslyn();

            Assert.That(result, Is.False);
            Assert.That(fallbackCalls, Is.Zero);
            Assert.That(RoslynLoader.RoslynCore, Is.Null);
            Assert.That(RoslynLoader.RoslynCompiler, Is.Null);
        }

        [Test]
        public void EnsureRoslyn_CachedPairBecomesAmbiguous_ClearsPublishedPair()
        {
            var pair = CompatiblePair();
            IReadOnlyList<RoslynAssemblySnapshot> current = pair;
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => current,
                Probe = (_, __) => true
            };
            Assert.That(RoslynLoader.EnsureRoslyn(), Is.True);
            current = new[] { pair[0], pair[0], pair[1] };

            var result = RoslynLoader.EnsureRoslyn();

            Assert.That(result, Is.False);
            Assert.That(RoslynLoader.RoslynCore, Is.Null);
            Assert.That(RoslynLoader.RoslynCompiler, Is.Null);
        }

    }

    internal static class RoslynLoaderTestData
    {
        internal const string Token = "31bf3856ad364e35";
        internal static readonly Assembly CoreToken = typeof(object).Assembly;
        internal static readonly Assembly CompilerToken = typeof(RoslynLoaderTests).Assembly;

        internal static RoslynAssemblySnapshot[] CompatiblePair(
            Version coreVersion = null,
            Version compilerVersion = null,
            Version referenceVersion = null,
            string compilerToken = Token,
            string compilerCulture = "neutral",
            string pairDirectory = null,
            string compilerDirectory = null,
            Guid? compilerMvid = null)
        {
            coreVersion = coreVersion ?? new Version(4, 6, 0, 0);
            compilerVersion = compilerVersion ?? coreVersion;
            referenceVersion = referenceVersion ?? coreVersion;
            var coreIdentity = Identity(
                "Microsoft.CodeAnalysis", coreVersion, "neutral", Token);
            var compilerIdentity = Identity(
                "Microsoft.CodeAnalysis.CSharp", compilerVersion,
                compilerCulture, compilerToken);
            var referencedCore = Identity(
                "Microsoft.CodeAnalysis", referenceVersion, "neutral", Token);
            var coreDirectory = pairDirectory ?? PairDirectory("canonical");
            compilerDirectory = compilerDirectory ?? coreDirectory;
            return new[]
            {
                new RoslynAssemblySnapshot(
                    CoreToken,
                    coreIdentity,
                    Array.Empty<AssemblyName>(),
                    Path.Combine(coreDirectory, "Microsoft.CodeAnalysis.dll"),
                    new Guid("11111111-1111-1111-1111-111111111111"),
                    true),
                new RoslynAssemblySnapshot(
                    CompilerToken,
                    compilerIdentity,
                    new[] { referencedCore },
                    Path.Combine(compilerDirectory, "Microsoft.CodeAnalysis.CSharp.dll"),
                    compilerMvid ?? new Guid("22222222-2222-2222-2222-222222222222"),
                    true)
            };
        }

        private static AssemblyName Identity(
            string name, Version version, string culture, string token) =>
            new AssemblyName(
                name + ", Version=" + version + ", Culture=" + culture +
                ", PublicKeyToken=" + token);

        internal static string PairDirectory(string suffix) =>
            Path.Combine(Path.GetTempPath(), "unity-mcp-roslyn-loader", suffix);
    }
}
