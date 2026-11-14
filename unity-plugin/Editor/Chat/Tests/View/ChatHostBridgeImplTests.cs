// TDD tests for ChatHostBridgeImpl — the Chat.View implementation of IChatHostBridge
// (Task 3.1, sub-tasks 2.2/2.3 of Plans/task-phase2-chat-plugin.md).
using NUnit.Framework;
using UnityEngine.UIElements;

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

        [Test]
        public void BuildSettingsUI_DelegatesToChatConnectionSection()
        {
            var viaBridge = new VisualElement();
            var viaDirect = new VisualElement();

            new ChatHostBridgeImpl().BuildSettingsUI(viaBridge);
            ChatConnectionSection.Build(viaDirect);

            Assert.AreEqual(viaDirect.childCount, viaBridge.childCount,
                "BuildSettingsUI must produce the same content as calling ChatConnectionSection.Build directly");
        }

        [Test]
        public void IsBinaryAvailable_MatchesChatBinaryResolver()
        {
            Assert.AreEqual(ChatBinaryResolver.Resolve(false) != null,
                new ChatHostBridgeImpl().IsBinaryAvailable());
        }
    }
}
