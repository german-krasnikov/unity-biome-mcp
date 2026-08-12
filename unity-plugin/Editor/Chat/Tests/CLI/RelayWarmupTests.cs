// TDD tests for RelayWarmup — the silent background uvx pre-warm (ARCH-coldstart-ux.md).
//
// What's covered: the skip-condition decision logic (ShouldWarm) and the pure argv-building
// helper (BuildWarmupArgv), plus TryWarm's main-thread orchestration up to the Task.Run hop —
// all via injected seams (CommandResolverOverride / WarmKeyGetterOverride / WarmKeySetterOverride
// / SkipForTests / OnWarmStarted), mirroring the InstallSourceDetector.SetSourceForTest and
// ChatBinaryResolver.WhichOverride seam patterns already used elsewhere in this codebase.
//
// What is NOT covered (integration-only, needs a real Unity + real uvx binary):
//   - The actual Process.Start/WaitForExit(90s)/ExitCode branch inside RunWarmup().
//   - EditorPrefs.SetBool actually persisting across a real Unity restart.
//   - MainThreadDispatcher.Enqueue actually draining on EditorApplication.update in a live editor.
using System;
using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayWarmupTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RelayWarmup.ResetForTests();
            InstallSourceDetector.ClearTestOverride();
        }

        [TearDown]
        public void TearDown()
        {
            RelayWarmup.ResetForTests();
            InstallSourceDetector.ClearTestOverride();
        }

        // ── ShouldWarm — skip conditions ──────────────────────────────────────

        [Test]
        public void ShouldWarm_LocalInstall_ReturnsFalse()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            RelayWarmup.WarmKeyGetterOverride = () => false;

            Assert.IsFalse(RelayWarmup.ShouldWarm());
        }

        [Test]
        public void ShouldWarm_WarmKeyAlreadySet_ReturnsFalse()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            RelayWarmup.WarmKeyGetterOverride = () => true;

            Assert.IsFalse(RelayWarmup.ShouldWarm());
        }

        [Test]
        public void ShouldWarm_NotLocalAndNotWarmed_ReturnsTrue()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            RelayWarmup.WarmKeyGetterOverride = () => false;

            Assert.IsTrue(RelayWarmup.ShouldWarm());
        }

        [Test]
        public void ShouldWarm_SkipForTestsTrue_ReturnsFalseEvenWhenOtherwiseEligible()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            RelayWarmup.WarmKeyGetterOverride = () => false;
            RelayWarmup.SkipForTests = () => true;

            Assert.IsFalse(RelayWarmup.ShouldWarm());
        }

        [Test]
        public void ShouldWarm_RegistrySource_ReturnsTrue()
        {
            // Only Local is special-cased — Registry/Embedded/Unknown all warm like Git.
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Registry);
            RelayWarmup.WarmKeyGetterOverride = () => false;

            Assert.IsTrue(RelayWarmup.ShouldWarm());
        }

        // ── BuildWarmupArgv — pure ────────────────────────────────────────────

        [Test]
        public void BuildWarmupArgv_AppendsVersionFlag()
        {
            var argv = new[] { "--from", "git+https://example.com/repo", "unity-biome-mcp-relay" };
            var result = RelayWarmup.BuildWarmupArgv(argv);

            CollectionAssert.AreEqual(
                new[] { "--from", "git+https://example.com/repo", "unity-biome-mcp-relay", "--version" },
                result);
        }

        [Test]
        public void BuildWarmupArgv_NullArgv_ReturnsVersionOnly()
        {
            var result = RelayWarmup.BuildWarmupArgv(null);
            CollectionAssert.AreEqual(new[] { "--version" }, result);
        }

        [Test]
        public void BuildWarmupArgv_EmptyArgv_ReturnsVersionOnly()
        {
            var result = RelayWarmup.BuildWarmupArgv(Array.Empty<string>());
            CollectionAssert.AreEqual(new[] { "--version" }, result);
        }

        // ── TryWarm — main-thread orchestration up to the Task.Run hop ────────

        [Test]
        public void TryWarm_ShouldWarmFalse_NeverCallsCommandResolver()
        {
            RelayWarmup.SkipForTests = () => true;
            var resolverCalled = false;
            RelayWarmup.CommandResolverOverride = () => { resolverCalled = true; return ("uvx", new[] { "x" }); };

            RelayWarmup.TryWarm();

            Assert.IsFalse(resolverCalled, "CommandResolver must not run when ShouldWarm() is false");
        }

        [Test]
        public void TryWarm_ShouldWarmFalse_NeverInvokesOnWarmStarted()
        {
            RelayWarmup.SkipForTests = () => true;
            var started = false;
            RelayWarmup.OnWarmStarted = () => started = true;

            RelayWarmup.TryWarm();

            Assert.IsFalse(started);
        }

        [Test]
        public void TryWarm_UvxNotFound_DoesNotInvokeOnWarmStarted()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            RelayWarmup.WarmKeyGetterOverride = () => false;
            RelayWarmup.CommandResolverOverride = () => (null, null);
            var started = false;
            RelayWarmup.OnWarmStarted = () => started = true;

            RelayWarmup.TryWarm();

            Assert.IsFalse(started, "uvx missing — the real spawn path reports this error later, warmup stays silent");
        }

        [Test]
        public void TryWarm_Eligible_ResolvesCommandAndInvokesOnWarmStarted()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            RelayWarmup.WarmKeyGetterOverride = () => false;
            var resolverCalled = false;
            RelayWarmup.CommandResolverOverride = () =>
            {
                resolverCalled = true;
                return ("/definitely/not/a/real/binary", new[] { "--from", "url", "unity-biome-mcp-relay" });
            };
            var started = false;
            RelayWarmup.OnWarmStarted = () => started = true;

            RelayWarmup.TryWarm();

            Assert.IsTrue(resolverCalled);
            Assert.IsTrue(started);
        }

        // ── T1.5: batch mode (CI) must not start warmup process ──────────────

        [Test]
        public void ShouldWarm_BatchMode_ReturnsFalse()
        {
            // T1.5 fix: Application.isBatchMode check prevents uvx process on CI.
            // IsBatchModeGetter seam mirrors WarmKeyGetterOverride pattern.
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            RelayWarmup.WarmKeyGetterOverride = () => false;  // would otherwise return true
            RelayWarmup.IsBatchModeGetter = () => true;       // simulate CI -batchmode

            Assert.IsFalse(RelayWarmup.ShouldWarm(),
                "ShouldWarm must return false in batch mode — spawning uvx in CI is wasteful and wrong");
        }

        [Test]
        public void ShouldWarm_NotBatchMode_StillEligible()
        {
            // Verify the batch mode guard does not fire when running interactively.
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            RelayWarmup.WarmKeyGetterOverride = () => false;
            RelayWarmup.IsBatchModeGetter = () => false;      // normal editor session

            Assert.IsTrue(RelayWarmup.ShouldWarm(),
                "ShouldWarm must remain true when not in batch mode (git install, not warmed)");
        }
    }
}
