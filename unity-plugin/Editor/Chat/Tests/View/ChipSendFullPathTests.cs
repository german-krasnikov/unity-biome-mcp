// Integration: full send flow verifies ToLlmPayload uses Path, not DisplayName.
// Mirrors ChipSendSequenceTests — reuses ChipTestHelpers/SimulateSend.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSendFullPathTests
    {
        private InlineChipField _chipField;
        private ChatTranscript  _transcript;
        private VisualElement   _container;
        private ChipConfig      _cfg;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField  = new InlineChipField();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container, ChatBlockRendererFactory.CreateDefault(null, null));
            _cfg        = new ChipConfig();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private (string turnJson, string rawText) SimulateSend()
            => ChipViewTestHelpers.SimulateSend(_chipField, _transcript, _cfg);

        // Dual-chip: hierarchy + script
        // turnJson must contain both full-path @-mentions AND both [kind:path] brackets.
        [Test]
        public void DualChip_HierarchyAndScript_TurnJsonHasFullPathMentions()
        {
            _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/GridPlayer", "GridPlayer", 42));
            _chipField.AddChip(new ChipData(ChipKindKeys.Script,
                "Assets/Tests/Editor/CommandRouterTests.cs", "CommandRouterTests", 0));
            ChipViewTestHelpers.Type(_chipField, "что это?");

            var (tj, _) = SimulateSend();

            // Full-path inline @-mentions in LLM text
            StringAssert.Contains("@/GridPlayer",                                tj);
            StringAssert.Contains("@Assets/Tests/Editor/CommandRouterTests.cs",  tj);

            // Bracket context blocks also present
            StringAssert.Contains("[hierarchy:/GridPlayer#42]",                                    tj);
            StringAssert.Contains("[script:Assets/Tests/Editor/CommandRouterTests.cs]",             tj);
        }

        // Bare short-name must NOT be the inline @-mention token in the text block
        [Test]
        public void DualChip_TurnJsonTextBlock_DoesNotContainBareShortName()
        {
            _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/GridPlayer", "GridPlayer", 42));
            _chipField.AddChip(new ChipData(ChipKindKeys.Script,
                "Assets/Tests/Editor/CommandRouterTests.cs", "CommandRouterTests", 0));
            ChipViewTestHelpers.Type(_chipField, "что это?");

            var (tj, _) = SimulateSend();
            var textBlock = tj.Split('\n')[0]; // first line is the plain-text portion

            StringAssert.DoesNotContain("@GridPlayer ",       textBlock);
            StringAssert.DoesNotContain("@CommandRouterTests ", textBlock);
        }

        // UI bubble display: chip-strip pills use short DisplayName (unchanged)
        [Test]
        public void DualChip_BubblePills_ShowShortDisplayName()
        {
            _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/GridPlayer", "GridPlayer", 42));
            _chipField.AddChip(new ChipData(ChipKindKeys.Script,
                "Assets/Tests/Editor/CommandRouterTests.cs", "CommandRouterTests", 0));
            ChipViewTestHelpers.Type(_chipField, "что это?");
            SimulateSend();

            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasChipStrip(bubble, 2);
            var wrap   = bubble.Q(className: "msg-user-content");
            var pills  = wrap.Query(className: "inline-chip-pill").ToList();
            // Pills show short display name (UI unchanged)
            ChatWindowAssertions.AssertPillContent(pills[0], ChipKindKeys.Hierarchy, "GridPlayer");
            ChatWindowAssertions.AssertPillContent(pills[1], ChipKindKeys.Script,    "CommandRouterTests");
        }
    }
}
