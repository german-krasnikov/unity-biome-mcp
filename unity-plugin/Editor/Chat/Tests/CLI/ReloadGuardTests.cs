// TDD — RED first. Tests drive ReloadGuard lock/save/load/clear contract.
// Uses a temp file path to avoid touching Library/ during tests.
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ReloadGuardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tmpPath;
        private RecordingReloadGuardOps _ops;

        [SetUp]
        public void SetUp()
        {
            _tmpPath = Path.Combine(Path.GetTempPath(), $"ReloadGuardTest_{System.Guid.NewGuid()}.txt");
            ReloadGuard.OverrideFilePath(_tmpPath); // test seam
            ReloadGuard.ResetForTest();              // clear in-memory lock counter
            _ops = new RecordingReloadGuardOps();
            ReloadGuard.OverrideOpsForTest(_ops);
        }

        [TearDown]
        public void TearDown()
        {
            ReloadGuard.ResetForTest();
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
        }

        // ── Lock / Unlock ─────────────────────────────────────────────────────

        [Test]
        public void OnTurnStarted_SetsLockActive()
        {
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked);
        }

        [Test]
        public void OnTurnFinished_ClearsLock()
        {
            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnFinished();
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        [Test]
        public void OnTurnFinished_WithoutStart_NoThrow()
        {
            // Double-finish / finish-without-start must be idempotent
            Assert.DoesNotThrow(() => ReloadGuard.OnTurnFinished());
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        [Test]
        public void DoubleFinish_NoThrow()
        {
            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnFinished();
            Assert.DoesNotThrow(() => ReloadGuard.OnTurnFinished());
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        // ── Save / Load / Clear ───────────────────────────────────────────────

        [Test]
        public void SaveAndLoad_SameState_RoundTrips()
        {
            var state = new PendingTurnState("sid", "hello", new[] { "/A" }, true, "reviewer", "Sending");
            ReloadGuard.SavePendingState(state);

            // Simulate domain reload: reset the in-memory state
            ReloadGuard.ResetForTest();

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNotNull(loaded, "loaded state must not be null after save");
            Assert.AreEqual("sid",   loaded.Value.SessionId);
            Assert.AreEqual("hello", loaded.Value.PendingText);
        }

        [Test]
        public void ClearPendingState_LoadReturnsNull()
        {
            var state = new PendingTurnState("sid", "hello", new string[0], false, null, "Idle");
            ReloadGuard.SavePendingState(state);
            ReloadGuard.ClearPendingState();

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadPendingState_NoFile_ReturnsNull()
        {
            // File was never created
            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadPendingState_CorruptedFile_ReturnsNull()
        {
            File.WriteAllText(_tmpPath, "corrupted|||garbage");
            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadPendingState_EmptyFile_ReturnsNull()
        {
            File.WriteAllText(_tmpPath, "");
            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNull(loaded);
        }

        // ── New: double-start / double-finish balance ─────────────────────────

        [Test]
        public void DoubleStart_DoubleFinish_NotLocked()
        {
            // Start→Start→Finish→Finish ⇒ IsLocked false (counter returns to 0)
            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked, "must be locked after two starts");
            ReloadGuard.OnTurnFinished();
            Assert.IsTrue(ReloadGuard.IsLocked, "still locked after first finish (depth=1)");
            ReloadGuard.OnTurnFinished();
            Assert.IsFalse(ReloadGuard.IsLocked, "must be unlocked after two finishes");
        }

        [Test]
        public void ResetForTest_ClearsWatchdogDelegate()
        {
            // Verify ResetForTest removes the watchdog so it cannot accumulate across tests.
            ReloadGuard.OnTurnStarted();
            ReloadGuard.ResetForTest(); // must remove watchdog from EditorApplication.update
            // After reset, IsLocked is false
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        [Test]
        public void ResetForTest_BalancesEveryAcquiredOperation()
        {
            ReloadGuard.OnTurnStarted();

            ReloadGuard.ResetForTest();

            Assert.That(_ops.DisallowCount, Is.EqualTo(1));
            Assert.That(_ops.LockCount, Is.EqualTo(1));
            Assert.That(_ops.UnlockCount, Is.EqualTo(1));
            Assert.That(_ops.AllowCount, Is.EqualTo(1));
            Assert.That(_ops.RefreshCount, Is.EqualTo(1));
            Assert.That(_ops.Watchdogs, Is.EqualTo(0));
            Assert.That(ReloadGuard.IsLocked, Is.False);
            Assert.That(ReloadGuard.HasPersistedLock, Is.False);
        }

        [Test]
        public void FailedAssemblyLock_RollsBackAutoRefreshWithoutPublishingLock()
        {
            _ops.ThrowOnLock = true;

            ReloadGuard.OnTurnStarted();

            Assert.That(_ops.DisallowCount, Is.EqualTo(1));
            Assert.That(_ops.LockCount, Is.EqualTo(1));
            Assert.That(_ops.AllowCount, Is.EqualTo(1));
            Assert.That(_ops.UnlockCount, Is.EqualTo(0));
            Assert.That(ReloadGuard.IsLocked, Is.False);
            Assert.That(ReloadGuard.HasPersistedLock, Is.False);
        }

        // CH4.test.4: watchdog fires and force-unlocks when timeout exceeded
        [Test]
        public void WatchdogTick_AfterTimeout_ForceUnlocks()
        {
            // Set watchdog to -1 seconds — always exceeded (timeSinceStartup - start > -1 is always true).
            ReloadGuard.OverrideWatchdogSeconds(-1.0);
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked, "must be locked before watchdog fires");

            // Invoke WatchdogTick directly — threshold is -1s so it always fires.
            ReloadGuard.InvokeWatchdogTickForTest();

            Assert.IsFalse(ReloadGuard.IsLocked, "watchdog must force-unlock after timeout");
        }

        // ── Chips persistence ─────────────────────────────────────────────────

        [Test]
        public void SaveAndLoad_WithChips_ChipsPreserved()
        {
            var state = new PendingTurnState("sid", "t",
                new[] { "/Player", "Assets/Foo.cs" },
                false, null, "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Script });
            ReloadGuard.SavePendingState(state);
            ReloadGuard.ResetForTest();

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNotNull(loaded);
            Assert.AreEqual("/Player",              loaded.Value.ChipPaths[0]);
            Assert.AreEqual("Assets/Foo.cs",        loaded.Value.ChipPaths[1]);
            Assert.AreEqual(ChipKindKeys.Hierarchy, loaded.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Script,    loaded.Value.KindKeys[1]);
        }

        [Test]
        public void SaveAndLoad_IdleState_TextAndChipsPreserved()
        {
            var state = new PendingTurnState(null, "draft",
                new[] { "/Enemy" },
                false, null, "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy });
            ReloadGuard.SavePendingState(state);
            ReloadGuard.ResetForTest();

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNotNull(loaded);
            Assert.AreEqual("draft",   loaded.Value.PendingText);
            Assert.AreEqual("/Enemy",  loaded.Value.ChipPaths[0]);
            Assert.AreEqual("Idle",    loaded.Value.ActivityPhase);
        }

        [Test]
        public void SaveTwice_SecondOverwritesFirst()
        {
            var first  = new PendingTurnState("s1", "first",  new string[0], false, null, "Idle");
            var second = new PendingTurnState("s2", "second", new string[0], false, null, "Idle");
            ReloadGuard.SavePendingState(first);
            ReloadGuard.SavePendingState(second);
            ReloadGuard.ResetForTest();

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNotNull(loaded);
            Assert.AreEqual("second", loaded.Value.PendingText);
            Assert.AreEqual("s2",     loaded.Value.SessionId);
        }

        // ── SD-2: ForceUnlock Refresh kick ────────────────────────────────────

        [Test]
        public void ForceUnlock_DoesNotThrow_WhenLockDepthIsOne()
        {
            // Covers SD-2: ForceUnlock() (via OnTurnFinished) must not throw.
            // AssetDatabase.Refresh() cannot be intercepted in EditMode tests;
            // what we verify is: no exception and IsLocked becomes false.
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked);
            Assert.DoesNotThrow(() => ReloadGuard.OnTurnFinished(),
                "ForceUnlock path (via OnTurnFinished) must not throw");
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        // ── SH-2: OnTurnStarted exception safety ──────────────────────────────

        [Test]
        public void OnTurnStarted_IsLockedAfterFirstCall()
        {
            // SH-2: _lockDepth incremented only after successful acquisition.
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked);
        }

        [Test]
        public void OnTurnFinished_UnlocksAfterMatchingTurnStarted()
        {
            // SH-2: nested depth unwinds correctly.
            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnFinished();
            Assert.IsTrue(ReloadGuard.IsLocked, "still locked after first finish");
            ReloadGuard.OnTurnFinished();
            Assert.IsFalse(ReloadGuard.IsLocked, "unlocked after second finish");
        }

        private sealed class RecordingReloadGuardOps : IReloadGuardTestOps
        {
            public int DisallowCount { get; private set; }
            public int AllowCount { get; private set; }
            public int LockCount { get; private set; }
            public int UnlockCount { get; private set; }
            public int RefreshCount { get; private set; }
            public int Watchdogs { get; private set; }
            public bool ThrowOnLock { get; set; }
            public double TimeSinceStartup => UnityEditor.EditorApplication.timeSinceStartup;

            public void DisallowAutoRefresh() => DisallowCount++;
            public void AllowAutoRefresh() => AllowCount++;
            public void UnlockReloadAssemblies() => UnlockCount++;
            public void RefreshAssets() => RefreshCount++;
            public void ScheduleRefresh() { }

            public void LockReloadAssemblies()
            {
                LockCount++;
                if (ThrowOnLock) throw new System.InvalidOperationException("expected lock failure");
            }

            public void AddWatchdog(UnityEditor.EditorApplication.CallbackFunction callback) =>
                Watchdogs++;

            public void RemoveWatchdog(UnityEditor.EditorApplication.CallbackFunction callback)
            {
                if (Watchdogs > 0) Watchdogs--;
            }
        }
    }

    // ── Stress tests: _lockDepth and source structure (SH-2, SD-2) ───────────

    [TestFixture]
    public class ReloadGuardStressTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ReloadGuard.ResetForTest();
        [TearDown] public void TearDown() => ReloadGuard.ResetForTest();

        // T-C: ForceUnlock resets _lockDepth to exactly 0 (via watchdog path)
        [Test]
        public void ForceUnlock_LockDepthIsZeroAfterUnlock()
        {
            var f = typeof(ReloadGuard).GetField("_lockDepth",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, "_lockDepth field must exist");

            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnStarted(); // depth = 2
            ReloadGuard.OverrideWatchdogSeconds(-1.0); // always-expired
            ReloadGuard.InvokeWatchdogTickForTest();   // ForceUnlock → depth = 0
            Assert.AreEqual(0, (int)f.GetValue(null), "_lockDepth must be 0 after ForceUnlock");
        }

        // T-D: OnTurnStarted SH-2 — _lockDepth++ is NOT inside a finally block
        [Test]
        public void OnTurnStarted_LockDepthIncrementNotInFinallyBlock()
        {
            var src = ReadRequiredPackageSource(
                typeof(ReloadGuard), "Editor/Chat/CLI/ReloadGuard.cs");
            var methodStart = src.IndexOf(
                "internal static void OnTurnStarted()", System.StringComparison.Ordinal);
            Assert.That(methodStart, Is.GreaterThanOrEqualTo(0),
                "OnTurnStarted source was not found");
            var methodEnd = src.IndexOf(
                "internal static void OnTurnFinished()", methodStart,
                System.StringComparison.Ordinal);
            Assert.That(methodEnd, Is.GreaterThan(methodStart),
                "OnTurnStarted source boundary was not found");

            var method = src.Substring(methodStart, methodEnd - methodStart);
            StringAssert.DoesNotContain("finally", method,
                "SH-2: OnTurnStarted must not increment lock depth from a finally block");
            Assert.That(method.IndexOf("_lockDepth++;", System.StringComparison.Ordinal),
                Is.GreaterThan(method.IndexOf(
                    "Ops.LockReloadAssemblies();", System.StringComparison.Ordinal)),
                "SH-2: lock depth must increment only after reload lock acquisition succeeds");
        }

        // T-G: ForceUnlock source contains AssetDatabase.Refresh() (SD-2)
        [Test]
        public void ForceUnlock_SourceContainsAssetDatabaseRefresh()
        {
            var src = ReadRequiredPackageSource(
                typeof(ReloadGuard), "Editor/Chat/CLI/ReloadGuard.cs");
            StringAssert.Contains("AssetDatabase.Refresh()", src,
                "SD-2: ForceUnlock must call AssetDatabase.Refresh() to re-arm the file watcher");
        }
    }
}
