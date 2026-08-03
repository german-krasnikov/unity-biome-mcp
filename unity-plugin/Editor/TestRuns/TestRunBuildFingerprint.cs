using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_INCLUDE_TESTS
using UnityEditor.TestTools.TestRunner.Api;
#endif

namespace UnityMCP.Editor.TestRuns
{
    internal sealed class TestRunBuildFingerprint
    {
        internal string ProjectIdentity = "";
        internal string EditorProcessIdentity = "";
        internal string Fingerprint = "";
        internal string UtfVersion = "";
        internal string AssemblyPath = "";
        internal string SourcePath = "";
        internal string AssemblyWriteUtc = "";
        internal string SourceWriteUtc = "";
        internal bool IsCoherent;
        internal string Error = "";

        internal void ApplyTo(TestRunRecord run)
        {
            run.project_identity = ProjectIdentity;
            run.editor_process_identity = EditorProcessIdentity;
            run.editor_session_id = TestRunBuildFingerprintProbe.EditorSessionId();
            run.build_fingerprint = Fingerprint;
            run.utf_version = UtfVersion;
            run.assembly_path = AssemblyPath;
            run.source_path = SourcePath;
            run.assembly_write_utc = AssemblyWriteUtc;
            run.source_write_utc = SourceWriteUtc;
            run.build_coherent = IsCoherent;
            run.build_error = Error;
        }

        internal static string DescribeCompletionMismatch(
            TestRunRecord started,
            TestRunBuildFingerprint completed)
        {
            if (started == null || string.IsNullOrEmpty(started.build_fingerprint))
                return ""; // Legacy evidence predating completion validation.
            if (completed == null)
                return "completion build fingerprint capture returned no evidence";
            if (!completed.IsCoherent)
                return "completion build is incoherent: " +
                    (completed.Error ?? "unknown build error");
            if (!string.Equals(started.project_identity ?? "",
                    completed.ProjectIdentity ?? "", StringComparison.Ordinal))
                return "project identity changed during the test run";
            if (!string.Equals(started.build_fingerprint ?? "",
                    completed.Fingerprint ?? "", StringComparison.Ordinal))
                return "build fingerprint changed during the test run: started='" +
                    started.build_fingerprint + "' completed='" + completed.Fingerprint + "'";
            if (!string.Equals(started.utf_version ?? "", completed.UtfVersion ?? "",
                    StringComparison.Ordinal))
                return "Unity Test Framework version changed during the test run";
            if (!string.Equals(started.assembly_path ?? "", completed.AssemblyPath ?? "",
                    StringComparison.Ordinal) ||
                !string.Equals(started.source_path ?? "", completed.SourcePath ?? "",
                    StringComparison.Ordinal))
                return "loaded assembly or source path changed during the test run";
            if (!string.Equals(started.assembly_write_utc ?? "",
                    completed.AssemblyWriteUtc ?? "", StringComparison.Ordinal) ||
                !string.Equals(started.source_write_utc ?? "",
                    completed.SourceWriteUtc ?? "", StringComparison.Ordinal))
                return "loaded assembly or source timestamp changed during the test run";
            return "";
        }
    }

    internal static class TestRunBuildFingerprintProbe
    {
        internal const string CanonicalValidationUtfVersion = "1.6.0";
        private const string EditorSessionKey = "UnityMCP_test_editor_session_id";

        internal static string EditorSessionId()
        {
            var value = SessionState.GetString(EditorSessionKey, "");
            if (!string.IsNullOrEmpty(value)) return value;
            value = Guid.NewGuid().ToString("N");
            SessionState.SetString(EditorSessionKey, value);
            return value;
        }

        internal static string EditorProcessIdentity() =>
            Application.unityVersion + ":" + Process.GetCurrentProcess().Id + ":" +
            ProcessStartTicks();

        internal static TestRunBuildFingerprint Capture()
        {
            var result = new TestRunBuildFingerprint
            {
                ProjectIdentity = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                EditorProcessIdentity = EditorProcessIdentity()
            };

            try
            {
                var assemblies = TestRunAssemblyFingerprint.Capture(
                    result.ProjectIdentity);
                result.AssemblyPath = assemblies.MainAssemblyPath;
                result.SourcePath = assemblies.LatestMainSourcePath;
                result.AssemblyWriteUtc = assemblies.MainAssemblyWriteUtc;
                result.SourceWriteUtc = assemblies.LatestMainSourceWriteUtc;
#if UNITY_INCLUDE_TESTS
                result.UtfVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(TestRunnerApi).Assembly)?.version ?? "";
#endif
                result.Error = assemblies.Error;
                result.Fingerprint = assemblies.Fingerprint +
                    ";utf=" + result.UtfVersion;
                result.IsCoherent = string.IsNullOrEmpty(result.Error);
            }
            catch (Exception e)
            {
                result.Error = "build fingerprint failed: " + e.Message;
                result.IsCoherent = false;
            }

            return result;
        }

        private static long ProcessStartTicks()
        {
            try
            {
                return Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
            }
            catch
            {
                return 0L;
            }
        }
    }
}
