// TDD — AssistantBubbleNormalizationTests (F14a review fix).
// Verifies FreezeAssistantBubble normalizes @mentions and bare names to [kind:ref] tags.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AssistantBubbleNormalizationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ChatTranscript _transcript;
        private VisualElement  _container;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container,
                ChatBlockRendererFactory.CreateDefault(null, null));
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        [Test]
        public void FreezeAssistantBubble_NormalizesAtMentionToTag()
        {
            var chips = new List<ChipData>
            {
                new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1)
            };
            _transcript.SetLastTurnChips(chips);
            _transcript.AppendOrExtendAssistant("check @Player health");
            _transcript.FinalizeAssistant();

            var bubble = _container.Q(className: "msg-bubble--assistant");
            Assert.IsNotNull(bubble);
            var userData = bubble.userData as string ?? "";
            StringAssert.Contains("[hierarchy:/Player$1]", userData);
            StringAssert.DoesNotContain("@Player", userData);
        }

        [Test]
        public void FreezeAssistantBubble_NormalizesBareNameToTag()
        {
            var chips = new List<ChipData>
            {
                new ChipData(ChipKindKeys.Hierarchy, "/Enemy", "Enemy", 7)
            };
            _transcript.SetLastTurnChips(chips);
            _transcript.AppendOrExtendAssistant("the Enemy is active");
            _transcript.FinalizeAssistant();

            var bubble = _container.Q(className: "msg-bubble--assistant");
            Assert.IsNotNull(bubble);
            var userData = bubble.userData as string ?? "";
            StringAssert.Contains("[hierarchy:/Enemy$7]", userData);
        }
    }
}
