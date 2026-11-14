using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChatHostBridgeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeChatHostBridge : IChatHostBridge
        {
            public void BuildSettingsUI(VisualElement root) { }
            public bool IsBinaryAvailable() => false;
            public void StopRelay() { }
            public void OnAnalyzeInChatRequested(string sessionId) { }
        }

        [SetUp]
        public void SetUp()
        {
            var previous = ChatHostBridge.Current;
            RegisterCleanup(() => ChatHostBridge.Current = previous);
        }

        [Test]
        public void Current_CanBeSetToNull()
        {
            // Not a global-default assertion: once Chat.View's ChatHostBridgeImpl registers
            // via [InitializeOnLoad] (sub-task 2.2), Current is non-null for the rest of the
            // process. What every call site relies on is that null is still a valid, safely
            // null-checkable state.
            ChatHostBridge.Current = null;
            Assert.IsNull(ChatHostBridge.Current);
        }

        [Test]
        public void Current_RoundTrips()
        {
            var fake = new FakeChatHostBridge();
            ChatHostBridge.Current = fake;
            Assert.AreSame(fake, ChatHostBridge.Current);
        }

        [Test]
        public void Current_SecondAssignment_Overwrites()
        {
            var first = new FakeChatHostBridge();
            var second = new FakeChatHostBridge();
            ChatHostBridge.Current = first;
            ChatHostBridge.Current = second;
            Assert.AreSame(second, ChatHostBridge.Current);
        }

        [Test]
        public void PreserveForTests_RestoresExactValue()
        {
            var original = new FakeChatHostBridge();
            ChatHostBridge.Current = original;

            using (ChatHostBridge.PreserveForTests())
            {
                ChatHostBridge.Current = new FakeChatHostBridge();
            }

            Assert.AreSame(original, ChatHostBridge.Current);
        }
    }
}
