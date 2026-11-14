using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProfileContextBridgeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeChatHostBridge : IChatHostBridge
        {
            internal string ReceivedSessionId;
            public void BuildSettingsUI(VisualElement root) { }
            public bool IsBinaryAvailable() => false;
            public void StopRelay() { }
            public void OnAnalyzeInChatRequested(string sessionId) => ReceivedSessionId = sessionId;
        }

        [SetUp]
        public void SetUp()
        {
            var previous = ChatHostBridge.Current;
            RegisterCleanup(() => ChatHostBridge.Current = previous);
        }

        [Test]
        public void RequestAnalyzeInChat_ForwardsSessionId_ToChatHostBridgeCurrent()
        {
            var fake = new FakeChatHostBridge();
            ChatHostBridge.Current = fake;

            ProfileContextBridge.RequestAnalyzeInChat("sess-42");

            Assert.AreEqual("sess-42", fake.ReceivedSessionId);
        }

        [Test]
        public void RequestAnalyzeInChat_NoBridgeRegistered_DoesNotThrow()
        {
            ChatHostBridge.Current = null;

            Assert.DoesNotThrow(() => ProfileContextBridge.RequestAnalyzeInChat("sess-1"));
        }
    }
}
