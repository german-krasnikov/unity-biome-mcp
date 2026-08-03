using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChatSettingsHookEventTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(ChatSettingsHook.PreserveConnectionEventForTests().Dispose);
            ChatSettingsHook.ResetConnectionEvent();
        }

        [Test]
        public void OnBuildConnection_NullByDefault_NoSubscribers()
        {
            Assert.IsFalse(ChatSettingsHook.HasConnectionSubscribers);
        }

        [Test]
        public void InvokeConnection_WithSubscriber_CallsIt()
        {
            var called = false;
            ChatSettingsHook.OnBuildConnection += _ => called = true;
            ChatSettingsHook.InvokeConnection(new VisualElement());
            Assert.IsTrue(called);
        }

        [Test]
        public void InvokeConnection_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ChatSettingsHook.InvokeConnection(new VisualElement()));
        }

        [Test]
        public void InvokeConnection_MultipleSubscribers_AllCalled()
        {
            int callCount = 0;
            ChatSettingsHook.OnBuildConnection += _ => callCount++;
            ChatSettingsHook.OnBuildConnection += _ => callCount++;
            ChatSettingsHook.InvokeConnection(new VisualElement());
            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void InvokeConnection_PassesRootElement_ToSubscriber()
        {
            var expected = new VisualElement();
            VisualElement received = null;
            ChatSettingsHook.OnBuildConnection += root => received = root;
            ChatSettingsHook.InvokeConnection(expected);
            Assert.AreSame(expected, received);
        }

        [Test]
        public void PreserveConnectionEventForTests_RestoresExactInvocationList()
        {
            var originalCalls = 0;
            var replacementCalls = 0;
            System.Action<VisualElement> original = _ => originalCalls++;
            ChatSettingsHook.OnBuildConnection += original;

            using (ChatSettingsHook.PreserveConnectionEventForTests())
            {
                ChatSettingsHook.ResetConnectionEvent();
                ChatSettingsHook.OnBuildConnection += _ => replacementCalls++;
            }

            ChatSettingsHook.InvokeConnection(new VisualElement());
            Assert.AreEqual(1, originalCalls);
            Assert.AreEqual(0, replacementCalls);
        }
    }
}
