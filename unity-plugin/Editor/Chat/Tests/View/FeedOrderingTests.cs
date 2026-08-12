// Barrier tests — Phase 0.4. Must be GREEN on current codebase before Phase 1.
// These lock: feed ordering (text→chip→text) and FreezeAssistantBubble separation.
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
            // Sequence: user → assistant text → tool chip → finalize
            // Expected container layout: [0]=user row, [1]=assistant row, [2]=tool chip
            var t = MakeTranscript(out var container);
            t.AppendUserBubble("q");
            t.AppendOrExtendAssistant("before text");
            t.AppendToolChip("Bash", ok: true, toolId: "id1");
            t.FinalizeAssistant();
            var children = container.Children().ToList();

            Assert.GreaterOrEqual(children.Count, 3, "need at least 3 rows: user, assistant, chip");

            // [0] user bubble
            Assert.IsTrue(children[0].ClassListContains("msg-user"),
                "children[0] must be user bubble row");

            // [1] assistant text row contains the bubble element
            Assert.IsNotNull(children[1].Q(className: "msg-bubble--assistant"),
                "children[1] must contain the assistant bubble (msg-bubble--assistant)");

            // [2] chip row — the chip element is a direct child of container
            Assert.IsNotNull(children[2].Q(className: "tool-chip") ?? (children[2].ClassListContains("tool-chip") ? children[2] : null),
                "children[2] must be or contain the tool chip");
        }

        [Test]
        public void Feed_ToolChip_DoesNotMergeIntoAssistantBubble()
        {
            // FreezeAssistantBubble() must separate tool chip from assistant text.
            // chip.parent must NOT be the assistant bubble element.
            var t = MakeTranscript(out var container);
            t.AppendOrExtendAssistant("assistant text");
            t.FlushStreaming();
            t.AppendToolChip("Edit", ok: true, toolId: "id2");

            var chipEl      = container.Q(className: "tool-chip");
            var assistantEl = container.Q(className: "msg-bubble--assistant");

            Assert.IsNotNull(chipEl,      "tool-chip element must exist in container after AppendToolChip");
            Assert.IsNotNull(assistantEl, "msg-bubble--assistant must exist in container after AppendOrExtendAssistant");
            Assert.AreNotEqual(assistantEl, chipEl.parent,
                "chip must not be a direct child of the assistant bubble — FreezeAssistantBubble must separate them");
        }

        // B1: card-chip bypass — Edit has CodeEditDiffRenderer [InitializeOnLoad].
        // With 2 chips in a turn, grouper normally promotes both into a collapsed Foldout.
        // Card-rendered chips must bypass the grouper and be directly visible in the feed.
        [Test]
        public void CardChip_BypassesGrouper_VisibleDirectlyInFeed()
        {
            var t = MakeTranscript(out var container);
            t.AppendToolChip("Bash", ok: true, toolId: "id1"); // no card renderer → grouper
            t.AppendToolChip("Edit", ok: true, toolId: "id2"); // has card renderer → must bypass
            t.FinalizeAssistant();

            // card-chip class is added only when the chip bypasses the grouper
            var cardChip = container.Q(className: "card-chip");
            Assert.IsNotNull(cardChip,
                "Edit chip (with CodeEditDiffRenderer) must have 'card-chip' class and be visible in feed");

            // card-chip must NOT be inside a collapsed tool-group foldout
            var foldout = container.Q<Foldout>(className: "tool-group");
            if (foldout != null)
                Assert.IsNull(foldout.Q(className: "card-chip"),
                    "card-chip must NOT be hidden inside the collapsed tool-group foldout");
        }

        // T0.2: grouper regression — two consecutive read-tool calls in one turn must BOTH
        // appear as visible card-chip elements, not be collapsed into a tool-group foldout.
        // This is the exact failure mode from the prior iteration: grouper silently absorbed
        // all chips when 2+ tool calls were present, even when renderers were registered.
        // UnityMcpTestBase isolates ToolCardRendererRegistry; registrations here are temporary.
        [Test]
        public void TwoReadCards_BothVisibleInFeed_NotAbsorbedByGrouper()
        {
            // Fake renderers simulate future get_hierarchy/get_component card implementations.
            // Without registered renderers the grouper absorbs both chips → Count == 0 → RED.
            ToolCardRendererRegistry.Register("get_hierarchy", new FakeReadRenderer());
            ToolCardRendererRegistry.Register("get_component", new FakeReadRenderer());

            var t = MakeTranscript(out var container);
            t.AppendToolChip("get_hierarchy", ok: true, toolId: "id1");
            t.AppendToolChip("get_component", ok: true, toolId: "id2");
            t.FinalizeAssistant();

            // POSITIVE: both chips must carry card-chip (grouper bypass fired for each).
            var cardChips = container.Query(className: "card-chip").ToList();
            Assert.AreEqual(2, cardChips.Count,
                "Both read cards must bypass the grouper and appear as card-chip elements");

            // POSITIVE: no card-chip may be hidden inside a collapsed tool-group foldout.
            var foldout = container.Q<Foldout>(className: "tool-group");
            if (foldout != null)
                Assert.IsNull(foldout.Q(className: "card-chip"),
                    "No card-chip may reside inside a collapsed tool-group foldout");
        }

        private sealed class FakeReadRenderer : IToolCardRenderer
        {
            public void OnStart(VisualElement chip, ToolCallRecord rec) { }
            public void OnUpdate(VisualElement chip, ToolCallRecord rec) { }
        }
    }
}
