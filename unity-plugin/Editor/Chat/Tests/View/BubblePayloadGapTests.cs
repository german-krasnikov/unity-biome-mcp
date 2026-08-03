// TDD — G1/G2/G3 payload gap tests.
// G1: legacy DispatchTurn overload bubbles get llmPayload == displayText.
// G2: reload-resume bubble captures sentText (snapshot + displayText).
// G3: LlmPayload survives serialize/restore round-trip; old data backward-compat.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BubblePayloadGapTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ChatTranscript _transcript;
        private VisualElement  _container;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
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

        private VisualElement Bubble(int i = 0)
            => ChatWindowAssertions.GetUserBubble(_container, i);

        // ── G1: legacy overload with explicit llmPayload ──────────────────────

        // G1a: legacy overload + non-null llmPayload → UserBubbleData with Llm == llmPayload.
        [Test]
        public void G1a_LegacyOverload_ExplicitLlmPayload_UserBubbleData()
        {
            const string displayText  = "Compile errors after your edit:\nCS0117\nFix them.";
            const string llmPayload   = displayText; // compile-error strings: sent == displayed

            _transcript.AppendUserBubble(displayText, (IReadOnlyList<ChipData>)null, llmPayload: llmPayload);

            var ud = Bubble().userData;
            Assert.IsInstanceOf<UserBubbleData>(ud, "userData must be UserBubbleData when llmPayload is non-null");
            var data = (UserBubbleData)ud;
            Assert.AreEqual(displayText, data.Display, "Display must match displayText");
            Assert.AreEqual(llmPayload,  data.Llm,     "Llm must match llmPayload");
        }

        // G1b: approve-string variant — same contract.
        [Test]
        public void G1b_ApproveString_LlmPayload_Matches()
        {
            const string prompt = "Execute the plan above.";

            _transcript.AppendUserBubble(prompt, (IReadOnlyList<ChipData>)null, llmPayload: prompt);

            var data = (UserBubbleData)Bubble().userData;
            Assert.AreEqual(prompt, data.Llm,     "Llm must equal approve prompt");
            Assert.AreEqual(prompt, data.Display, "Display must equal approve prompt");
        }

        // G1c: legacy overload with null llmPayload → still plain string userData (regression).
        [Test]
        public void G1c_LegacyOverload_NullLlmPayload_PlainStringUserData()
        {
            _transcript.AppendUserBubble("hello", (IReadOnlyList<ChipData>)null, llmPayload: null);

            Assert.IsInstanceOf<string>(Bubble().userData,
                "null llmPayload → plain string userData (regression guard)");
        }

        // ── G2: sentText threading ────────────────────────────────────────────

        // G2a: AppendUserBubble(displayText, chips, llmPayload: sentText) stores sentText in Llm.
        [Test]
        public void G2a_SentText_StoredInLlmField()
        {
            const string displayText = "fix it";
            const string sentText    = "<SNAP:scene=Main>\nfix it";

            _transcript.AppendUserBubble(displayText, (IReadOnlyList<ChipData>)null, llmPayload: sentText);

            var data = (UserBubbleData)Bubble().userData;
            StringAssert.Contains("<SNAP:scene=Main>", data.Llm,    "Llm must contain snapshot");
            StringAssert.DoesNotContain("<SNAP:",       data.Display,"Display must NOT contain snapshot");
        }

        // G2b: Display does not contain snapshot — only original displayText.
        [Test]
        public void G2b_Display_DoesNotContainSnapshot()
        {
            const string displayText = "fix it";
            const string sentText    = "<SNAP>\n" + displayText;

            _transcript.AppendUserBubble(displayText, (IReadOnlyList<ChipData>)null, llmPayload: sentText);

            var data = (UserBubbleData)Bubble().userData;
            Assert.AreEqual(displayText, data.Display);
            StringAssert.Contains("<SNAP>", data.Llm);
        }

        // ── G3: LlmPayload round-trip ─────────────────────────────────────────

        // G3a: primary AppendUserBubble(UserMessage, llmPayload) → serialize → restore → UserBubbleData.Llm preserved.
        [Test]
        public void G3a_PrimaryOverload_LlmPayload_SurvivesReloadRoundTrip()
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Env/Player", "Player", 1);
            var pos  = new List<PositionedChip> { new PositionedChip(chip, 0) };
            var msg  = ChipTextInterleaver.BuildFromRaw("@Player fix", pos);
            const string llm = "@/Env/Player fix\n[hierarchy:/Env/Player#1]";

            _transcript.AppendUserBubble(msg, llm);
            var serialized = _transcript.SerializeForReload();

            var c2 = new VisualElement();
            var t2 = new ChatTranscript(c2, ChatBlockRendererFactory.CreateDefault(null, null));
            t2.RestoreFromReload(serialized);

            var restored = ChatWindowAssertions.GetUserBubble(c2, 0);
            Assert.IsInstanceOf<UserBubbleData>(restored.userData,
                "Restored bubble must have UserBubbleData when LlmPayload was persisted");
            var data = (UserBubbleData)restored.userData;
            StringAssert.Contains("[hierarchy:/Env/Player#1]", data.Llm,
                "Restored Llm must contain bracket payload");
            StringAssert.DoesNotContain("[hierarchy:", data.Display,
                "Restored Display must not contain bracket payload");
        }

        // G3b: backward-compat — old serialized blob (3-column, no LlmPayload) restores to plain string.
        [Test]
        public void G3b_OldSerializedData_NoLlmPayload_RestoresToString()
        {
            // Manually craft old-format serialized data (no 4th column).
            const string text = "old user message";
            var textB64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
            var oldData = $"0|{textB64}|\n"; // Kind=User, text, empty chips, NO llmPayload column

            var c2 = new VisualElement();
            var t2 = new ChatTranscript(c2, ChatBlockRendererFactory.CreateDefault(null, null));
            Assert.DoesNotThrow(() => t2.RestoreFromReload(oldData),
                "Old format must not throw");

            var restored = ChatWindowAssertions.GetUserBubble(c2, 0);
            Assert.IsInstanceOf<string>(restored.userData,
                "Old serialized data without LlmPayload → plain string userData (no crash)");
            Assert.AreEqual(text, restored.userData);
        }

        // G3c: legacy AppendUserBubble(string, chips) without llmPayload → serialize → restore → still plain string.
        [Test]
        public void G3c_LegacyNoLlmPayload_RoundTrip_StillString()
        {
            _transcript.AppendUserBubble("hello world");
            var serialized = _transcript.SerializeForReload();

            var c2 = new VisualElement();
            var t2 = new ChatTranscript(c2, ChatBlockRendererFactory.CreateDefault(null, null));
            t2.RestoreFromReload(serialized);

            var restored = ChatWindowAssertions.GetUserBubble(c2, 0);
            Assert.IsInstanceOf<string>(restored.userData,
                "Legacy bubble with no llmPayload must restore as plain string");
            Assert.AreEqual("hello world", restored.userData);
        }

        // G3d: LlmPayload column survives double round-trip (idempotency).
        [Test]
        public void G3d_LlmPayload_Idempotent_DoubleRoundTrip()
        {
            var msg = ChipTextInterleaver.Build("hello", new List<PositionedChip>());
            const string llm = "hello\n[hierarchy:/A#1]";
            _transcript.AppendUserBubble(msg, llm);
            var data1 = _transcript.SerializeForReload();

            var c2 = new VisualElement();
            var t2 = new ChatTranscript(c2, ChatBlockRendererFactory.CreateDefault(null, null));
            t2.RestoreFromReload(data1);
            var data2 = t2.SerializeForReload();

            Assert.AreEqual(data1, data2, "Double round-trip must be idempotent with LlmPayload");
        }
    }
}
