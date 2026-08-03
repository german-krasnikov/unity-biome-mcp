using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.TestRuns;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestIsolationBaseTests : SceneTestBase
    {
        [Test]
        public void Cleanup_RunsHooksAndActionsInOrder_OnlyOnce()
        {
            var events = new List<string>();
            var probe = new CleanupProbe(events);
            probe.BeginUnityMcpIsolation();
            probe.AddCleanup(() => events.Add("first"));
            probe.AddCleanup(() => events.Add("second"));

            probe.EndUnityMcpIsolation();
            probe.EndUnityMcpIsolation();

            CollectionAssert.AreEqual(
                new[] { "hook", "prepare", "second", "first", "final" }, events);
        }

        [Test]
        public void Cleanup_ContinuesAfterFailure_AndAggregatesErrors()
        {
            var events = new List<string>();
            var probe = new CleanupProbe(events);
            probe.BeginUnityMcpIsolation();
            probe.AddCleanup(() => events.Add("still-ran"));
            probe.AddCleanup(() => throw new InvalidOperationException("expected cleanup failure"));

            var error = Assert.Throws<AggregateException>(probe.EndUnityMcpIsolation);

            Assert.That(error.InnerExceptions, Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { "hook", "prepare", "still-ran", "final" }, events);
            Assert.DoesNotThrow(probe.EndUnityMcpIsolation, "cleanup must be idempotent after failure");
        }

        [Test]
        public void RegisterCleanup_RequiresAnActiveScope()
        {
            var probe = new CleanupProbe(new List<string>());

            Assert.Throws<InvalidOperationException>(() => probe.AddCleanup(() => { }));
        }

        [Test]
        public void CommonBase_RestoresGlobalSyncSeamAutomatically()
        {
            var productionOps = SyncHelper.Ops;
            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            SyncHelper.OverrideOpsForTest(new MockSyncOps());

            probe.EndUnityMcpIsolation();

            Assert.That(SyncHelper.Ops, Is.SameAs(productionOps));
            Assert.That(SyncHelper.Ops, Is.TypeOf<UnitySyncOps>());
        }

        [Test]
        public void CommonBase_RepairsLeakedGlobalSyncSeamAndFailsClosed()
        {
            SyncHelper.OverrideOpsForTest(new MockSyncOps());
            var probe = new CleanupProbe(new List<string>());

            var error = Assert.Throws<InvalidOperationException>(
                probe.BeginUnityMcpIsolation);

            StringAssert.Contains("SyncHelper.Ops retained test double", error.Message);
            Assert.That(SyncHelper.Ops, Is.TypeOf<UnitySyncOps>());
        }

        [Test]
        public void CommonBase_RestoresReloadGuardStateAutomatically()
        {
            var baselineOps = ReloadGuard.Ops;
            var baselinePath = ReloadGuard.FilePath;
            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            ReloadGuard.OverrideFilePath(Path.Combine(Path.GetTempPath(),
                "reload-guard-probe-" + Guid.NewGuid().ToString("N")));
            ReloadGuard.OnTurnStarted();

            probe.EndUnityMcpIsolation();

            Assert.That(ReloadGuard.Ops, Is.SameAs(baselineOps));
            Assert.That(ReloadGuard.FilePath, Is.EqualTo(baselinePath));
            Assert.That(ReloadGuard.IsLocked, Is.False);
            Assert.That(ReloadGuard.HasPersistedLock, Is.False);
        }

        [Test]
        public void CommonBase_RestoresLogAssertionPolicyAutomatically()
        {
            var baseline = LogAssert.ignoreFailingMessages;
            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            LogAssert.ignoreFailingMessages = !baseline;

            probe.EndUnityMcpIsolation();

            Assert.That(LogAssert.ignoreFailingMessages, Is.EqualTo(baseline));
        }

        [Test]
        public void CommonBase_RestoresDomainStampAutomatically()
        {
            var baseline = SyncHelper.CurrentDomainStamp;
            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            SyncHelper.OverrideDomainStampForTest("probe-domain-stamp");

            probe.EndUnityMcpIsolation();

            Assert.That(SyncHelper.CurrentDomainStamp, Is.EqualTo(baseline));
        }

        [Test]
        public void SyncIsolation_RestoresExactKeysClockEventsAndOps()
        {
            const string epochKey = "MCP_SyncEpoch";
            const string cleanKey = "MCP_SyncClean";
            const string stateKey = "MCP_SyncState";
            const string errorKey = "MCP_SyncError";
            const string triggerKey = "MCP_SyncTriggerTime";
            const string startedKey = "MCP_SyncCompileStarted";
            const string stampKey = "MCP_DomainStamp";
            const string stampAtTriggerKey = "MCP_StampAtTrigger";
            const string allErrorsKey = "MCP_AllCompileErrors";

            SessionState.SetInt(epochKey, 41);
            SessionState.EraseBool(cleanKey);
            SessionState.SetString(stateKey, "baseline-state");
            SessionState.EraseString(errorKey);
            SessionState.SetFloat(triggerKey, 12.5f);
            SessionState.SetBool(startedKey, true);
            SessionState.SetString(stampKey, "baseline-stamp");
            SessionState.EraseString(stampAtTriggerKey);
            SessionState.SetString(allErrorsKey, "baseline-errors");

            var baselineOps = SyncHelper.Ops;
            Func<double> baselineClock = () => 123.0;
            SyncHelper.NowSeconds = baselineClock;
            var completeCount = 0;
            var failedCount = 0;
            var isolatedCount = 0;
            Action baselineComplete = () => completeCount++;
            Action<string> baselineFailed = _ => failedCount++;
            Action isolatedComplete = () => isolatedCount++;
            SyncHelper.OnSyncComplete += baselineComplete;
            SyncHelper.OnSyncFailed += baselineFailed;

            IDisposable scope = null;
            try
            {
                scope = SyncHelper.BeginTestIsolation();
                SyncHelper.ResetForTest();
                SyncHelper.OverrideOpsForTest(new MockSyncOps());
                SyncHelper.NowSeconds = () => 999.0;
                SyncHelper.OnSyncComplete += isolatedComplete;
                SessionState.SetInt(epochKey, 99);
                SessionState.SetBool(cleanKey, false);
                SessionState.SetString(stateKey, "isolated-state");
                SessionState.SetString(errorKey, "isolated-error");
                SessionState.SetFloat(triggerKey, 88.5f);
                SessionState.SetBool(startedKey, false);
                SessionState.SetString(stampKey, "isolated-stamp");
                SessionState.SetString(stampAtTriggerKey, "isolated-trigger-stamp");
                SessionState.SetString(allErrorsKey, "isolated-errors");

                scope.Dispose();

                Assert.That(SyncHelper.Ops, Is.SameAs(baselineOps));
                Assert.That(SyncHelper.NowSeconds, Is.SameAs(baselineClock));
                Assert.That(SessionState.GetInt(epochKey, 0), Is.EqualTo(41));
                Assert.That(HasBoolSessionKey(cleanKey), Is.False);
                Assert.That(SessionState.GetString(stateKey, ""), Is.EqualTo("baseline-state"));
                Assert.That(HasStringSessionKey(errorKey), Is.False);
                Assert.That(SessionState.GetFloat(triggerKey, 0f), Is.EqualTo(12.5f));
                Assert.That(SessionState.GetBool(startedKey, false), Is.True);
                Assert.That(SessionState.GetString(stampKey, ""), Is.EqualTo("baseline-stamp"));
                Assert.That(HasStringSessionKey(stampAtTriggerKey), Is.False);
                Assert.That(SessionState.GetString(allErrorsKey, ""),
                    Is.EqualTo("baseline-errors"));

                SyncHelper.InvokeSyncCompleteForTest();
                SyncHelper.InvokeSyncFailedForTest("expected");
                Assert.That(completeCount, Is.EqualTo(1));
                Assert.That(failedCount, Is.EqualTo(1));
                Assert.That(isolatedCount, Is.Zero,
                    "The isolated invocation list must not escape its scope.");
            }
            finally
            {
                scope?.Dispose();
                SyncHelper.OnSyncComplete -= isolatedComplete;
                SyncHelper.OnSyncComplete -= baselineComplete;
                SyncHelper.OnSyncFailed -= baselineFailed;
            }
        }

        [Test]
        public void ReloadIsolation_RejectsScopeOwnedByAnotherTest()
        {
            var baselineOps = ReloadGuard.Ops;

            var error = Assert.Throws<InvalidOperationException>(() =>
                ReloadGuard.BeginTestIsolation(
                    new NoOpReloadGuardOps(),
                    "definitely-not-the-current-nunit-test"));

            StringAssert.Contains("owned by another test", error.Message);
            Assert.That(ReloadGuard.Ops, Is.SameAs(baselineOps));
            Assert.That(ReloadGuard.HasActiveTestIsolation, Is.True);
        }

        [Test]
        public void CommonBase_RestoresExactCommandRegistrySnapshot()
        {
            const string command = "test_registry_snapshot_probe";
            CommandRegistry.Register(command, _ => "third-party", required: "");
            var readyBaseline = CommandRegistry.Ready;
            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            CommandRegistry.Clear();

            probe.EndUnityMcpIsolation();

            Assert.That(CommandRegistry.Ready, Is.EqualTo(readyBaseline));
            Assert.That(CommandRegistry.IsRegistered(command), Is.True,
                "Exact restoration must preserve non-core/plugin registrations.");
            Assert.That(CommandRegistry.Execute(command, "{}"), Is.EqualTo("third-party"));
        }

        [Test]
        public void CommonBase_UsesNestedRelayRuntimeWithoutTouchingOuterSession()
        {
            RelaySpawner.SetSessionForTests(24680, 99999999);
            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            Assert.That(RelaySpawner.RelayPort, Is.Zero);
            Assert.That(RelaySpawner.RelayPid, Is.Zero);
            RelaySpawner.SetSessionForTests(13579, 88888888);
            RelaySpawner.StopForTests();

            probe.EndUnityMcpIsolation();

            Assert.That(RelaySpawner.RelayPort, Is.EqualTo(24680));
            Assert.That(RelaySpawner.RelayPid, Is.EqualTo(99999999));
        }

        [Test]
        public void CommonBase_RestoresExactTypedEditorPrefsFromFirstMutation()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var stringKey = "UnityMCP_Test_String_" + suffix;
            var boolKey = "UnityMCP_Test_Bool_" + suffix;
            var intKey = "UnityMCP_Test_Int_" + suffix;
            var floatKey = "UnityMCP_Test_Float_" + suffix;
            SetEditorPrefString(stringKey, "original");
            SetEditorPrefBool(boolKey, true);
            SetEditorPrefInt(intKey, 17);
            SetEditorPrefFloat(floatKey, 1.25f);

            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            probe.SetStringPref(stringKey, "first");
            probe.SetStringPref(stringKey, "second");
            probe.SetBoolPref(boolKey, false);
            probe.SetIntPref(intKey, 99);
            probe.SetFloatPref(floatKey, 7.5f);
            probe.EndUnityMcpIsolation();

            Assert.That(EditorPrefs.GetString(stringKey), Is.EqualTo("original"));
            Assert.That(EditorPrefs.GetBool(boolKey), Is.True);
            Assert.That(EditorPrefs.GetInt(intKey), Is.EqualTo(17));
            Assert.That(EditorPrefs.GetFloat(floatKey), Is.EqualTo(1.25f));
        }

        [Test]
        public void CommonBase_RemovesInitiallyAbsentEditorPrefDespiteOtherCleanupFailure()
        {
            var key = "UnityMCP_Test_Absent_" + Guid.NewGuid().ToString("N");
            DeleteEditorPrefString(key);
            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            probe.SetStringPref(key, "temporary");
            probe.AddCleanup(() => throw new InvalidOperationException("expected"));

            Assert.Throws<AggregateException>(probe.EndUnityMcpIsolation);

            Assert.That(EditorPrefs.HasKey(key), Is.False);
        }

        [Test]
        public void CommonBase_RejectsConflictingEditorPrefTypes()
        {
            var key = "UnityMCP_Test_Type_" + Guid.NewGuid().ToString("N");
            DeleteEditorPrefString(key);
            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            probe.SetStringPref(key, "value");

            var error = Assert.Throws<InvalidOperationException>(
                () => probe.SetBoolPref(key, true));
            probe.EndUnityMcpIsolation();

            StringAssert.Contains("already owned as String", error.Message);
            Assert.That(EditorPrefs.HasKey(key), Is.False);
        }

        [Test]
        public void CommonBase_RestoresExactRelaySpawnStateSnapshot()
        {
            RelaySpawnState.LooksAlreadyRunningOverride = () => true;
            RelaySpawnState.EnsureRunningOverride = () => 24681;
            RelaySpawnState.RequestSpawn(_ => { }, error => Assert.Fail(error));
            Assert.That(RelaySpawnState.IsReady, Is.True);

            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            Assert.That(RelaySpawnState.IsReady, Is.False);
            Assert.That(RelaySpawnState.IsPending, Is.False);
            Assert.That(RelaySpawnState.Port, Is.Zero);

            probe.EndUnityMcpIsolation();

            Assert.That(RelaySpawnState.IsReady, Is.True);
            Assert.That(RelaySpawnState.Port, Is.EqualTo(24681));
        }

        [Test]
        public void SceneIsolation_FinalCleanupLeavesOneCleanEmptyScene()
        {
            var probe = new SceneProbe();
            probe.BeginUnityMcpIsolation();
            new GameObject("owned-by-probe");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            probe.EndUnityMcpIsolation();

            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(SceneManager.GetActiveScene().GetRootGameObjects(), Is.Empty);
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
        }

        [Test]
        public void CommonBase_WithoutRunHintFailsClosedWithoutMutatingScene()
        {
            const string ownedSceneKey = "UnityMCP_active_owned_test_scene_v1";
            var previousHint = SessionState.GetString(ownedSceneKey, "");
            var sceneBefore = SceneManager.GetActiveScene();
            var rootsBefore = sceneBefore.GetRootGameObjects();
            SessionState.EraseString(ownedSceneKey);
            try
            {
                var probe = new CleanupProbe(new List<string>());
                var error = Assert.Throws<InvalidOperationException>(
                    probe.BeginUnityMcpIsolation);

                StringAssert.Contains("no prepared UnityMCP scene transaction", error.Message);
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneBefore.handle));
                CollectionAssert.AreEqual(rootsBefore,
                    SceneManager.GetActiveScene().GetRootGameObjects());
            }
            finally
            {
                if (string.IsNullOrEmpty(previousHint))
                    SessionState.EraseString(ownedSceneKey);
                else
                    SessionState.SetString(ownedSceneKey, previousHint);
            }
        }

        [Test]
        public void TrackedSceneAsset_IsDeletedOnlyAfterItsSceneIsDetached()
        {
            TestPaths.EnsureFolder();
            var path = TestPaths.TempFolder + "/tracked-scene-order.unity";
            var probe = new SceneOwnershipProbe();
            probe.BeginUnityMcpIsolation();
            probe.OwnAsset(path);
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            probe.OwnScene(scene);

            probe.EndUnityMcpIsolation();

            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Null);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        [Test]
        public void TrackedPreviewScene_IsClosedByCommonOwnershipCleanup()
        {
            var activeBefore = SceneManager.GetActiveScene();
            var previewCountBefore = EditorSceneManager.previewSceneCount;
            var probe = new SceneOwnershipProbe();
            probe.BeginUnityMcpIsolation();
            Scene preview = default;
            try
            {
                preview = probe.CreatePreviewScene();

                Assert.That(EditorSceneManager.IsPreviewScene(preview), Is.True);
                Assert.That(EditorSceneManager.previewSceneCount,
                    Is.EqualTo(previewCountBefore + 1));
            }
            finally
            {
                probe.EndUnityMcpIsolation();
            }

            Assert.That(preview.IsValid() && preview.isLoaded, Is.False);
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCountBefore));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeBefore.handle));
        }

        [Test]
        public async Task WaitForEditorUpdatesAsync_CompletesInEditMode()
        {
            await WaitForEditorUpdatesAsync(2, 5.0);
        }

        [Test]
        public async Task WaitForEditorUpdatesAsync_TimesOutAndReleasesItsCallback()
        {
            TimeoutException error = null;
            try
            {
                await WaitForEditorUpdatesAsync(int.MaxValue, 0.1);
            }
            catch (TimeoutException caught)
            {
                error = caught;
            }

            Assert.That(error, Is.Not.Null, "The independent timeout must fault the wait.");
            StringAssert.Contains("did not produce", error.Message);
            await WaitForEditorUpdatesAsync(2, 5.0);
        }

        [Test]
        public async Task WaitForEditorUpdatesAsync_NestedTeardownCancelsLosingWait()
        {
            var probe = new SceneOwnershipProbe();
            probe.BeginUnityMcpIsolation();
            Task pending = null;
            try
            {
                pending = probe.WaitForUpdates(int.MaxValue, 60.0);
            }
            finally
            {
                probe.EndUnityMcpIsolation();
            }

            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.IsCanceled, Is.True,
                "Teardown must terminalize a wait that the test stopped observing.");
            try
            {
                await pending;
                Assert.Fail("The losing wait must remain cancelled.");
            }
            catch (TaskCanceledException)
            {
            }
            await WaitForEditorUpdatesAsync(2, 5.0);
            Assert.That(pending.IsCanceled, Is.True,
                "A queued timer callback must not fault a teardown-cancelled wait.");
        }

        [Test]
        public void CommonBase_RecreatesPollutedManagedSceneAssetAsEmpty()
        {
            const string ownedSceneKey = "UnityMCP_active_owned_test_scene_v1";
            var runId = SessionState.GetString(
                TestRunAssetOwnership.OwnedRunIdSessionKey, "");
            var ownedPath = SessionState.GetString(ownedSceneKey, "");
            Assert.That(ownedPath, Is.EqualTo(
                TestRunAssetOwnership.ExpectedRunScenePath(runId)));

            var current = SceneManager.GetActiveScene();
            Assert.That(current.path, Is.EqualTo(ownedPath));
            new GameObject("persisted-pollution");
            Assert.That(EditorSceneManager.SaveScene(current), Is.True);

            var probe = new CleanupProbe(new List<string>());
            probe.BeginUnityMcpIsolation();
            probe.EndUnityMcpIsolation();

            var restored = SceneManager.GetActiveScene();
            Assert.That(restored.path, Is.EqualTo(ownedPath));
            Assert.That(restored.isDirty, Is.False);
            Assert.That(restored.GetRootGameObjects(), Is.Empty);
        }

        [Test]
        public void EnvironmentPreparation_DirtySceneFailsClosedWithoutChangingIt()
        {
            var scene = SceneManager.GetActiveScene();
            var pathBefore = scene.path;
            var owned = new GameObject("dirty-baseline-proof");
            EditorSceneManager.MarkSceneDirty(scene);
            var storeRoot = Path.Combine(Path.GetTempPath(),
                "unity-mcp-dirty-preflight-" + Guid.NewGuid().ToString("N"));
            RegisterCleanup(() =>
            {
                if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
            });
            var store = TestRunStore.ForProject(storeRoot);
            store.WriteRun(new TestRunRecord
            {
                run_id = "run-dirty-preflight",
                mode = "PlayMode",
                lifecycle = TestRunProtocol.Lifecycle.Prepared,
                created_utc = DateTime.UtcNow.ToString("O")
            });
            var controller = new UnityTestRunEnvironmentController();

            var error = Assert.Throws<InvalidOperationException>(
                () => controller.Prepare(store, "run-dirty-preflight", DateTime.UtcNow.ToString("O")));

            StringAssert.Contains("unsaved changes", error.Message);
            Assert.That(scene.path, Is.EqualTo(pathBefore));
            Assert.That(scene.isDirty, Is.True);
            Assert.That(owned, Is.Not.Null);
        }

        [Test]
        public void TestAssetOwnership_RejectsPathsOutsideDedicatedRoot()
        {
            Assert.DoesNotThrow(TestPaths.EnsureRoot);
            Assert.That(AssetDatabase.IsValidFolder(TestPaths.Root), Is.True);
            Assert.Throws<ArgumentException>(() => TestPaths.RequireOwnedPath(TestPaths.Root));
            Assert.Throws<ArgumentException>(() => TestPaths.DeleteOwnedAsset(TestPaths.Root));
            Assert.Throws<ArgumentException>(() => TestPaths.RequireOwnedPath("Assets/UserScene.unity"));
            Assert.Throws<ArgumentException>(() =>
                TestPaths.RequireOwnedPath("Assets/TestsTemp/../UserScene.unity"));
            Assert.DoesNotThrow(() =>
                TestPaths.RequireOwnedPath("Assets/TestsTemp/Fixture/result.asset"));
        }

        [Test]
        public void SceneSpecializationsShareTheCommonIsolationBase()
        {
            Assert.That(typeof(SceneCleanTestBase).IsSubclassOf(typeof(SceneTestBase)), Is.True);
            Assert.That(typeof(MultiSceneTestBase).IsSubclassOf(typeof(SceneTestBase)), Is.True);
            Assert.That(typeof(SceneTestBase).IsSubclassOf(typeof(UnityMcpTestBase)), Is.True);
        }

        private sealed class CleanupProbe : UnityMcpTestBase
        {
            private readonly ICollection<string> _events;

            internal CleanupProbe(ICollection<string> events)
            {
                _events = events;
            }

            internal void AddCleanup(Action action)
            {
                RegisterCleanup(action);
            }

            internal void SetStringPref(string key, string value) =>
                SetEditorPrefString(key, value);

            internal void SetBoolPref(string key, bool value) =>
                SetEditorPrefBool(key, value);

            internal void SetIntPref(string key, int value) =>
                SetEditorPrefInt(key, value);

            internal void SetFloatPref(string key, float value) =>
                SetEditorPrefFloat(key, value);

            protected override void OnBeforeIsolationCleanup()
            {
                _events.Add("hook");
            }

            protected override void PrepareForOwnershipCleanup()
            {
                _events.Add("prepare");
            }

            protected override void PerformFinalIsolationCleanup()
            {
                _events.Add("final");
            }
        }

        private sealed class NoOpReloadGuardOps : IReloadGuardTestOps
        {
            public double TimeSinceStartup => 0.0;
            public void DisallowAutoRefresh() { }
            public void AllowAutoRefresh() { }
            public void LockReloadAssemblies() { }
            public void UnlockReloadAssemblies() { }
            public void RefreshAssets() { }
            public void ScheduleRefresh() { }
            public void AddWatchdog(EditorApplication.CallbackFunction callback) { }
            public void RemoveWatchdog(EditorApplication.CallbackFunction callback) { }
        }

        private static bool HasBoolSessionKey(string key) =>
            SessionState.GetBool(key, false) == SessionState.GetBool(key, true);

        private static bool HasStringSessionKey(string key) =>
            string.Equals(
                SessionState.GetString(key, "__absent_a__"),
                SessionState.GetString(key, "__absent_b__"),
                StringComparison.Ordinal);

        private sealed class SceneProbe : SceneTestBase
        {
        }

        private sealed class SceneOwnershipProbe : UnityMcpTestBase
        {
            internal void OwnAsset(string path)
            {
                TrackOwnedAsset(path);
            }

            internal void OwnScene(Scene scene)
            {
                TrackOwnedScene(scene);
            }

            internal Scene CreatePreviewScene()
            {
                return CreateOwnedPreviewScene();
            }

            internal Task WaitForUpdates(int updateCount, double timeoutSeconds)
            {
                return WaitForEditorUpdatesAsync(updateCount, timeoutSeconds);
            }
        }

    }
}
