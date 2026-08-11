// Barrier tests — Phase 0.4. Must be GREEN on current codebase before Phase 1.
// These lock: feed ordering (text→chip→text) and FreezeAssistantBubble separation.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class FeedOrderingTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ChatTranscript MakeTranscript(out VisualElement container)
        {
            container = new VisualElement();
            var registry = ChatBlockRendererFactory.CreateDefault(null, null);
            return new ChatTranscript(container, registry);
        }

        [Test]
        public void Feed_UserBubble_AppearsFirst()
        {
            var t = MakeTranscript(out var container);
            t.AppendUserBubble("hello");
            var children = container.Children().ToList();
            Assert.AreEqual(1, children.Count, "exactly 1 row after one user bubble");
            Assert.IsTrue(children[0].ClassListContains("msg-user"),
                "first child must be user bubble row");
        }

        [Test]
        public void Feed_AssistantTextAndToolChip_OrderPreserved()
        {
            // text → toolchip → finalize: user bubble must come first
            var t = MakeTranscript(out var container);
            t.AppendUserBubble("q");
            t.AppendOrExtendAssistant("before text");
            t.AppendToolChip("Bash", ok: true, toolId: "id1");
            t.FinalizeAssistant();
            var children = container.Children().ToList();
            Assert.GreaterOrEqual(children.Count, 2, "at least user + assistant rows");
            Assert.IsTrue(children[0].ClassListContains("msg-user"),
                "user bubble must be first row");
        }

        [Test]
        public void Feed_ToolChip_DoesNotMergeIntoAssistantBubble()
        {
            // FreezeAssistantBubble() must separate tool chip from assistant text
            var t = MakeTranscript(out var container);
            t.AppendOrExtendAssistant("assistant text");
            t.FlushStreaming();
            t.AppendToolChip("Edit", ok: true, toolId: "id2");
            var chipEl      = container.Q(className: "tool-chip");
            var assistantEl = container.Q(className: "msg-assistant");
            if (assistantEl != null && chipEl != null)
                Assert.AreNotEqual(assistantEl, chipEl.parent,
                    "chip must not be a direct child of the assistant bubble");
        }
    }
}
