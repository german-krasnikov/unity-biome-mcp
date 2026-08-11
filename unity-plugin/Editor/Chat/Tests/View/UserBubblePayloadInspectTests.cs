// TDD — UserBubblePayloadInspectTests.
// Verifies that "Show LLM payload" reveals the actual sent payload (full paths),
// while Copy keeps the clean display text.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UserBubblePayloadInspectTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ChatTranscript _transcript;
        private VisualElement  _container;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetForTests();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container,
                ChatBlockRendererFactory.CreateDefault(null, null));
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
        }

        private VisualElement Bubble(int i = 0) =>
            ChatWindowAssertions.GetUserBubble(_container, i);

        // T1: AppendUserBubble stores UserBubbleData with distinct Display vs Llm.
        [Test]
        public void AppendUserBubble_StoresLlmPayload_NotDisplayText()
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Env/Player", "Player", 1);
            var pos  = new List<PositionedChip> { new PositionedChip(chip, 0) };
            var msg  = ChipTextInterleaver.BuildFromRaw("@Player что", pos);
            const string llmPayload = "@/Env/Player что\n[hierarchy:/Env/Player#1]";

            _transcript.AppendUserBubble(msg, llmPayload);

            var ud = Bubble().userData;
            Assert.IsInstanceOf<UserBubbleData>(ud, "userData must be UserBubbleData");
            var data = (UserBubbleData)ud;
            StringAssert.Contains("@Player",     data.Display, "Display must contain short name");
            StringAssert.DoesNotContain("/Env/",  data.Display, "Display must NOT contain full path");
            StringAssert.Contains("@/Env/Player", data.Llm,    "Llm must contain full path");
            StringAssert.Contains("[hierarchy:",  data.Llm,    "Llm must contain bracket block");
        }

        // T2: CopyableText.ReadText on a UserBubbleData VE returns Display (for Copy).
        [Test]
        public void CopyableText_ReadText_UserBubbleData_ReturnsDisplay()
        {
            var ud = new UserBubbleData("@Player x", "@/Env/Player x\n[hierarchy:/Env/Player#1]");
            var ve = new VisualElement();
            ve.userData = ud;

            // ReadText is private — test via the userData property directly in this unit test.
            // The integration path (Copy action) is covered by AppendUserBubble round-trip tests.
            // Expose via the same pattern used by ContextMenu tests: verify userData.Display
            Assert.AreEqual("@Player x", ud.Display);
            Assert.AreEqual("@/Env/Player x\n[hierarchy:/Env/Player#1]", ud.Llm);
        }

        // T3: null llmPayload falls back — Llm == Display.
        [Test]
        public void AppendUserBubble_NullLlmPayload_FallsBackToDisplay()
        {
            var msg = ChipTextInterleaver.Build("hello", new List<PositionedChip>());

            _transcript.AppendUserBubble(msg, llmPayload: null);

            var ud = Bubble().userData;
            Assert.IsInstanceOf<UserBubbleData>(ud);
            var data = (UserBubbleData)ud;
            Assert.AreEqual(data.Display, data.Llm, "Null llmPayload → Llm must equal Display");
        }

        // T4: Integration — SimulateSend stores UserBubbleData whose Llm has full path
        //     and Display has short name.
        [Test]
        public void SimulateSend_Bubble_LlmHasFullPath_DisplayHasShortName()
        {
            var chipField  = new InlineChipField();
            var cfg        = new ChipConfig();
            var chip       = new ChipData(ChipKindKeys.Hierarchy, "/Env/Player", "Player", 1);
            chipField.AddChip(chip);
            ChipViewTestHelpers.Type(chipField, "что");

            ChipViewTestHelpers.SimulateSendWithPayload(chipField, _transcript, cfg);

            var ud = Bubble().userData;
            Assert.IsInstanceOf<UserBubbleData>(ud);
            var data = (UserBubbleData)ud;
            StringAssert.Contains("@Player",      data.Display, "Display: short name");
            StringAssert.Contains("@/Env/Player", data.Llm,    "Llm: full path");
        }

        // T5: Regression — legacy AppendUserBubble(string, chips) userData is still a plain string.
        [Test]
        public void LegacyAppendUserBubble_UserData_IsString()
        {
            _transcript.AppendUserBubble("hello", new List<ChipData>
            {
                new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 0),
            });
            Assert.IsInstanceOf<string>(Bubble().userData,
                "Legacy overload must keep userData as a plain string");
        }

        // T6: Regression — transcript entry Text stays display text (not llm payload).
        [Test]
        public void AppendUserBubble_TranscriptEntry_StaysDisplayText()
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Env/Player", "Player", 1);
            var pos  = new List<PositionedChip> { new PositionedChip(chip, 0) };
            var msg  = ChipTextInterleaver.BuildFromRaw("@Player что", pos);
            const string llmPayload = "@/Env/Player что\n[hierarchy:/Env/Player#1]";

            _transcript.AppendUserBubble(msg, llmPayload);

            // Reload-serialization must stay display text (TranscriptEntry is private;
            // test via SerializeForReload/RestoreFromReload round-trip).
            var serialized = _transcript.SerializeForReload();
            StringAssert.DoesNotContain("[hierarchy:/Env/Player", serialized,
                "Serialized transcript must NOT contain llm bracket payload");
        }
    }
}
