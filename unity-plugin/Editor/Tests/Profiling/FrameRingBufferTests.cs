// TDD RED: FrameRingBuffer behavior tests.
using NUnit.Framework;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class FrameRingBufferTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Empty_Buffer_Returns_Empty_Array()
        {
            var buf = new FrameRingBuffer(10);
            Assert.AreEqual(0, buf.ToArray().Length);
        }

        [Test]
        public void Add_Below_Capacity_Fills_In_Order()
        {
            var buf = new FrameRingBuffer(10);
            buf.Add(new FrameSample { CpuMs = 1f });
            buf.Add(new FrameSample { CpuMs = 2f });
            buf.Add(new FrameSample { CpuMs = 3f });
            var arr = buf.ToArray();
            Assert.AreEqual(3, arr.Length);
            Assert.AreEqual(1f, arr[0].CpuMs);
            Assert.AreEqual(3f, arr[2].CpuMs);
        }

        [Test]
        public void Add_At_Capacity_Overwrites_Oldest()
        {
            var buf = new FrameRingBuffer(5);
            for (int i = 0; i < 6; i++)
                buf.Add(new FrameSample { CpuMs = i + 1 });
            var arr = buf.ToArray();
            Assert.AreEqual(5, arr.Length);
            // Oldest (CpuMs=1) should be overwritten; first element is CpuMs=2
            Assert.AreEqual(2f, arr[0].CpuMs);
            Assert.AreEqual(6f, arr[4].CpuMs);
        }

        [Test]
        public void Add_Beyond_Capacity_Count_Stays_Capped()
        {
            var buf = new FrameRingBuffer(600);
            for (int i = 0; i < 601; i++)
                buf.Add(new FrameSample());
            Assert.AreEqual(600, buf.Count);
        }

        [Test]
        public void Clear_Resets_Count_And_Head()
        {
            var buf = new FrameRingBuffer(10);
            buf.Add(new FrameSample { CpuMs = 5f });
            buf.Add(new FrameSample { CpuMs = 6f });
            buf.Clear();
            Assert.AreEqual(0, buf.Count);
            Assert.AreEqual(0, buf.ToArray().Length);
        }

        // CopyTo tests (zero-alloc path)
        [Test]
        public void CopyTo_Empty_ReturnsZero()
        {
            var buf = new FrameRingBuffer(10);
            var dest = new FrameSample[10];
            Assert.AreEqual(0, buf.CopyTo(dest));
        }

        [Test]
        public void CopyTo_Partial_CopiesInOrder()
        {
            var buf = new FrameRingBuffer(10);
            buf.Add(new FrameSample { CpuMs = 1f });
            buf.Add(new FrameSample { CpuMs = 2f });
            var dest = new FrameSample[10];
            int n = buf.CopyTo(dest);
            Assert.AreEqual(2, n);
            Assert.AreEqual(1f, dest[0].CpuMs);
            Assert.AreEqual(2f, dest[1].CpuMs);
        }

        [Test]
        public void CopyTo_Wrapped_CopiesChronological()
        {
            var buf = new FrameRingBuffer(3);
            buf.Add(new FrameSample { CpuMs = 1f });
            buf.Add(new FrameSample { CpuMs = 2f });
            buf.Add(new FrameSample { CpuMs = 3f });
            buf.Add(new FrameSample { CpuMs = 4f }); // overwrites CpuMs=1
            var dest = new FrameSample[3];
            buf.CopyTo(dest);
            Assert.AreEqual(2f, dest[0].CpuMs, "oldest surviving = 2");
            Assert.AreEqual(4f, dest[2].CpuMs, "newest = 4");
        }
    }
}
