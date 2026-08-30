using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using static UnityMCP.Editor.Tests.RoslynLoaderTestData;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class RoslynLoaderFallbackTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUpRoslynLoaderIsolation()
        {
            RegisterCleanup(RoslynLoader.BeginTestIsolation());
            RoslynLoader.ResetForTests();
        }

        [Test]
        public void SelectFallbackDirectory_FirstPairIncomplete_SelectsCompletePair()
        {
            var first = PairDirectory("dotnet");
            var second = PairDirectory("legacy");
            var existing = new HashSet<string>
            {
                Path.Combine(first, "Microsoft.CodeAnalysis.dll"),
                Path.Combine(second, "Microsoft.CodeAnalysis.dll"),
                Path.Combine(second, "Microsoft.CodeAnalysis.CSharp.dll")
            };

            var result = RoslynLoader.SelectFallbackDirectory(
                new[] { first, second }, _ => true, existing.Contains);

            Assert.That(result, Is.EqualTo(second));
        }

        [Test]
        public void EnsureRoslyn_NoRoots_LoadsExactFallbackPair()
        {
            var fallback = PairDirectory("fallback");
            var pair = CompatiblePair(pairDirectory: fallback);
            var loadCount = 0;
            var loadedPaths = new List<string>();
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => Volatile.Read(ref loadCount) < 2
                    ? Array.Empty<RoslynAssemblySnapshot>() : pair,
                Fallback = () => fallback,
                Load = path =>
                {
                    loadedPaths.Add(path);
                    Interlocked.Increment(ref loadCount);
                    return path.EndsWith("CSharp.dll", StringComparison.Ordinal)
                        ? CompilerToken : CoreToken;
                },
                Probe = (_, __) => true
            };

            Assert.That(RoslynLoader.EnsureRoslyn(), Is.True);
            Assert.That(loadedPaths.Select(Path.GetFileName), Is.EqualTo(new[]
            {
                "Microsoft.CodeAnalysis.dll",
                "Microsoft.CodeAnalysis.CSharp.dll"
            }));
            Assert.That(RoslynLoader.RoslynCore, Is.SameAs(CoreToken));
            Assert.That(RoslynLoader.RoslynCompiler, Is.SameAs(CompilerToken));
        }

        [Test]
        public void EnsureRoslyn_SecondFallbackLoadThrows_NextCallDoesNotLoadAgain()
        {
            var pair = CompatiblePair(pairDirectory: PairDirectory("fallback"));
            var loadCount = 0;
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => Volatile.Read(ref loadCount) == 0
                    ? Array.Empty<RoslynAssemblySnapshot>() : new[] { pair[0] },
                Fallback = () => PairDirectory("fallback"),
                Load = _ =>
                {
                    if (Interlocked.Increment(ref loadCount) == 2)
                        throw new InvalidOperationException("compiler load failed");
                    return CoreToken;
                }
            };

            Assert.That(RoslynLoader.EnsureRoslyn(), Is.False);
            Assert.That(RoslynLoader.EnsureRoslyn(), Is.False);
            Assert.That(loadCount, Is.EqualTo(2));
            Assert.That(RoslynLoader.RoslynCore, Is.Null);
            Assert.That(RoslynLoader.RoslynCompiler, Is.Null);
        }

        [Test]
        public void EnsureRoslyn_FallbackReturnRedirect_FailsClosed()
        {
            var fallback = PairDirectory("fallback");
            var pair = CompatiblePair(pairDirectory: fallback);
            var loadCount = 0;
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => loadCount < 2
                    ? Array.Empty<RoslynAssemblySnapshot>() : pair,
                Fallback = () => fallback,
                Load = _ => { loadCount++; return CoreToken; },
                Probe = (_, __) => true
            };

            Assert.That(RoslynLoader.EnsureRoslyn(), Is.False);
            Assert.That(RoslynLoader.RoslynCore, Is.Null);
            Assert.That(RoslynLoader.RoslynCompiler, Is.Null);
        }

        [Test]
        public void EnsureRoslyn_FallbackPathRedirect_FailsClosed()
        {
            var fallback = PairDirectory("fallback");
            var pair = CompatiblePair(pairDirectory: PairDirectory("redirect"));
            var loadCount = 0;
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => loadCount < 2
                    ? Array.Empty<RoslynAssemblySnapshot>() : pair,
                Fallback = () => fallback,
                Load = path =>
                {
                    loadCount++;
                    return path.EndsWith("CSharp.dll", StringComparison.Ordinal)
                        ? CompilerToken : CoreToken;
                },
                Probe = (_, __) => true
            };

            Assert.That(RoslynLoader.EnsureRoslyn(), Is.False);
            Assert.That(RoslynLoader.RoslynCore, Is.Null);
            Assert.That(RoslynLoader.RoslynCompiler, Is.Null);
        }

        [Test]
        public void EnsureRoslyn_FallbackFileNameRedirect_FailsClosed()
        {
            var fallback = PairDirectory("fallback");
            var pair = CompatiblePair(pairDirectory: fallback);
            pair[1] = new RoslynAssemblySnapshot(
                pair[1].Assembly, pair[1].Identity, pair[1].References,
                Path.Combine(fallback, "Redirected.dll"), pair[1].Mvid, true);
            var loadCount = 0;
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => loadCount < 2
                    ? Array.Empty<RoslynAssemblySnapshot>() : pair,
                Fallback = () => fallback,
                Load = path =>
                {
                    loadCount++;
                    return path.EndsWith("CSharp.dll", StringComparison.Ordinal)
                        ? CompilerToken : CoreToken;
                },
                Probe = (_, __) => true
            };

            Assert.That(RoslynLoader.EnsureRoslyn(), Is.False);
            Assert.That(RoslynLoader.RoslynCore, Is.Null);
            Assert.That(RoslynLoader.RoslynCompiler, Is.Null);
        }

        [Test]
        public void EnsureRoslyn_ConcurrentCalls_LoadFallbackPairOnce()
        {
            var fallback = PairDirectory("fallback");
            var pair = CompatiblePair(pairDirectory: fallback);
            var loadCount = 0;
            RoslynLoader.TestOpsOverride = new RoslynLoader.TestOps
            {
                Snapshots = () => Volatile.Read(ref loadCount) < 2
                    ? Array.Empty<RoslynAssemblySnapshot>() : pair,
                Fallback = () => fallback,
                Load = path =>
                {
                    Interlocked.Increment(ref loadCount);
                    return path.EndsWith("CSharp.dll", StringComparison.Ordinal)
                        ? CompilerToken : CoreToken;
                },
                Probe = (_, __) => true
            };
            var results = new bool[8];

            Parallel.For(0, results.Length,
                index => results[index] = RoslynLoader.EnsureRoslyn());

            Assert.That(results.All(result => result), Is.True);
            Assert.That(loadCount, Is.EqualTo(2));
        }
    }
}
