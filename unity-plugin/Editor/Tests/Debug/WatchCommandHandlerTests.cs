// TDD RED: WatchCommandHandler — null validation, round-trips, edge cases.
using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WatchCommandHandlerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            WatchRegistry.Clear();
            CommandRegistry.Clear();
            WatchCommandHandler.RegisterAll();
        }

        [TearDown]
        public void TearDown()
        {
            WatchRegistry.Clear();
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
        }

        // --- Null validation ---

        [Test]
        public void WatchAdd_MissingPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                CommandRegistry.Execute("watch_add",
                    @"{""component"":""Health"",""field"":""hp""}"));
        }

        [Test]
        public void WatchAdd_MissingComponent_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                CommandRegistry.Execute("watch_add",
                    @"{""path"":""/Player"",""field"":""hp""}"));
        }

        [Test]
        public void WatchAdd_MissingField_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                CommandRegistry.Execute("watch_add",
                    @"{""path"":""/Player"",""component"":""Health""}"));
        }

        [Test]
        public void WatchAdd_EmptyPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                CommandRegistry.Execute("watch_add",
                    @"{""path"":"""",""component"":""Health"",""field"":""hp""}"));
        }

        // --- Round-trip ---

        [Test]
        public void WatchAdd_ValidArgs_ReturnsId()
        {
            var id = CommandRegistry.Execute("watch_add",
                @"{""path"":""/Player"",""component"":""Health"",""field"":""hp""}");
            Assert.IsNotNull(id);
            Assert.IsNotEmpty(id);
        }

        [Test]
        public void WatchAdd_ThenGetWatches_ContainsEntry()
        {
            CommandRegistry.Execute("watch_add",
                @"{""path"":""/Player"",""component"":""Health"",""field"":""hp""}");
            var result = CommandRegistry.Execute("get_watches", "{}");
            StringAssert.Contains("/Player", result);
            StringAssert.Contains("Health", result);
            StringAssert.Contains("hp", result);
        }

        [Test]
        public void WatchAdd_CountedInWatchRegistry()
        {
            CommandRegistry.Execute("watch_add",
                @"{""path"":""/A"",""component"":""C"",""field"":""f""}");
            CommandRegistry.Execute("watch_add",
                @"{""path"":""/B"",""component"":""C"",""field"":""f""}");
            Assert.AreEqual(2, WatchRegistry.All.Count);
        }

        // --- watch_remove ---

        [Test]
        public void WatchRemove_UnknownId_ReturnsNotFound()
        {
            var result = CommandRegistry.Execute("watch_remove", @"{""id"":""w999""}");
            StringAssert.Contains("not found", result);
        }

        [Test]
        public void WatchRemove_KnownId_ReturnsRemoved()
        {
            var id = CommandRegistry.Execute("watch_add",
                @"{""path"":""/P"",""component"":""C"",""field"":""f""}");
            var result = CommandRegistry.Execute("watch_remove", $@"{{""id"":""{id}""}}");
            StringAssert.Contains("removed", result);
            Assert.IsFalse(WatchRegistry.All.ContainsKey(id));
        }

        // --- watch_clear ---

        [Test]
        public void WatchClear_EmptiesRegistry()
        {
            CommandRegistry.Execute("watch_add",
                @"{""path"":""/A"",""component"":""C"",""field"":""f""}");
            CommandRegistry.Execute("watch_clear", "{}");
            Assert.AreEqual(0, WatchRegistry.All.Count);
        }

        [Test]
        public void WatchClear_ReturnsClearedMessage()
        {
            var result = CommandRegistry.Execute("watch_clear", "{}");
            Assert.AreEqual("cleared", result);
        }

        // --- watch_reset ---

        [Test]
        public void WatchReset_UnknownId_ReturnsNotFound()
        {
            var result = CommandRegistry.Execute("watch_reset", @"{""id"":""w999""}");
            StringAssert.Contains("not found", result);
        }
    }
}
