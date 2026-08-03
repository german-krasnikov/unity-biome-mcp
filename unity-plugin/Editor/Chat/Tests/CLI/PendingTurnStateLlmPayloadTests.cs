// TDD — Group F: PendingTurnState v6 (full-path LLM payload) serialize/deserialize.
// task#10: reload-resume must re-send the full-path payload, not the short-name display text.
// Pure headless — System only, no Unity deps.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PendingTurnStateLlmPayloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // F1: v6 roundtrip — full-path payload preserved verbatim (paths + bracket block).
        [Test]
        public void Serialize_LlmPayload_RoundTripsCleanly()
        {
            var payload = "look at @/Env/Player\n[hierarchy:/Env/Player#1234]";
            var orig = new PendingTurnState(
                "sess", "look at @Player", new[] { "/Env/Player" },
                false, null, "Sending",
                kindKeys:        new[] { "hierarchy" },
                chipTextOffsets: new[] { 8 },
                pendingLlmPayload: payload);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(payload, rt.Value.PendingLlmPayload);
        }

        // F2: payload independent of display text — both survive distinctly.
        [Test]
        public void LlmPayload_IndependentOfDisplayText_BothPreserved()
        {
            var orig = new PendingTurnState(
                "s", "@Player hi", new[] { "/Env/Player" },
                false, null, "Sending",
                kindKeys: new[] { "hierarchy" },
                pendingLlmPayload: "@/Env/Player hi\n[hierarchy:/Env/Player#5]");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("@Player hi", rt.Value.PendingText, "display text unchanged");
            Assert.AreEqual("@/Env/Player hi\n[hierarchy:/Env/Player#5]",
                rt.Value.PendingLlmPayload, "llm payload carries full path + bracket block");
        }

        // F3: null payload in ctor → serializes as empty, deserializes to "".
        [Test]
        public void NullLlmPayload_SerializesAsEmptyString()
        {
            var orig = new PendingTurnState(
                "s", "t", new[] { "/A" },
                false, null, "Idle",
                kindKeys: new[] { "hierarchy" },
                pendingLlmPayload: null);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("", rt.Value.PendingLlmPayload);
        }

        // F4: BACKWARD-COMPAT — a v5 blob (no payload field) deserializes with empty payload, no crash.
        [Test]
        public void Deserialize_LegacyV5Blob_LlmPayloadDefaultsToEmpty()
        {
            // Build a v5 blob, then strip the trailing header field to simulate pre-v6 persisted state.
            var v6 = new PendingTurnState(
                "sess", "hello", new[] { "/A" },
                false, null, "Sending",
                kindKeys:        new[] { "hierarchy" },
                chipTextOffsets: new[] { 0 },
                pendingLlmPayload: "@/A\n[hierarchy:/A#1]");
            var serialized = v6.Serialize();
            var lines  = serialized.Split('\n');
            var header = lines[0].Split('|');
            // v5 header has 9 fields (no payload). Drop the last (payload) field.
            lines[0] = string.Join("|", header[0], header[1], header[2], header[3],
                header[4], header[5], header[6], header[7], header[8]);
            var v5Str = string.Join("\n", lines);

            var rt = PendingTurnState.Deserialize(v5Str);
            Assert.IsNotNull(rt, "old blob must deserialize, not crash");
            Assert.AreEqual("", rt.Value.PendingLlmPayload, "missing field → empty payload");
            Assert.AreEqual("hello", rt.Value.PendingText, "rest of v5 blob intact");
            Assert.AreEqual("/A", rt.Value.ChipPaths[0]);
        }

        // F5: payload with embedded newlines survives (base64-encoded, line format safe).
        [Test]
        public void MultilinePayload_SurvivesRoundTrip()
        {
            var payload = "line1 @/X\nline2\n[hierarchy:/X#9]\n[component:/X/Rb#9]";
            var orig = new PendingTurnState(
                "s", "line1 @X\nline2", new[] { "/X" },
                false, null, "Sending",
                kindKeys: new[] { "hierarchy" },
                pendingLlmPayload: payload);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(payload, rt.Value.PendingLlmPayload);
        }

        // F6: regression — every other v5 field still round-trips with the new field present.
        [Test]
        public void AllPriorFields_UnchangedAfterAddingLlmPayload()
        {
            var orig = new PendingTurnState(
                "sess-xyz", "hello world", new[] { "/P", "/E" },
                true, "agent", "Sending",
                undoGroupId: 5, savedAtUtc: 9999L,
                backendKind: BackendKind.Codex,
                kindKeys:        new[] { "hierarchy", "hierarchy" },
                chipTextOffsets: new[] { 2, 7 },
                pendingLlmPayload: "@/P @/E\n[hierarchy:/P#1]");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("sess-xyz",        rt.Value.SessionId);
            Assert.AreEqual("hello world",     rt.Value.PendingText);
            Assert.AreEqual(2,                 rt.Value.ChipPaths.Length);
            Assert.IsTrue(rt.Value.AgentMode);
            Assert.AreEqual("agent",           rt.Value.AgentName);
            Assert.AreEqual(5,                 rt.Value.UndoGroupId);
            Assert.AreEqual(9999L,             rt.Value.SavedAtUtc);
            Assert.AreEqual(BackendKind.Codex, rt.Value.BackendKind);
            Assert.AreEqual(2,                 rt.Value.ChipTextOffsets[0]);
            Assert.AreEqual(7,                 rt.Value.ChipTextOffsets[1]);
            Assert.AreEqual("@/P @/E\n[hierarchy:/P#1]", rt.Value.PendingLlmPayload);
        }
    }
}
