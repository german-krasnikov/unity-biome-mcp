using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ResponseGovernanceTests
    {
        [Test]
        public void TruncateResponse_UnderLimit_ReturnsOriginal()
        {
            var data = "short response";
            Assert.AreEqual(data, ResponseGovernance.Truncate(data, 100));
        }

        [Test]
        public void TruncateResponse_OverLimit_TruncatesWithSignal()
        {
            var data = new string('x', 200);
            var result = ResponseGovernance.Truncate(data, 50);
            Assert.IsTrue(result.StartsWith(new string('x', 50)));
            Assert.IsTrue(result.Contains("[TRUNCATED"));
            Assert.IsFalse(result.Contains(new string('x', 51)));
        }

        [Test]
        public void TruncateResponse_ZeroLimit_NoTruncation()
        {
            var data = new string('x', 200);
            Assert.AreEqual(data, ResponseGovernance.Truncate(data, 0));
        }

        [Test]
        public void TruncateResponse_ExactLimit_ReturnsOriginal()
        {
            var data = new string('x', 100);
            Assert.AreEqual(data, ResponseGovernance.Truncate(data, 100));
        }

        [Test]
        public void TruncateResponse_SignalContainsOriginalLength()
        {
            var data = new string('x', 500);
            var result = ResponseGovernance.Truncate(data, 100);
            Assert.IsTrue(result.Contains("500 chars"));
            Assert.IsTrue(result.Contains("first 100"));
        }

        [Test]
        public void TruncateResponse_NullData_ReturnsNull()
        {
            Assert.IsNull(ResponseGovernance.Truncate(null, 100));
        }

        [Test]
        public void TruncateResponse_NegativeLimit_NoTruncation()
        {
            var data = "hello";
            Assert.AreEqual(data, ResponseGovernance.Truncate(data, -1));
        }
    }
}
