using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.TestRuns
{
    internal static class OrdinarySceneInventory
    {
        internal static List<Scene> CaptureLoaded(string invalidOrUnloadedMessage)
        {
            var loaded = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    throw new InvalidOperationException(invalidOrUnloadedMessage);
                if (EditorSceneManager.IsPreviewScene(scene)) continue;
                loaded.Add(scene);
            }
            return loaded;
        }
    }

    internal static class PreviewSceneCountEvidence
    {
        private static string _cachedRunId = "";
        private static bool _cachedBaselineCaptured;
        private static int _cachedBaselineCount;

        internal static int Capture() => EditorSceneManager.previewSceneCount;

        internal static void RequireActiveRunBaseline()
        {
            var runId = SessionState.GetString(
                TestRunAssetOwnership.OwnedRunIdSessionKey, "");
            if (string.IsNullOrEmpty(runId))
                throw new InvalidOperationException(
                    "The UTF run has no active durable preview-scene baseline.");

            if (!string.Equals(_cachedRunId, runId, StringComparison.Ordinal))
            {
                var projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                var store = TestRunStore.ForProject(projectRoot);
                if (!store.TryReadEnvironment(runId, out var environment))
                    throw new InvalidOperationException(
                        "The active UTF run has no durable environment evidence: " + runId);
                _cachedRunId = runId;
                _cachedBaselineCaptured = environment.preview_scene_baseline_captured;
                _cachedBaselineCount = environment.preview_scene_count;
            }

            RequireRecordedBaseline(
                _cachedBaselineCaptured,
                _cachedBaselineCount);
        }

        internal static void RequireRecordedBaseline(
            bool baselineCaptured,
            int baselineCount)
        {
            if (!baselineCaptured)
                throw new InvalidOperationException(
                    "The durable run predates preview-scene baseline evidence; " +
                    "refusing to execute another test in an unproven Editor state.");
            if (baselineCount < 0)
                throw new InvalidOperationException(
                    "The durable preview-scene baseline is invalid.");
            RequireBaseline(baselineCount);
        }

        internal static void RequireBaseline(int baselineCount)
        {
            var currentCount = Capture();
            if (currentCount != baselineCount)
                throw new InvalidOperationException(
                    $"Preview scene count changed from {baselineCount} to {currentCount}. " +
                    "Unity cannot enumerate lost preview-scene handles; tests must create " +
                    "them only through CreateOwnedPreviewScene().");
        }

        internal static string RestoreRunBaseline(
            bool baselineCaptured,
            int baselineCount)
        {
            if (!baselineCaptured)
                return "preview scene baseline was not captured by this legacy " +
                       "environment record; cleanup could not prove preview-scene isolation";
            var currentCount = Capture();
            return currentCount == baselineCount
                ? ""
                : $"preview scene count changed from {baselineCount} to {currentCount}; " +
                  "Unity cannot safely enumerate or close an unknown preview scene";
        }
    }

    internal interface ITestRunEnvironmentController
    {
        TestRunEnvironmentRecord Prepare(TestRunStore store, string runId, string utcNow);
        void Restore(TestRunStore store, string runId, string utcNow);
    }

    /// <summary>
    /// Owns the scene used while UTF runs. User scenes are accepted only as a
    /// clean named baseline. A clean untitled UTF bootstrap is replaced only in
    /// a marker-verified disposable worker. User scenes are never overwritten
    /// or have dirty flags cleared.
    /// </summary>
    internal sealed class UnityTestRunEnvironmentController : ITestRunEnvironmentController
    {
        private const string OwnedSceneSessionKey =
            "UnityMCP_active_owned_test_scene_v1";

        public TestRunEnvironmentRecord Prepare(TestRunStore store, string runId, string utcNow)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            RequireMainStage("start");
            if (store.TryReadEnvironment(runId, out var existing))
            {
                ValidateEnvironmentShape(existing);
                if (!string.IsNullOrEmpty(existing.restored_utc))
                    throw new InvalidOperationException(
                        "test environment was already restored for run " + runId);
                if (string.IsNullOrEmpty(existing.owned_scene_path) ||
                    string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(
                        existing.owned_scene_path)))
                    throw new InvalidOperationException(
                        "durable owned test scene is missing for run " + runId);

                SessionState.SetString(OwnedSceneSessionKey, existing.owned_scene_path);
                SessionState.SetString(TestRunAssetOwnership.OwnedRunIdSessionKey, runId);
                return existing;
            }

            var durableRun = store.ReadRun(runId);
            if (IsUtfManagedEditModeRun(durableRun))
                return PrepareUtfManagedBootstrap(store, runId, utcNow);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            TryOpenDisposableWorkerBootstrap(projectRoot);
            var baselineScenes = OrdinarySceneInventory.CaptureLoaded(
                "an invalid or unloaded scene prevents test environment preparation");
            var paths = new List<string>();
            var active = SceneManager.GetActiveScene();
            var restoreSingleUntitled = false;
            var untitledSceneSetup = "";

            foreach (var scene in baselineScenes)
            {
                if (string.IsNullOrEmpty(scene.path))
                {
                    if (baselineScenes.Count != 1 || !scene.IsValid() ||
                        !scene.isLoaded || scene.isDirty || scene.isSubScene ||
                        EditorSceneManager.IsPreviewScene(scene))
                        throw new InvalidOperationException(
                            "only one clean untitled startup scene can be used as a test baseline");
                    restoreSingleUntitled = true;
                    untitledSceneSetup = DescribeUntitledSetup(scene);
                    continue;
                }
                ValidateBaselineScene(scene, projectRoot);
                paths.Add(scene.path);
            }

            if ((!restoreSingleUntitled && paths.Count == 0) || !active.IsValid() ||
                !active.isLoaded || EditorSceneManager.IsPreviewScene(active) ||
                !baselineScenes.Exists(scene => scene.handle == active.handle))
                throw new InvalidOperationException("no valid named baseline scene is open");

            var initialAssetCleanup = TestRunAssetOwnership.CleanupForRun(runId, "");
            AppendCleanupWarning(store, runId, initialAssetCleanup, utcNow);
            var ownedPath = TestRunAssetOwnership.ExpectedRunScenePath(runId);
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ownedPath)))
                throw new InvalidOperationException("owned test scene already exists: " + ownedPath);

            var environment = new TestRunEnvironmentRecord
            {
                run_id = runId,
                scene_paths = paths.ToArray(),
                active_scene_path = active.path,
                restore_single_untitled = restoreSingleUntitled,
                untitled_scene_setup = untitledSceneSetup,
                owned_scene_path = ownedPath,
                preview_scene_baseline_captured = true,
                preview_scene_count = PreviewSceneCountEvidence.Capture(),
                prepared_utc = utcNow
            };
            store.WriteEnvironment(environment);
            SessionState.SetString(TestRunAssetOwnership.OwnedRunIdSessionKey, runId);

            try
            {
                var absoluteOwnedPath = Path.Combine(projectRoot, ownedPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absoluteOwnedPath));
                var owned = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(owned, ownedPath))
                    throw new InvalidOperationException(
                        "could not save owned test scene: " + ownedPath);
                SessionState.SetString(OwnedSceneSessionKey, ownedPath);
                return environment;
            }
            catch (Exception prepareError)
            {
                try
                {
                    Restore(store, runId, utcNow);
                }
                catch (Exception restoreError)
                {
                    throw new InvalidOperationException(
                        prepareError.Message + "; baseline restoration also failed: " +
                        restoreError.Message, prepareError);
                }
                throw;
            }
        }

        public void Restore(TestRunStore store, string runId, string utcNow)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (!store.TryReadEnvironment(runId, out var environment)) return;
            if (!string.IsNullOrEmpty(environment.restored_utc))
            {
                ClearOwnershipHintsForRun(runId);
                return;
            }
            ValidateEnvironmentShape(environment);
            if (environment.scene_restore_delegated_to_utf)
            {
                RestoreUtfManagedEnvironment(store, environment, utcNow);
                return;
            }
            RequireMainStage("restore");

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (var path in environment.scene_paths)
            {
                if (string.IsNullOrEmpty(path) ||
                    string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)) ||
                    !File.Exists(Path.Combine(projectRoot, path)))
                    throw new InvalidOperationException("baseline scene is missing: " + path);
            }

            var openScenes = OrdinarySceneInventory.CaptureLoaded(
                "an invalid or unloaded scene prevents baseline restoration");
            if (openScenes.Count == 0)
                throw new InvalidOperationException(
                    "no ordinary loaded scene is available for baseline restoration");
            var dirtyUnownedScenes = new List<string>();
            foreach (var scene in openScenes)
            {
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path) &&
                    !string.Equals(scene.path, environment.owned_scene_path,
                        StringComparison.Ordinal))
                    dirtyUnownedScenes.Add(scene.path);
            }

            // A failed test may replace the owned scene with an unsaved scene.
            // Unity 6000 can refuse to create an additive keeper in that state.
            // Giving the test-owned scene a quarantine path makes explicit close
            // possible without invoking the native save dialog.
            var recoveryPlan = TestSceneRecovery.PrepareForDiscard(
                openScenes, environment.run_id);

            // Keep one clean scene loaded while closing the previous set. Using
            // OpenSceneMode.Single here can invoke Unity's native save dialog for
            // a dirty scene; CloseScene is explicit, and only the runner-owned
            // disposable scene is ever allowed to be dirty.
            var keeper = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            var committed = false;
            var previewViolation = "";
            TestRunAssetCleanupReport assetCleanup = null;
            try
            {
                foreach (var scene in openScenes)
                {
                    if (scene.IsValid() && scene.isLoaded &&
                        !EditorSceneManager.CloseScene(scene, true))
                        throw new InvalidOperationException(
                            "could not close scene during test-run restoration: " +
                            (string.IsNullOrEmpty(scene.path) ? "<untitled>" : scene.path));
                }

                Scene active;
                if (environment.restore_single_untitled)
                {
                    active = keeper;
                }
                else
                {
                    foreach (var path in environment.scene_paths)
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    active = SceneManager.GetSceneByPath(environment.active_scene_path);
                }
                var currentActive = SceneManager.GetActiveScene();
                if (!active.IsValid() || !active.isLoaded ||
                    (currentActive.handle != active.handle &&
                     !SceneManager.SetActiveScene(active)))
                    throw new InvalidOperationException(
                        "could not restore active scene: " + environment.active_scene_path);

                if (!environment.restore_single_untitled &&
                    keeper.IsValid() && keeper.isLoaded &&
                    !EditorSceneManager.CloseScene(keeper, true))
                    throw new InvalidOperationException(
                        "could not close the temporary restoration scene");

                if (!string.IsNullOrEmpty(environment.owned_scene_path) &&
                    !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(environment.owned_scene_path)) &&
                    !AssetDatabase.DeleteAsset(environment.owned_scene_path))
                    throw new InvalidOperationException(
                        "could not delete owned test scene: " + environment.owned_scene_path);

                foreach (var temporaryPath in recoveryPlan.TemporaryPaths)
                {
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(temporaryPath)) &&
                        !AssetDatabase.DeleteAsset(temporaryPath))
                        throw new InvalidOperationException(
                            "could not delete temporary test scene: " + temporaryPath);
                }

                assetCleanup = TestRunAssetOwnership.CleanupForRun(
                    environment.run_id, "", allowCompileAffectingAssets: true);
                previewViolation = PreviewSceneCountEvidence.RestoreRunBaseline(
                    environment.preview_scene_baseline_captured,
                    environment.preview_scene_count);

                environment.restored_utc = utcNow;
                store.WriteEnvironment(environment);
                committed = true;
            }
            catch
            {
                // The keeper is also runner-owned. Leaving user-owned dirty scenes
                // untouched is more important than completing a partial restore.
                if (!committed && keeper.IsValid() && keeper.isLoaded)
                    EditorSceneManager.CloseScene(keeper, true);
                throw;
            }

            ClearOwnershipHintsForRun(runId);
            AppendCleanupWarning(store, runId, assetCleanup, utcNow);
            if (!string.IsNullOrEmpty(previewViolation))
                AppendInfrastructureOnce(store, runId, previewViolation, utcNow);
            if (dirtyUnownedScenes.Count > 0 || recoveryPlan.RecoveryPaths.Count > 0)
                AppendInfrastructureOnce(store, runId,
                    "test modified scene state outside its ownership root; recovery copies " +
                    "were retained at: " + string.Join(", ", recoveryPlan.RecoveryPaths),
                    utcNow);
        }

        private static string DescribeUntitledSetup(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            if (roots.Length == 0)
                return TestRunProtocol.UntitledSceneSetup.Empty;
            throw new InvalidOperationException(
                "a clean untitled baseline must be empty; save non-empty scenes before tests");
        }

        private static void ValidateEnvironmentShape(TestRunEnvironmentRecord environment)
        {
            var expectedOwnedPath =
                TestRunAssetOwnership.ExpectedRunScenePath(environment.run_id);
            TestRunAssetOwnership.RequireOwnedAssetPath(
                environment.owned_scene_path, allowRunScene: true);
            if (!string.Equals(environment.owned_scene_path, expectedOwnedPath,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "owned scene evidence does not match the run identity for run " +
                    environment.run_id);

            if (environment.scene_restore_delegated_to_utf)
            {
                if (environment.restore_single_untitled ||
                    (environment.scene_paths ?? Array.Empty<string>()).Length != 0 ||
                    !string.IsNullOrEmpty(environment.active_scene_path) ||
                    !string.IsNullOrEmpty(environment.untitled_scene_setup))
                    throw new InvalidOperationException(
                        "UTF-managed scene restoration evidence is contradictory for run " +
                        environment.run_id);
                return;
            }

            var paths = environment.scene_paths ?? Array.Empty<string>();
            if (environment.restore_single_untitled)
            {
                var knownSetup = environment.untitled_scene_setup ==
                                 TestRunProtocol.UntitledSceneSetup.Empty;
                if (!knownSetup || paths.Length != 0 ||
                    !string.IsNullOrEmpty(environment.active_scene_path))
                    throw new InvalidOperationException(
                        "untitled scene baseline evidence is incomplete or contradictory for run " +
                        environment.run_id);
                return;
            }

            if (paths.Length == 0 ||
                !string.IsNullOrEmpty(environment.untitled_scene_setup) ||
                string.IsNullOrEmpty(environment.active_scene_path) ||
                Array.IndexOf(paths, environment.active_scene_path) < 0)
                throw new InvalidOperationException(
                    "named scene baseline evidence is incomplete or contradictory for run " +
                    environment.run_id);

            foreach (var path in paths)
                RequireCanonicalBaselinePath(path);
        }

        internal static bool IsUtfManagedEditModeRun(TestRunRecord run) =>
            run != null && string.Equals(run.source, "unity-ui", StringComparison.Ordinal) &&
            string.Equals(run.mode, "EditMode", StringComparison.Ordinal);

        private static TestRunEnvironmentRecord PrepareUtfManagedBootstrap(
            TestRunStore store,
            string runId,
            string utcNow)
        {
            var bootstrapScenes = OrdinarySceneInventory.CaptureLoaded(
                "an invalid or unloaded scene prevents direct UTF EditMode isolation");
            if (bootstrapScenes.Count != 1)
                throw new InvalidOperationException(
                    "direct UTF EditMode isolation requires exactly one bootstrap scene");
            var bootstrap = SceneManager.GetActiveScene();
            if (bootstrap.handle != bootstrapScenes[0].handle ||
                !HasExactUtf16BootstrapFingerprint(bootstrap))
                throw new InvalidOperationException(
                    "direct UTF EditMode RunStarted did not expose the exact clean UTF 1.6 bootstrap");

            var initialAssetCleanup = TestRunAssetOwnership.CleanupForRun(runId, "");
            AppendCleanupWarning(store, runId, initialAssetCleanup, utcNow);
            var ownedPath = TestRunAssetOwnership.ExpectedRunScenePath(runId);
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ownedPath)))
                throw new InvalidOperationException("owned test scene already exists: " + ownedPath);

            var environment = new TestRunEnvironmentRecord
            {
                run_id = runId,
                owned_scene_path = ownedPath,
                scene_restore_delegated_to_utf = true,
                preview_scene_baseline_captured = true,
                preview_scene_count = PreviewSceneCountEvidence.Capture(),
                prepared_utc = utcNow
            };
            store.WriteEnvironment(environment);
            SessionState.SetString(TestRunAssetOwnership.OwnedRunIdSessionKey, runId);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, ownedPath)));
            var owned = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(owned, ownedPath))
                throw new InvalidOperationException(
                    "could not save direct UTF owned test scene: " + ownedPath);
            SessionState.SetString(OwnedSceneSessionKey, ownedPath);
            return environment;
        }

        private static void RestoreUtfManagedEnvironment(
            TestRunStore store,
            TestRunEnvironmentRecord environment,
            string utcNow)
        {
            RequireMainStage("restore");
            var openScenes = OrdinarySceneInventory.CaptureLoaded(
                "an invalid or unloaded scene prevents UTF-managed cleanup");
            foreach (var scene in openScenes)
            {
                if (string.Equals(scene.path, environment.owned_scene_path,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "UTF has not restored its original SceneSetup; refusing to close the " +
                        "runner-owned scene from the UnityMCP finalizer");
            }

            var assetCleanup = TestRunAssetOwnership.CleanupForRun(
                environment.run_id, "", allowCompileAffectingAssets: true);
            var previewViolation = PreviewSceneCountEvidence.RestoreRunBaseline(
                environment.preview_scene_baseline_captured,
                environment.preview_scene_count);
            environment.restored_utc = utcNow;
            store.WriteEnvironment(environment);
            ClearOwnershipHintsForRun(environment.run_id);
            AppendCleanupWarning(store, environment.run_id, assetCleanup, utcNow);
            if (!string.IsNullOrEmpty(previewViolation))
                AppendInfrastructureOnce(
                    store, environment.run_id, previewViolation, utcNow);
        }

        private static void ClearOwnershipHintsForRun(string runId)
        {
            if (!string.Equals(SessionState.GetString(
                    TestRunAssetOwnership.OwnedRunIdSessionKey, ""), runId,
                    StringComparison.Ordinal)) return;
            SessionState.EraseString(OwnedSceneSessionKey);
            SessionState.EraseString(TestRunAssetOwnership.OwnedRunIdSessionKey);
        }

        private static void RequireCanonicalBaselinePath(string path)
        {
            var segments = (path ?? "").Split('/');
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\\') >= 0 ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                segments.Length < 2 || Array.Exists(segments,
                    segment => string.IsNullOrEmpty(segment) || segment == "." || segment == "..") ||
                path.Equals(TestRunAssetOwnership.Root, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(TestRunAssetOwnership.Root + "/",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "baseline scene evidence contains an unsafe path: " + path);
        }

        internal static void RequireMainStage(string operation)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                StageUtility.GetCurrentStageHandle() != StageUtility.GetMainStageHandle())
                throw new InvalidOperationException(
                    "return to the main Unity stage before test environment " + operation);
        }

        internal static void RequireDisposableWorker(string operation)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ReadVerifiedDisposableWorkerMarker(projectRoot, operation);
        }

        private static void TryOpenDisposableWorkerBootstrap(string projectRoot)
        {
            var startupScenes = OrdinarySceneInventory.CaptureLoaded(
                "an invalid or unloaded scene prevents disposable-worker bootstrap");
            if (startupScenes.Count != 1) return;
            var startup = startupScenes[0];
            if (SceneManager.GetActiveScene().handle != startup.handle ||
                !string.IsNullOrEmpty(startup.path))
                return;

            var marker = ReadVerifiedDisposableWorkerMarker(
                projectRoot, "replace the untitled UTF bootstrap");
            if (!HasExactUtf16BootstrapFingerprint(startup))
                throw new InvalidOperationException(
                    "untitled scene does not match the pinned UTF 1.6 bootstrap fingerprint");

            RequireCanonicalBaselinePath(marker.bootstrap_scene);
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(marker.bootstrap_scene)) ||
                !File.Exists(Path.Combine(projectRoot, marker.bootstrap_scene)))
                throw new InvalidOperationException(
                    "disposable worker bootstrap scene is missing: " +
                    marker.bootstrap_scene);
            var opened = EditorSceneManager.OpenScene(
                marker.bootstrap_scene, OpenSceneMode.Single);
            if (!opened.IsValid() || !opened.isLoaded ||
                !string.Equals(opened.path, marker.bootstrap_scene,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "could not open disposable worker bootstrap scene: " +
                    marker.bootstrap_scene);
        }

        private static DisposableWorkerMarker ReadVerifiedDisposableWorkerMarker(
            string projectRoot,
            string operation)
        {
            var markerPath = Path.Combine(
                projectRoot, "Library", "UnityMCP", "disposable-worker.json");
            if (!File.Exists(markerPath))
                throw new InvalidOperationException(
                    operation + " is allowed only in a marked disposable worker");

            DisposableWorkerMarker marker;
            try
            {
                marker = JsonUtility.FromJson<DisposableWorkerMarker>(
                    File.ReadAllText(markerPath));
            }
            catch (Exception error)
            {
                throw new InvalidOperationException(
                    "disposable worker marker is unreadable", error);
            }
            if (marker == null || marker.schema_version != 1 || !marker.disposable ||
                string.IsNullOrEmpty(marker.unity_version) ||
                !marker.unity_version.StartsWith("6000.0.", StringComparison.Ordinal) ||
                string.IsNullOrEmpty(marker.unity_revision) ||
                !string.Equals(marker.utf_version,
                    TestRunBuildFingerprintProbe.CanonicalValidationUtfVersion,
                    StringComparison.Ordinal) ||
                string.IsNullOrEmpty(marker.bootstrap_scene))
                throw new InvalidOperationException(
                    "disposable worker marker is incomplete");

            var actualUtfVersion = TestRunBuildFingerprintProbe.Capture().UtfVersion;
            if (!string.Equals(Application.unityVersion, marker.unity_version,
                    StringComparison.Ordinal) ||
                !string.Equals(actualUtfVersion, marker.utf_version,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "disposable worker marker does not match the loaded Unity/UTF toolchain");
            var projectVersionPath = Path.Combine(
                projectRoot, "ProjectSettings", "ProjectVersion.txt");
            var expectedRevision = "m_EditorVersionWithRevision: " +
                marker.unity_version + " (" + marker.unity_revision + ")";
            if (!File.Exists(projectVersionPath) ||
                File.ReadAllText(projectVersionPath).IndexOf(
                    expectedRevision, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "disposable worker marker does not match ProjectVersion.txt");
            return marker;
        }

        internal static bool HasExactUtf16BootstrapFingerprint(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.isDirty || scene.isSubScene ||
                !string.IsNullOrEmpty(scene.path) || EditorSceneManager.IsPreviewScene(scene))
                return false;

            var roots = scene.GetRootGameObjects();
            if (roots.Length == 0) return true;
            if (EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D)
                return roots.Length == 1 && IsExactDefaultCamera(
                    roots[0], scene, new Vector3(0f, 0f, -10f), true);
            if (EditorSettings.defaultBehaviorMode != EditorBehaviorMode.Mode3D ||
                roots.Length != 2)
                return false;

            GameObject cameraRoot = null;
            GameObject lightRoot = null;
            foreach (var root in roots)
            {
                if (root != null && string.Equals(
                        root.name, "Main Camera", StringComparison.Ordinal))
                    cameraRoot = root;
                else if (root != null && string.Equals(
                             root.name, "Directional Light", StringComparison.Ordinal))
                    lightRoot = root;
                else
                    return false;
            }
            return IsExactDefaultCamera(
                       cameraRoot, scene, new Vector3(0f, 1f, -10f), false) &&
                   IsExactDefaultDirectionalLight(lightRoot, scene);
        }

        private static bool IsExactDefaultCamera(
            GameObject root,
            Scene scene,
            Vector3 expectedPosition,
            bool orthographic)
        {
            if (!IsExactRoot(root, scene, "Main Camera", "MainCamera",
                    expectedPosition, Quaternion.identity) ||
                !HasOnlyComponents(root,
                    typeof(Transform), typeof(Camera), typeof(AudioListener)))
                return false;

            var camera = root.GetComponent<Camera>();
            var listener = root.GetComponent<AudioListener>();
            return camera != null && listener != null && camera.enabled && listener.enabled &&
                   camera.orthographic == orthographic && camera.orthographicSize == 5f &&
                   camera.nearClipPlane == 0.3f && camera.farClipPlane == 1000f &&
                   camera.depth == -1f && camera.clearFlags == CameraClearFlags.Skybox &&
                   camera.cullingMask == -1 && camera.targetTexture == null &&
                   camera.targetDisplay == 0;
        }

        private static bool IsExactDefaultDirectionalLight(GameObject root, Scene scene)
        {
            if (!IsExactRoot(root, scene, "Directional Light", "Untagged",
                    new Vector3(0f, 3f, 0f), Quaternion.Euler(50f, -30f, 0f)) ||
                !HasOnlyComponents(root, typeof(Transform), typeof(Light)))
                return false;
            var light = root.GetComponent<Light>();
            return light != null && light.enabled && light.type == LightType.Directional &&
                   light.color == new Color(1f, 244f / 255f, 214f / 255f, 1f) &&
                   light.intensity == 1f &&
                   light.cullingMask == -1;
        }

        private static bool IsExactRoot(
            GameObject root,
            Scene scene,
            string expectedName,
            string expectedTag,
            Vector3 expectedPosition,
            Quaternion expectedRotation)
        {
            return root != null && root.scene.handle == scene.handle &&
                   string.Equals(root.name, expectedName, StringComparison.Ordinal) &&
                   root.CompareTag(expectedTag) && root.layer == 0 && root.activeSelf &&
                   !root.isStatic && root.hideFlags == HideFlags.None &&
                   root.transform.parent == null && root.transform.childCount == 0 &&
                   root.transform.localPosition == expectedPosition &&
                   Quaternion.Angle(root.transform.localRotation, expectedRotation) < 0.0001f &&
                   root.transform.localScale == Vector3.one;
        }

        private static bool HasOnlyComponents(GameObject root, params Type[] expected)
        {
            var components = root.GetComponents<Component>();
            if (components.Length != expected.Length ||
                Array.Exists(components, component => component == null))
                return false;
            foreach (var expectedType in expected)
            {
                var count = 0;
                foreach (var component in components)
                    if (component.GetType() == expectedType) count++;
                if (count != 1) return false;
            }
            return true;
        }

        [Serializable]
        private sealed class DisposableWorkerMarker
        {
            public int schema_version;
            public bool disposable;
            public string unity_version = "";
            public string unity_revision = "";
            public string utf_version = "";
            public string bootstrap_scene = "";
        }

        private static void AppendCleanupWarning(
            TestRunStore store,
            string runId,
            TestRunAssetCleanupReport report,
            string utcNow)
        {
            if (report == null || !report.HasWarning) return;
            var suffix = string.IsNullOrEmpty(report.QuarantinedLedgerPath)
                ? ""
                : "; quarantined at " + report.QuarantinedLedgerPath;
            AppendInfrastructureOnce(store, runId, report.Warning + suffix, utcNow);
        }

        private static void AppendInfrastructureOnce(
            TestRunStore store,
            string runId,
            string message,
            string utcNow)
        {
            foreach (var existing in store.ReadJournal(runId).events)
            {
                if (existing != null &&
                    existing.event_type == TestRunProtocol.EventType.InfrastructureError &&
                    string.Equals(existing.message, message, StringComparison.Ordinal))
                    return;
            }
            store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.InfrastructureError,
                occurred_utc = utcNow,
                message = message
            });
        }

        private static void ValidateBaselineScene(Scene scene, string projectRoot)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("an invalid or unloaded scene is open");
            if (scene.isDirty)
                throw new InvalidOperationException(
                    "scene has unsaved changes; save or discard them before tests: " + scene.path);
            if (string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException(
                    "an untitled scene is open; save or close it before tests");
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(scene.path)) ||
                !File.Exists(Path.Combine(projectRoot, scene.path)))
                throw new InvalidOperationException("scene asset is missing: " + scene.path);
            if (!scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                scene.isSubScene || EditorSceneManager.IsPreviewScene(scene))
                throw new InvalidOperationException(
                    "only ordinary main-stage .unity scenes can be a test baseline: " + scene.path);
            if (scene.path.Equals(TestRunAssetOwnership.Root, StringComparison.OrdinalIgnoreCase) ||
                scene.path.StartsWith(TestRunAssetOwnership.Root + "/",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "the test ownership root cannot be used as a user scene baseline: " + scene.path);
        }
    }
}
