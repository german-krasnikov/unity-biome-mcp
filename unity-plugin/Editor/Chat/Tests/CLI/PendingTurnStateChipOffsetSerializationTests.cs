// TDD — Group E: PendingTurnState v5 (chip text offsets) serialize/deserialize.
// Pure headless — System only, no Unity deps.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PendingTurnStateChipOffsetSerializationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // E1: v5 roundtrip — offsets preserved
        [Test]
        public void Serialize_ChipTextOffsets_RoundTripsCleanly()
        {
            var orig = new PendingTurnState(
                "sess", "hello", new[] { "/Player", "/Enemy" },
                false, null, "Sending",
                kindKeys:        new[] { "hierarchy", "hierarchy" },
                chipTextOffsets: new[] { 3, 9 });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(2, rt.Value.ChipTextOffsets.Length);
            Assert.AreEqual(3, rt.Value.ChipTextOffsets[0]);
            Assert.AreEqual(9, rt.Value.ChipTextOffsets[1]);
        }

        // E2: v4 deserialized → offsets default to 0, no throw
        [Test]
        public void Deserialize_LegacyV4Blob_OffsetsDefaultToZero()
        {
            // Build a v4 string: PathB64|KindKeyB64 (no offset)
            var v4 = new PendingTurnState(
                "sess", "text", new[] { "/A" },
                false, null, "Sending",
                kindKeys: new[] { "hierarchy" });
            // Manually remove the offset from chip line to simulate v4 format.
            var serialized = v4.Serialize();
            // v5 format is PathB64|KindKeyB64|Offset; strip offset from chip line.
            var lines = serialized.Split('\n');
            // Chip line is lines[1]: strip last |0
            var chipLine = lines[1];
            var lastPipe = chipLine.LastIndexOf('|');
            lines[1] = chipLine.Substring(0, lastPipe); // remove |0 → v4 format
            var v4Str = string.Join("\n", lines);

            var rt = PendingTurnState.Deserialize(v4Str);
            Assert.IsNotNull(rt);
            Assert.IsNotNull(rt.Value.ChipTextOffsets);
            Assert.AreEqual(0, rt.Value.ChipTextOffsets[0], "v4 back-compat: offset defaults to 0");
        }

        // E3: v3 deserialized → offsets default to 0, no throw
        [Test]
        public void Deserialize_LegacyV3Blob_OffsetsDefaultToZero()
        {
            // v3 format: chip line has only PathB64 (no pipe at all)
            var v4 = new PendingTurnState(
                "sess", "text", new[] { "/A" },
                false, null, "Sending",
                kindKeys: new[] { "hierarchy" });
            var serialized = v4.Serialize();
            var lines = serialized.Split('\n');
            // Strip chip line to just PathB64 (simulate v3: no pipe)
            // lines[1] = PathB64|KindKeyB64|Offset → take only PathB64 portion
            var chipLine = lines[1];
            var firstPipe = chipLine.IndexOf('|');
            lines[1] = chipLine.Substring(0, firstPipe);
            var v3Str = string.Join("\n", lines);

            var rt = PendingTurnState.Deserialize(v3Str);
            Assert.IsNotNull(rt);
            Assert.IsNotNull(rt.Value.ChipTextOffsets);
            Assert.AreEqual(0, rt.Value.ChipTextOffsets[0]);
        }

        // E4: zero offset chips survive roundtrip
        [Test]
        public void ZeroOffsets_RoundTripsCleanly()
        {
            var orig = new PendingTurnState(
                "s", "t", new[] { "/A", "/B" },
                false, null, "Idle",
                kindKeys:        new[] { "hierarchy", "hierarchy" },
                chipTextOffsets: new[] { 0, 0 });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(0, rt.Value.ChipTextOffsets[0]);
            Assert.AreEqual(0, rt.Value.ChipTextOffsets[1]);
        }

        // E5: null offsets in constructor → serializes with 0
        [Test]
        public void NullOffsets_SerializeAsZero()
        {
            var orig = new PendingTurnState(
                "s", "t", new[] { "/A" },
                false, null, "Idle",
                kindKeys: new[] { "hierarchy" },
                chipTextOffsets: null);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(0, rt.Value.ChipTextOffsets[0]);
        }

        // E6: existing KindKey tests still work (regression guard)
        [Test]
        public void AllPriorFields_UnchangedAfterAddingOffsets()
        {
            var orig = new PendingTurnState(
                "sess-xyz", "hello world", new[] { "/P", "/E" },
                true, "agent", "Sending",
                undoGroupId: 5, savedAtUtc: 9999L,
                backendKind: BackendKind.Codex,
                kindKeys: new[] { "hierarchy", "hierarchy" },
                chipTextOffsets: new[] { 2, 7 });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("sess-xyz",     rt.Value.SessionId);
            Assert.AreEqual("hello world",  rt.Value.PendingText);
            Assert.AreEqual(2,              rt.Value.ChipPaths.Length);
            Assert.AreEqual("/P",           rt.Value.ChipPaths[0]);
            Assert.IsTrue(rt.Value.AgentMode);
            Assert.AreEqual("agent",        rt.Value.AgentName);
            Assert.AreEqual(5,              rt.Value.UndoGroupId);
            Assert.AreEqual(9999L,          rt.Value.SavedAtUtc);
            Assert.AreEqual(BackendKind.Codex, rt.Value.BackendKind);
            Assert.AreEqual(2,              rt.Value.ChipTextOffsets[0]);
            Assert.AreEqual(7,              rt.Value.ChipTextOffsets[1]);
        }
    }
}
