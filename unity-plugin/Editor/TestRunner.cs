using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#if UNITY_INCLUDE_TESTS
using UnityEditor.TestTools.TestRunner.Api;
#endif

namespace UnityMCP.Editor
{
    public static class TestRunner
    {
        private static int _isRunning;
        private static double _runStartedAt;
        internal const string KeyPending = "UnityMCP_tests_pending";
        internal const string KeyResults = "UnityMCP_test_results";
        internal const string KeyProgress = "UnityMCP_test_progress";
        private const string KeyStartTime = "UnityMCP_tests_start";
        private const string KeyTestCount = "UnityMCP_test_count";
        private const string KeyCountDiscovering = "UnityMCP_count_discovering";
        internal const string TempScenePath = "Assets/TestsTemp/__mcp_test_temp.unity";
        private const double StaleTimeoutSec = 600.0; // 10 min max test run

        // Testable seam — override in tests to avoid Editor-uptime dependency
        internal static Func<double> GetTimeSinceStartup = () => EditorApplication.timeSinceStartup;
        internal static Func<bool> GetIsCompiling    = () => EditorApplication.isCompiling;
        internal static Func<bool> GetIsCompileClean = () => SyncHelper.IsCompileClean;

        internal static bool IsRunning => _isRunning == 1
            || SessionState.GetBool(KeyPending, false);

        [InitializeOnLoadMethod]
        private static void ResetOnReload()
        {
            _isRunning = 0;
            // Restore persisted results instead of unconditionally clearing.
            // Domain reload wipes volatile SessionState; file-backed results survive.
            RestorePersistedResults();
            // Clear cached count — new assemblies may have been compiled.
            SessionState.SetString(KeyTestCount, "");
            SessionState.SetBool(KeyCountDiscovering, false);
#if UNITY_INCLUDE_TESTS
            // Re-register callbacks if tests were running when domain reload occurred.
            // Unity Test Framework preserves execution state; only our callbacks are lost.
            if (!SessionState.GetBool(KeyPending, false)) return;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultCollector(null, api, true));
#endif
        }

        /// <summary>Returns stored test results, "pending" if running, or "none" if no run.</summary>
        public static string GetResults()
        {
            if (SessionState.GetBool(KeyPending, false))
            {
                // Clear stale pending flag (crashed/cancelled test run)
                var start = SessionState.GetFloat(KeyStartTime, 0f);
                if (start > 0f && GetTimeSinceStartup() - start > StaleTimeoutSec)
                {
                    SessionState.SetBool(KeyPending, false);
                    return "none (stale pending cleared)";
                }
                return "pending";
            }
            var r = SessionState.GetString(KeyResults, "");
            return string.IsNullOrEmpty(r) ? "none" : r;
        }

        /// <summary>Returns real-time test progress: "idle", "pending|no-progress-yet", or "running|ran|passed|failed|skipped|total|elapsed|eta=Ns".</summary>
        public static string GetProgress()
        {
            if (!SessionState.GetBool(KeyPending, false))
                return "idle";
            var p = SessionState.GetString(KeyProgress, "");
            if (string.IsNullOrEmpty(p))
                return "pending|no-progress-yet";
            var parts = p.Split('|');
            if (parts.Length >= 6
                && int.TryParse(parts[0], out var ran)
                && int.TryParse(parts[4], out var total)
                && double.TryParse(parts[5], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var elapsed)
                && ran > 0 && total > 0)
            {
                var rate = elapsed / ran;
                var remaining = (total - ran) * rate;
                return $"running|{p}|eta={remaining:F0}s";
            }
            return $"running|{p}";
        }

        /// <summary>Restores last persisted test results into SessionState (called on domain reload).</summary>
        internal static void RestorePersistedResults()
        {
            var persisted = TestResultPersistence.Load();
            SessionState.SetString(KeyResults, persisted ?? "");
        }

        /// <summary>Parses a raw Execute() completion result into (ok, text) for the async
        /// command router, ending the current undo group unconditionally first (C7a, review
        /// sprint v0.70 — extracted from CommandRouter.AsyncRunTests' completion callback).</summary>
        internal static (bool ok, string text) FinishRun(string result)
        {
            UndoGroupHelper.EndGroup();
            return result.StartsWith("Error:") ? (false, result.Substring(7)) : (true, result);
        }

#if UNITY_INCLUDE_TESTS
        public static void Execute(string mode, Action<string> onComplete, string group = null, string filter = null)
        {
            if (GetIsCompiling())
            {
                onComplete?.Invoke("Error: compilation in progress — poll sync_status and retry after compile completes");
                return;
            }

            if (!GetIsCompileClean())
            {
                onComplete?.Invoke("Error: domain reload pending — poll sync_status and retry after reload completes");
                return;
            }

            if (_isRunning == 1 && GetTimeSinceStartup() - _runStartedAt > 120.0)
                _isRunning = 0;

            if (System.Threading.Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                onComplete("Error: test run already in progress");
                return;
            }
            _runStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetBool(KeyPending, true);
            SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);
            SessionState.SetString(KeyResults, "");

            try
            {
                // Unity Test Framework calls SaveCurrentModifiedScenesIfUserWantsTo
                // which shows a modal dialog on dirty untitled scenes. Pre-save to avoid.
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (scene.isDirty && string.IsNullOrEmpty(scene.path))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/TestsTemp"))
                        AssetDatabase.CreateFolder("Assets", "TestsTemp");
                    EditorSceneManager.SaveScene(scene, TempScenePath);
                }
                else if (scene.isDirty)
                    EditorSceneManager.SaveScene(scene);

                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                var collector = new ResultCollector(onComplete, api);
                api.RegisterCallbacks(collector);

                var f = new Filter { testMode = ParseMode(mode) };
                if (!string.IsNullOrEmpty(group)) f.groupNames = new[] { group };
                if (!string.IsNullOrEmpty(filter)) f.groupNames = filter.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                api.Execute(new ExecutionSettings(f));
            }
            catch (Exception e)
            {
                System.Threading.Interlocked.Exchange(ref _isRunning, 0);
                SessionState.SetBool(KeyPending, false);
                SessionState.SetFloat(KeyStartTime, 0f);
                onComplete($"Error: {e.Message}");
            }
        }

        private static TestMode ParseMode(string mode)
        {
            if (string.IsNullOrEmpty(mode) || mode == "EditMode")
                return TestMode.EditMode;
            if (mode == "PlayMode")
                return TestMode.PlayMode;
            Debug.LogWarning($"[MCP] Unknown test mode '{mode}', defaulting to EditMode");
            return TestMode.EditMode;
        }

        private class ResultCollector : ICallbacks
        {
            private readonly Action<string> _onComplete;
            private readonly TestRunnerApi _api;
            private readonly bool _destroyApi;
            private readonly List<TestCaseResult> _results = new List<TestCaseResult>();
            private DateTime _startTime;
            private int _totalCount;

            private struct TestCaseResult
            {
                public string Name;
                public string Status;
                public double Duration;
                public string Message;
            }

            public ResultCollector(Action<string> onComplete, TestRunnerApi api, bool destroyApi = false)
            {
                _onComplete = onComplete;
                _api = api;
                _destroyApi = destroyApi;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                _startTime = DateTime.Now;
                _totalCount = testsToRun.TestCaseCount;
                SessionState.SetString(KeyProgress, $"0|0|0|0|{_totalCount}|0.0");
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite)
                {
                    _results.Add(new TestCaseResult
                    {
                        Name = result.Test.FullName,
                        Status = result.TestStatus.ToString(),
                        Duration = result.Duration,
                        Message = result.Message
                    });
                    var ran = _results.Count;
                    var passed = _results.Count(r => r.Status == "Passed");
                    var failed = _results.Count(r => r.Status == "Failed");
                    var skipped = _results.Count(r => r.Status == "Skipped");
                    var elapsed = (DateTime.Now - _startTime).TotalSeconds;
                    SessionState.SetString(KeyProgress,
                        $"{ran}|{passed}|{failed}|{skipped}|{_totalCount}|{elapsed:F1}");
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _api.UnregisterCallbacks(this);
                if (_destroyApi) UnityEngine.Object.DestroyImmediate(_api);
                System.Threading.Interlocked.Exchange(ref _isRunning, 0);
                var formatted = FormatResults();
                SessionState.SetString(KeyResults, formatted);
                SessionState.SetString(KeyProgress, "");
                SessionState.SetBool(KeyPending, false);
                SessionState.SetFloat(KeyStartTime, 0f);
                TestResultPersistence.Save(formatted);
                try { _onComplete?.Invoke(formatted); }
                catch (Exception e) { Debug.LogException(e); }
                EditorApplication.delayCall += DeleteTempScene;
            }

            private static void DeleteTempScene()
            {
                // Replace active scene if still pointing at temp file (guard against dirty-scene dialog)
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.path == TempScenePath)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (AssetDatabase.AssetPathToGUID(TempScenePath) != "")
                    AssetDatabase.DeleteAsset(TempScenePath);
            }

            private string FormatResults()
            {
                var elapsed = (DateTime.Now - _startTime).TotalSeconds;
                var passed = _results.Count(r => r.Status == "Passed");
                var failed = _results.Count(r => r.Status == "Failed");
                var skipped = _results.Count(r => r.Status == "Skipped");

                var sb = new StringBuilder();
                sb.AppendFormat("{0} tests: {1} passed", _results.Count, passed);
                if (failed > 0) sb.AppendFormat(", {0} FAILED", failed);
                if (skipped > 0) sb.AppendFormat(", {0} skipped", skipped);
                sb.AppendFormat(" ({0:F1}s)", elapsed);

                foreach (var r in _results.Where(r => r.Status == "Failed"))
                {
                    sb.AppendLine();
                    sb.AppendFormat("FAIL {0} ({1:F2}s)", r.Name, r.Duration);
                    if (!string.IsNullOrEmpty(r.Message))
                    {
                        sb.AppendLine();
                        sb.Append("  ").Append(r.Message);
                    }
                }

                return sb.ToString();
            }
        }
        /// <summary>Discovery-only: returns total|edit=N|play=M. First call returns "discovering", subsequent calls return cached result.</summary>
        public static string GetTestCount()
        {
            var cached = SessionState.GetString(KeyTestCount, "");
            if (!string.IsNullOrEmpty(cached)) return cached;

            if (SessionState.GetBool(KeyCountDiscovering, false)) return "discovering";

            SessionState.SetBool(KeyCountDiscovering, true);
            int editCount = 0, playCount = 0;
            int pending = 2;

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RetrieveTestList(TestMode.EditMode, root =>
            {
                editCount = root.TestCaseCount;
                if (--pending == 0) StoreCount(api, editCount, playCount);
            });
            api.RetrieveTestList(TestMode.PlayMode, root =>
            {
                playCount = root.TestCaseCount;
                if (--pending == 0) StoreCount(api, editCount, playCount);
            });
            return "discovering";
        }

        private static void StoreCount(TestRunnerApi api, int editCount, int playCount)
        {
            UnityEngine.Object.DestroyImmediate(api);
            var total = editCount + playCount;
            var result = $"{total}|edit={editCount}|play={playCount}";
            SessionState.SetString(KeyTestCount, result);
            SessionState.SetBool(KeyCountDiscovering, false);
        }
#else
        public static void Execute(string mode, Action<string> onComplete, string group = null, string filter = null)
        {
            onComplete("Error: com.unity.test-framework package not installed");
        }

        public static string GetTestCount() => "0|edit=0|play=0";
#endif

        /// <summary>File-backed persistence for test results — survives domain reload.
        /// Saves/loads from ~/.unity-mcp/test-results/port-{port}.txt.</summary>
        internal static class TestResultPersistence
        {
            // Seam: redirect to temp dir in tests to avoid touching real ~/.unity-mcp/
            internal static string FilePathOverride = null;

            private static string FilePath => FilePathOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-mcp", "test-results",
                $"port-{PortFileManager.Port}.txt");

            internal static void Save(string results)
            {
                try
                {
                    var path = FilePath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var tmp = path + ".tmp";
                    File.WriteAllText(tmp, results);
                    try { File.Delete(path); } catch { }
                    File.Move(tmp, path);
                }
                catch { }
            }

            internal static string Load()
            {
                try
                {
                    var path = FilePath;
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
                catch { return null; }
            }
        }
    }
}
