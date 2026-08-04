using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Server
{
    // Issue 27, Cycle 3: problem-type logs must survive a domain reload via SessionState —
    // in-memory buffers are wiped by a real Unity domain reload, SessionState is not.
    // SimulateDomainReloadForTest() mirrors CompileErrorCapture.SimulateDomainReload(): wipes
    // in-memory state, leaves the already-written SessionState value untouched.
    [TestFixture]
    public class ConsoleCaptureReloadSurvivalTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(ConsoleCapture.Clear);
            ConsoleCapture.Clear();
        }

        [Test]
        public void GetErrorsSince_SurvivesReload_ViaSessionStateFallback()
        {
            ConsoleCapture.InjectForTest("reload-survivor-error", LogType.Error);

            ConsoleCapture.SimulateDomainReloadForTest();

            var result = ConsoleCapture.GetErrorsSince(DateTime.MinValue);

            StringAssert.Contains("reload-survivor-error", result);
        }

        [Test]
        public void GetLogs_SurvivesReload_ViaSessionStateFallback()
        {
            ConsoleCapture.InjectForTest("reload-survivor-exception", LogType.Exception);

            ConsoleCapture.SimulateDomainReloadForTest();

            var result = ConsoleCapture.GetLogs();

            StringAssert.Contains("reload-survivor-exception", result);
        }

        [Test]
        public void GetLogs_NoReload_StillReturnsInMemoryEntries()
        {
            // Sanity: without a reload the in-memory buffer answers directly (fallback unused).
            ConsoleCapture.InjectForTest("normal-path-error", LogType.Error);

            var result = ConsoleCapture.GetLogs();

            StringAssert.Contains("normal-path-error", result);
        }

        [Test]
        public void GetErrorsSince_AfterReload_FiltersOutTimestampBeforeSince()
        {
            // C1 regression guard: the SessionState fallback must still honor `since` — a
            // reload must not resurrect every persisted problem regardless of when it happened.
            ConsoleCapture.InjectForTest("stale-before-reload-error", LogType.Error);
            var sinceAfterInject = DateTime.Now.AddSeconds(1);

            ConsoleCapture.SimulateDomainReloadForTest();

            var result = ConsoleCapture.GetErrorsSince(sinceAfterInject);

            Assert.IsNull(result, $"Expected null (nothing newer than `since`), got: {result}");
        }

        [Test]
        public void GetLogs_AfterReload_LevelFilterAppliesToFallback()
        {
            // C2 regression guard: the SessionState fallback must still honor level filtering —
            // an Error-type persisted problem must not leak into a level="Warning" query.
            ConsoleCapture.InjectForTest("reload-error-not-warning", LogType.Error);

            ConsoleCapture.SimulateDomainReloadForTest();

            var result = ConsoleCapture.GetLogs(level: "Warning");

            Assert.IsEmpty(result, $"Expected empty for level=Warning, got: {result}");
        }

        [Test]
        public void GetLogs_AfterReload_PreservesEmbeddedNewlineAndDoesNotDesyncSubsequentEntry()
        {
            // Regression guard: ConsoleProblemPersistence used to join/split messages, types
            // and timestamps on '\n'. A message containing an embedded newline (e.g. a
            // validation error formatted as "Validation failed:\nMissing X") added an extra
            // "line" to the messages list on restore that the parallel types/timestamps lists
            // didn't have — desyncing the index alignment and silently dropping any entry
            // injected after it.
            ConsoleCapture.InjectForTest("Validation failed:\nMissing X", LogType.Error);
            ConsoleCapture.InjectForTest("second-error", LogType.Exception);

            ConsoleCapture.SimulateDomainReloadForTest();

            var result = ConsoleCapture.GetLogs();

            StringAssert.Contains("Validation failed:\nMissing X", result);
            StringAssert.Contains("[Exception]", result);
            StringAssert.Contains("second-error", result);
        }
    }
}
