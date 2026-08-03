using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    // ── CS5.test.5 — MCPStatusModel.GetSub(State) coverage ───────────────────

    [TestFixture]
    public class MCPStatusModelGetSubStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void GetSub_State_Down_ReturnsServerStopped()
            => Assert.AreEqual("server stopped", MCPStatusModel.GetSub(MCPStatusModel.State.Down));

        [Test]
        public void GetSub_State_Listen_ReturnsNoClient()
            => Assert.AreEqual("no client", MCPStatusModel.GetSub(MCPStatusModel.State.Listen));

        [Test]
        public void GetSub_State_Up_ReturnsClientConnected()
            => Assert.AreEqual("client connected", MCPStatusModel.GetSub(MCPStatusModel.State.Up));

        [Test]
        public void GetSub_State_ChatActive_ReturnsChatBackendActive()
            => Assert.AreEqual("chat backend active", MCPStatusModel.GetSub(MCPStatusModel.State.ChatActive));
    }
}
