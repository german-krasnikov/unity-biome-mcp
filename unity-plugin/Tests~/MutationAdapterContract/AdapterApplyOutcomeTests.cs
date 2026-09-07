// Offline compile-contract tests for the pinned Source Patch FSR adapter
// (adapter-cache/*.cs) against the real SourcePatch seam (Seam.csproj,
// ProjectReference). Goes RED at compile time on any seam member
// rename/removal/signature change; goes RED at runtime on any adapter
// behavior drift from the outcome table in
// Plans/MUTATION-REGRESSION-MODULE.md Phase C1.
using System;
using System.Reflection;
using Biome.SourcePatch.FSRAdapter;
using FastScriptReload.Editor.Compilation;
using FastScriptReload.Runtime;
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace AdapterContract.Tests
{
    [TestFixture]
    public sealed class AdapterApplyOutcomeTests
    {
        // Compiled directly into this assembly (see adapter-cache class
        // under test): reflection target for BiomeFsrSourcePatchProvider's
        // ProjectTypeCache/MethodInfo lookup.
        public class Foo
        {
            public void Bar() { }
        }

        private const string BeforeAdmitted = "class Foo { void Bar() { } }";
        private const string AfterAdmitted = "class Foo { void Bar() { int changed = 1; } }";

        [SetUp]
        public void SetUp()
        {
            SourcePatchProviderSlot.ResetForTests();
            ProjectTypeCache.AllTypesInNonDynamicGeneratedAssemblies.Clear();
            ProjectTypeCache.AllTypesInNonDynamicGeneratedAssemblies["Foo"] = typeof(Foo);
            DynamicAssemblyCompiler.CompileHook = null;
            AssemblyChangesLoader.Instance.DetourHook = null;
        }

        [TearDown]
        public void TearDown()
        {
            SourcePatchProviderSlot.ResetForTests();
            ProjectTypeCache.AllTypesInNonDynamicGeneratedAssemblies.Clear();
            DynamicAssemblyCompiler.CompileHook = null;
            AssemblyChangesLoader.Instance.DetourHook = null;
        }

        private static SourcePatchRequest MakeRequest(string beforeSource, string afterSource)
        {
            var ok = SourcePatchRequest.TryCreate(
                "Assets/Foo.cs",
                System.Text.Encoding.UTF8.GetBytes(beforeSource),
                System.Text.Encoding.UTF8.GetBytes(afterSource),
                out var request);
            Assert.That(ok, Is.True, "SourcePatchRequest.TryCreate rejected a well-formed request.");
            return request;
        }

        [Test]
        public void Register_TryGet_RoundTrip()
        {
            var provider = new BiomeFsrSourcePatchProvider();

            var result = SourcePatchProviderSlot.Register("test-provider", provider);

            Assert.That(result, Is.EqualTo(SourcePatchRegistrationResult.Registered));
            Assert.That(SourcePatchProviderSlot.TryGet(out var retrieved), Is.True);
            Assert.That(retrieved, Is.SameAs(provider));
        }

        [Test]
        public void Apply_ClassifierReject_ReturnsRejected()
        {
            // Syntax error on both sides -- BiomeBodyOnlyMethodClassifier.Classify
            // rejects before the provider ever consults ProjectTypeCache or compiles.
            var request = MakeRequest("not valid c#{{{", "still not valid c#{{{");
            var provider = new BiomeFsrSourcePatchProvider();

            var outcome = provider.Apply(request);

            Assert.That(outcome, Is.EqualTo(SourcePatchApplyOutcome.Rejected));
        }

        [Test]
        public void Apply_CompileThrows_ReturnsRejected()
        {
            DynamicAssemblyCompiler.CompileHook = (_, _) => throw new InvalidOperationException("compile boom");
            var request = MakeRequest(BeforeAdmitted, AfterAdmitted);
            var provider = new BiomeFsrSourcePatchProvider();

            var outcome = provider.Apply(request);

            Assert.That(outcome, Is.EqualTo(SourcePatchApplyOutcome.Rejected));
        }

        [Test]
        public void Apply_DetourThrows_ReturnsUncertain()
        {
            DynamicAssemblyCompiler.CompileHook = (_, _) => new CompileResult
            {
                IsError = false,
                CompiledAssembly = typeof(Foo).Assembly,
            };
            AssemblyChangesLoader.Instance.DetourHook =
                (_, _) => throw new InvalidOperationException("detour boom");
            var request = MakeRequest(BeforeAdmitted, AfterAdmitted);
            var provider = new BiomeFsrSourcePatchProvider();

            var outcome = provider.Apply(request);

            Assert.That(outcome, Is.EqualTo(SourcePatchApplyOutcome.Uncertain));
        }

        [Test]
        public void Apply_DetourNotApplied_ReturnsUncertain()
        {
            DynamicAssemblyCompiler.CompileHook = (_, _) => new CompileResult
            {
                IsError = false,
                CompiledAssembly = typeof(Foo).Assembly,
            };
            AssemblyChangesLoader.Instance.DetourHook =
                (_, _) => new ExactTargetPatchResult { Applied = false };
            var request = MakeRequest(BeforeAdmitted, AfterAdmitted);
            var provider = new BiomeFsrSourcePatchProvider();

            var outcome = provider.Apply(request);

            Assert.That(outcome, Is.EqualTo(SourcePatchApplyOutcome.Uncertain));
        }

        [Test]
        public void Apply_DetourApplied_ReturnsApplied()
        {
            DynamicAssemblyCompiler.CompileHook = (_, _) => new CompileResult
            {
                IsError = false,
                CompiledAssembly = typeof(Foo).Assembly,
            };
            AssemblyChangesLoader.Instance.DetourHook =
                (_, _) => new ExactTargetPatchResult { Applied = true };
            var request = MakeRequest(BeforeAdmitted, AfterAdmitted);
            var provider = new BiomeFsrSourcePatchProvider();

            var outcome = provider.Apply(request);

            Assert.That(outcome, Is.EqualTo(SourcePatchApplyOutcome.Applied));
        }
    }
}
