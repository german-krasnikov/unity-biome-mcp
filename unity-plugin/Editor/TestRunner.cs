using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.TestRuns;

#if UNITY_INCLUDE_TESTS
using UnityEditor.TestTools.TestRunner.Api;
#endif

namespace UnityMCP.Editor
{
    /// <summary>
    /// MCP facade for Unity Test Framework. The durable store and one global
    /// observer own lifecycle truth; this facade only dispatches and projects it.
    /// </summary>
    public static class TestRunner
    {
        internal const string TempScenePath = "Assets/TestsTemp/__mcp_test_temp.unity";
        private const string KeyTestCount = "UnityMCP_test_count";
        private const string KeyCountDiscovering = "UnityMCP_count_discovering";
        private const string KeyTestCountFingerprint = "UnityMCP_test_count_fingerprint";
        private const string KeyCountGeneration = "UnityMCP_test_count_generation";
        private static readonly string CountDomainGeneration = Guid.NewGuid().ToString("N");

#if UNITY_INCLUDE_TESTS
        private static TestRunService _service;

        private static TestRunService Service => _service ?? (_service = new TestRunService(
            ProductionStore(),
            new UnityTestRunEnvironmentController(),
            new UnityTestFrameworkDriver(),
            TestRunBuildFingerprintProbe.Capture,
            () => EditorApplication.isPlaying,
            CommandRouter.DefaultIsCompiling,
            () => SyncHelper.IsCompileClean,
            UtcNow));
#endif

        internal static TestRunStore ProductionStore() => TestRunStore.ForProject(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")));

        internal static bool IsRunning
        {
            get
            {
                try
                {
                    var store = ProductionStore();
                    return store.TryReadActive(out var pointer) &&
                           store.TryReadRun(pointer.run_id, out var run) &&
                           run.lifecycle != TestRunProtocol.Lifecycle.Terminal;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string GetResults() => GetResults(null);

        public static string GetResults(string runId)
        {
#if UNITY_INCLUDE_TESTS
            return Service.GetLegacyResults(runId);
#else
            return "none";
#endif
        }

        public static string GetProgress() => GetProgress(null);

        public static string GetProgress(string runId)
        {
#if UNITY_INCLUDE_TESTS
            return Service.GetLegacyProgress(runId);
#else
            return "idle";
#endif
        }

        public static string ResolveRequest(string requestId)
        {
#if UNITY_INCLUDE_TESTS
            return Service.Resolve(requestId);
#else
            return "none";
#endif
        }

        public static string GetRun(string runId)
        {
#if UNITY_INCLUDE_TESTS
            return Service.GetRunJson(runId);
#else
            return "none";
#endif
        }

        public static string CancelRun(string runId)
        {
#if UNITY_INCLUDE_TESTS
            return Service.Cancel(runId);
#else
            return "cancel-rejected|run_id=" + (runId ?? "") + "|reason=utf-unavailable";
#endif
        }

        public static string ListRuns(int limit = 20)
        {
#if UNITY_INCLUDE_TESTS
            return Service.ListRunsJson(limit);
#else
            return "{\"schema_version\":1,\"runs\":[]}";
#endif
        }

        /// <summary>
        /// Dispatches synchronously and invokes onComplete with the immediate GUID
        /// acknowledgement. RunFinished is intentionally not awaited here.
        /// </summary>
        public static void Execute(
            string mode,
            Action<string> onComplete,
            string group = null,
            string filter = null) =>
            Execute(mode, onComplete, group, filter,
                "direct-" + Guid.NewGuid().ToString("N"));

        public static void Execute(
            string mode,
            Action<string> onComplete,
            string group,
            string filter,
            string requestId) =>
            ExecuteWithSelection(mode, onComplete, group, filter, requestId, null);

        /// <summary>
        /// Same as Execute, plus a categories/assemblies/tests selection. Kept
        /// internal (not an Execute overload) because TestRunSelection is
        /// internal -- a public overload exposing it would be CS0051.
        /// </summary>
        internal static void ExecuteWithSelection(
            string mode,
            Action<string> onComplete,
            string group,
            string filter,
            string requestId,
            TestRunSelection selection)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
#if UNITY_INCLUDE_TESTS
            try
            {
                onComplete(Service.Start(requestId, mode, group, filter, selection));
            }
            catch (Exception e)
            {
                onComplete("Error: " + e.Message);
            }
#else
            onComplete("Error: com.unity.test-framework package not installed");
#endif
        }

        internal static (bool ok, string text) FinishRun(string result)
        {
            UndoGroupHelper.EndGroup();
            if (result != null && result.StartsWith("Error:", StringComparison.Ordinal))
                return (false, result.Substring(6).TrimStart());
            return (true, result ?? "");
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Discovery-only count. It has no lifecycle role and may use a small
        /// SessionState cache because a stale count can never certify a run.
        /// </summary>
        public static string GetTestCount()
        {
            var build = TestRunBuildFingerprintProbe.Capture();
            if (!build.IsCoherent)
                return "unavailable|build-incoherent|" + build.Error;
            var fingerprint = build.Fingerprint ?? "";
            var cached = SessionState.GetString(KeyTestCount, "");
            if (!string.IsNullOrEmpty(cached) && string.Equals(
                    SessionState.GetString(KeyTestCountFingerprint, ""), fingerprint,
                    StringComparison.Ordinal))
                return cached;
            SessionState.SetString(KeyTestCount, "");

            var sameDomain = string.Equals(
                SessionState.GetString(KeyCountGeneration, ""),
                CountDomainGeneration, StringComparison.Ordinal);
            if (SessionState.GetBool(KeyCountDiscovering, false) && sameDomain)
                return "discovering";

            SessionState.SetBool(KeyCountDiscovering, true);
            SessionState.SetString(KeyCountGeneration, CountDomainGeneration);
            var editCount = 0;
            var playCount = 0;
            var pending = 2;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                api.RetrieveTestList(TestMode.EditMode, root =>
                {
                    editCount = root?.TestCaseCount ?? 0;
                    if (System.Threading.Interlocked.Decrement(ref pending) == 0)
                        StoreCount(api, editCount, playCount, fingerprint,
                            CountDomainGeneration);
                });
                api.RetrieveTestList(TestMode.PlayMode, root =>
                {
                    playCount = root?.TestCaseCount ?? 0;
                    if (System.Threading.Interlocked.Decrement(ref pending) == 0)
                        StoreCount(api, editCount, playCount, fingerprint,
                            CountDomainGeneration);
                });
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(api);
                SessionState.SetBool(KeyCountDiscovering, false);
                throw;
            }
            return "discovering";
        }

        private static void StoreCount(
            TestRunnerApi api,
            int editCount,
            int playCount,
            string fingerprint,
            string generation)
        {
            UnityEngine.Object.DestroyImmediate(api);
            if (!string.Equals(SessionState.GetString(KeyCountGeneration, ""),
                    generation, StringComparison.Ordinal)) return;
            SessionState.SetString(KeyTestCount,
                (editCount + playCount) + "|edit=" + editCount + "|play=" + playCount);
            SessionState.SetString(KeyTestCountFingerprint, fingerprint);
            SessionState.SetBool(KeyCountDiscovering, false);
        }
#else
        public static string GetTestCount() => "0|edit=0|play=0";
#endif

        private static string UtcNow() => DateTime.UtcNow.ToString("O");
    }
}
