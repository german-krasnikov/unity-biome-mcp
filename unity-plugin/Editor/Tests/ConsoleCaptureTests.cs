// TDD: ConsoleCapture — comma-separated level filter fix.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConsoleCaptureTests
    {
        [SetUp]
        public void SetUp() => ConsoleCapture.Clear();

        [TearDown]
        public void TearDown() => ConsoleCapture.Clear();

        [Test]
        public void GetLogs_CommaLevel_FiltersCorrectly()
        {
            ConsoleCapture.InjectForTest("err msg", LogType.Error);
            ConsoleCapture.InjectForTest("warn msg", LogType.Warning);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: "Error,Warning");

            StringAssert.Contains("err msg", result);
            StringAssert.Contains("warn msg", result);
            StringAssert.DoesNotContain("log msg", result);
        }

        [Test]
        public void GetLogs_SingleLevel_FiltersCorrectly()
        {
            ConsoleCapture.InjectForTest("err msg", LogType.Error);
            ConsoleCapture.InjectForTest("warn msg", LogType.Warning);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: "Error");

            StringAssert.Contains("err msg", result);
            StringAssert.DoesNotContain("warn msg", result);
            StringAssert.DoesNotContain("log msg", result);
        }

        [Test]
        public void GetLogs_InvalidLevel_ReturnsAll()
        {
            ConsoleCapture.InjectForTest("err msg", LogType.Error);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: "invalid");

            StringAssert.Contains("err msg", result);
            StringAssert.Contains("log msg", result);
        }

        [Test]
        public void GetLogs_FirstParam_WithLevelFilter_FiltersCorrectly()
        {
            // Inject into init buffer (within 5s window after Clear)
            ConsoleCapture.InjectForTest("init err", LogType.Error);
            ConsoleCapture.InjectForTest("init log", LogType.Log);

            var result = ConsoleCapture.GetLogs(count: 10, level: "Error", first: 2);

            StringAssert.Contains("init err", result);
            StringAssert.DoesNotContain("init log", result);
        }

        [Test]
        public void GetLogs_NullLevel_ReturnsAll()
        {
            ConsoleCapture.InjectForTest("err msg", LogType.Error);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: null);

            StringAssert.Contains("err msg", result);
            StringAssert.Contains("log msg", result);
        }

        [Test]
        public void GetLogs_Count_ReturnsLastN()
        {
            for (int i = 0; i < 5; i++)
                ConsoleCapture.InjectForTest($"msg-{i}", LogType.Log);

            var result = ConsoleCapture.GetLogs(count: 2);

            StringAssert.Contains("msg-3", result);
            StringAssert.Contains("msg-4", result);
            StringAssert.DoesNotContain("msg-0", result);
        }

        [Test]
        public void GetLogs_RingBuffer_ReturnsEntries()
        {
            // Entries 0–49 fill _initBuffer. Entry 50 triggers _initPhaseOpen=false and lands in ring.
            for (int i = 0; i < 50; i++)
                ConsoleCapture.InjectForTest($"init-{i}", LogType.Log);

            ConsoleCapture.InjectForTest("ring-error", LogType.Error);

            var result = ConsoleCapture.GetLogs(level: "Error");

            StringAssert.Contains("ring-error", result);
        }

        [Test]
        public void GetLogs_RingBuffer_PreservesChronologicalOrder()
        {
            for (int i = 0; i < 50; i++)
                ConsoleCapture.InjectForTest($"init-{i}", LogType.Log);

            ConsoleCapture.InjectForTest("ring-first", LogType.Error);
            ConsoleCapture.InjectForTest("ring-second", LogType.Error);

            var result = ConsoleCapture.GetLogs(level: "Error");

            Assert.Less(result.IndexOf("ring-first"), result.IndexOf("ring-second"));
        }

        [Test]
        public void GetErrorsSince_ReturnsErrorsAfterTimestamp()
        {
            ConsoleCapture.InjectForTest("before-error", LogType.Error);
            System.Threading.Thread.Sleep(20);
            var since = DateTime.Now;
            System.Threading.Thread.Sleep(20);
            ConsoleCapture.InjectForTest("after-error", LogType.Error);

            var result = ConsoleCapture.GetErrorsSince(since);

            StringAssert.Contains("after-error", result);
            StringAssert.DoesNotContain("before-error", result);
        }

        [Test]
        public void GetErrorsSince_ReturnsNull_WhenNoErrorsAfterTimestamp()
        {
            ConsoleCapture.InjectForTest("old-error", LogType.Error);
            var since = DateTime.Now.AddSeconds(1);

            var result = ConsoleCapture.GetErrorsSince(since);

            Assert.IsNull(result);
        }

        [Test]
        public void GetErrorsSince_IgnoresNonErrors()
        {
            var since = DateTime.Now.AddSeconds(-1);
            ConsoleCapture.InjectForTest("a warning", LogType.Warning);
            ConsoleCapture.InjectForTest("a log", LogType.Log);

            var result = ConsoleCapture.GetErrorsSince(since);

            Assert.IsNull(result);
        }

        [Test]
        public void GetLogs_CountMinusOne_ReturnsAll()
        {
            ConsoleCapture.InjectForTest("error", LogType.Error);
            ConsoleCapture.InjectForTest("warning", LogType.Warning);
            ConsoleCapture.InjectForTest("log", LogType.Log);

            var result = ConsoleCapture.GetLogs(count: -1);

            StringAssert.Contains("error", result);
            StringAssert.Contains("warning", result);
            StringAssert.Contains("log", result);
        }

        [Test]
        public void GetLogs_EmptyBuffer_ReturnsEmptyString()
        {
            var result = ConsoleCapture.GetLogs();

            Assert.AreEqual("", result);
        }

        [Test]
        public void GetLogs_keyword_filters_by_substring()
        {
            ConsoleCapture.InjectForTest("player died", LogType.Log);
            ConsoleCapture.InjectForTest("enemy spawned", LogType.Log);
            ConsoleCapture.InjectForTest("player respawned", LogType.Log);

            var result = ConsoleCapture.GetLogs(keyword: "player");

            StringAssert.Contains("player died", result);
            StringAssert.Contains("player respawned", result);
            StringAssert.DoesNotContain("enemy spawned", result);
        }

        [Test]
        public void GetLogs_keyword_case_insensitive()
        {
            ConsoleCapture.InjectForTest("NullReferenceException in Update", LogType.Error);
            ConsoleCapture.InjectForTest("normal log", LogType.Log);

            var result = ConsoleCapture.GetLogs(keyword: "nullreferenceexception");

            StringAssert.Contains("NullReferenceException", result);
            StringAssert.DoesNotContain("normal log", result);
        }

        [Test]
        public void GetLogs_countOnly_returns_count_string()
        {
            ConsoleCapture.InjectForTest("error one", LogType.Error);
            ConsoleCapture.InjectForTest("error two", LogType.Error);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: "Error", countOnly: true);

            Assert.AreEqual("2", result);
        }

        [Test]
        public void GetLogs_keyword_plus_level_combined()
        {
            ConsoleCapture.InjectForTest("NPC error", LogType.Error);
            ConsoleCapture.InjectForTest("NPC warning", LogType.Warning);
            ConsoleCapture.InjectForTest("player error", LogType.Error);

            var result = ConsoleCapture.GetLogs(level: "Error", keyword: "NPC");

            StringAssert.Contains("NPC error", result);
            StringAssert.DoesNotContain("NPC warning", result);
            StringAssert.DoesNotContain("player error", result);
        }

        // ── S5: sinceSeconds param ──────────────────────────────────────────────

        [Test]
        public void GetLogs_SinceSeconds_FiltersOldEntries()
        {
            ConsoleCapture.InjectForTest("old entry", LogType.Log);

            // sinceSeconds=9999 → wide window, includes all recent entries
            var resultWide = ConsoleCapture.GetLogs(sinceSeconds: 9999f);
            StringAssert.Contains("old entry", resultWide);

            // sinceSeconds=0.001 → tiny window, entry is already older
            System.Threading.Thread.Sleep(10);
            var resultNarrow = ConsoleCapture.GetLogs(sinceSeconds: 0.001f);
            StringAssert.DoesNotContain("old entry", resultNarrow);
        }

        [Test]
        public void GetLogs_SinceZero_ReturnsAll()
        {
            ConsoleCapture.InjectForTest("a", LogType.Log);
            ConsoleCapture.InjectForTest("b", LogType.Warning);
            ConsoleCapture.InjectForTest("c", LogType.Error);

            // sinceSeconds=0 means no time filtering
            var result = ConsoleCapture.GetLogs(sinceSeconds: 0f);

            StringAssert.Contains("a", result);
            StringAssert.Contains("b", result);
            StringAssert.Contains("c", result);
        }

        [Test]
        public void GetLogs_Since_CombinesWithLevel()
        {
            ConsoleCapture.InjectForTest("err1", LogType.Error);
            ConsoleCapture.InjectForTest("log1", LogType.Log);

            // sinceSeconds=9999 + level=Error → only errors within window
            var result = ConsoleCapture.GetLogs(sinceSeconds: 9999f, level: "Error");

            StringAssert.Contains("err1", result);
            StringAssert.DoesNotContain("log1", result);
        }
    }
}
