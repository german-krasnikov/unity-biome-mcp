// TDD: N1b -- sync/force_refresh/sync_status registry metadata alignment.
// Audit (Plans/N1b-second-module-sync.md) found 'sync' and 'force_refresh'
// registered mutating:false/notBatchable:false by default even though both
// call SyncHelper.Ops (AssetDatabase.Refresh + RequestScriptCompilation +,
// for force_refresh, ImportPackageSources/StartTickPump) -- a real mutation
// that read-only mode and the batch route must both refuse before dispatch.
// sync_status is correctly non-mutating/batchable/allowed-during-compile
// already (unchanged here). Split from CommandRegistryGuardFlagsTests.cs to
// keep that file under the 300-line budget; SetUp mirrors
// CommandRouterReadOnlyEnforcementTests.cs (IsReadOnly/IsPlayMode override)
// combined with SyncHelperTests.cs's MockSyncOps injection so the read-only
// and batch guards are proven through the real dispatch path with an actual
// effect spy, not just a metadata assertion.
using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRegistrySyncOwnerFlagsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private Func<bool> _savedIsReadOnly;
        private Func<bool> _savedIsPlayMode;
        private Func<bool> _savedIsCompiling;
        private ISyncOps _savedOps;
        private MockSyncOps _mock;

        [SetUp]
        public void SetUp()
        {
            _savedIsReadOnly = CommandRouter.IsReadOnly;
            _savedIsPlayMode = CommandRouter.IsPlayMode;
            _savedIsCompiling = CommandRouter.IsCompiling;
            _savedOps = SyncHelper.Ops;
            CommandRouter.IsPlayMode = () => false;
            CommandRouter.IsCompiling = () => false;
            _mock = new MockSyncOps();
            SyncHelper.OverrideOpsForTest(_mock);
            RegisterCleanup(() =>
            {
                CommandRouter.IsReadOnly = _savedIsReadOnly;
                CommandRouter.IsPlayMode = _savedIsPlayMode;
                CommandRouter.IsCompiling = _savedIsCompiling;
                SyncHelper.RestoreOpsForTest(_savedOps);
                CommandRegistry.CallerIsPlugin = false;
                CommandRegistry.CallerPluginName = null;
            });
        }

        // ── C1-C3: mutating flag ────────────────────────────────────────────

        [Test]
        public void Sync_IsMutating_True() => Assert.IsTrue(CommandRegistry.IsMutating("sync"));

        [Test]
        public void ForceRefresh_IsMutating_True() => Assert.IsTrue(CommandRegistry.IsMutating("force_refresh"));

        [Test]
        public void SyncStatus_IsMutating_False() => Assert.IsFalse(CommandRegistry.IsMutating("sync_status"));

        // ── C4-C6: batchability ──────────────────────────────────────────────

        [Test]
        public void Sync_IsNotBatchable() => Assert.IsFalse(CommandRegistry.IsBatchable("sync"));

        [Test]
        public void ForceRefresh_IsNotBatchable() => Assert.IsFalse(CommandRegistry.IsBatchable("force_refresh"));

        [Test]
        public void SyncStatus_IsBatchable() => Assert.IsTrue(CommandRegistry.IsBatchable("sync_status"));

        // ── C7-C9: read-only gate, real CommandRouter.Process dispatch,
        // MockSyncOps proves zero effect calls (not just an error string) ────

        [Test]
        public void Sync_BlockedInReadOnly_NoEffect()
        {
            CommandRouter.IsReadOnly = () => true;

            var result = CommandRouter.Process("{\"id\":\"t-sync-ro\",\"cmd\":\"sync\",\"args\":{}}");

            StringAssert.Contains("READ_ONLY_BLOCKED", result, result);
            Assert.AreEqual(0, _mock.RefreshCount);
            Assert.AreEqual(0, _mock.RequestScriptCompilationCount);
        }

        [Test]
        public void ForceRefresh_BlockedInReadOnly_NoEffect()
        {
            CommandRouter.IsReadOnly = () => true;

            var result = CommandRouter.Process("{\"id\":\"t-fr-ro\",\"cmd\":\"force_refresh\",\"args\":{}}");

            StringAssert.Contains("READ_ONLY_BLOCKED", result, result);
            Assert.AreEqual(0, _mock.RefreshCount);
            Assert.AreEqual(0, _mock.ImportPackageSourcesCount);
            Assert.AreEqual(0, _mock.RequestScriptCompilationCount);
            Assert.AreEqual(0, _mock.StartTickPumpCount);
        }

        [Test]
        public void SyncStatus_AllowedInReadOnly()
        {
            CommandRouter.IsReadOnly = () => true;

            var result = CommandRouter.Process("{\"id\":\"t-ss-ro\",\"cmd\":\"sync_status\",\"args\":{}}");

            StringAssert.DoesNotContain("READ_ONLY_BLOCKED", result, result);
        }

        // ── Batch route: 'sync' refused before dispatch via the real
        // BatchHelper.Execute path (not a hand-rolled preprocess check) ──────

        [Test]
        public void Batch_ContainingSync_RefusedNotExecuted()
        {
            var result = BatchHelper.Execute("sync", "continue");

            StringAssert.Contains("'sync' is async-only", result, result);
            Assert.AreEqual(0, _mock.RefreshCount);
            Assert.AreEqual(0, _mock.RequestScriptCompilationCount);
        }

        // ── Compile guard: 'sync' is not allowlisted during compile. The C#
        // guard rejection ("Unity is compiling. Retry in 5s.") is expected to
        // trip here; sync_unity absorbs that text at the Python layer (PD-1,
        // SYNC_COMPILE_GUARD_TEXT), it is not suppressed in C# ──────────────

        [Test]
        public void Sync_NotAllowedDuringCompile() =>
            Assert.IsFalse(CommandRouter.IsAllowedDuringCompile("sync"));

        // ── Owner: host-registered (null), plugin collision refused ─────────

        [Test]
        public void Sync_ForceRefresh_SyncStatus_OwnerIsHostNull()
        {
            Assert.IsNull(CommandRegistry.GetOwner("sync"));
            Assert.IsNull(CommandRegistry.GetOwner("force_refresh"));
            Assert.IsNull(CommandRegistry.GetOwner("sync_status"));
        }

        [Test]
        public void Sync_PluginCollision_Refused()
        {
            CommandRegistry.CallerIsPlugin = true;
            CommandRegistry.CallerPluginName = "TestPlugin";

            Assert.Throws<InvalidOperationException>(() =>
                CommandRegistry.Register("sync", _ => "hijacked", required: "", optional: ""));

            // The throw happens before _commands is touched (AlreadyRegistered
            // guards before DenyPluginCoreFlags/assignment) -- ownership must be
            // untouched by the refused attempt.
            Assert.IsNull(CommandRegistry.GetOwner("sync"));
        }
    }
}
