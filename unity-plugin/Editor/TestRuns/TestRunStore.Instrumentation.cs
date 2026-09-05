using UnityEditor;

namespace UnityMCP.Editor.TestRuns
{
    /// <summary>
    /// SessionState-backed key names for the isolation-harness instrumentation
    /// UnityMcpTestBase accumulates as tests execute. Namespaced by run_id so
    /// sequential runs in one long-lived Editor session never bleed into each
    /// other's numbers; a shared source between the writer (UnityMcpTestBase)
    /// and the reader (TestRunStore) avoids duplicated key literals.
    /// </summary>
    public static class TestRunInstrumentationKeys
    {
        public static string BaseSetupMs(string runId) => "UnityMCP_bsm_" + runId;
        public static string SceneRepairs(string runId) => "UnityMCP_scr_" + runId;
        public static string SceneRepairFull(string runId) => "UnityMCP_scrf_" + runId;
    }

    public sealed partial class TestRunStore
    {
        /// <summary>
        /// Stamps accumulated isolation-harness instrumentation onto the summary,
        /// the same way StampEvidence stamps file evidence -- read fresh on every
        /// Reconcile so a live counter is never lost to a rebuilt summary.
        /// </summary>
        private void StampInstrumentation(TestRunSummary summary)
        {
            summary.base_setup_ms = SessionState.GetFloat(
                TestRunInstrumentationKeys.BaseSetupMs(summary.run_id), 0f);
            summary.scene_repairs = SessionState.GetInt(
                TestRunInstrumentationKeys.SceneRepairs(summary.run_id), 0);
            summary.scene_repair_full = SessionState.GetInt(
                TestRunInstrumentationKeys.SceneRepairFull(summary.run_id), 0);
        }
    }
}
