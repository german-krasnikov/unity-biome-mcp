// TDD: ConsoleCapture overflow boundary — sentinel format and capacity enforcement.
// MCP-CONSOLE-032: tests the dropped-count sentinel on full vs. watermark queries.
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConsoleCaptureOverflowBoundaryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => ConsoleCapture.Clear();

        [TearDown]
        public void TearDown() => ConsoleCapture.Clear();

        // ConsoleProblemPersistence.MAX_PERSISTED_PROBLEMS = 20.
        // Injecting 21 errors causes the 21st to evict the oldest from the persistence FIFO
        // cap, incrementing ConsoleCapture._droppedProblemCount to 1 without needing to
        // fill the ring buffer (RING_CAPACITY=450).
        private static void InjectPersistenceOverflow()
        {
            for (int i = 0; i < 21; i++)
                ConsoleCapture.InjectForTest($"overflow-err-{i}", LogType.Error);
        }

        // MCP-CONSOLE-032 (Test 1): Full query (sinceSeconds=0) with _droppedProblemCount > 0
        // must append the "#MCP_INTERNAL [+N older problem entries dropped]" sentinel so that
        // the Python server can distinguish it from ordinary log content.
        // RED: current AppendDroppedSuffix produces "[+N ...]" without the "#MCP_INTERNAL " prefix.
        [Ignore("Documents gap MCP-CONSOLE-032: AppendDroppedSuffix lacks #MCP_INTERNAL prefix")]
        [Test]
        public void GetLogs_DroppedCountAboveZero_AppendsSentinelOnFullQuery()
        {
            InjectPersistenceOverflow();

            var result = ConsoleCapture.GetLogs(sinceSeconds: 0);

            StringAssert.Contains("#MCP_INTERNAL [+", result,
                "Full-query path must prefix the dropped-count suffix with #MCP_INTERNAL");
            StringAssert.Contains("dropped]", result,
                "Full-query path must include the ...dropped] sentinel text");
        }

        // MCP-CONSOLE-032 (Test 2): Watermark query (sinceSeconds > 0) must never append any
        // dropped-count sentinel, even when _droppedProblemCount > 0. The watermark path is a
        // delta probe; injecting overflow metadata corrupts the caller's result.
        // RED: current code appends "#MCP_INTERNAL overflow:N" on the watermark path when dropped > 0.
        [Ignore("Documents gap MCP-CONSOLE-032: watermark branch appends overflow marker")]
        [Test]
        public void GetLogs_WatermarkQuery_NeverAppendsSentinel()
        {
            InjectPersistenceOverflow();

            var result = ConsoleCapture.GetLogs(sinceSeconds: 9999f);

            StringAssert.DoesNotContain("dropped]", result,
                "Watermark path must not append the [+N...dropped] sentinel");
            StringAssert.DoesNotContain("#MCP_INTERNAL", result,
                "Watermark path must not append any #MCP_INTERNAL marker when dropped > 0");
        }

        // Regression (B23 gate): GetErrorsSince is itself a delta/watermark query, exactly
        // like the GetLogs(sinceSeconds>0) path documented above -- it must never manufacture
        // a phantom result from the global _droppedProblemCount when its own since-window has
        // zero new problem entries. This was the root cause of PlaytestCorpusEditModeTests
        // reporting false CONSOLE_ERR failures on every step once a full-suite run had already
        // overflowed the 20-entry persisted-problem FIFO from earlier, unrelated tests.
        [Test]
        public void GetErrorsSince_DroppedCountAboveZeroButNoNewErrors_ReturnsNull()
        {
            InjectPersistenceOverflow(); // _droppedProblemCount > 0, all entries in the past
            // +50ms buffer: DateTime.Now has ~15.6ms resolution on Windows, so without
            // this an injected entry can share the same tick as `since` and the GetErrorsSince
            // >= comparison would wrongly include it.
            var since = DateTime.Now.AddMilliseconds(50); // window starts strictly after every injected entry

            var result = ConsoleCapture.GetErrorsSince(since, maxCount: 5);

            Assert.IsNull(result, $"Expected null (no new errors since the window start), got: {result}");
        }

        // MCP-CONSOLE-032 (Test 3): Injecting more entries than total capacity
        // (INIT_CAPACITY=50 + RING_CAPACITY=450 = 500) must return exactly 500 entries —
        // oldest are silently dropped from the ring buffer.
        [Test]
        public void InjectForTest_ExceedingCapacity_DropsOldest()
        {
            // Use Log type (not Error/Exception/Assert) so _droppedProblemCount stays 0,
            // keeping the output clean for line-counting.
            const int totalCapacity = 500; // INIT_CAPACITY(50) + RING_CAPACITY(450)
            for (int i = 0; i < totalCapacity + 10; i++)
                ConsoleCapture.InjectForTest($"log-{i}", LogType.Log);

            var result = ConsoleCapture.GetLogs();

            // Each injected entry is one line in the output (no stack traces).
            var lines = result.Split('\n').Where(l => l.Length > 0).ToArray();
            Assert.AreEqual(totalCapacity, lines.Length,
                $"Expected exactly {totalCapacity} entries after overflow, got {lines.Length}");
        }
    }
}
