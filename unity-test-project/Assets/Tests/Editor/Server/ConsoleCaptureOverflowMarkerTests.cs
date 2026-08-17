using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Server
{
    // Issue 27, Cycle 4: ring buffer overflow must surface an explicit marker instead of
    // silently dropping problem-type entries. 600 > INIT_CAPACITY(50) + RING_CAPACITY(450) = 500,
    // guaranteeing at least some ring eviction regardless of the exact init/ring split.
    [TestFixture]
    public class ConsoleCaptureOverflowMarkerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const int OVERFLOW_INJECT_COUNT = 600;
        private static readonly Regex DroppedMarker = new Regex(@"\[\+\d+ older problem entries dropped\]");

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(ConsoleCapture.Clear);
            ConsoleCapture.Clear();
        }

        [Test]
        public void GetLogs_RingOverflow_ShowsDroppedProblemMarker()
        {
            for (int i = 0; i < OVERFLOW_INJECT_COUNT; i++)
                ConsoleCapture.InjectForTest($"overflow-error-{i}", LogType.Error);

            var result = ConsoleCapture.GetLogs(level: "Error");

            Assert.IsTrue(DroppedMarker.IsMatch(result), $"Expected overflow marker in: {result}");
        }

        [Test]
        public void GetErrorsSince_RingOverflow_ShowsDroppedProblemMarker()
        {
            for (int i = 0; i < OVERFLOW_INJECT_COUNT; i++)
                ConsoleCapture.InjectForTest($"overflow-error-{i}", LogType.Error);

            var result = ConsoleCapture.GetErrorsSince(DateTime.MinValue, maxCount: 5);

            Assert.IsTrue(DroppedMarker.IsMatch(result), $"Expected overflow marker in: {result}");
        }

        [Test]
        public void GetLogs_PersistedProblemListOverflow_ShowsDroppedProblemMarker()
        {
            // M9 regression guard: ConsoleProblemPersistence's own 20-entry FIFO cap must count
            // its evictions too — not just the 500-entry ring buffer's. 25 stays well under the
            // ring capacity, so only the small persisted-problem list overflows here.
            const int PERSISTED_CAP_OVERFLOW_COUNT = 25;
            for (int i = 0; i < PERSISTED_CAP_OVERFLOW_COUNT; i++)
                ConsoleCapture.InjectForTest($"persisted-overflow-{i}", LogType.Error);

            var result = ConsoleCapture.GetLogs(level: "Error");

            Assert.IsTrue(DroppedMarker.IsMatch(result), $"Expected persisted-list overflow marker in: {result}");
        }

        // X10: countOnly=true must include overflow marker when _droppedProblemCount > 0
        [Test]
        public void CountOnly_WithDroppedEntries_IncludesOverflowSuffix()
        {
            // Inject enough errors to saturate ring + persisted list
            for (int i = 0; i < OVERFLOW_INJECT_COUNT; i++)
                ConsoleCapture.InjectForTest($"count-overflow-{i}", LogType.Error);

            var result = ConsoleCapture.GetLogs(countOnly: true);

            // Result must start with a digit (the count) and include the overflow marker
            Assert.IsTrue(result.Length > 0 && char.IsDigit(result[0]),
                $"Expected count prefix in: {result}");
            Assert.IsTrue(DroppedMarker.IsMatch(result),
                $"Expected overflow marker in countOnly result: {result}");
        }
    }
}
