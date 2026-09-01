// P2-04: thin construction/wiring tests for MutationModeToggle. Exercises the
// real SourcePatchHost/SourcePatchModePolicy statics (same pattern as
// SourcePatchMutationModeTests.cs) — no mock/interface needed, ApplyIntent is
// a 2-line pass-through and Build() only needs to prove initial-paint wiring,
// not re-derive the mapping table (that belongs to MutationModeToggleStateTests).
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class MutationModeToggleTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeProvider : ISourcePatchProvider
        {
            public SourcePatchApplyOutcome Apply(SourcePatchRequest request) => SourcePatchApplyOutcome.Applied;
        }

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(SourcePatchHost.ResetForTests);
            RegisterCleanup(SourcePatchProviderSlot.ResetForTests);
            SourcePatchHost.ResetForTests();
            SourcePatchProviderSlot.ResetForTests();
            SyncHelper.OverrideOpsForTest(new MockSyncOps());
        }

        private static void RegisterFakeProvider() =>
            SourcePatchProviderSlot.Register("fake-mutation-toggle-tests", new FakeProvider());

        [Test]
        public void Build_ReturnsElementContainingToggleLabeledMutationMode()
        {
            var element = MutationModeToggle.Build();

            var toggle = element.Q<Toggle>();
            Assert.IsNotNull(toggle, "Build() must contain a Toggle");
            Assert.AreEqual("Mutation Mode (experimental)", toggle.label);
        }

        [Test]
        public void Build_ProviderAbsent_InitialToggleIsUncheckedAndDisabled()
        {
            var element = MutationModeToggle.Build();

            var toggle = element.Q<Toggle>();
            Assert.IsFalse(toggle.value);
            Assert.IsFalse(toggle.enabledSelf);
        }

        [Test]
        public void Build_ProviderPresentOnReady_InitialToggleIsCheckedAndEnabled()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;

            var element = MutationModeToggle.Build();

            var toggle = element.Q<Toggle>();
            Assert.IsTrue(toggle.value);
            Assert.IsTrue(toggle.enabledSelf);
        }

        [Test]
        public void Build_Recovery_WarningLabelVisible()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.Recovery;

            var element = MutationModeToggle.Build();

            var warning = element.Q<Label>(className: "biome-status");
            Assert.IsNotNull(warning, "warning label must carry BiomeUI.StatusLabel's class");
            Assert.IsTrue(warning.visible);
        }

        [Test]
        public void Build_Off_WarningLabelHidden()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.Off;

            var element = MutationModeToggle.Build();

            var warning = element.Q<Label>(className: "biome-status");
            Assert.IsNotNull(warning);
            Assert.IsFalse(warning.visible);
        }

        [Test]
        public void ApplyIntent_EnableTrue_ProviderPresentAndOff_TransitionsHostToOnReady()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.Off;

            MutationModeToggle.ApplyIntent(true);

            Assert.AreEqual(SourcePatchState.OnReady, SourcePatchHost.CurrentState);
        }

        [Test]
        public void ApplyIntent_EnableFalse_FromOnReady_TransitionsToDisablingAndSyncsExactlyOnce()
        {
            RegisterFakeProvider();
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            var epochBefore = SyncHelper.CurrentEpoch;

            MutationModeToggle.ApplyIntent(false);

            Assert.AreEqual(SourcePatchState.Disabling, SourcePatchHost.CurrentState);
            Assert.AreEqual(epochBefore + 1, SyncHelper.CurrentEpoch, "exactly one sync");
        }

        [Test]
        public void ApplyIntent_ProviderAbsent_SwallowsExceptionAndLeavesStateUnchanged()
        {
            SourcePatchHost.CurrentState = SourcePatchState.Unavailable;

            Assert.DoesNotThrow(() => MutationModeToggle.ApplyIntent(true));

            Assert.AreEqual(SourcePatchState.Unavailable, SourcePatchHost.CurrentState);
        }
    }
}
