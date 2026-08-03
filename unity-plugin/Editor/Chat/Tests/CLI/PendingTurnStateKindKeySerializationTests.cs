// TDD — PendingTurnState v4 KindKeys, idle/reload roundtrips, staleness, corrupt input, backward compat.
// Zero Unity deps: System only. Pure NUnit.
using System;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PendingTurnStateKindKeySerializationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── v4 KindKeys roundtrip ─────────────────────────────────────────────

        [Test]
        public void Serialize_KindKeys_RoundTripsCleanly()
        {
            var orig = new PendingTurnState(
                sessionId: "s", pendingText: "t",
                chipPaths: new[] { "/Player", "Assets/Script.cs" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Script });

            var rt = PendingTurnState.Deserialize(orig.Serialize());

            Assert.IsNotNull(rt);
            Assert.AreEqual(2,                       rt.Value.KindKeys.Length);
            Assert.AreEqual(ChipKindKeys.Hierarchy,  rt.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Script,     rt.Value.KindKeys[1]);
        }

        [Test]
        public void Serialize_ChipPaths_RoundTripsCleanly()
        {
            var orig = new PendingTurnState(
                sessionId: "s", pendingText: "t",
                chipPaths: new[] { "/Enemy" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy });

            var rt = PendingTurnState.Deserialize(orig.Serialize());

            Assert.IsNotNull(rt);
            Assert.AreEqual("/Enemy", rt.Value.ChipPaths[0]);
        }

        [Test]
        public void Deserialize_LegacyV3ChipLine_KindKeyDefaultsToEmpty()
        {
            // Simulate a v3 chip line (just PathB64, no pipe separator)
            var pathB64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Assets/foo.fbx"));
            var textB64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("t"));
            var actB64  = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Idle"));
            var raw     = $"sess|{textB64}|0||{actB64}|1|5|100|0\n{pathB64}";

            var rt = PendingTurnState.Deserialize(raw);

            Assert.IsNotNull(rt);
            Assert.AreEqual("Assets/foo.fbx", rt.Value.ChipPaths[0]);
            Assert.AreEqual("",               rt.Value.KindKeys[0], "v3 chip line → empty KindKey (re-detect)");
        }

        [Test]
        public void Deserialize_MissingKindKeys_DefaultsToEmptyString()
        {
            var orig = new PendingTurnState("s", "t", new[] { "/P" }, false, "", "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(1, rt.Value.KindKeys.Length);
            Assert.AreEqual("", rt.Value.KindKeys[0]);
        }

        [Test]
        public void RoundTrip_IdlePhase_ChipsAndTextPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: null, pendingText: "draft text",
                chipPaths: new[] { "/Player", "/Enemy" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { "hierarchy", "hierarchy" });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("Idle",       rt.Value.ActivityPhase);
            Assert.AreEqual("draft text", rt.Value.PendingText);
            Assert.AreEqual(2,            rt.Value.ChipPaths.Length);
            Assert.AreEqual("/Player",    rt.Value.ChipPaths[0]);
        }

        // ── Idle + domain reload roundtrips ───────────────────────────────────

        [Test]
        public void RoundTrip_IdleChipsAndText_KindKeysPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: null, pendingText: "fix player",
                chipPaths: new[] { "/Player", "/Enemy" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Hierarchy });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(ChipKindKeys.Hierarchy, rt.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Hierarchy, rt.Value.KindKeys[1]);
        }

        [Test]
        public void RoundTrip_IdleChipsNoText_ChipsPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: null, pendingText: "",
                chipPaths: new[] { "/Player" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("/Player", rt.Value.ChipPaths[0]);
        }

        [Test]
        public void RoundTrip_IdleNoChipsText_TextPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: null, pendingText: "just typing",
                chipPaths: new string[0],
                agentMode: false, agentName: "", activityPhase: "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("just typing", rt.Value.PendingText);
        }

        [Test]
        public void RoundTrip_ActiveTurnWithChips_AllFieldsPreserved()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var orig = new PendingTurnState(
                sessionId: "sess-123", pendingText: "fix player",
                chipPaths: new[] { "/Player", "Assets/Foo.cs" },
                agentMode: true, agentName: "agent", activityPhase: "Sending",
                undoGroupId: 7, savedAtUtc: now, backendKind: BackendKind.Claude,
                kindKeys: new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Script });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("sess-123",             rt.Value.SessionId);
            Assert.AreEqual("fix player",           rt.Value.PendingText);
            Assert.AreEqual(2,                      rt.Value.ChipPaths.Length);
            Assert.IsTrue(rt.Value.AgentMode);
            Assert.AreEqual("Sending",              rt.Value.ActivityPhase);
            Assert.AreEqual(7,                      rt.Value.UndoGroupId);
            Assert.AreEqual(now,                    rt.Value.SavedAtUtc);
            Assert.AreEqual(ChipKindKeys.Hierarchy, rt.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Script,    rt.Value.KindKeys[1]);
        }

        [Test]
        public void RoundTrip_FiveChipsDifferentKinds_AllPreserved()
        {
            var paths = new[] { "/A", "/B", "Assets/C.cs", "Assets/D.prefab", "Assets/E.mat" };
            var kinds = new[] {
                ChipKindKeys.Hierarchy, ChipKindKeys.Hierarchy,
                ChipKindKeys.Script, ChipKindKeys.Prefab, ChipKindKeys.Material };
            var orig = new PendingTurnState("s", "t", paths, false, "", "Idle", kindKeys: kinds);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(5, rt.Value.ChipPaths.Length);
            for (var i = 0; i < 5; i++)
            {
                Assert.AreEqual(paths[i], rt.Value.ChipPaths[i]);
                Assert.AreEqual(kinds[i], rt.Value.KindKeys[i]);
            }
        }

        [Test]
        public void RoundTrip_ChipOrder_Preserved()
        {
            var paths = new[] { "/C", "/A", "/B" };
            var kinds = new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Script, ChipKindKeys.Prefab };
            var orig = new PendingTurnState("s", "t", paths, false, "", "Idle", kindKeys: kinds);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("/C",                  rt.Value.ChipPaths[0]);
            Assert.AreEqual("/A",                  rt.Value.ChipPaths[1]);
            Assert.AreEqual("/B",                  rt.Value.ChipPaths[2]);
            Assert.AreEqual(ChipKindKeys.Hierarchy, rt.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Script,    rt.Value.KindKeys[1]);
            Assert.AreEqual(ChipKindKeys.Prefab,    rt.Value.KindKeys[2]);
        }

        [Test]
        public void RoundTrip_AtMentionText_SurvivesBase64()
        {
            const string text = "@Player fix @Enemy health";
            var orig = new PendingTurnState("s", text, new string[0], false, "", "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(text, rt.Value.PendingText);
        }

    }
}
