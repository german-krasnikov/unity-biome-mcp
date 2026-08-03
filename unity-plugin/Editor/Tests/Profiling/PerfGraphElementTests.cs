// TDD RED: PerfGraphElement ring-buffer data management tests.
// Does NOT test Painter2D rendering (visual-only, tested manually).
using NUnit.Framework;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PerfGraphElementTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void PushValue_IncrementsCount()
        {
            var el = new PerfGraphElement(10);
            el.PushValue(1f);
            el.PushValue(2f);
            Assert.AreEqual(2, el.Count);
        }

        [Test]
        public void PushValue_BeyondCapacity_OverwritesOldest()
        {
            var el = new PerfGraphElement(3);
            el.PushValue(10f);
            el.PushValue(20f);
            el.PushValue(30f);
            el.PushValue(40f); // capacity=3 → overwrites 10
            var vals = el.GetValues();
            Assert.AreEqual(3, vals.Length);
            Assert.AreEqual(20f, vals[0], "oldest surviving = 20");
            Assert.AreEqual(40f, vals[2], "newest = 40");
        }

        [Test]
        public void SetValues_ReplacesBuffer()
        {
            var el = new PerfGraphElement(10);
            el.PushValue(99f);
            el.SetValues(new[] { 1f, 2f, 3f });
            var vals = el.GetValues();
            Assert.AreEqual(3, vals.Length);
            Assert.AreEqual(1f, vals[0]);
            Assert.AreEqual(3f, vals[2]);
        }

        [Test]
        public void GetValues_ReturnsChronological()
        {
            var el = new PerfGraphElement(5);
            el.PushValue(10f);
            el.PushValue(20f);
            el.PushValue(30f);
            var vals = el.GetValues();
            Assert.AreEqual(10f, vals[0]);
            Assert.AreEqual(20f, vals[1]);
            Assert.AreEqual(30f, vals[2]);
        }
    }
}
