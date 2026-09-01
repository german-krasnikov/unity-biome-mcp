// P2-04: pure mapping-table tests for MutationModeToggleState.Resolve — the
// view-model behind the MCP Settings Hub "Mutation Mode (experimental)"
// checkbox. Zero Unity statics touched; every case corresponds to one row
// (or precedence guard) in Plans/mutation-mode-hub-toggle.md's state table.
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class MutationModeToggleStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Resolve_ProviderAbsent_Unavailable_ReturnsDisabledWithInstallHint()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Unavailable, intentOn: false, providerPresent: false, isPlaying: false);

            Assert.IsFalse(ui.Checked);
            Assert.IsFalse(ui.Enabled);
            Assert.AreEqual(MutationModeToggleState.ProviderAbsentTooltip, ui.Tooltip);
            Assert.IsFalse(ui.ShowRecoveryWarning);
        }

        [Test]
        public void Resolve_ProviderAbsent_OffWithAbsentProvider_ReturnsDisabledWithInstallHint()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Off, intentOn: false, providerPresent: false, isPlaying: false);

            Assert.IsFalse(ui.Checked);
            Assert.IsFalse(ui.Enabled);
            Assert.AreEqual(MutationModeToggleState.ProviderAbsentTooltip, ui.Tooltip);
        }

        [Test]
        public void Resolve_Off_ProviderPresentNotPlaying_ReturnsUncheckedEnabled()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Off, intentOn: false, providerPresent: true, isPlaying: false);

            Assert.IsFalse(ui.Checked);
            Assert.IsTrue(ui.Enabled);
            Assert.AreEqual(MutationModeToggleState.OffTooltip, ui.Tooltip);
        }

        [Test]
        public void Resolve_OnReady_ProviderPresentNotPlaying_ReturnsCheckedEnabled_TooltipMentionsReload()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.OnReady, intentOn: true, providerPresent: true, isPlaying: false);

            Assert.IsTrue(ui.Checked);
            Assert.IsTrue(ui.Enabled);
            StringAssert.Contains("one script reload", ui.Tooltip);
        }

        [Test]
        public void Resolve_Busy_ReturnsCheckedDisabled()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Busy, intentOn: true, providerPresent: true, isPlaying: false);

            Assert.IsTrue(ui.Checked);
            Assert.IsFalse(ui.Enabled);
        }

        [Test]
        public void Resolve_Disabling_ReturnsUncheckedDisabled()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Disabling, intentOn: false, providerPresent: true, isPlaying: false);

            Assert.IsFalse(ui.Checked);
            Assert.IsFalse(ui.Enabled);
        }

        [Test]
        public void Resolve_Recovery_ReturnsDisabledWithRecoveryWarningTrue()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Recovery, intentOn: false, providerPresent: true, isPlaying: false);

            Assert.IsFalse(ui.Enabled);
            Assert.IsTrue(ui.ShowRecoveryWarning);
        }

        [Test]
        public void Resolve_PlayMode_OtherwiseOff_ReturnsDisabledWithPlayModeHintAndUnchecked()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Off, intentOn: false, providerPresent: true, isPlaying: true);

            Assert.IsFalse(ui.Checked);
            Assert.IsFalse(ui.Enabled);
            Assert.AreEqual(MutationModeToggleState.PlayModeTooltip, ui.Tooltip);
        }

        [Test]
        public void Resolve_PlayMode_OtherwiseOnReady_ReturnsDisabledButStillChecked()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.OnReady, intentOn: true, providerPresent: true, isPlaying: true);

            Assert.IsTrue(ui.Checked, "Play Mode must never hide a true ON state");
            Assert.IsFalse(ui.Enabled);
        }

        [Test]
        public void Resolve_RecoveryTakesPrecedenceOverPlayMode()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Recovery, intentOn: false, providerPresent: true, isPlaying: true);

            Assert.AreEqual(MutationModeToggleState.RecoveryTooltip, ui.Tooltip);
            Assert.IsTrue(ui.ShowRecoveryWarning);
        }

        [Test]
        public void Resolve_ProviderAbsentTakesPrecedenceOverRecovery()
        {
            var ui = MutationModeToggleState.Resolve(
                SourcePatchState.Recovery, intentOn: false, providerPresent: false, isPlaying: false);

            Assert.AreEqual(MutationModeToggleState.ProviderAbsentTooltip, ui.Tooltip);
            Assert.IsFalse(ui.ShowRecoveryWarning, "row 1 (provider absent) must win over row 2 (Recovery)");
        }

        [Test]
        public void Resolve_NeverThrows_ForEveryStateProviderIntentPlayingCombination()
        {
            var states = (SourcePatchState[])System.Enum.GetValues(typeof(SourcePatchState));
            foreach (var state in states)
            foreach (var intentOn in new[] { false, true })
            foreach (var providerPresent in new[] { false, true })
            foreach (var isPlaying in new[] { false, true })
            {
                Assert.DoesNotThrow(
                    () => MutationModeToggleState.Resolve(state, intentOn, providerPresent, isPlaying));
            }
        }
    }
}
