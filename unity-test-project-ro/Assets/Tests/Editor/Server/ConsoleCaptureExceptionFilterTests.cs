using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Server
{
    // Issue 27, Cycle 1: GetErrorsSince/GetLogs must treat LogType.Exception and
    // LogType.Assert as "problems" too, not just LogType.Error — unhandled C# exceptions
    // arrive as LogType.Exception and were previously invisible to error-scanning callers.
    [TestFixture]
    public class ConsoleCaptureExceptionFilterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(ConsoleCapture.Clear);
            ConsoleCapture.Clear();
        }

        [Test]
        public void GetErrorsSince_IncludesExceptionType()
        {
            var since = DateTime.Now.AddSeconds(-1);
            ConsoleCapture.InjectForTest("unhandled-exception-msg", LogType.Exception);

            var result = ConsoleCapture.GetErrorsSince(since);

            StringAssert.Contains("unhandled-exception-msg", result);
        }

        [Test]
        public void GetErrorsSince_IncludesAssertType()
        {
            var since = DateTime.Now.AddSeconds(-1);
            ConsoleCapture.InjectForTest("failed-assert-msg", LogType.Assert);

            var result = ConsoleCapture.GetErrorsSince(since);

            StringAssert.Contains("failed-assert-msg", result);
        }

        [Test]
        public void GetLogs_LevelErrorExceptionAssert_ReturnsAllThree()
        {
            ConsoleCapture.InjectForTest("err-entry", LogType.Error);
            ConsoleCapture.InjectForTest("exception-entry", LogType.Exception);
            ConsoleCapture.InjectForTest("assert-entry", LogType.Assert);
            ConsoleCapture.InjectForTest("regular-log", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: "Error,Exception,Assert");

            StringAssert.Contains("err-entry", result);
            StringAssert.Contains("exception-entry", result);
            StringAssert.Contains("assert-entry", result);
            StringAssert.DoesNotContain("regular-log", result);
        }
    }
}
