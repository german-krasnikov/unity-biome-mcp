using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    internal sealed class ReloadAdmissionTests
#if UNITY_EDITOR
        : UnityMCP.Editor.Testing.UnityMcpTestBase
#endif
    {
        [Test]
        public void RejectedModeCannotReceiveAcceptedAckOrDispatch()
        {
            var gate = new ReloadMainThreadDispatchGate();
            var calls = new List<string>();
            if (gate.TryStart(() => "active-mode", out var reason))
                gate.ExecuteStarted(() => calls.Add("accepted"), () => calls.Add("effect"));
            else calls.Add("blocked:" + reason);
            Assert.That(calls, Is.EqualTo(new[] { "blocked:active-mode" }));
        }

        [Test]
        public void TimeoutDuringAdmissionCannotReceiveAcceptedAckOrDispatch()
        {
            var gate = new ReloadMainThreadDispatchGate();
            var admitted = gate.TryStart(() => { Assert.That(gate.TryAbandon(), Is.True); return null; }, out _);
            Assert.That(admitted, Is.False);
            Assert.That(gate.HasStarted, Is.False);
        }

        [TestCase("force_refresh", false)]
        [TestCase("recompile", false)]
        [TestCase("force_refresh", true)]
        public void MissingOrThrowingAdmissionPerformsZeroEffects(string command, bool throwing)
        {
            WithPorts(() => throwing ? throw new InvalidOperationException("owner missing") : null,
                effects =>
                {
                    Assert.That(ReloadCommands.Dispatch(command), Is.EqualTo("blocked|reason=reload_admission_unavailable"));
                    Assert.That(effects(), Is.Zero);
                    Assert.That(ReloadCommands.Dispatch("ping"), Is.EqualTo("pong"));
                });
        }

        [TestCase("force_refresh")]
        [TestCase("recompile")]
        public void KnownModeBlockIsNotBypassedByMiniDispatch(string command)
        {
            WithPorts(() => owned => "active-mode", effects =>
            {
                Assert.That(ReloadCommands.Dispatch(command), Is.EqualTo("blocked|reason=active-mode"));
                Assert.That(effects(), Is.Zero);
            });
        }

        [Test]
        public void ExplicitClearAdmissionPerformsEachEffectOnce()
        {
            WithPorts(() => owned => owned ? "invalid-owned-override" : null, effects =>
            {
                Assert.That(ReloadCommands.Dispatch("force_refresh"), Is.EqualTo("force_refresh triggered"));
                Assert.That(effects(), Is.EqualTo(2));
                Assert.That(ReloadCommands.Dispatch("recompile"), Is.EqualTo("recompile triggered"));
                Assert.That(effects(), Is.EqualTo(3));
            });
        }

        private static void WithPorts(Func<Func<bool, string>> admission, Action<Func<int>> test)
        {
            var oldAdmission = ReloadCommands.AdmissionResolver;
            var oldRefresh = ReloadCommands.RefreshForReload;
            var oldCompile = ReloadCommands.CompileScripts;
            var oldOnly = ReloadCommands.RefreshOnly;
            var count = 0;
            try
            {
                ReloadCommands.AdmissionResolver = admission;
                ReloadCommands.RefreshForReload = () => count++;
                ReloadCommands.CompileScripts = () => count++;
                ReloadCommands.RefreshOnly = () => count++;
                test(() => count);
            }
            finally
            {
                ReloadCommands.AdmissionResolver = oldAdmission;
                ReloadCommands.RefreshForReload = oldRefresh;
                ReloadCommands.CompileScripts = oldCompile;
                ReloadCommands.RefreshOnly = oldOnly;
            }
        }
    }
}
