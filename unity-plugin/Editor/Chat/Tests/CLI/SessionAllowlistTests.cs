using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SessionAllowlistTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TestTool = "TestTool_SAL_" + nameof(SessionAllowlistTests);
        private SessionAllowlist _list;

        [SetUp]
        public void SetUp()
        {
            _list = new SessionAllowlist();
            DeleteEditorPrefBool("MCPChat.AlwaysAllow." + TestTool);
        }

        [Test]
        public void IsAutoApproved_NotAdded_ReturnsFalse()
            => Assert.IsFalse(_list.IsAutoApproved(TestTool));

        [Test]
        public void AddSession_ThenIsAutoApproved_ReturnsTrue()
        {
            _list.AddSession(TestTool);
            Assert.IsTrue(_list.IsAutoApproved(TestTool));
        }

        [Test]
        public void ClearSession_ThenIsAutoApproved_ReturnsFalse()
        {
            _list.AddSession(TestTool);
            _list.ClearSession();
            Assert.IsFalse(_list.IsAutoApproved(TestTool));
        }

        [Test]
        public void AddAlways_PersistsToEditorPrefs()
        {
            _list.AddAlways(TestTool);
            Assert.IsTrue(EditorPrefs.GetBool("MCPChat.AlwaysAllow." + TestTool, false));
        }

        [Test]
        public void IsAutoApproved_AlwaysAllowed_ReturnsTrue()
        {
            _list.AddAlways(TestTool);
            var freshList = new SessionAllowlist(); // new instance reads EditorPrefs
            Assert.IsTrue(freshList.IsAutoApproved(TestTool));
        }

        [Test]
        public void RemoveAlways_ThenIsAutoApproved_ReturnsFalse()
        {
            _list.AddAlways(TestTool);
            _list.RemoveAlways(TestTool);
            var freshList = new SessionAllowlist();
            Assert.IsFalse(freshList.IsAlwaysAllowed(TestTool));
            Assert.IsFalse(freshList.IsAutoApproved(TestTool));
        }
    }
}
