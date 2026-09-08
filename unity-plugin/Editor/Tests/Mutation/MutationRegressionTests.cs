// A4 placeholder: this fixture only compiles when UNITYMCP_HAS_FSR_PROVIDER
// is defined (unity-plugin/Editor/Tests/Mutation/UnityMCP.Editor.Tests.Mutation
// .asmdef's versionDefines binds that to com.handzlikchris.fastscriptreload
// being installed). No lane in this repository installs that package, so
// this assembly never compiles and this test never runs today -- proven by
// the ScriptAssemblies absence check in A4's verification.
//
// The single test below is not a tautology: it exercises the one public,
// provider-agnostic seam this repo owns (SourcePatchProviderSlot.TryGet,
// see unity-plugin/Editor/SourcePatch/SourcePatchProviderSlot.cs) and can
// only pass once a real external FSR adapter has registered a provider.
// B4 (Plans/MUTATION-REGRESSION-MODULE.md) replaces this placeholder with
// the full S2-S16 regression matrix once the disposable worker actually
// installs the provider package.
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests.Mutation
{
    [TestFixture]
    [Category(TestCategories.MutationLive)]
    [BiomeWorkerOnly("mutation regression requires the provider package")]
    public class MutationRegressionTests : UnityMcpTestBase
    {
        [Test]
        public void ProviderSlot_ReportsRegisteredProvider_WhenPackageInstalled()
        {
            var registered = SourcePatchProviderSlot.TryGet(out var provider);

            Assert.IsTrue(registered,
                "This fixture only compiles with UNITYMCP_HAS_FSR_PROVIDER defined " +
                "(FSR provider package installed) -- by the time NUnit runs, the " +
                "adapter must already have registered into SourcePatchProviderSlot.");
            Assert.IsNotNull(provider);
        }
    }
}
