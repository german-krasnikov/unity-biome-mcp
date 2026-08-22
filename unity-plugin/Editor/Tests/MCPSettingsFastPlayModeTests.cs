using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPSettingsFastPlayModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void GetFastPlayMode_DefaultsFalse()
        {
            DeleteEditorPrefBool("UnityMCP_FastPlayMode");
            Assert.IsFalse(MCPSettings.GetFastPlayMode());
        }

        [Test]
        public void SetFastPlayMode_RoundTrip()
        {
            ProtectEditorPrefBool("UnityMCP_FastPlayMode");
            MCPSettings.SetFastPlayMode(true);
            Assert.IsTrue(MCPSettings.GetFastPlayMode());
        }
    }
}
