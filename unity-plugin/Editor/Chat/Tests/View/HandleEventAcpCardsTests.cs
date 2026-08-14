// TDD RED: HandleEvent dispatch tests for new ACP event kinds.
// Tests fail at runtime:
//   - PlanUpdate/FileChange: no switch case in EventHandlers yet → 0 elements in transcript
//   - CapabilitiesChanged: _capabilities field doesn't exist yet → NullReferenceException
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class HandleEventAcpCardsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        static readonly FieldInfo s_transcript = typeof(MCPChatWindow)
            .GetField("_transcript", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_capabilities = typeof(MCPChatWindow)
            .GetField("_capabilities", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo s_handleEvent = typeof(MCPChatWindow)
            .GetMethod("HandleEvent", BindingFlags.NonPublic | BindingFlags.Instance);

        private MCPChatWindow _window;
        private VisualElement _container;
        private ChatTranscript _transcript;

        [SetUp]
        public void SetUp()
        {
            _window    = CreateOwnedEditorWindow<MCPChatWindow>();
            _container = new VisualElement();
            var registry = ChatBlockRendererFactory.CreateDefault(null, null);
            _transcript  = new ChatTranscript(_container, registry);
            s_transcript.SetValue(_window, _transcript);
        }

        private void Fire(ChatEvent ev) =>
            s_handleEvent.Invoke(_window, new object[] { ev });

        [Test]
        public void HandleEvent_PlanUpdate_AddsCardToTranscript()
        {
            Fire(ChatEvent.PlanUpdate("plan_step_started", "Install packages"));

            var card = _container.Q(null, "plan-step-card");
            Assert.IsNotNull(card, "PlanUpdate must add a .plan-step-card element to the transcript");
        }

        [Test]
        public void HandleEvent_FileChange_AddsChipToTranscript()
        {
            Fire(ChatEvent.FileChange("/Assets/Foo.cs"));

            var labels = _container.Query<Label>().ToList();
            bool found = labels.Any(l => l.text != null && l.text.Contains("/Assets/Foo.cs"));
            Assert.IsTrue(found, "FileChange must add a chip showing the file path");
        }

        [Test]
        public void HandleEvent_CapabilitiesChanged_StoresState()
        {
            Fire(ChatEvent.CapabilitiesChanged("connected"));

            // _capabilities field must exist and be set to the state string
            var capabilities = (string)s_capabilities.GetValue(_window);
            Assert.AreEqual("connected", capabilities,
                "_capabilities field must store the CapabilitiesChanged state");
        }
    }
}
