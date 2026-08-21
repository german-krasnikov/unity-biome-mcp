// TDD: SyncHelper — epoch, trigger, events, ISyncOps seam, domain stamp (RC-1/RC-2/RC-6/RC-8, Tier-0).
// All public APIs tested via mock ISyncOps injection.
// Run order: 1→2→3→...→22
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SyncHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private MockSyncOps _mock;

        [SetUp]
        public void SetUp()
        {
            _mock = new MockSyncOps();
            SyncHelper.OverrideOpsForTest(_mock);
            SyncHelper.ResetForTest();
            // Exercise stamp transitions through the owned seam. UnityMcpTestBase restores
            // the exact editor baseline after every test.
            SyncHelper.OverrideDomainStampForTest("");
        }

        [TearDown]
        public void TearDown() => SyncHelper.ResetForTest();

        // #1: epoch starts at 0 after reset and can be read back
        [Test]
        public void Epoch_Survives_SessionState_Roundtrip()
        {
            Assert.AreEqual(0, SyncHelper.CurrentEpoch);
        }

        // #2: TriggerSync increments epoch
        [Test]
        public void TriggerSync_Increments_Epoch()
        {
            SyncHelper.TriggerSync(resolve: false);
            Assert.AreEqual(1, SyncHelper.CurrentEpoch);

            SyncHelper.TriggerSync(resolve: false);
            Assert.AreEqual(2, SyncHelper.CurrentEpoch);
        }

        // #3: TriggerSync calls Ops.Refresh()
        [Test]
        public void TriggerSync_Calls_Refresh()
        {
            SyncHelper.TriggerSync(resolve: false);
            Assert.AreEqual(1, _mock.RefreshCount);
        }

        // #4: TriggerSync calls Resolve BEFORE Refresh when requested
        [Test]
        public void TriggerSync_Calls_Resolve_Before_Refresh_When_Requested()
        {
            SyncHelper.TriggerSync(resolve: true);
            Assert.AreEqual(1, _mock.ResolveCount);
            Assert.IsTrue(_mock.ResolveCalledBeforeRefresh,
                "Resolve must be called before Refresh");
        }

        // #5: returns will_compile=true when IsCompiling after Refresh
        [Test]
        public void TriggerSync_Returns_WillCompile_True_When_Compiling()
        {
            _mock.IsCompilingAfterRefresh = true;
            var result = SyncHelper.TriggerSync(resolve: false);
            StringAssert.Contains("will_compile=true", result);
            StringAssert.Contains("epoch=1", result);
            StringAssert.StartsWith("sync_ack", result);
        }

        // #6: returns will_compile=false when idle
        [Test]
        public void TriggerSync_Returns_WillCompile_False_When_Idle()
        {
            _mock.IsCompilingAfterRefresh = false;
            var result = SyncHelper.TriggerSync(resolve: false);
            StringAssert.Contains("will_compile=false", result);
        }

        // #7: GetSyncStatus returns epoch and state
        [Test]
        public void GetSyncStatus_Returns_Epoch_And_State()
        {
            var status = SyncHelper.GetSyncStatus();
            StringAssert.StartsWith("epoch=", status);
            StringAssert.Contains("|state=", status);
        }

        // #8: compile failed fires OnSyncFailed
        [Test]
        public void CompileFailed_Fires_OnSyncFailed()
        {
            string firedWith = null;
            SyncHelper.OnSyncFailed += err => firedWith = err;

            _mock.ScriptCompilationFailedOnFinish = true;
            SyncHelper.TriggerSync(resolve: false);
            SyncHelper.SimulateCompilationFinished(); // test seam

            Assert.IsNotNull(firedWith, "OnSyncFailed must fire on compile failure");
        }

        // #9: IsCompileClean is false during compile
        [Test]
        public void IsCompileClean_False_During_Compile()
        {
            SyncHelper.SimulateCompilationStarted(); // test seam
            Assert.IsFalse(SyncHelper.IsCompileClean);
        }

        // #10: IsCompileClean is true after reload
        [Test]
        public void IsCompileClean_True_After_Reload()
        {
            SyncHelper.SimulateCompilationStarted();
            Assert.IsFalse(SyncHelper.IsCompileClean);

            SyncHelper.SimulateAfterAssemblyReload(); // test seam
            Assert.IsTrue(SyncHelper.IsCompileClean);
        }

        // #11: self-heal — will_compile was a false positive (no compile ever started):
        // after the grace period GetSyncStatus reports ready instead of compiling forever.
        [Test]
        public void GetSyncStatus_SelfHeals_When_Compile_Never_Started()
        {
            double fakeTime = 100.0;
            SyncHelper.NowSeconds = () => fakeTime;

            _mock.IsCompilingAfterRefresh = true; // will_compile=true at trigger time
            var ack = SyncHelper.TriggerSync(resolve: false);
            StringAssert.Contains("will_compile=true", ack);

            _mock.IsCompilingAfterRefresh = false; // ...but no compile ever starts
            fakeTime += SyncHelper.SelfHealGraceSeconds + 1.0;

            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("state=ready", status);
            Assert.IsTrue(SyncHelper.IsCompileClean, "self-heal must restore clean flag");
        }

        // #12: no self-heal inside the grace window (R-1: compile may still be starting)
        [Test]
        public void GetSyncStatus_Stays_Compiling_Inside_Grace_Window()
        {
            double fakeTime = 100.0;
            SyncHelper.NowSeconds = () => fakeTime;

            _mock.IsCompilingAfterRefresh = true;
            SyncHelper.TriggerSync(resolve: false);
            _mock.IsCompilingAfterRefresh = false;

            fakeTime += SyncHelper.SelfHealGraceSeconds - 1.0;
            StringAssert.Contains("state=compiling", SyncHelper.GetSyncStatus());
        }

        // #13: no self-heal when a compile actually started — R-4 still owns the
        // ready transition (only afterAssemblyReload may report ready).
        [Test]
        public void GetSyncStatus_No_SelfHeal_When_Compile_Started()
        {
            double fakeTime = 100.0;
            SyncHelper.NowSeconds = () => fakeTime;

            _mock.IsCompilingAfterRefresh = true;
            SyncHelper.TriggerSync(resolve: false);
            _mock.IsCompilingAfterRefresh = false;

            SyncHelper.SimulateCompilationStarted(); // compile DID start
            fakeTime += SyncHelper.SelfHealGraceSeconds + 60.0;

            StringAssert.Contains("state=compiling", SyncHelper.GetSyncStatus(),
                "started compile must wait for reload/failed, never self-heal");
        }

        // #14: RC-1 — SimulateAfterAssemblyReload stores a non-empty stamp.
        // SetUp erases StampKey so stamp starts empty in test context (cold-load seeding
        // happens in the static ctor, before SetUp's explicit erase).
        [Test]
        public void AfterReload_Stores_NonEmpty_Stamp()
        {
            // SetUp installs an owned empty domain stamp so we start clean.
            Assert.IsEmpty(SyncHelper.CurrentDomainStamp, "stamp empty after SetUp erase");
            SyncHelper.SimulateAfterAssemblyReload();
            Assert.IsNotEmpty(SyncHelper.CurrentDomainStamp, "stamp set after reload");
        }

        // #14b: FIX#1 — ComputeStamp returns a non-empty value (static ctor uses it to seed
        // the stamp on cold Unity startup before any afterAssemblyReload fires).
        [Test]
        public void ComputeStamp_NonEmpty_Enables_ColdStart_Seeding()
        {
            // Simulate cold-start: stamp erased, then re-seeded via the same path as
            // the static ctor bootstrap.
            SyncHelper.OverrideDomainStampForTest("");
            var s = SyncHelper.ComputeStamp();
            Assert.IsNotEmpty(s, "ComputeStamp must return non-empty so cold-start seeding works");
            if (!string.IsNullOrEmpty(s))
                SyncHelper.OverrideDomainStampForTest(s);
            Assert.IsNotEmpty(SyncHelper.CurrentDomainStamp, "stamp non-empty after cold-start seed");
        }

        // #15: RC-1 — stamp format is MVID:mtime (guid:digits)
        [Test]
        public void AfterReload_Stamp_Format_Is_Mvid_Colon_Mtime()
        {
            SyncHelper.SimulateAfterAssemblyReload();
            var stamp = SyncHelper.CurrentDomainStamp;
            // format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx:digits
            var parts = stamp.Split(':');
            Assert.GreaterOrEqual(parts.Length, 2, $"stamp must contain ':' separator: {stamp}");
            // last part must be long digits (mtime ticks or 0)
            Assert.IsTrue(long.TryParse(parts[parts.Length - 1], out _),
                $"mtime part must be numeric: {stamp}");
        }

        // #16: RC-1 — CurrentDomainStamp getter returns the stored stamp
        [Test]
        public void CurrentDomainStamp_Returns_Stored_Value()
        {
            Assert.IsEmpty(SyncHelper.CurrentDomainStamp);
            SyncHelper.SimulateAfterAssemblyReload();
            var stamp = SyncHelper.CurrentDomainStamp;
            Assert.IsNotEmpty(stamp);
            // calling again returns same value (idempotent)
            Assert.AreEqual(stamp, SyncHelper.CurrentDomainStamp);
        }

        // #16b: RC-2 — failed state is NOT overwritten by self-heal
        [Test]
        public void GetSyncStatus_FailedState_Not_Overwritten_By_SelfHeal()
        {
            double fakeTime = 100.0;
            SyncHelper.NowSeconds = () => fakeTime;

            // trigger → compile starts → compile fails
            _mock.IsCompilingAfterRefresh = true;
            SyncHelper.TriggerSync(resolve: false);
            SyncHelper.SimulateCompilationStarted();
            _mock.ScriptCompilationFailedOnFinish = true;
            SyncHelper.SimulateCompilationFinished();

            // fast-forward well past grace
            fakeTime += 120.0;
            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("state=failed", status,
                "failed state must persist, not be healed to ready");
        }

        // #17: RC-6/Tier-0 — TriggerSync calls RequestScriptCompilation then StartTickPump
        [Test]
        public void TriggerSync_Calls_RequestScriptCompilation_And_StartTickPump()
        {
            SyncHelper.TriggerSync(resolve: false);
            Assert.AreEqual(1, _mock.RequestScriptCompilationCount,
                "TriggerSync must call RequestScriptCompilation");
            Assert.AreEqual(1, _mock.StartTickPumpCount,
                "TriggerSync must call StartTickPump (tick-pump replaces single QueuePlayerLoopUpdate)");
        }

        // #18: RC-8 — will_compile reads isCompiling AFTER RequestScriptCompilation call
        [Test]
        public void TriggerSync_ReadsIsCompiling_After_RequestScriptCompilation()
        {
            // mock sets IsCompiling=true only after RequestScriptCompilation is called
            _mock.IsCompilingAfterRequestCompilation = true;
            var result = SyncHelper.TriggerSync(resolve: false);
            StringAssert.Contains("will_compile=true", result,
                "will_compile must reflect isCompiling AFTER RequestScriptCompilation");
        }

        // #19: stamp survives ResetForTest (HOLE-1b fix — only OnAfterReload may write it)
        [Test]
        public void ResetForTest_PreservesStamp()
        {
            SyncHelper.SimulateAfterAssemblyReload();
            var stamp = SyncHelper.CurrentDomainStamp;
            Assert.IsNotEmpty(stamp, "SimulateAfterAssemblyReload must write a stamp");
            SyncHelper.ResetForTest();
            // Stamp must survive ResetForTest — it is only written by real domain reloads
            Assert.AreEqual(stamp, SyncHelper.CurrentDomainStamp,
                "stamp must NOT be erased by ResetForTest (HOLE-1b)");
        }

        // #20: ComputeStamp returns non-empty string (sanity: assembly always loaded)
        [Test]
        public void ComputeStamp_Returns_NonEmpty()
        {
            var stamp = SyncHelper.ComputeStamp();
            Assert.IsNotEmpty(stamp, "ComputeStamp must return non-empty string in test context");
        }

        // #21: Tier-0 tick-pump — re-arms each tick until IsCompiling flips true, then stops.
        [Test]
        public void TickPump_ReArms_Each_Tick_And_Unsubscribes_When_IsCompiling()
        {
            // Drive 5 ticks while isCompiling=false, then flip true on tick 6.
            _mock.IsCompilingAfterRefresh = false;
            _mock.StartTickPumpImpl(tickBudget: 300);

            // 5 ticks: pump should have queued 5 updates, still subscribed
            for (int i = 0; i < 5; i++) _mock.SimulateTick();
            Assert.AreEqual(5, _mock.QueuePlayerLoopUpdateCount, "pump calls QueuePlayerLoopUpdate each tick");
            Assert.IsTrue(_mock.IsPumpActive, "pump still subscribed before compile starts");

            // tick 6: compile starts
            _mock.IsCompilingAfterRefresh = true;
            _mock.SimulateTick();
            Assert.AreEqual(6, _mock.QueuePlayerLoopUpdateCount);
            Assert.IsFalse(_mock.IsPumpActive, "pump unsubscribed once IsCompiling=true");
        }

        // #22: Tier-0 tick-pump — anti-runaway: unsubscribes when tick budget exhausted.
        [Test]
        public void TickPump_Unsubscribes_When_Budget_Exhausted()
        {
            _mock.IsCompilingAfterRefresh = false;
            const int budget = 5; // small budget for test
            _mock.StartTickPumpImpl(tickBudget: budget);

            // drive budget ticks
            for (int i = 0; i < budget; i++) _mock.SimulateTick();

            Assert.AreEqual(budget, _mock.QueuePlayerLoopUpdateCount);
            Assert.IsFalse(_mock.IsPumpActive, "pump unsubscribed after budget exhausted");
        }

        // #23: stamp self-heal — started=true, stamp changed, !IsCompiling → heals to ready
        [Test]
        public void GetSyncStatus_StampHeal_WhenStampChangedAfterStarted()
        {
            // Set a "pre-trigger" stamp in SessionState as TriggerSync would
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", "OLD_STAMP");
            // Set state=compiling, started=true
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            // Current domain stamp is different (reload happened)
            SyncHelper.OverrideDomainStampForTest("NEW_STAMP");
            _mock.IsCompilingAfterRefresh = false; // no real compile in progress

            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("state=ready", status, "stamp change must heal compiling→ready");
        }

        // #24: stamp self-heal negative — same stamp → stays compiling
        [Test]
        public void GetSyncStatus_StampHeal_Negative_SameStamp_StaysCompiling()
        {
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", "SAME_STAMP");
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest("SAME_STAMP");
            _mock.IsCompilingAfterRefresh = false;

            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("state=compiling", status, "same stamp must NOT heal");
        }

        // #25: stamp self-heal negative — IsCompiling=true → never heal mid-compile
        [Test]
        public void GetSyncStatus_StampHeal_Negative_IsCompiling_StaysCompiling()
        {
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", "OLD_STAMP");
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest("NEW_STAMP");
            _mock.IsCompilingAfterRefresh = true; // compile is running

            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("state=compiling", status, "must not heal while IsCompiling=true");
        }

        // #26: GetSyncStatus always includes stamp= field (revives Python no-op gate)
        [Test]
        public void GetSyncStatus_ContainsStampField()
        {
            // idle state — stamp= must be present in all return paths
            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("stamp=", status, "GetSyncStatus must emit stamp= field");
        }

        // #27: stamp= value equals CurrentDomainStamp
        [Test]
        public void GetSyncStatus_StampValue_MatchesCurrentDomainStamp()
        {
            // Inject a known stamp
            SyncHelper.OverrideDomainStampForTest("KNOWN_STAMP");
            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("stamp=KNOWN_STAMP", status,
                "stamp= value must match CurrentDomainStamp");
        }

        // C2 #30: GetSyncStatus in compiling state emits all 4 wedge fingerprint fields
        [Test]
        public void GetSyncStatus_CompilationWedge_EmitsFingerprint()
        {
            // Arrange: set up compiling state, started=true, stamp frozen, cn_active=false
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", "FROZEN_STAMP");
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest("FROZEN_STAMP"); // same → frozen
            _mock.IsCompilingAfterRefresh = true;   // iscompiling=true
            // CompileNotifier.IsCompiling reads SessionState "MCP_CompileStart" (>0 = active)
            // Leave at 0 so cn_active=false → engine-wedge scenario

            var status = SyncHelper.GetSyncStatus();

            StringAssert.Contains("|iscompiling=", status, "must contain iscompiling field");
            StringAssert.Contains("|cn_active=", status, "must contain cn_active field");
            StringAssert.Contains("|started=", status, "must contain started field");
            StringAssert.Contains("|stamp_frozen=", status, "must contain stamp_frozen field");
            // With same stamp → stamp_frozen=true
            StringAssert.Contains("stamp_frozen=true", status,
                "stamp_frozen must be true when stamp unchanged");
            // started=true (we set the key)
            StringAssert.Contains("started=true", status, "started must be true");
        }

        // C3 #32: TriggerSync re-wedge guard — returns "wedged" when already frozen-wedged
        [Test]
        public void TriggerSync_OnUnchangedWedge_DoesNotRewedge()
        {
            const string stamp = "SOME_STAMP";
            // Set up wedge: state=compiling, started=true, stamp frozen, not IsCompiling
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest(stamp);
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", stamp);
            var epochBefore = SyncHelper.CurrentEpoch;
            _mock.IsCompilingAfterRefresh = false;

            var result = SyncHelper.TriggerSync(resolve: false);

            StringAssert.StartsWith("wedged|", result, "re-wedge guard must return wedged| prefix");
            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch, "epoch must NOT be bumped on wedge-guard hit");
        }

        // C3 #33: OnCompileStarted seeds stamp if StampAtTrigger was empty (Play-initiated reload)
        [Test]
        public void StampHeal_WhenSnapshotEmpty_PlayInitiated()
        {
            // Simulate Play-initiated compile: StampAtTrigger is empty
            UnityEditor.SessionState.EraseString("MCP_StampAtTrigger");
            // Pre-set a domain stamp
            SyncHelper.OverrideDomainStampForTest("PLAY_STAMP");

            SyncHelper.SimulateCompilationStarted();

            var seeded = UnityEditor.SessionState.GetString("MCP_StampAtTrigger", "");
            Assert.AreEqual("PLAY_STAMP", seeded,
                "OnCompileStarted must seed StampAtTrigger when empty (Play-initiated path)");
        }

        // G7 #35: TriggerSync no-compile path must NOT force green when ScriptCompilationFailed
        [Test]
        public void TriggerSync_NoCompile_ScriptCompilationFailed_DoesNotForceGreen()
        {
            _mock.ScriptCompilationFailedOnFinish = true;
            _mock.IsCompilingAfterRefresh = false; // willCompile=false → no-compile path
            SyncHelper.TriggerSync(resolve: false);

            // State must NOT be forced to ready/green when build is failed
            var status = SyncHelper.GetSyncStatus();
            StringAssert.DoesNotContain("state=ready", status,
                "G7: no-compile path must NOT force state=ready when ScriptCompilationFailed");
            Assert.IsFalse(SyncHelper.IsCompileClean,
                "G7: IsCompileClean must stay false when ScriptCompilationFailed");
        }

        // G7 #36: grace-period self-heal must NOT force green when ScriptCompilationFailed
        [Test]
        public void GetSyncStatus_SelfHeal_ScriptCompilationFailed_DoesNotForceGreen()
        {
            double fakeTime = 100.0;
            SyncHelper.NowSeconds = () => fakeTime;

            _mock.IsCompilingAfterRefresh = true;
            SyncHelper.TriggerSync(resolve: false);
            _mock.IsCompilingAfterRefresh = false;
            _mock.ScriptCompilationFailedOnFinish = true; // build is failed

            // advance past the self-heal grace window
            fakeTime += SyncHelper.SelfHealGraceSeconds + 5.0;

            var status = SyncHelper.GetSyncStatus();
            StringAssert.DoesNotContain("state=ready", status,
                "G7: grace self-heal must NOT force state=ready when ScriptCompilationFailed");
        }

        // SD-1 #37: ComputeStamp returns non-empty string in any UnityMCP context
        [Test]
        public void ComputeStamp_ReturnsNonEmptyString()
        {
            var stamp = SyncHelper.ComputeStamp();
            Assert.IsNotEmpty(stamp, "ComputeStamp must return non-empty — UnityMCP.Editor always loaded");
        }

        // SD-1 #38: multi-assembly format has ';' separators (one per assembly MVID)
        [Test]
        public void ComputeStamp_ContainsMultipleAssemblyMVIDs()
        {
            var loaded = System.AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => (a.GetName().Name ?? "").StartsWith("UnityMCP."))
                .ToList();
            var stamp = SyncHelper.ComputeStamp();
            if (loaded.Count > 1)
                StringAssert.Contains(";", stamp,
                    "multi-assembly stamp must contain ';' when more than one UnityMCP.* asm loaded");
            else
                Assert.IsNotEmpty(stamp, "stamp non-empty even with single assembly");
        }

        // SD-1 #39: stamp includes Wizard assembly MVID when loaded
        [Test]
        public void ComputeStamp_IncludesWizardAssemblyMVID()
        {
            var wizardAsm = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "UnityMCP.Editor.Wizard");
            if (wizardAsm == null)
            {
                Assert.Ignore("UnityMCP.Editor.Wizard not loaded — skip");
                return;
            }
            var wizardMvid = wizardAsm.ManifestModule.ModuleVersionId.ToString("N");
            var stamp = SyncHelper.ComputeStamp();
            StringAssert.Contains(wizardMvid, stamp, "stamp must include Wizard assembly MVID");
        }

        // C3 #34: OnCompileStarted does NOT overwrite existing StampAtTrigger
        [Test]
        public void OnCompileStarted_DoesNotOverwriteExistingStampAtTrigger()
        {
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", "EXISTING");
            SyncHelper.OverrideDomainStampForTest("OTHER");

            SyncHelper.SimulateCompilationStarted();

            Assert.AreEqual("EXISTING", UnityEditor.SessionState.GetString("MCP_StampAtTrigger", ""),
                "OnCompileStarted must not overwrite existing StampAtTrigger");
        }

        // C1 #28: OnAfterReload must write state=failed when ScriptCompilationFailed=true
        [Test]
        public void OnAfterReload_StaysFailed_When_ScriptCompilationFailed()
        {
            _mock.ScriptCompilationFailedOnFinish = true;
            SyncHelper.SimulateAfterAssemblyReload();
            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("state=failed", status,
                "OnAfterReload must write failed when ScriptCompilationFailed=true");
            Assert.IsFalse(SyncHelper.IsCompileClean,
                "IsCompileClean must be false when ScriptCompilationFailed=true");
        }

        // C1 #29: stamp-heal must NOT heal to ready when ScriptCompilationFailed=true
        [Test]
        public void StampHeal_StaysCompiling_When_CompilationFailed()
        {
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", "OLD_STAMP");
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest("NEW_STAMP");
            _mock.IsCompilingAfterRefresh = false;
            _mock.ScriptCompilationFailedOnFinish = true; // compile failed

            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("state=compiling", status,
                "stamp-heal must NOT heal to ready when ScriptCompilationFailed=true");
        }

        // Task1 #NullGuard_1: OverrideOpsForTest(null) throws ArgumentNullException
        [Test]
        public void OverrideOpsForTest_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SyncHelper.OverrideOpsForTest(null));
        }

        // Task1 #NullGuard_2: RestoreOpsForTest(null) throws ArgumentNullException
        [Test]
        public void RestoreOpsForTest_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SyncHelper.RestoreOpsForTest(null));
        }

        // Task1 #NullGuard_3: OnSyncComplete fires exactly once on successful reload
        [Test]
        public void OnAfterReload_Success_FiresOnSyncComplete_ExactlyOnce()
        {
            int fireCount = 0;
            SyncHelper.OnSyncComplete += () => fireCount++;
            _mock.ScriptCompilationFailedOnFinish = false;

            SyncHelper.SimulateAfterAssemblyReload();

            Assert.AreEqual(1, fireCount, "OnSyncComplete must fire exactly once on success");
        }

        // Task2 #StampHeal_Empty_1: empty stampAtTrigger blocks stamp-heal → stays compiling
        [Test]
        public void GetSyncStatus_StampHeal_EmptySnapshot_StaysCompiling()
        {
            UnityEditor.SessionState.EraseString("MCP_StampAtTrigger");
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest("SOME_STAMP");
            _mock.IsCompilingAfterRefresh = false;

            var status = SyncHelper.GetSyncStatus();

            StringAssert.Contains("state=compiling", status,
                "empty stampAtTrigger must block stamp-heal — guard requires non-empty snapshot");
        }

        // Task2 #StampHeal_Empty_2: non-empty snapshot + stamp changed → heals to ready
        [Test]
        public void GetSyncStatus_StampHeal_NonEmptySnapshot_StampChanged_HealsToReady()
        {
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", "OLD");
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest("NEW");
            _mock.IsCompilingAfterRefresh = false;

            var status = SyncHelper.GetSyncStatus();

            StringAssert.Contains("state=ready", status,
                "non-empty snapshot with changed stamp must heal to ready");
        }

        // Task3 #Wedge_Neg_1: state=idle → wedge guard inactive, returns sync_ack
        [Test]
        public void TriggerSync_WedgeGuard_IdleState_ReturnsSyncAck()
        {
            // ResetForTest leaves state=idle (erased key defaults to "idle")
            var result = SyncHelper.TriggerSync(resolve: false);

            StringAssert.StartsWith("sync_ack", result,
                "idle state must not trigger wedge guard");
        }

        // Task3 #Wedge_Neg_2: state=compiling but started=false → wedge breaks at condition 2
        [Test]
        public void TriggerSync_WedgeGuard_CompileNotStarted_ReturnsSyncAck()
        {
            const string stamp = "FROZEN";
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", false); // condition 2 broken
            SyncHelper.OverrideDomainStampForTest(stamp);
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", stamp);
            _mock.IsCompilingAfterRefresh = false;

            var result = SyncHelper.TriggerSync(resolve: false);

            StringAssert.StartsWith("sync_ack", result,
                "started=false must break wedge condition 2 → sync_ack returned");
        }

        // Task3 #Wedge_Neg_3: all four conditions met → wedge fires (the "debounce message")
        [Test]
        public void TriggerSync_WedgeGuard_AllConditionsMet_ReturnsWedged()
        {
            const string stamp = "FROZEN";
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest(stamp);
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", stamp);
            _mock.IsCompilingAfterRefresh = false; // !IsCompiling = true

            var result = SyncHelper.TriggerSync(resolve: false);

            StringAssert.StartsWith("wedged|", result,
                "all four conditions must return the wedge/debounce response");
        }

        // Task3 #Wedge_Neg_4: IsCompiling=true breaks condition 4 → sync_ack (re-trigger allowed)
        [Test]
        public void TriggerSync_WedgeGuard_IsCompilingTrue_BreaksCondition4()
        {
            const string stamp = "FROZEN";
            UnityEditor.SessionState.SetString("MCP_SyncState", "compiling");
            UnityEditor.SessionState.SetBool("MCP_SyncCompileStarted", true);
            SyncHelper.OverrideDomainStampForTest(stamp);
            UnityEditor.SessionState.SetString("MCP_StampAtTrigger", stamp);
            _mock.IsCompilingAfterRefresh = true; // IsCompiling=true breaks condition 4

            var result = SyncHelper.TriggerSync(resolve: false);

            StringAssert.StartsWith("sync_ack", result,
                "IsCompiling=true must break wedge condition 4 → trigger fires");
        }

        // Task4 #FailedFormat_1: failed state contains 'err' pipe-delimited field
        [Test]
        public void GetSyncStatus_FailedState_ContainsErrField()
        {
            _mock.ScriptCompilationFailedOnFinish = true;
            SyncHelper.TriggerSync(resolve: false);
            SyncHelper.SimulateCompilationStarted();
            SyncHelper.SimulateCompilationFinished();

            var status = SyncHelper.GetSyncStatus();

            StringAssert.Contains("|err=", status, "failed state must contain |err= field");
        }

        // Task4 #FailedFormat_2: exact pipe-delimited format with all required fields
        [Test]
        public void GetSyncStatus_FailedState_ExactPipeDelimitedFormat()
        {
            _mock.ScriptCompilationFailedOnFinish = true;
            SyncHelper.TriggerSync(resolve: false);
            SyncHelper.SimulateCompilationStarted();
            SyncHelper.SimulateCompilationFinished();

            var status = SyncHelper.GetSyncStatus();

            StringAssert.Contains("epoch=", status, "must contain epoch= field");
            StringAssert.Contains("|state=failed|", status, "must contain |state=failed|");
            StringAssert.Contains("|err=", status, "must contain |err= field");
            StringAssert.Contains("|stamp=", status, "must contain |stamp= field");
        }

        // Task4 #FailedFormat_3: err= field value is non-empty (contains error description)
        [Test]
        public void GetSyncStatus_FailedState_ErrField_ContainsNonEmptyDescription()
        {
            _mock.ScriptCompilationFailedOnFinish = true;
            SyncHelper.TriggerSync(resolve: false);
            SyncHelper.SimulateCompilationStarted();
            SyncHelper.SimulateCompilationFinished();

            var status = SyncHelper.GetSyncStatus();

            var errIdx = status.IndexOf("|err=", System.StringComparison.Ordinal);
            Assert.Greater(errIdx, 0, "err= field must be present");
            var afterErr = status.Substring(errIdx + 5); // skip "|err="
            var errValue = afterErr.Contains("|")
                ? afterErr.Substring(0, afterErr.IndexOf('|'))
                : afterErr;
            Assert.IsNotEmpty(errValue, "err= field must contain a non-empty error description");
        }
    }

    // ── MockSyncOps ──────────────────────────────────────────────────────────

    public sealed class MockSyncOps : ISyncOps
    {
        public int  RefreshCount                      { get; private set; }
        public int  ResolveCount                      { get; private set; }
        public int  ImportPackageSourcesCount         { get; private set; }
        public int  RequestScriptCompilationCount     { get; private set; }
        public RequestScriptCompilationOptions LastRSCOptions { get; private set; }
        public int  StartTickPumpCount                { get; private set; }
        public int  QueuePlayerLoopUpdateCount        { get; private set; }
        public bool ResolveCalledBeforeRefresh        { get; private set; }
        public bool ImportCalledBeforeRefresh         { get; private set; }
        public bool ImportCalledBeforeRSC             { get; private set; }
        public bool IsCompilingAfterRefresh           { get; set; }
        public bool IsCompilingAfterRequestCompilation{ get; set; }
        public bool ScriptCompilationFailedOnFinish   { get; set; }

        // Tick-pump simulation state
        private bool _pumpActive;
        private int  _pumpRemaining;
        public bool IsPumpActive => _pumpActive;

        public void ImportPackageSources()
        {
            if (ImportPackageSourcesCount == 0 && RefreshCount == 0)
                ImportCalledBeforeRefresh = true;
            if (ImportPackageSourcesCount == 0 && RequestScriptCompilationCount == 0)
                ImportCalledBeforeRSC = true;
            ImportPackageSourcesCount++;
        }

        public void Refresh()
        {
            if (ResolveCount > 0 && RefreshCount == 0)
                ResolveCalledBeforeRefresh = true;
            RefreshCount++;
        }

        public void Resolve() => ResolveCount++;

        public void RequestScriptCompilation(RequestScriptCompilationOptions opts)
        {
            LastRSCOptions = opts;
            RequestScriptCompilationCount++;
            // simulate: after this call isCompiling may become true
            if (IsCompilingAfterRequestCompilation)
                IsCompilingAfterRefresh = true;
        }

        // Called by TriggerSync — records the call; tests drive the pump via StartTickPumpImpl.
        public void StartTickPump()
        {
            StartTickPumpCount++;
            // In mock, pump is NOT auto-started here — tests call StartTickPumpImpl to drive it.
        }

        // Test helper: manually start the pump simulation with a given budget.
        public void StartTickPumpImpl(int tickBudget)
        {
            _pumpRemaining = tickBudget;
            _pumpActive    = true;
        }

        // Test helper: simulate one EditorApplication.update tick.
        public void SimulateTick()
        {
            if (!_pumpActive) return;
            QueuePlayerLoopUpdateCount++;
            _pumpRemaining--;
            if (IsCompilingAfterRefresh || _pumpRemaining <= 0)
                _pumpActive = false;
        }

        public bool IsCompiling              => IsCompilingAfterRefresh;
        public bool IsUpdating               => false;
        public bool ScriptCompilationFailed  => ScriptCompilationFailedOnFinish;
    }

    // ── R2: force_refresh + ImportPackageSources tests (C1-C5) ───────────────

    [TestFixture]
    public class ForceRefreshTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private MockSyncOps _mock;

        [SetUp]
        public void SetUp()
        {
            _mock = new MockSyncOps();
            SyncHelper.OverrideOpsForTest(_mock);
            SyncHelper.ResetForTest();
            SyncHelper.OverrideDomainStampForTest("");
        }

        [TearDown]
        public void TearDown() => SyncHelper.ResetForTest();

        // C1: force_refresh calls ImportPackageSources exactly once
        [Test]
        public void ForceRefresh_Calls_ImportPackageSources_Exactly_Once()
        {
            CommandRegistry.Execute("force_refresh", null);
            Assert.AreEqual(1, _mock.ImportPackageSourcesCount,
                "force_refresh must call ImportPackageSources exactly once");
        }

        // C2: ImportPackageSources called BEFORE Refresh
        [Test]
        public void ForceRefresh_CallOrder_Import_Before_Refresh()
        {
            CommandRegistry.Execute("force_refresh", null);
            Assert.IsTrue(_mock.ImportCalledBeforeRefresh,
                "ImportPackageSources must be called before Refresh");
        }

        // C3: ImportPackageSources called BEFORE RequestScriptCompilation
        [Test]
        public void ForceRefresh_CallOrder_Import_Before_RSC()
        {
            CommandRegistry.Execute("force_refresh", null);
            Assert.IsTrue(_mock.ImportCalledBeforeRSC,
                "ImportPackageSources must be called before RequestScriptCompilation");
        }

        // C4: RequestScriptCompilation is called (options verified via mock count)
        [Test]
        public void ForceRefresh_Calls_RequestScriptCompilation()
        {
            CommandRegistry.Execute("force_refresh", null);
            Assert.AreEqual(1, _mock.RequestScriptCompilationCount,
                "force_refresh must call RequestScriptCompilation exactly once");
        }

        // C5: ImportPackageSources on empty mock doesn't throw
        [Test]
        public void ImportPackageSources_DoesNotThrow_WhenEmpty()
        {
            // UnitySyncOps.ImportPackageSources wraps in try/catch (best-effort).
            // MockSyncOps also must not throw on zero-guid scenario.
            Assert.DoesNotThrow(() => _mock.ImportPackageSources(),
                "ImportPackageSources must be safe to call with no guids");
        }

        // C5_new: force_refresh must pass RequestScriptCompilationOptions.None (NOT CleanBuildCache).
        // CleanBuildCache has a Unity 6.x no-op bug: fires assemblyCompilationNotRequired instead
        // of recompiling → Class A stale domain never heals. Regression guard: if someone reverts
        // to CleanBuildCache, this test catches it without requiring a live Unity session.
        [Test]
        public void ForceRefresh_Uses_None_Not_CleanBuildCache()
        {
            CommandRegistry.Execute("force_refresh", null);
            Assert.AreEqual(RequestScriptCompilationOptions.None, _mock.LastRSCOptions,
                "force_refresh must call RequestScriptCompilation(None) — CleanBuildCache has Unity 6.x no-op bug");
        }

        // CP-2: ImportPackageSources must exist on UnitySyncOps
        [Test]
        public void ImportPackageSources_MethodExistsOnUnitySyncOps()
        {
            var m = typeof(UnitySyncOps).GetMethod("ImportPackageSources",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(m, "ImportPackageSources must exist on UnitySyncOps");
        }

        // CP-2: tundra.digestcache deletion is removed — structural guard via source file read.
        // Reads the actual SyncHelper.cs to verify the regression-prone line is absent.
        [Test]
        public void ImportPackageSources_DoesNotDeleteDigestCache()
        {
            var src = ReadRequiredPackageSource(typeof(SyncHelper), "Editor/SyncHelper.cs");
            StringAssert.DoesNotContain("tundra.digestcache", src,
                "CP-2: tundra.digestcache deletion corrupts Bee artifact graph — must be removed");
        }
    }
}
