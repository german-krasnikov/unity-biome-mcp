// TDD: Phase D — WriteSessionGuard lifecycle, watchdog, and crash recovery.
// Tests use static delegate seams (same pattern as BatchDeferImportTests.cs).
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WriteSessionGuardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private int _startC, _stopC, _lockC, _unlockC, _disallowC, _allowC, _refreshC;
        private double _fakeTime;

        [SetUp]
        public void SetUp()
        {
            _startC = _stopC = _lockC = _unlockC = _disallowC = _allowC = _refreshC = 0;
            _fakeTime = 0;
            WriteSessionGuard._startEditing    = () => _startC++;
            WriteSessionGuard._stopEditing     = () => _stopC++;
            WriteSessionGuard._lockAssemblies  = () => _lockC++;
            WriteSessionGuard._unlockAssemblies = () => _unlockC++;
            WriteSessionGuard._disallowRefresh = () => _disallowC++;
            WriteSessionGuard._allowRefresh    = () => _allowC++;
            WriteSessionGuard._refresh         = () => _refreshC++;
            WriteSessionGuard._time            = () => _fakeTime;
            WriteSessionGuard.OverrideWatchdogSeconds(120.0);
            WriteSessionGuard.ResetForTest();
        }

        [TearDown]
        public void TearDown() => WriteSessionGuard.ResetForTest();

        // ── Subtask 17: core lifecycle ────────────────────────────────────────

        [Test]
        public void Start_WhenIdle_ReturnsStarted()
        {
            var result = WriteSessionGuard.Start();
            Assert.That(result, Does.Contain("write_session_started"));
            Assert.AreEqual(1, _startC,    "StartAssetEditing must fire once");
            Assert.AreEqual(1, _lockC,     "LockReloadAssemblies must fire once");
            Assert.AreEqual(1, _disallowC, "DisallowAutoRefresh must fire once");
            Assert.IsTrue(WriteSessionGuard.IsActive);
        }

        [Test]
        public void Start_WhenAlreadyActive_ReturnsError()
        {
            WriteSessionGuard.Start();
            var before = (_startC, _lockC, _disallowC);
            var result = WriteSessionGuard.Start();
            Assert.That(result, Does.Contain("already_active"));
            Assert.AreEqual(before, (_startC, _lockC, _disallowC), "Second Start must not acquire");
        }

        [Test]
        public void End_WhenActive_ReleasesAll()
        {
            WriteSessionGuard.Start();
            var result = WriteSessionGuard.End();
            Assert.That(result, Does.Contain("write_session_ended"));
            Assert.AreEqual(1, _stopC,    "StopAssetEditing must fire");
            Assert.AreEqual(1, _unlockC,  "UnlockReloadAssemblies must fire");
            Assert.AreEqual(1, _allowC,   "AllowAutoRefresh must fire");
            Assert.AreEqual(1, _refreshC, "Refresh must fire");
            Assert.IsFalse(WriteSessionGuard.IsActive);
        }

        [Test]
        public void End_WhenNotActive_ReturnsError()
        {
            var result = WriteSessionGuard.End();
            Assert.That(result, Does.Contain("not_active"));
            Assert.AreEqual(0, _stopC + _unlockC + _allowC + _refreshC, "No seam calls when inactive");
        }

        // ── Subtask 18: ForceRelease hardening ───────────────────────────────

        [Test]
        public void ForceRelease_StopEditing_CalledEvenIfUnlockThrows()
        {
            WriteSessionGuard.Start();
            WriteSessionGuard._unlockAssemblies = () => throw new Exception("unlock-boom");
            // ForceRelease may re-throw the unlock exception; swallow it.
            try { WriteSessionGuard.ForceRelease(); } catch { }
            Assert.AreEqual(1, _stopC,    "StopAssetEditing must run before unlock throws");
            Assert.AreEqual(1, _allowC,   "AllowAutoRefresh must still run in finally");
            Assert.AreEqual(1, _refreshC, "Refresh must still run in finally");
            Assert.IsFalse(WriteSessionGuard.IsActive);
        }

        [Test]
        public void ForceRelease_StopEditingThrows_UnlockStillCalled()
        {
            WriteSessionGuard.Start();
            WriteSessionGuard._stopEditing = () => throw new Exception("stop-boom");
            try { WriteSessionGuard.ForceRelease(); } catch { }
            Assert.AreEqual(1, _unlockC,  "UnlockReloadAssemblies must run in finally despite stop-boom");
            Assert.AreEqual(1, _allowC,   "AllowAutoRefresh must run in finally");
            Assert.AreEqual(1, _refreshC, "Refresh must run in finally");
            Assert.IsFalse(WriteSessionGuard.IsActive);
        }

        // ── Subtask 18: watchdog ─────────────────────────────────────────────

        [Test]
        public void Watchdog_FiresAfterTimeout_ReleasesAll()
        {
            WriteSessionGuard.Start();
            _fakeTime = 121.0; // past 120s timeout
            WriteSessionGuard.InvokeWatchdogTickForTest();
            Assert.AreEqual(1, _stopC,    "StopAssetEditing must fire on watchdog");
            Assert.AreEqual(1, _refreshC, "Refresh must fire on watchdog");
            Assert.IsFalse(WriteSessionGuard.IsActive);
        }

        [Test]
        public void Watchdog_NotFiredBeforeTimeout()
        {
            WriteSessionGuard.Start();
            _fakeTime = 60.0; // under 120s
            WriteSessionGuard.InvokeWatchdogTickForTest();
            Assert.IsTrue(WriteSessionGuard.IsActive,    "Session must still be active");
            Assert.AreEqual(0, _stopC, "StopAssetEditing must not fire before timeout");
        }

        // ── Subtask 18: Start() acquisition failure rollback ─────────────────

        [Test]
        public void Start_WhenStartEditingThrows_RollsBackAndReturnsError()
        {
            WriteSessionGuard._startEditing = () => throw new Exception("disk-full");
            var result = WriteSessionGuard.Start();
            Assert.That(result, Does.Contain("err:acquire_failed"), "Must return error on acquisition failure");
            Assert.That(result, Does.Contain("disk-full"), "Must include exception message");
            Assert.IsFalse(WriteSessionGuard.IsActive, "Must not be active after failed Start");
            // Rollback: stopEditing called to undo partial acquire
            Assert.AreEqual(1, _stopC,   "StopEditing must be called for rollback");
            Assert.AreEqual(1, _allowC,  "AllowRefresh must be called for rollback");
            Assert.AreEqual(1, _unlockC, "UnlockAssemblies must be called for rollback");
        }

        [Test]
        public void Watchdog_WhenForceReleaseThrows_LogsErrorDoesNotPropagate()
        {
            WriteSessionGuard.Start();
            WriteSessionGuard._stopEditing = () => throw new Exception("stop-boom");
            _fakeTime = 121.0;
            // Watchdog emits a warning (fired) and an error (ForceRelease exception)
            LogAssert.Expect(LogType.Warning, new Regex("watchdog fired"));
            LogAssert.Expect(LogType.Error, new Regex("watchdog.*stop-boom"));
            // Must not throw — watchdog catches ForceRelease exceptions
            Assert.DoesNotThrow(() => WriteSessionGuard.InvokeWatchdogTickForTest());
            Assert.IsFalse(WriteSessionGuard.IsActive);
        }

        // ── Subtask 18: crash recovery ───────────────────────────────────────

        [Test]
        public void CrashRecovery_WhenMarkerPresent_StopsEditing()
        {
            SessionState.SetBool(WriteSessionGuard.ActiveKey, true);
            WriteSessionGuard.SimulateDomainReloadForTest();
            Assert.AreEqual(1, _stopC,   "StopAssetEditing must fire during crash recovery");
            Assert.AreEqual(1, _unlockC, "UnlockReloadAssemblies must fire during crash recovery");
            Assert.AreEqual(1, _allowC,  "AllowAutoRefresh must fire during crash recovery");
            Assert.IsFalse(SessionState.GetBool(WriteSessionGuard.ActiveKey, false),
                "SessionState marker must be erased after recovery");
        }
    }
}
