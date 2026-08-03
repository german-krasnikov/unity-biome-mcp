// TDD — F21: transcript survives domain reload via SerializeForReload/RestoreFromReload.
// Zero Unity deps beyond UIElements (VisualElement). Pure NUnit.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TranscriptReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        ChatTranscript Make(out VisualElement c)
        {
            c = new VisualElement();
            return new ChatTranscript(c, ChatBlockRendererFactory.CreateDefault(null, null));
        }
        static ChipData H(string p, string n) => new ChipData(ChipKindKeys.Hierarchy, p, n, 0);

        // 1. empty transcript → empty string
        [Test]
        public void SerializeForReload_EmptyTranscript_ReturnsEmpty()
        {
            var t = Make(out _);
            Assert.IsEmpty(t.SerializeForReload());
        }

        // 2. single user message round-trips
        [Test]
        public void SerializeForReload_UserMessage_RoundTrips()
        {
            var t = Make(out _);
            t.AppendUserBubble("hello world");
            var data = t.SerializeForReload();
            Assert.IsNotEmpty(data);

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);
            // One row added = one user bubble
            Assert.Greater(c2.childCount, 0);
        }

        // 3. assistant message round-trips
        [Test]
        public void SerializeForReload_AssistantMessage_RoundTrips()
        {
            var t = Make(out _);
            t.AppendOrExtendAssistant("**Hello**");
            t.FinalizeAssistant();
            var data = t.SerializeForReload();
            Assert.IsNotEmpty(data);

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);
            Assert.Greater(c2.childCount, 0);
        }

        // 4. user+assistant+user preserves 3 entries in order
        [Test]
        public void SerializeForReload_MixedMessages_PreservesOrder()
        {
            var t = Make(out var c);
            t.AppendUserBubble("first");
            t.AppendOrExtendAssistant("reply");
            t.FinalizeAssistant();
            t.AppendUserBubble("second");
            var data = t.SerializeForReload();

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);
            // 3 messages → 3 rows in the container
            Assert.AreEqual(c.childCount, c2.childCount,
                "Restored container should match original child count");
        }

        // 5. corrupt data doesn't throw
        [Test]
        public void RestoreFromReload_InvalidData_NoThrow()
        {
            var t = Make(out _);
            Assert.DoesNotThrow(() => t.RestoreFromReload("notvalid|||garbage\n99|!!!\n"));
        }

        // 6. empty string is no-op
        [Test]
        public void RestoreFromReload_EmptyString_NoOp()
        {
            var t = Make(out var c);
            t.RestoreFromReload("");
            Assert.AreEqual(0, c.childCount);
        }

        // 7. Clear also resets entries → serialize returns empty
        [Test]
        public void Clear_AlsoResetsEntries()
        {
            var t = Make(out _);
            t.AppendUserBubble("hello");
            t.Clear();
            Assert.IsEmpty(t.SerializeForReload());
        }

        // 8. user message with chips round-trips chip data
        [Test]
        public void SerializeForReload_UserWithChips_ChipsSurvive()
        {
            var t = Make(out _);
            t.AppendUserBubble("ref", new List<ChipData> { H("/Player", "Player") });
            var data = t.SerializeForReload();

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);
            // bubble should have a chip strip
            var bubble = ChatWindowAssertions.GetUserBubble(c2, 0);
            Assert.IsNotNull(bubble.Q(className: "user-chip-strip"),
                "Chip strip should be restored");
        }

        // 9. special chars (pipe, newline) in text survive round-trip
        [Test]
        public void SerializeForReload_SpecialCharsInText_SurviveRoundTrip()
        {
            const string special = "line1\nline2|piped";
            var t = Make(out _);
            t.AppendUserBubble(special);
            var data = t.SerializeForReload();

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);
            // userData on bubble should be the original text
            var bubble = ChatWindowAssertions.GetUserBubble(c2, 0);
            Assert.AreEqual(special, bubble.userData);
        }

        // P0-1 supplemental: in-flight user bubble survives reload round-trip
        [Test]
        public void RestoreFromReload_InFlightUserBubble_PresentInRestoredTranscript()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("in-flight question");
            var data = t1.SerializeForReload();

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);

            var bubble = ChatWindowAssertions.GetUserBubble(c2, 0);
            Assert.IsNotNull(bubble, "User bubble must exist after restore");
            Assert.AreEqual("in-flight question", bubble.userData,
                "Bubble userData must match the original user text");
        }
    }
}
