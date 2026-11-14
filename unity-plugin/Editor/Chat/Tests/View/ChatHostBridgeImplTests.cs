// TDD tests for ChatHostBridgeImpl — the Chat.View implementation of IChatHostBridge
// (Task 3.1, sub-tasks 2.2/2.3 of Plans/task-phase2-chat-plugin.md).
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatHostBridgeImplTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void StopRelay_DelegatesToRelaySpawnerStop()
        {
            using (RelaySpawner.BeginTestIsolation())
            {
                RelaySpawner.SetSessionForTests(12345, 99999);

                new ChatHostBridgeImpl().StopRelay();

                Assert.AreEqual(0, RelaySpawner.RelayPort);
                Assert.AreEqual(0, RelaySpawner.RelayPid);
            }
        }
    }
}
