using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Server
{
    [TestFixture]
    public class ConsoleCaptureTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void Setup() { ConsoleCapture.Clear(); }

        [Test]
        public void GetLogs_ReturnsEmpty_WhenNoLogs()
        {
            var result = ConsoleCapture.GetLogs();
            Assert.IsTrue(string.IsNullOrEmpty(result) || result.Trim().Length == 0);
        }

        [Test]
        public void GetLogs_ReturnsEntries_AfterLog()
        {
            Debug.Log("hello-console-test");
            var result = ConsoleCapture.GetLogs();
            StringAssert.Contains("hello-console-test", result);
        }

        [Test]
        public void GetLogs_First_ReturnsInitEntries()
        {
            // After Clear(), init phase is open — first Debug.Log goes to init buffer
            Debug.Log("init-entry");
            // Force close init phase by calling GetLogs with first param
            var result = ConsoleCapture.GetLogs(count: 10, first: 5);
            StringAssert.Contains("init-entry", result);
        }

        [Test]
        public void Clear_ResetsAllBuffers()
        {
            Debug.Log("before-clear");
            ConsoleCapture.Clear();
            var result = ConsoleCapture.GetLogs();
            Assert.IsTrue(string.IsNullOrEmpty(result) || result.Trim().Length == 0);
        }

        [Test]
        public void GetLogs_LevelFilter_FiltersCorrectly()
        {
            Debug.Log("regular-log");
            LogAssert.Expect(LogType.Error, "error-only");
            Debug.LogError("error-only");
            var result = ConsoleCapture.GetLogs(level: "Error");
            StringAssert.Contains("error-only", result);
            StringAssert.DoesNotContain("regular-log", result);
        }

        [Test]
        public void Timestamp_HasSubSecondPrecision()
        {
            Debug.Log("ts-test");
            var result = ConsoleCapture.GetLogs();
            // Format: [LogType] HH:mm:ss.fff message
            // Verify the .fff millisecond part exists: look for pattern :\d\d\.\d\d\d
            var match = System.Text.RegularExpressions.Regex.IsMatch(result, @"\d{2}:\d{2}\.\d{3}");
            Assert.IsTrue(match, $"Expected HH:mm:ss.fff in: {result}");
        }
    }
}
