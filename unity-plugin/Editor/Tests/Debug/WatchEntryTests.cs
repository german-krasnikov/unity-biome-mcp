// TDD RED: WatchEntry data model tests.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WatchEntryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void WatchEntry_DefaultValues()
        {
            var e = new WatchEntry { Id = "w1", Path = "/Go", Component = "Comp", Field = "f" };
            Assert.AreEqual("w1", e.Id);
            Assert.AreEqual(0f, e.IntervalMs, 0.001f);
            Assert.IsNull(e.LastValue);
            Assert.IsFalse(e.Triggered);
            Assert.AreEqual(0, e.ChangeCount);
        }

        [Test]
        public void WatchEntry_AllFields_SetCorrectly()
        {
            var e = new WatchEntry
            {
                Id = "w2", Path = "/Player", Component = "Health",
                Field = "hp", Condition = "> 0", Action = "pause", IntervalMs = 500f,
                Triggered = true, ChangeCount = 3
            };
            Assert.AreEqual("pause", e.Action);
            Assert.IsTrue(e.Triggered);
            Assert.AreEqual(3, e.ChangeCount);
        }

        [Test]
        public void WatchEntry_NonSerializedFields_NotInJson()
        {
            var e = new WatchEntry { Id = "w3", Path = "/X", Component = "C", Field = "f" };
            e.LastValue = "test";
            e.Triggered = true;
            var json = UnityEngine.JsonUtility.ToJson(e);
            // NonSerialized fields excluded from JsonUtility output
            Assert.IsFalse(json.Contains("LastValue"), "LastValue should not serialize");
            Assert.IsFalse(json.Contains("LastSampleTime"), "LastSampleTime should not serialize");
        }
    }
}
