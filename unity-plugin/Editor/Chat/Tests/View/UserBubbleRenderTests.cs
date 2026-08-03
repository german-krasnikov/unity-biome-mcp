using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UserBubbleRenderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        ChatTranscript MakeTranscript(out VisualElement c) {
            c = new VisualElement();
            return new ChatTranscript(c, ChatBlockRendererFactory.CreateDefault(null, null));
        }
        static ChipData H(string p, string n, int id=0) => new ChipData(ChipKindKeys.Hierarchy, p, n, id);
        static ChipData S(string p, string n) => new ChipData(ChipKindKeys.Script, p, n, 0);
        static ChipData A(string p, string n) => new ChipData(ChipKindKeys.Asset, p, n, 0);
        static VisualElement Bubble(VisualElement c, int i=0) => ChatWindowAssertions.GetUserBubble(c, i);
        static void HasStrip(VisualElement b, int n) => ChatWindowAssertions.AssertBubbleHasChipStrip(b, n);
        static void NoStrip(VisualElement b) => ChatWindowAssertions.AssertBubbleHasNoChipStrip(b);

        // ── Chip strip ────────────────────────────────────────────────────────

        [Test] public void ChipStrip_OneChip_StripWithOnePill() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("hi", new List<ChipData> { H("/A","A") });
            HasStrip(Bubble(c), 1);
        }

        [Test] public void ChipStrip_FiveChips_FivePills() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("hi", new List<ChipData> { H("/A","A"),H("/B","B"),H("/C","C"),H("/D","D"),H("/E","E") });
            HasStrip(Bubble(c), 5);
        }

        [Test] public void ChipStrip_MixedKinds_PreservesOrder() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("hi", new List<ChipData> { H("/A","A"), S("S.cs","S"), A("a.png","a") });
            var pills = Bubble(c).Q(className:"user-chip-strip").Query(className:"inline-chip-pill").ToList();
            Assert.AreEqual("hierarchy:", pills[0].Q<Label>(className:"inline-chip-kind").text);
            Assert.AreEqual("script:",    pills[1].Q<Label>(className:"inline-chip-kind").text);
            Assert.AreEqual("asset:",     pills[2].Q<Label>(className:"inline-chip-kind").text);
        }

        [Test] public void ChipStrip_WithText_StripBeforeText() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("hello", new List<ChipData> { H("/A","A") });
            var b = Bubble(c);
            Assert.IsTrue(b[0].ClassListContains("user-chip-strip"));
            Assert.IsTrue(b[1].ClassListContains("msg-text"));
        }

        [Test] public void ChipStrip_NullText_StripOnlyNoMsgText() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble(null, new List<ChipData> { H("/A","A") });
            var b = Bubble(c);
            Assert.IsNotNull(b.Q(className:"user-chip-strip"));
            Assert.IsNull(b.Q(className:"msg-text"));
        }

        [Test] public void ChipStrip_EmptyText_StripOnly() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("", new List<ChipData> { H("/A","A") });
            var b = Bubble(c);
            Assert.IsNotNull(b.Q(className:"user-chip-strip"));
            Assert.IsNull(b.Q(className:"msg-text"));
        }

        // ── Bubble structure ─────────────────────────────────────────────────

        [Test] public void BubbleStructure_HasUserModifier() {
            var t = MakeTranscript(out var c); t.AppendUserBubble("hello");
            var b = Bubble(c);
            Assert.IsTrue(b.ClassListContains("msg-bubble"));
            Assert.IsTrue(b.ClassListContains("msg-bubble--user"));
        }

        [Test] public void BubbleStructure_UserData_IsRawText() {
            var t = MakeTranscript(out var c); t.AppendUserBubble("hello");
            Assert.AreEqual("hello", Bubble(c).userData); }

        [Test] public void BubbleStructure_NullText_UserDataIsEmptyString() {
            var t = MakeTranscript(out var c); t.AppendUserBubble(null);
            Assert.AreEqual("", Bubble(c).userData); }

        [Test] public void BubbleStructure_ChildOrder_StripThenText() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("hello", new List<ChipData> { H("/A","A") });
            var b = Bubble(c);
            Assert.AreEqual(2, b.childCount);
            Assert.IsTrue(b[0].ClassListContains("user-chip-strip"));
            Assert.IsTrue(b[1].ClassListContains("msg-text"));
        }

        // ── Multiple bubbles ─────────────────────────────────────────────────

        [Test] public void MultiBubble_TwoSends_TwoBubbles() {
            var t = MakeTranscript(out var c); t.AppendUserBubble("a"); t.AppendUserBubble("b");
            ChatWindowAssertions.AssertUserBubbleCount(c, 2); }

        [Test] public void MultiBubble_DifferentChipCounts() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("a", new List<ChipData> { H("/A","A"), S("S.cs","S") });
            t.AppendUserBubble("b", new List<ChipData> { H("/B","B") });
            t.AppendUserBubble("c");
            HasStrip(Bubble(c,0),2); HasStrip(Bubble(c,1),1); NoStrip(Bubble(c,2));
        }

        [Test] public void MultiBubble_EachBubbleHasOwnUserData() {
            var t = MakeTranscript(out var c); t.AppendUserBubble("a"); t.AppendUserBubble("b");
            Assert.AreEqual("a", Bubble(c,0).userData); Assert.AreEqual("b", Bubble(c,1).userData); }

        [Test] public void MultiBubble_FirstBubbleUnchangedAfterSecond() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("first", new List<ChipData> { H("/A","A") }); t.AppendUserBubble("second");
            HasStrip(Bubble(c,0), 1); }

        // ── Mixed kinds ──────────────────────────────────────────────────────

        [Test] public void MixedKinds_AllEightBuiltIns() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("all", new List<ChipData> {
                new ChipData(ChipKindKeys.Hierarchy,"/H","H",0), new ChipData(ChipKindKeys.Scene,"S.unity","S",0),
                new ChipData(ChipKindKeys.Script,"Sc.cs","Sc",0), new ChipData(ChipKindKeys.Prefab,"P.prefab","P",0),
                new ChipData(ChipKindKeys.Material,"M.mat","M",0), new ChipData(ChipKindKeys.Texture,"T.png","T",0),
                new ChipData(ChipKindKeys.ScriptableObject,"So.asset","So",0), new ChipData(ChipKindKeys.Asset,"A.asset","A",0),
            });
            HasStrip(Bubble(c), 8);
        }

        [Test] public void MixedKinds_EachPillHasDistinctColor() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("x", new List<ChipData> { H("/H","H"), S("S.cs","S"), new ChipData(ChipKindKeys.Material,"M.mat","M",0) });
            var pills = Bubble(c).Q(className:"user-chip-strip").Query(className:"inline-chip-pill").ToList();
            Assert.AreNotEqual(pills[0].style.backgroundColor.value, pills[1].style.backgroundColor.value);
            Assert.AreNotEqual(pills[1].style.backgroundColor.value, pills[2].style.backgroundColor.value);
        }

        // ── Edge cases ───────────────────────────────────────────────────────

        [Test] public void Edge_ChipWithEmptyDisplayName_PillRendersEmptyName() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("x", new List<ChipData> { new ChipData(ChipKindKeys.Hierarchy,"/A","",0) });
            var pill = Bubble(c).Q(className:"user-chip-strip").Query(className:"inline-chip-pill").First();
            Assert.AreEqual("", pill.Q<Label>(className:"inline-chip-label").text);
        }

        [Test] public void Edge_VeryLongDisplayName_PillRendersWithFullName() {
            var name = new string('x', 200);
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("x", new List<ChipData> { new ChipData(ChipKindKeys.Script,"p",name,0) });
            var pill = Bubble(c).Q(className:"user-chip-strip").Query(className:"inline-chip-pill").First();
            Assert.AreEqual(200, pill.Q<Label>(className:"inline-chip-label").text.Length);
        }

        [Test] public void Edge_SpecialCharsInDisplayName_PreservedExactly() {
            const string special = "<Test> & \"quotes\"";
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("x", new List<ChipData> { new ChipData(ChipKindKeys.Asset,"p",special,0) });
            var pill = Bubble(c).Q(className:"user-chip-strip").Query(className:"inline-chip-pill").First();
            Assert.AreEqual(special, pill.Q<Label>(className:"inline-chip-label").text);
        }

        [Test] public void Edge_NullKindKey_DefaultsToAsset() {
            var chip = new ChipData(null, "/P", "P", 0);
            Assert.AreEqual(ChipKindKeys.Asset, chip.KindKey);
            Assert.AreEqual("asset:", ChipPillFactory.Build(chip).Q<Label>(className:"inline-chip-kind").text);
        }

        [Test] public void Edge_ChipsOnlyNoText_BubbleHasOnlyStrip() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble(null, new List<ChipData> { H("/A","A"), S("S.cs","S") });
            var b = Bubble(c);
            Assert.AreEqual(1, b.childCount);
            Assert.IsTrue(b[0].ClassListContains("user-chip-strip"));
        }

        // ── Tag pills + chip strip coexistence ────────────────────────────────

        [Test] public void TagPillsAndChipStrip_BothPresent_NoDuplication() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("[hierarchy:/A#1]", new List<ChipData> { S("S.cs","S") });
            var b = Bubble(c);
            Assert.AreEqual(1, b.Q(className:"user-chip-strip").Query(className:"inline-chip-pill").ToList().Count);
            Assert.AreEqual(1, b.Q(className:"msg-text").Query(className:"inline-chip-pill").ToList().Count);
        }

        [Test] public void TagPillsAndChipStrip_SameKindBothPlaces() {
            var t = MakeTranscript(out var c);
            t.AppendUserBubble("[hierarchy:/A#1]", new List<ChipData> { H("/B","B") });
            var b = Bubble(c);
            string KindText(string stripClass) => b.Q(className:stripClass).Q(className:"inline-chip-pill").Q<Label>(className:"inline-chip-kind").text;
            Assert.AreEqual("hierarchy:", KindText("user-chip-strip"));
            Assert.AreEqual("hierarchy:", KindText("msg-text"));
        }
    }
}
