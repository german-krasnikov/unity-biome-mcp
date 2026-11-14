using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BackendTableTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Get_Claude_ReturnsMatchingRow()
        {
            var row = BackendTable.Get(BackendKind.Claude);
            Assert.AreEqual("claude", row.Binary);
            Assert.AreEqual("claude", row.Id);
            Assert.IsTrue(row.ResumeCd);
            Assert.AreEqual(SessionFormat.Jsonl, row.SessionFormat);
            Assert.AreEqual(200_000, row.ContextFallback);
        }

        [Test]
        public void Get_OpenCode_ReturnsRowWithEmptySessionDir()
        {
            var row = BackendTable.Get(BackendKind.OpenCode);
            Assert.AreEqual("opencode", row.Binary);
            Assert.IsFalse(row.ResumeCd);
            Assert.AreEqual(SessionFormat.None, row.SessionFormat);
            Assert.AreEqual(0, row.SessionDir.Length);
        }

        [Test]
        public void Get_InvalidKind_ReturnsNull()
        {
            Assert.IsNull(BackendTable.Get((BackendKind)999));
        }
    }
}
