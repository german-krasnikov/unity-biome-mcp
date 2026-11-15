// Shared fakes for TestRunner/TestRunService dispatch tests. Promoted out of
// TestRunnerTests.cs and TestRunServiceTests.cs (A20/A21 review minor) -- both
// fixtures used byte-identical FakeEnvironment logic and TestRunServiceTests'
// FakeFrameworkDriver was a strict subset of TestRunnerTests' (its hardcoded
// Probe=Active/ProbeAny=Inactive matches this class's own defaults), so this
// single pair covers both without behavior change.
using System;
using UnityEditor.TestTools.TestRunner.Api;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    internal sealed class FakeFrameworkDriver : ITestFrameworkDriver
    {
        internal int ExecuteCalls;
        internal int CancelCalls;
        internal ExecutionSettings LastSettings;
        internal string LastCancelledGuid;
        internal bool CancelResult = true;
        internal Exception CancelError;
        internal Exception ProbeAnyError;
        internal UtfRunActivity Activity = UtfRunActivity.Active;
        internal UtfRunActivity AnyActivity = UtfRunActivity.Inactive;
        internal Action OnExecute;

        public string Execute(ExecutionSettings settings)
        {
            ExecuteCalls++;
            LastSettings = settings;
            OnExecute?.Invoke();
            return "utf-guid-1";
        }

        public bool Cancel(string utfGuid)
        {
            CancelCalls++;
            LastCancelledGuid = utfGuid;
            if (CancelError != null) throw CancelError;
            return CancelResult;
        }

        public UtfRunActivity Probe(string utfGuid) => Activity;

        public UtfRunActivity ProbeAny()
        {
            if (ProbeAnyError != null) throw ProbeAnyError;
            return AnyActivity;
        }
    }

    internal sealed class FakeEnvironment : ITestRunEnvironmentController
    {
        internal int PrepareCalls;
        internal int RestoreCalls;
        internal Exception PrepareError;
        internal Exception RestoreError;

        public TestRunEnvironmentRecord Prepare(
            TestRunStore store, string runId, string utcNow)
        {
            PrepareCalls++;
            if (PrepareError != null) throw PrepareError;
            if (store.TryReadEnvironment(runId, out var existing)) return existing;
            var environment = new TestRunEnvironmentRecord
            {
                run_id = runId,
                prepared_utc = utcNow
            };
            store.WriteEnvironment(environment);
            return environment;
        }

        public void Restore(TestRunStore store, string runId, string utcNow)
        {
            RestoreCalls++;
            if (RestoreError != null) throw RestoreError;
        }
    }
}
