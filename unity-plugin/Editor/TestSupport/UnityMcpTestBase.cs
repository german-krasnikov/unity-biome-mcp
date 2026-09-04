using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Testing
{
    /// <summary>
    /// Common isolation scope for every Unity MCP test fixture. Test-specific
    /// cleanup is registered as ownership. NUnit runs base setup before derived
    /// setup and base teardown after derived teardown. The non-virtual lifecycle
    /// methods keep the shared transaction independent from fixture hooks.
    /// </summary>
    public abstract class UnityMcpTestBase
    {
        private readonly List<Action> _cleanupActions = new List<Action>();
        private readonly List<Action> _assetCleanupActions = new List<Action>();
        private readonly Dictionary<string, EditorPrefValueKind> _ownedEditorPrefs =
            new Dictionary<string, EditorPrefValueKind>(StringComparer.Ordinal);
        private IDisposable _syncIsolation;
        private IDisposable _compileNotifierIsolation;
        private IDisposable _reloadGuardIsolation;
        private IDisposable _chatWindowIsolation;
        private bool _logAssertIgnoreBaseline;
        private CommandRegistry.TestSnapshot _commandRegistryBaseline;
        private IDisposable _settingsProviderIsolation;
        private IDisposable _toolbarButtonIsolation;
        private IDisposable _panelProviderIsolation;
        private IDisposable _chipKindIsolation;
        private IDisposable _pluginRegistryIsolation;
        private IDisposable _assetViewerFactoryIsolation;
        private IDisposable _previewBuilderRegistryIsolation;
        private IDisposable _toolCardRendererIsolation;
        private IDisposable _inlinePreviewBuilderIsolation;
        private IDisposable _mixedParagraphRendererIsolation;
        private IDisposable _chatSettingsHookIsolation;
        private IDisposable _updateCheckerIsolation;
        private IDisposable _relayIsolation;
        private IDisposable _relaySpawnIsolation;
        private int _previewSceneCountBaseline;
        private HashSet<EditorWindow> _editorWindowBaseline;
        private bool _isolationActive;
        private bool _cleanupStarted;
        private static int s_editorWindowBaselineCount = -1;
        private static HashSet<EditorWindow> s_editorWindowBaselineCache;
        protected static int EditorWindowBaselineRebuildCount;
        private Stopwatch _isolationStopwatch;

        [SetUp]
        public void BeginUnityMcpIsolation()
        {
            _isolationStopwatch = Stopwatch.StartNew();
            if (_isolationActive)
                throw new InvalidOperationException("The test isolation scope is already active.");

            RequireDisposableWorkerBoundary();
            UnityMcpManagedSceneSafety.RequirePreparedEnvironment();
            PreviewSceneCountEvidence.RequireActiveRunBaseline();
            var priorViolation = UnityMcpManagedSceneSafety.RepairAndDescribeViolation();
            var isolationOwnerId = CurrentIsolationOwnerId();
            var globalSeamViolation = RepairGlobalSeamsAndDescribeViolation(isolationOwnerId);
            var assetReport = TestRunAssetOwnership.CleanupForActiveRun(
                SessionState.GetString(UnityMcpManagedSceneSafety.OwnedSceneSessionKey, ""));
            if (!string.IsNullOrEmpty(priorViolation) ||
                !string.IsNullOrEmpty(globalSeamViolation) ||
                assetReport.HasWarning)
                throw new InvalidOperationException(
                    "Pre-test isolation recovered an interrupted test: " +
                    string.Join("; ", new[]
                    {
                        priorViolation,
                        globalSeamViolation,
                        assetReport.Warning
                    }
                        .Where(value => !string.IsNullOrEmpty(value))));

            _cleanupActions.Clear();
            _assetCleanupActions.Clear();
            _ownedEditorPrefs.Clear();
            _previewSceneCountBaseline = PreviewSceneCountEvidence.Capture();
            _logAssertIgnoreBaseline = LogAssert.ignoreFailingMessages;
            _cleanupStarted = false;
            _isolationActive = true;
            MainThreadDispatcher.Clear();
            try
            {
                _syncIsolation = SyncHelper.BeginTestIsolation();
                _compileNotifierIsolation = CompileNotifier.BeginTestIsolation();
                _commandRegistryBaseline = CommandRegistry.CaptureForTest();
                _settingsProviderIsolation = SettingsProviderRegistry.PreserveStateForTests();
                _toolbarButtonIsolation = ToolbarButtonRegistry.PreserveStateForTests();
                _panelProviderIsolation = PanelProviderRegistry.PreserveStateForTests();
                _chipKindIsolation = ChipKindRegistry.PreserveStateForTests();
                _pluginRegistryIsolation = PluginRegistry.PreserveStateForTests();
                // Accessing AssetViewerFactory first runs its static built-in registration;
                // every following snapshot therefore represents the initialized runtime.
                _assetViewerFactoryIsolation = AssetViewerFactory.PreserveStateForTests();
                _previewBuilderRegistryIsolation =
                    PreviewBuilderRegistry.PreserveStateForTests();
                _toolCardRendererIsolation = ToolCardRendererRegistry.PreserveStateForTests();
                _inlinePreviewBuilderIsolation = InlinePreviewBuilder.PreserveStateForTests();
                _mixedParagraphRendererIsolation =
                    MixedParagraphRenderer.PreserveStateForTests();
                _chatSettingsHookIsolation =
                    ChatSettingsHook.PreserveConnectionEventForTests();
                _updateCheckerIsolation = UpdateChecker.BeginTestIsolation();
                _relaySpawnIsolation = RelaySpawnState.BeginTestIsolation();
                _relayIsolation = RelaySpawner.BeginTestIsolation();
                _reloadGuardIsolation = ReloadGuard.BeginTestIsolation(
                    new IsolatedReloadGuardOps(), isolationOwnerId);
                _chatWindowIsolation = MCPChatWindow.BeginTestIsolation(isolationOwnerId);
                var openWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                if (s_editorWindowBaselineCache != null &&
                    openWindows.Length == s_editorWindowBaselineCount)
                {
                    _editorWindowBaseline = s_editorWindowBaselineCache;
                }
                else
                {
                    _editorWindowBaseline = new HashSet<EditorWindow>(openWindows);
                    EditorWindowBaselineRebuildCount++;
                }
            }
            catch (Exception setupError)
            {
                try
                {
                    EndUnityMcpIsolation();
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException(
                        "Test isolation setup and rollback both failed.",
                        setupError,
                        rollbackError);
                }
                throw;
            }
            RequireReadWriteBoundary();
        }

        /// <summary>
        /// Best-effort instrumentation: accumulates into the active run's
        /// summary.json (via TestRunStore.StampInstrumentation). A missing run id
        /// means there is no durable run to stamp into (e.g. a standalone manual
        /// test outside a durable dispatch) -- silently skipped, never thrown.
        /// </summary>
        private static void RecordBaseSetupMs(double elapsedMs)
        {
            var runId = SessionState.GetString(TestRunAssetOwnership.OwnedRunIdSessionKey, "");
            if (string.IsNullOrEmpty(runId)) return;
            var key = TestRunInstrumentationKeys.BaseSetupMs(runId);
            SessionState.SetFloat(key, SessionState.GetFloat(key, 0f) + (float)elapsedMs);
        }

        [TearDown]
        public void EndUnityMcpIsolation()
        {
            if (!_isolationActive || _cleanupStarted)
                return;

            _cleanupStarted = true;
            var errors = new List<Exception>();

            // A leaked ignore flag must not suppress failures emitted by fixture cleanup.
            // Restore the exact baseline later, after every cleanup stage has been observed.
            LogAssert.ignoreFailingMessages = false;

            RunCleanup(OnBeforeIsolationCleanup, "fixture cleanup hook", errors);
            RunCleanup(PrepareForOwnershipCleanup, "ownership cleanup preparation", errors);
            for (var i = _cleanupActions.Count - 1; i >= 0; i--)
                RunCleanup(_cleanupActions[i], $"registered cleanup #{i + 1}", errors);
            RunCleanup(PerformFinalIsolationCleanup, "final isolation cleanup", errors);
            RunCleanup(
                () => PreviewSceneCountEvidence.RequireBaseline(
                    _previewSceneCountBaseline),
                "preview scene isolation cleanup",
                errors);

            // Scene and asset rollback intentionally executes while every test seam remains
            // installed. Cleanup must never reach native reload/refresh or live runtime state.
            var canDeleteAssets = NormalizeForOwnershipCleanup(
                "pre-asset managed scene rollback", errors);
            if (canDeleteAssets)
            {
                for (var i = _assetCleanupActions.Count - 1; i >= 0; i--)
                    RunCleanup(_assetCleanupActions[i],
                        $"registered asset cleanup #{i + 1}", errors);
                RunCleanup(
                    () => RequireCleanAssetSweep(TestRunAssetOwnership.CleanupForActiveRun(
                        SessionState.GetString(
                            UnityMcpManagedSceneSafety.OwnedSceneSessionKey, ""))),
                    "durable owned-asset sweep", errors);
            }
            else
            {
                errors.Add(new InvalidOperationException(
                    "Tracked assets were not deleted because test scenes could not be detached safely."));
            }
            NormalizeForOwnershipCleanup("final managed test scene rollback", errors);

            // Unwind globals only after ownership cleanup, in exact reverse acquisition order.
            RunCleanup(
                () => _chatWindowIsolation?.Dispose(),
                "chat window runtime restoration",
                errors);
            RunCleanup(CloseLeakedEditorWindows, "editor window leak sweep", errors);
            RunCleanup(() => MainThreadDispatcher.Clear(), "main-thread dispatcher cleanup", errors);
            RunCleanup(
                () => _reloadGuardIsolation?.Dispose(),
                "reload guard test-isolation restoration",
                errors);
            RunCleanup(
                () => _relayIsolation?.Dispose(),
                "relay test-isolation restoration",
                errors);
            RunCleanup(
                () => _relaySpawnIsolation?.Dispose(),
                "relay spawn-state restoration",
                errors);
            RunCleanup(
                () => _updateCheckerIsolation?.Dispose(),
                "update checker state restoration",
                errors);
            RunCleanup(
                () => _chatSettingsHookIsolation?.Dispose(),
                "chat settings event restoration",
                errors);
            RunCleanup(
                () => _mixedParagraphRendererIsolation?.Dispose(),
                "mixed paragraph preview context restoration",
                errors);
            RunCleanup(
                () => _inlinePreviewBuilderIsolation?.Dispose(),
                "inline preview loader restoration",
                errors);
            RunCleanup(
                () => _toolCardRendererIsolation?.Dispose(),
                "tool card renderer registry restoration",
                errors);
            RunCleanup(
                () => _previewBuilderRegistryIsolation?.Dispose(),
                "preview builder registry restoration",
                errors);
            RunCleanup(
                () => _assetViewerFactoryIsolation?.Dispose(),
                "asset viewer registry restoration",
                errors);
            RunCleanup(
                () => _pluginRegistryIsolation?.Dispose(),
                "plugin registry restoration",
                errors);
            RunCleanup(
                () => _chipKindIsolation?.Dispose(),
                "chip provider registry restoration",
                errors);
            RunCleanup(
                () => _panelProviderIsolation?.Dispose(),
                "panel provider registry restoration",
                errors);
            RunCleanup(
                () => _toolbarButtonIsolation?.Dispose(),
                "toolbar provider registry restoration",
                errors);
            RunCleanup(
                () => _settingsProviderIsolation?.Dispose(),
                "settings provider registry restoration",
                errors);
            RunCleanup(
                () => CommandRegistry.RestoreForTest(_commandRegistryBaseline),
                "command registry restoration",
                errors);
            RunCleanup(
                CommandRouter.InvalidateEnabledToolsCache,
                "enabled-tool cache restoration",
                errors);
            RunCleanup(
                () => _compileNotifierIsolation?.Dispose(),
                "compile notifier state restoration",
                errors);
            RunCleanup(
                () => _syncIsolation?.Dispose(),
                "global sync state restoration",
                errors);
            RunCleanup(
                () => LogAssert.ignoreFailingMessages = _logAssertIgnoreBaseline,
                "Unity log assertion policy restoration",
                errors);
            RunCleanup(
                () => RecordBaseSetupMs(_isolationStopwatch?.Elapsed.TotalMilliseconds ?? 0),
                "base setup timing instrumentation",
                errors);

            _cleanupActions.Clear();
            _assetCleanupActions.Clear();
            _ownedEditorPrefs.Clear();
            _syncIsolation = null;
            _compileNotifierIsolation = null;
            _reloadGuardIsolation = null;
            _chatWindowIsolation = null;
            _commandRegistryBaseline = null;
            _settingsProviderIsolation = null;
            _toolbarButtonIsolation = null;
            _panelProviderIsolation = null;
            _chipKindIsolation = null;
            _pluginRegistryIsolation = null;
            _assetViewerFactoryIsolation = null;
            _previewBuilderRegistryIsolation = null;
            _toolCardRendererIsolation = null;
            _inlinePreviewBuilderIsolation = null;
            _mixedParagraphRendererIsolation = null;
            _chatSettingsHookIsolation = null;
            _updateCheckerIsolation = null;
            _relayIsolation = null;
            _relaySpawnIsolation = null;
            _previewSceneCountBaseline = 0;
            _editorWindowBaseline = null;
            _isolationStopwatch = null;
            _isolationActive = false;

            if (errors.Count > 0)
                throw new AggregateException(
                    $"{GetType().Name} cleanup failed in {errors.Count} operation(s).", errors);
        }

        /// <summary>Registers test-owned cleanup. Actions run once in reverse order.</summary>
        protected void RegisterCleanup(Action cleanup)
        {
            if (cleanup == null)
                throw new ArgumentNullException(nameof(cleanup));
            if (!_isolationActive || _cleanupStarted)
                throw new InvalidOperationException(
                    "Cleanup can only be registered while a test isolation scope is active.");

            _cleanupActions.Add(cleanup);
        }

        /// <summary>
        /// Waits for bounded Editor update ticks without depending on the runtime
        /// player loop. UTF 1.6 EditMode tests do not reliably advance
        /// Awaitable.NextFrameAsync.
        /// </summary>
        protected Task WaitForEditorUpdatesAsync(
            int updateCount = 1,
            double timeoutSeconds = 5.0)
        {
            if (updateCount < 1)
                throw new ArgumentOutOfRangeException(nameof(updateCount));
            if (timeoutSeconds <= 0 || double.IsNaN(timeoutSeconds) ||
                double.IsInfinity(timeoutSeconds))
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            if (!_isolationActive || _cleanupStarted)
                throw new InvalidOperationException(
                    "Editor update waits require an active test isolation scope.");

            const int pendingState = 0;
            const int completedState = 1;
            const int timedOutState = 2;
            const int teardownState = 3;
            var terminalState = pendingState;
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var remaining = updateCount;
            var deadline = EditorApplication.timeSinceStartup + timeoutSeconds;
            System.Threading.Timer timeout = null;
            EditorApplication.CallbackFunction onUpdate = null;
            onUpdate = () =>
            {
                if (Volatile.Read(ref terminalState) != pendingState)
                {
                    EditorApplication.update -= onUpdate;
                    timeout?.Dispose();
                    return;
                }
                if (EditorApplication.timeSinceStartup >= deadline)
                {
                    if (Interlocked.CompareExchange(
                            ref terminalState, timedOutState, pendingState) == pendingState)
                    {
                        EditorApplication.update -= onUpdate;
                        timeout?.Dispose();
                        completion.TrySetException(new TimeoutException(
                            $"Unity Editor did not produce {updateCount} update tick(s) " +
                            $"within {timeoutSeconds:0.###} seconds."));
                    }
                    return;
                }

                remaining--;
                if (remaining > 0) return;
                if (Interlocked.CompareExchange(
                        ref terminalState, completedState, pendingState) == pendingState)
                {
                    EditorApplication.update -= onUpdate;
                    timeout?.Dispose();
                    completion.TrySetResult(true);
                }
            };

            timeout = new System.Threading.Timer(
                _ =>
                {
                    if (Interlocked.CompareExchange(
                            ref terminalState, timedOutState, pendingState) != pendingState)
                        return;
                    completion.TrySetException(new TimeoutException(
                        $"Unity Editor did not produce {updateCount} update tick(s) " +
                        $"within {timeoutSeconds:0.###} seconds."));
                },
                null,
                TimeSpan.FromSeconds(timeoutSeconds),
                Timeout.InfiniteTimeSpan);
            RegisterCleanup(() =>
            {
                Interlocked.CompareExchange(
                    ref terminalState, teardownState, pendingState);
                EditorApplication.update -= onUpdate;
                timeout.Dispose();
                // Dispose does not wait for an already queued Timer callback. A
                // cancellation attempt closes the CAS-to-TCS publication window;
                // it harmlessly loses when success/timeout is already terminal.
                completion.TrySetCanceled();
            });
            EditorApplication.update += onUpdate;
            return completion.Task;
        }

        /// <summary>
        /// Takes typed ownership of an EditorPrefs key without changing it. Use this
        /// before production code that writes the key. The first ownership call
        /// snapshots both key existence and its exact typed value for automatic restore.
        /// </summary>
        protected void ProtectEditorPrefString(string key)
        {
            ProtectEditorPref(key, EditorPrefValueKind.String,
                prefKey => EditorPrefs.GetString(prefKey), EditorPrefs.SetString);
        }

        protected void ProtectEditorPrefBool(string key)
        {
            ProtectEditorPref(key, EditorPrefValueKind.Bool,
                prefKey => EditorPrefs.GetBool(prefKey), EditorPrefs.SetBool);
        }

        protected void ProtectEditorPrefInt(string key)
        {
            ProtectEditorPref(key, EditorPrefValueKind.Int,
                prefKey => EditorPrefs.GetInt(prefKey), EditorPrefs.SetInt);
        }

        protected void ProtectEditorPrefFloat(string key)
        {
            ProtectEditorPref(key, EditorPrefValueKind.Float,
                prefKey => EditorPrefs.GetFloat(prefKey), EditorPrefs.SetFloat);
        }

        protected void SetEditorPrefString(string key, string value)
        {
            ProtectEditorPrefString(key);
            EditorPrefs.SetString(key, value);
        }

        protected void SetEditorPrefBool(string key, bool value)
        {
            ProtectEditorPrefBool(key);
            EditorPrefs.SetBool(key, value);
        }

        protected void SetEditorPrefInt(string key, int value)
        {
            ProtectEditorPrefInt(key);
            EditorPrefs.SetInt(key, value);
        }

        protected void SetEditorPrefFloat(string key, float value)
        {
            ProtectEditorPrefFloat(key);
            EditorPrefs.SetFloat(key, value);
        }

        protected void DeleteEditorPrefString(string key)
        {
            ProtectEditorPrefString(key);
            EditorPrefs.DeleteKey(key);
        }

        protected void DeleteEditorPrefBool(string key)
        {
            ProtectEditorPrefBool(key);
            EditorPrefs.DeleteKey(key);
        }

        protected void DeleteEditorPrefInt(string key)
        {
            ProtectEditorPrefInt(key);
            EditorPrefs.DeleteKey(key);
        }

        protected void DeleteEditorPrefFloat(string key)
        {
            ProtectEditorPrefFloat(key);
            EditorPrefs.DeleteKey(key);
        }

        private void ProtectEditorPref<T>(
            string key,
            EditorPrefValueKind valueKind,
            Func<string, T> read,
            Action<string, T> write)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("An EditorPrefs key is required.", nameof(key));
            if (!_isolationActive || _cleanupStarted)
                throw new InvalidOperationException(
                    "EditorPrefs ownership requires an active test isolation scope.");

            if (_ownedEditorPrefs.TryGetValue(key, out var existingKind))
            {
                if (existingKind != valueKind)
                    throw new InvalidOperationException(
                        $"EditorPrefs key '{key}' is already owned as {existingKind}, " +
                        $"not {valueKind}.");
                return;
            }

            var existed = EditorPrefs.HasKey(key);
            var originalValue = existed ? read(key) : default;
            _ownedEditorPrefs.Add(key, valueKind);
            RegisterCleanup(() =>
            {
                if (existed)
                    write(key, originalValue);
                else
                    EditorPrefs.DeleteKey(key);
            });
        }

        protected T TrackOwnedObject<T>(T ownedObject) where T : UnityEngine.Object
        {
            if (ownedObject == null)
                throw new ArgumentNullException(nameof(ownedObject));

            RegisterCleanup(() =>
            {
                if (ownedObject != null)
                    UnityEngine.Object.DestroyImmediate(ownedObject);
            });
            return ownedObject;
        }

        /// <summary>
        /// Creates a test-owned EditorWindow without looking up or reusing a window
        /// that existed before the test. The common teardown destroys it even when
        /// derived setup, the test body, or derived teardown fails.
        /// </summary>
        protected T CreateOwnedEditorWindow<T>() where T : EditorWindow
        {
            var window = ScriptableObject.CreateInstance<T>();
            RegisterCleanup(() =>
            {
                if (window == null) return;
                try { window.Close(); }
                catch (Exception) { /* m_Parent null — window was never shown */ }
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
            });
            return window;
        }

        private void CloseLeakedEditorWindows()
        {
            if (_editorWindowBaseline == null) return;
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (w == null || _editorWindowBaseline.Contains(w)) continue;
                try { w.Close(); }
                catch (Exception) { /* m_Parent null — window was never shown */ }
                if (w != null) UnityEngine.Object.DestroyImmediate(w);
            }
            // Cache the post-cleanup baseline so the next test's SetUp can skip
            // rebuilding the HashSet when the window count has not changed.
            s_editorWindowBaselineCache = _editorWindowBaseline;
            s_editorWindowBaselineCount = _editorWindowBaseline.Count;
            _editorWindowBaseline = null;
        }

        /// <summary>
        /// Reads a source file from the package that owns <paramref name="assemblyAnchor"/>.
        /// Source-inspection tests must resolve through UPM so they behave identically for
        /// embedded packages and LocalPackages copies.
        /// </summary>
        protected static string ReadRequiredPackageSource(
            Type assemblyAnchor,
            string packageRelativePath)
        {
            Assert.That(assemblyAnchor, Is.Not.Null, "A package assembly anchor is required.");
            Assert.That(packageRelativePath, Is.Not.Null.And.Not.Empty,
                "A package-relative source path is required.");
            Assert.That(Path.IsPathRooted(packageRelativePath), Is.False,
                $"Package source path must be relative: {packageRelativePath}");

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                assemblyAnchor.Assembly);
            Assert.That(package, Is.Not.Null,
                $"UPM package not found for assembly {assemblyAnchor.Assembly.GetName().Name}.");
            Assert.That(package.resolvedPath, Is.Not.Null.And.Not.Empty,
                $"UPM package has no resolved path for assembly {assemblyAnchor.Assembly.GetName().Name}.");

            var packageRoot = Path.GetFullPath(package.resolvedPath);
            Assert.That(Directory.Exists(packageRoot), Is.True,
                $"Resolved UPM package root does not exist: {packageRoot}");

            var sourcePath = Path.GetFullPath(Path.Combine(packageRoot, packageRelativePath));
            var rootPrefix = packageRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            Assert.That(sourcePath.StartsWith(rootPrefix, pathComparison), Is.True,
                $"Package source path escapes package root: {packageRelativePath}");
            Assert.That(File.Exists(sourcePath), Is.True,
                $"Required package source does not exist: {sourcePath}");
            return File.ReadAllText(sourcePath);
        }

        protected Scene TrackOwnedScene(Scene ownedScene)
        {
            if (!ownedScene.IsValid())
                throw new ArgumentException("The owned scene must be valid.", nameof(ownedScene));

            var isPreviewScene = EditorSceneManager.IsPreviewScene(ownedScene);

            var runScenePath = SessionState.GetString(
                UnityMcpManagedSceneSafety.OwnedSceneSessionKey, "");
            var runScene = string.IsNullOrEmpty(runScenePath)
                ? default
                : SceneManager.GetSceneByPath(runScenePath);
            if ((!string.IsNullOrEmpty(ownedScene.path) &&
                 string.Equals(ownedScene.path, runScenePath,
                     StringComparison.OrdinalIgnoreCase)) ||
                (runScene.IsValid() && runScene == ownedScene))
                throw new InvalidOperationException(
                    "A fixture cannot take ownership of the run-level scene transaction.");

            RegisterCleanup(() =>
            {
                if (!ownedScene.IsValid() || !ownedScene.isLoaded) return;
                if (isPreviewScene)
                {
                    if (!EditorSceneManager.ClosePreviewScene(ownedScene))
                        throw new InvalidOperationException(
                            "Unity refused to close a fixture-owned preview scene.");
                }
                else
                    EditorSceneManager.CloseScene(ownedScene, true);
            });
            return ownedScene;
        }

        /// <summary>
        /// Creates an empty additive scene under the common isolation transaction.
        /// Fixture lifecycle code must not manipulate Unity's global scene state directly.
        /// </summary>
        protected Scene CreateOwnedAdditiveScene()
        {
            return TrackOwnedScene(EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive));
        }

        /// <summary>
        /// Creates and immediately registers a preview scene. Unity exposes no API
        /// for enumerating a lost preview-scene handle, so tests must use this
        /// factory instead of calling EditorSceneManager.NewPreviewScene directly.
        /// </summary>
        protected Scene CreateOwnedPreviewScene()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                return TrackOwnedScene(preview);
            }
            catch (Exception ownershipError)
            {
                if (!preview.IsValid() || !preview.isLoaded ||
                    EditorSceneManager.ClosePreviewScene(preview))
                    throw;
                throw new AggregateException(
                    "Preview scene ownership registration failed and Unity refused " +
                    "to close the exact scene.", ownershipError);
            }
        }

        protected string TrackOwnedAsset(string ownedAssetPath)
        {
            if (!_isolationActive || _cleanupStarted)
                throw new InvalidOperationException(
                    "Asset cleanup can only be registered while a test isolation scope is active.");

            var canonical = TestRunAssetOwnership.RegisterForActiveRun(ownedAssetPath);
            _assetCleanupActions.Add(
                () => UnityMcpTestAssetOwnership.DeleteOwnedAsset(canonical));
            return canonical;
        }

        /// <summary>
        /// Restores the run-owned scene immediately. Scene-oriented compatibility
        /// bases may expose this under their existing reset method; ordinary tests
        /// should rely on the automatic final cleanup.
        /// </summary>
        protected void ResetManagedTestScene()
        {
            UnityMcpManagedSceneSafety.NormalizeAfterTest();
        }

        protected virtual void OnBeforeIsolationCleanup()
        {
        }

        protected virtual void PrepareForOwnershipCleanup()
        {
        }

        protected virtual void PerformFinalIsolationCleanup()
        {
        }

        private enum EditorPrefValueKind
        {
            String,
            Bool,
            Int,
            Float
        }

        private static bool RunCleanup(
            Action cleanup,
            string label,
            ICollection<Exception> errors)
        {
            try
            {
                cleanup();
                return true;
            }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException($"Failed during {label}.", ex));
                return false;
            }
        }

        private static string CurrentIsolationOwnerId()
        {
            var test = TestContext.CurrentContext.Test;
            if (!string.IsNullOrEmpty(test.ID)) return test.ID;
            if (!string.IsNullOrEmpty(test.FullName)) return test.FullName;
            throw new InvalidOperationException(
                "NUnit did not provide an identity for the current test isolation scope.");
        }

        private void RequireReadWriteBoundary()
        {
            var reason = EnforceReadWriteRequirement(
                GetType(),
                TestContext.CurrentContext.Test.MethodName,
                CommandRouter.IsReadOnly);
            if (reason != null)
                Assert.Ignore(reason);
        }

        /// <summary>
        /// Returns the ignore-reason string when the fixture or method requires a
        /// read-write worker and <paramref name="isReadOnly"/> returns true.
        /// Returns null when the test should proceed. Throws nothing — the caller
        /// decides whether to call Assert.Ignore (runtime) or assert on the result (unit test).
        /// </summary>
        public static string EnforceReadWriteRequirement(
            Type fixtureType,
            string methodName,
            Func<bool> isReadOnly)
        {
            var attr = fixtureType
                .GetCustomAttributes(typeof(RequiresReadWriteAttribute), true)
                .OfType<RequiresReadWriteAttribute>()
                .FirstOrDefault();
            if (attr == null && !string.IsNullOrEmpty(methodName))
            {
                attr = fixtureType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
                    .SelectMany(m => m
                        .GetCustomAttributes(typeof(RequiresReadWriteAttribute), true)
                        .OfType<RequiresReadWriteAttribute>())
                    .FirstOrDefault();
            }

            if (attr != null && isReadOnly())
                return $"ReadOnly worker — requires mutations: {attr.Reason}";
            return null;
        }

        private void RequireDisposableWorkerBoundary()
        {
            var fixtureType = GetType();
            var policy = fixtureType
                .GetCustomAttributes(typeof(BiomeWorkerOnlyAttribute), true)
                .OfType<BiomeWorkerOnlyAttribute>()
                .FirstOrDefault();
            var methodName = TestContext.CurrentContext.Test.MethodName;
            if (policy == null && !string.IsNullOrEmpty(methodName))
            {
                policy = fixtureType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                BindingFlags.Public |
                                BindingFlags.NonPublic)
                    .Where(method => string.Equals(
                        method.Name, methodName, StringComparison.Ordinal))
                    .SelectMany(method => method
                        .GetCustomAttributes(typeof(BiomeWorkerOnlyAttribute), true)
                        .OfType<BiomeWorkerOnlyAttribute>())
                    .FirstOrDefault();
            }

            if (policy != null)
                UnityTestRunEnvironmentController.RequireDisposableWorker(
                    "worker-only test '" + TestContext.CurrentContext.Test.FullName +
                    "' (" + policy.Reason + ")");
        }

        private static string RepairGlobalSeamsAndDescribeViolation(string isolationOwnerId)
        {
            var violations = new List<string>();
            var current = SyncHelper.Ops;
            if (!(current is UnitySyncOps))
            {
                SyncHelper.RestoreOpsForTest(new UnitySyncOps());
                violations.Add("SyncHelper.Ops retained test double " +
                    (current?.GetType().FullName ?? "<null>"));
            }

            try
            {
                if (MCPChatWindow.RepairOrphanedTestIsolation(isolationOwnerId))
                    violations.Add(
                        "MCPChatWindow retained test isolation owned by an earlier test");
            }
            catch (Exception error)
            {
                violations.Add("MCPChatWindow orphan repair failed: " + error.Message);
            }

            if (ReloadGuard.RepairOrphanedTestIsolation(isolationOwnerId))
                violations.Add("ReloadGuard retained test isolation owned by an earlier test");

            var nestedTestScope = ReloadGuard.IsTestIsolationOwnedBy(isolationOwnerId);
            if (ReloadGuard.IsLocked || ReloadGuard.HasPersistedLock)
            {
                ReloadGuard.ResetForTest();
                violations.Add("ReloadGuard retained a lock from an earlier test");
            }
            var reloadOps = ReloadGuard.Ops;
            if (!nestedTestScope && !(reloadOps is UnityReloadGuardOps))
            {
                ReloadGuard.RestoreOpsForTest(new UnityReloadGuardOps());
                violations.Add("ReloadGuard.Ops retained test double " +
                    (reloadOps?.GetType().FullName ?? "<null>"));
            }
            if (!nestedTestScope && !string.Equals(
                    ReloadGuard.FilePath,
                    Path.Combine("Library", "MCP_ChatPendingTurn.txt"),
                    StringComparison.Ordinal))
            {
                ReloadGuard.RestoreDefaultFilePathForTest();
                violations.Add("ReloadGuard retained a test state path");
            }
            if (LogAssert.ignoreFailingMessages)
            {
                LogAssert.ignoreFailingMessages = false;
                violations.Add("LogAssert.ignoreFailingMessages remained enabled");
            }
            if (!CommandRegistry.Ready)
            {
                CommandRegistry.Clear();
                CommandRegistry.InitDefaults();
                if (!Application.isBatchMode)
                    violations.Add("CommandRegistry was not ready and required reinitialization");
            }

            return string.Join("; ", violations);
        }

        private sealed class IsolatedReloadGuardOps : IReloadGuardTestOps
        {
            private readonly HashSet<EditorApplication.CallbackFunction> _watchdogs =
                new HashSet<EditorApplication.CallbackFunction>();

            public double TimeSinceStartup => EditorApplication.timeSinceStartup;
            public void DisallowAutoRefresh() { }
            public void AllowAutoRefresh() { }
            public void LockReloadAssemblies() { }
            public void UnlockReloadAssemblies() { }
            public void RefreshAssets() { }
            public void ScheduleRefresh() { }
            public void AddWatchdog(EditorApplication.CallbackFunction callback) =>
                _watchdogs.Add(callback);
            public void RemoveWatchdog(EditorApplication.CallbackFunction callback) =>
                _watchdogs.Remove(callback);
        }

        private static bool NormalizeForOwnershipCleanup(
            string label,
            ICollection<Exception> errors)
        {
            try
            {
                var violation = UnityMcpManagedSceneSafety.RepairAndDescribeViolation();
                if (!string.IsNullOrEmpty(violation))
                    errors.Add(new InvalidOperationException(
                        $"Isolation policy violation during {label}: {violation}"));
                return true;
            }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException($"Failed during {label}.", ex));
                return false;
            }
        }

        private static void RequireCleanAssetSweep(TestRunAssetCleanupReport report)
        {
            if (report != null && report.HasWarning)
                throw new InvalidDataException(report.Warning);
        }
    }

    public static class UnityMcpTestAssetOwnership
    {
        public const string Root = TestRunAssetOwnership.Root;

        public static bool IsOwnedPath(string assetPath)
        {
            try
            {
                TestRunAssetOwnership.RequireOwnedAssetPath(assetPath);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static void RequireOwnedPath(string assetPath)
        {
            TestRunAssetOwnership.RequireOwnedAssetPath(assetPath);
        }

        public static string EnsureOwnedRoot()
        {
            return TestRunAssetOwnership.EnsureRoot();
        }

        public static void DeleteOwnedAsset(string assetPath)
        {
            TestRunAssetOwnership.DeleteOwnedAsset(assetPath);
        }
    }

    /// <summary>
    /// Restores the runner-owned scene before UTF's own scene tasks execute. The
    /// durable runner sets the SessionState hint only after a clean preflight, so
    /// every currently loaded scene is test-owned for the lifetime of that run.
    /// </summary>
    internal static class UnityMcpManagedSceneSafety
    {
        internal const string OwnedSceneSessionKey =
            "UnityMCP_active_owned_test_scene_v1";

        /// <summary>
        /// Best-effort instrumentation: see UnityMcpTestBase.RecordBaseSetupMs for
        /// the missing-run-id skip rationale.
        /// </summary>
        private static void RecordSceneRepair(bool wasFullRestore)
        {
            var runId = SessionState.GetString(
                TestRunAssetOwnership.OwnedRunIdSessionKey, "");
            if (string.IsNullOrEmpty(runId)) return;
            var key = wasFullRestore
                ? TestRunInstrumentationKeys.SceneRepairFull(runId)
                : TestRunInstrumentationKeys.SceneRepairs(runId);
            SessionState.SetInt(key, SessionState.GetInt(key, 0) + 1);
        }

        internal static void RequirePreparedEnvironment()
        {
            var ownedPath = SessionState.GetString(OwnedSceneSessionKey, "");
            if (string.IsNullOrEmpty(ownedPath))
                throw new InvalidOperationException(
                    "The UTF run has no prepared UnityMCP scene transaction. " +
                    "Refusing to execute a test against user scene state.");

            TestRunAssetOwnership.RequireActiveRunScenePath(ownedPath);
        }

        internal static void NormalizeAfterTest()
        {
            var violation = RepairAndDescribeViolation();
            if (!string.IsNullOrEmpty(violation))
                throw new InvalidOperationException("Test scene isolation violation: " + violation);
        }

        internal static string RepairAndDescribeViolation()
        {
            RequirePreparedEnvironment();
            var ownedPath = SessionState.GetString(OwnedSceneSessionKey, "");
            var assetWasMissing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ownedPath) == null;
            var loaded = CaptureLoadedScenes();
            var dirtyUnowned = new List<string>();
            var recoveryPaths = new List<string>();
            var alreadyClean = !assetWasMissing && loaded.Count == 1;
            var canDestroyRootsOnly = !assetWasMissing && loaded.Count == 1;

            foreach (var scene in loaded)
            {
                var isRunOwnedScene = string.Equals(
                    scene.path, ownedPath, StringComparison.Ordinal);
                if (!isRunOwnedScene || scene.isDirty || scene.GetRootGameObjects().Length != 0)
                    alreadyClean = false;
                if (!isRunOwnedScene || scene.isDirty || scene.GetRootGameObjects().Length == 0)
                    canDestroyRootsOnly = false;
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path) &&
                    !isRunOwnedScene &&
                    !UnityMcpTestAssetOwnership.IsOwnedPath(scene.path))
                    dirtyUnowned.Add(scene.path);
            }

            if (alreadyClean)
            {
                Undo.ClearAll();
            }
            else if (canDestroyRootsOnly)
            {
                // Single owned, non-dirty scene with leftover roots: destroying them
                // in place is equivalent to (and far cheaper than) the full
                // NewScene + close/reopen restore below.
                foreach (var root in loaded[0].GetRootGameObjects())
                    UnityEngine.Object.DestroyImmediate(root);
                Undo.ClearAll();
                RecordSceneRepair(wasFullRestore: false);
            }
            else
            {
                recoveryPaths.AddRange(RestoreOwnedScene(loaded, ownedPath));
                RecordSceneRepair(wasFullRestore: true);
            }

            var violations = new List<string>();
            if (assetWasMissing)
                violations.Add("the run-owned scene asset was deleted and had to be recreated");
            if (dirtyUnowned.Count > 0)
                violations.Add("changes outside the ownership root required recovery: " +
                    string.Join(", ", dirtyUnowned));
            if (recoveryPaths.Count > 0)
                violations.Add("recovery copies were retained at: " +
                    string.Join(", ", recoveryPaths));
            return violations.Count == 0 ? "" : string.Join("; ", violations);
        }

        private static List<Scene> CaptureLoadedScenes()
        {
            var loaded = OrdinarySceneInventory.CaptureLoaded(
                "An invalid or unloaded scene prevents managed test cleanup.");
            if (loaded.Count == 0)
                throw new InvalidOperationException(
                    "No ordinary loaded scene is available for managed test cleanup.");
            return loaded;
        }

        private static IReadOnlyList<string> RestoreOwnedScene(
            IReadOnlyList<Scene> loaded,
            string ownedPath)
        {
            var runId = SessionState.GetString(
                TestRunAssetOwnership.OwnedRunIdSessionKey, "");
            var recoveryPlan = TestSceneRecovery.PrepareForDiscard(loaded, runId);
            var keeper = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var errors = new List<Exception>();
            Scene restored = default;
            try
            {
                foreach (var scene in loaded)
                {
                    try
                    {
                        if (scene.IsValid() && scene.isLoaded &&
                            !EditorSceneManager.CloseScene(scene, true))
                            throw new InvalidOperationException(
                                "Unity refused to close scene '" +
                                (string.IsNullOrEmpty(scene.path) ? "<untitled>" : scene.path) + "'.");
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }

                if (errors.Count > 0)
                    throw new AggregateException(
                        "Could not discard the managed test scene setup.", errors);

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ownedPath);
                if (sceneAsset != null)
                    restored = EditorSceneManager.OpenScene(ownedPath, OpenSceneMode.Additive);
                else
                    restored = SaveKeeperAsOwnedScene(ref keeper, ownedPath);

                if (!restored.IsValid() || !restored.isLoaded)
                    throw new InvalidOperationException(
                        $"Could not reopen managed test scene '{ownedPath}'.");

                if (restored.isDirty || restored.GetRootGameObjects().Length != 0)
                {
                    if (!EditorSceneManager.CloseScene(restored, true))
                        throw new InvalidOperationException(
                            $"Could not discard polluted managed scene '{ownedPath}'.");
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ownedPath) != null &&
                        !AssetDatabase.DeleteAsset(ownedPath))
                        throw new InvalidOperationException(
                            $"Could not delete polluted managed scene '{ownedPath}'.");
                    restored = SaveKeeperAsOwnedScene(ref keeper, ownedPath);
                }

                var currentActive = SceneManager.GetActiveScene();
                if (restored.isDirty || restored.GetRootGameObjects().Length != 0 ||
                    (currentActive != restored &&
                     !SceneManager.SetActiveScene(restored)))
                    throw new InvalidOperationException(
                        $"Could not restore clean managed test scene '{ownedPath}'.");

                if (keeper.IsValid() && keeper.isLoaded &&
                    !EditorSceneManager.CloseScene(keeper, true))
                    throw new InvalidOperationException(
                        "Could not close the managed scene cleanup keeper.");

                foreach (var temporaryPath in recoveryPlan.TemporaryPaths)
                    UnityMcpTestAssetOwnership.DeleteOwnedAsset(temporaryPath);
                Undo.ClearAll();
                return recoveryPlan.RecoveryPaths;
            }
            catch
            {
                if (keeper.IsValid() && keeper.isLoaded)
                {
                    SceneManager.SetActiveScene(keeper);
                    if (restored.IsValid() && restored.isLoaded)
                        EditorSceneManager.CloseScene(restored, true);
                }
                throw;
            }
        }

        private static Scene SaveKeeperAsOwnedScene(ref Scene keeper, string ownedPath)
        {
            EnsureOwnedDirectory();
            if (!keeper.IsValid() || !keeper.isLoaded || keeper.isDirty ||
                keeper.GetRootGameObjects().Length != 0)
                throw new InvalidOperationException(
                    "The managed scene cleanup keeper is not clean and empty.");
            if (!EditorSceneManager.SaveScene(keeper, ownedPath))
                throw new InvalidOperationException(
                    $"Could not recreate clean managed test scene '{ownedPath}'.");
            var restored = keeper;
            keeper = default;
            return restored;
        }

        private static void EnsureOwnedDirectory()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Directory.CreateDirectory(Path.Combine(projectRoot,
                UnityMcpTestAssetOwnership.Root));
        }
    }
}
