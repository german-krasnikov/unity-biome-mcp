// TDD RED: WatchRegistry CRUD, log, SessionState roundtrip.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WatchRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            WatchRegistry.Clear();
            WatchRegistry.Save(); // reset SessionState too
        }

        [TearDown]
        public void TearDown() => WatchRegistry.Clear();

        [Test]
        public void Add_ReturnsNonNullId()
        {
            var id = WatchRegistry.Add("/Player", "Health", "hp");
            Assert.IsNotNull(id);
            Assert.IsNotEmpty(id);
        }

        [Test]
        public void Add_StoresEntry()
        {
            var id = WatchRegistry.Add("/Player", "Health", "hp", "< 10", "log", 250f);
            Assert.IsTrue(WatchRegistry.All.ContainsKey(id));
            var e = WatchRegistry.All[id];
            Assert.AreEqual("/Player", e.Path);
            Assert.AreEqual("Health", e.Component);
            Assert.AreEqual("hp", e.Field);
            Assert.AreEqual("< 10", e.Condition);
            Assert.AreEqual("log", e.Action);
            Assert.AreEqual(250f, e.IntervalMs, 0.001f);
        }

        [Test]
        public void Remove_RemovesEntry()
        {
            var id = WatchRegistry.Add("/Player", "Health", "hp");
            Assert.IsTrue(WatchRegistry.Remove(id));
            Assert.IsFalse(WatchRegistry.All.ContainsKey(id));
        }

        [Test]
        public void Remove_ReturnsFalseForUnknown()
        {
            Assert.IsFalse(WatchRegistry.Remove("no_such_id"));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            WatchRegistry.Add("/A", "Comp", "field");
            WatchRegistry.Add("/B", "Comp", "field");
            WatchRegistry.Clear();
            Assert.AreEqual(0, WatchRegistry.All.Count);
        }

        [Test]
        public void Add_RespectsMaxWatchesCap()
        {
            for (int i = 0; i < 20; i++)
                WatchRegistry.Add("/Go" + i, "Comp", "field");
            var overflow = WatchRegistry.Add("/GoExtra", "Comp", "field");
            Assert.IsNull(overflow, "Should return null at MaxWatches");
            Assert.AreEqual(20, WatchRegistry.All.Count);
        }

        [Test]
        public void DrainLog_ReturnsEntries()
        {
            WatchRegistry.AddLogEntry("msg1");
            WatchRegistry.AddLogEntry("msg2");
            var log = WatchRegistry.DrainLog();
            Assert.AreEqual(2, log.Length);
        }

        [Test]
        public void DrainLog_ClearsLog()
        {
            WatchRegistry.AddLogEntry("msg");
            WatchRegistry.DrainLog();
            Assert.AreEqual(0, WatchRegistry.DrainLog().Length);
        }

        [Test]
        public void DrainLog_EmptyWhenNoEntries()
        {
            Assert.AreEqual(0, WatchRegistry.DrainLog().Length);
        }

        [Test]
        public void AddLogEntry_RingBufferCapsAt100()
        {
            for (int i = 0; i < 150; i++)
                WatchRegistry.AddLogEntry("entry" + i);
            var log = WatchRegistry.DrainLog();
            Assert.LessOrEqual(log.Length, 100);
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesEntries()
        {
            var id = WatchRegistry.Add("/Player", "Health", "hp", "< 10", "pause", 250f);
            WatchRegistry.Save();
            WatchRegistry.Clear();
            WatchRegistry.Load();
            Assert.IsTrue(WatchRegistry.All.ContainsKey(id));
            var e = WatchRegistry.All[id];
            Assert.AreEqual("/Player", e.Path);
            Assert.AreEqual("Health", e.Component);
            Assert.AreEqual("hp", e.Field);
            Assert.AreEqual("< 10", e.Condition);
            Assert.AreEqual("pause", e.Action);
            Assert.AreEqual(250f, e.IntervalMs, 0.001f);
        }

        [Test]
        public void Load_EmptySession_LeavesEmptyRegistry()
        {
            WatchRegistry.Clear();
            WatchRegistry.Save(); // write empty
            WatchRegistry.Load();
            Assert.AreEqual(0, WatchRegistry.All.Count);
        }
    }
}
