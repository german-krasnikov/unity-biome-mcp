// TDD: S6 — Console file:line extraction from Unity stack traces.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConsoleStackParserTests
    {
        [TearDown]
        public void TearDown() => ConsoleCapture.Clear();

        [Test]
        public void ExtractFileLocation_ValidStackTrace_ReturnsFileLine()
        {
            var stackTrace = "MyScript:Method() (at Assets/Scripts/MyScript.cs:42)";

            var result = ConsoleStackParser.ExtractFileLocation(stackTrace);

            Assert.AreEqual("Assets/Scripts/MyScript.cs:42", result);
        }

        [Test]
        public void ExtractFileLocation_MultiLine_ReturnsFirstMatch()
        {
            var stackTrace = "Foo:Bar() (at Assets/Scripts/Foo.cs:10)\n" +
                              "Baz:Qux() (at Assets/Scripts/Baz.cs:20)";

            var result = ConsoleStackParser.ExtractFileLocation(stackTrace);

            Assert.AreEqual("Assets/Scripts/Foo.cs:10", result);
        }

        [Test]
        public void ExtractFileLocation_NoMatch_ReturnsNull()
        {
            var result = ConsoleStackParser.ExtractFileLocation("no stack trace");

            Assert.IsNull(result);
        }

        [Test]
        public void ExtractFileLocation_NullInput_ReturnsNull()
        {
            var result = ConsoleStackParser.ExtractFileLocation(null);

            Assert.IsNull(result);
        }

        [Test]
        public void ExtractFileLocation_PackagesPath_SkipsPackages()
        {
            var stackTrace = "Pkg:Method() (at Packages/com.unity.pkg/Runtime/Pkg.cs:5)\n" +
                              "Foo:Bar() (at Assets/Scripts/Foo.cs:20)";

            var result = ConsoleStackParser.ExtractFileLocation(stackTrace);

            Assert.AreEqual("Assets/Scripts/Foo.cs:20", result);
        }

        [Test]
        public void GetLogs_WithStackTrace_AppendsFileLocation()
        {
            ConsoleCapture.Clear();
            ConsoleCapture.InjectForTest("boom", LogType.Error,
                "MyScript:Method() (at Assets/Scripts/X.cs:42)");

            var result = ConsoleCapture.GetLogs();

            StringAssert.Contains("@ Assets/Scripts/X.cs:42", result);
        }
    }
}
