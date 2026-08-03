// TDD RED: WatchEvaluator reflection + cache tests — pure logic, no scene required.
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WatchEvaluatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private class TestObj { public int hp = 42; public string label = "hero"; }

        [SetUp]
        public void SetUp()
        {
            WatchEvaluator.ClearCache();
            WatchRegistry.DrainLog();  // clear residual log entries
        }

        [TearDown]
        public void TearDown()
        {
            WatchEvaluator.ClearCache();
            WatchRegistry.DrainLog();
        }

        [Test]
        public void ReadObjectField_ValidField_ReturnsValue()
        {
            var result = WatchEvaluator.ReadObjectField(new TestObj(), "hp");
            Assert.AreEqual(42, result);
        }

        [Test]
        public void ReadObjectField_ValidStringField_ReturnsValue()
        {
            var result = WatchEvaluator.ReadObjectField(new TestObj(), "label");
            Assert.AreEqual("hero", result);
        }

        [Test]
        public void ReadObjectField_UnknownField_ReturnsNull()
        {
            var result = WatchEvaluator.ReadObjectField(new TestObj(), "nonexistent");
            Assert.IsNull(result);
        }

        [Test]
        public void ReadObjectField_UnknownField_LogsError()
        {
            WatchEvaluator.ReadObjectField(new TestObj(), "nonexistent");
            var log = WatchRegistry.DrainLog();
            Assert.IsTrue(log.Any(l => l.Contains("[ERR]") && l.Contains("nonexistent")),
                "Expected [ERR] log entry for missing member");
        }

        [Test]
        public void ReadObjectField_MissingField_LogsOnlyOnce()
        {
            // First call: cache miss → logs
            WatchEvaluator.ReadObjectField(new TestObj(), "ghost");
            // Second call: cache hit (null stored) → no new log
            WatchEvaluator.ReadObjectField(new TestObj(), "ghost");
            var log = WatchRegistry.DrainLog();
            Assert.AreEqual(1, log.Length, "Should log missing member only on first lookup");
        }

        [Test]
        public void ReadObjectField_NullObject_ReturnsNull()
        {
            var result = WatchEvaluator.ReadObjectField(null, "hp");
            Assert.IsNull(result);
        }

        [Test]
        public void ClearCache_AllowsRelookup()
        {
            // First lookup logs the error
            WatchEvaluator.ReadObjectField(new TestObj(), "ghost");
            WatchRegistry.DrainLog();  // consume log

            // After cache clear, next call should log again
            WatchEvaluator.ClearCache();
            WatchEvaluator.ReadObjectField(new TestObj(), "ghost");
            var log = WatchRegistry.DrainLog();
            Assert.AreEqual(1, log.Length, "After ClearCache, re-lookup should log once more");
        }

        [Test]
        public void ReadValue_NullPath_ReturnsNull()
        {
            // ComponentSerializer.FindObject(null) → null → ReadValue returns null
            var result = WatchEvaluator.ReadValue(null, "Health", "hp");
            Assert.IsNull(result);
        }

        [Test]
        public void ReadValue_EmptyPath_ReturnsNull()
        {
            var result = WatchEvaluator.ReadValue("", "Health", "hp");
            Assert.IsNull(result);
        }
    }
}
