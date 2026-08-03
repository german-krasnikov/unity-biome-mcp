using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ToolCallAccumulatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ToolCallAccumulator _acc;

        [SetUp] public void SetUp() => _acc = new ToolCallAccumulator();

        // ── ToolStart ─────────────────────────────────────────────────────────

        [Test]
        public void Feed_ToolStartWithName_ReturnsChipRecord()
        {
            var ev  = ChatEvent.ToolStart("get_hierarchy", "", "toolu_abc");
            var rec = _acc.Feed(ev);
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("get_hierarchy", rec.Value.Name);
            Assert.AreEqual("toolu_abc",    rec.Value.Id);
            Assert.IsNull(rec.Value.ArgsJson, "chip-creation record must have null ArgsJson (FIX 2)");
        }

        [Test]
        public void Feed_ArgDelta_ReturnsNull()
        {
            _acc.Feed(ChatEvent.ToolStart("tool", "", "id1"));
            var ev  = ChatEvent.ToolStart(null, "{\"pa");   // input_json_delta
            var rec = _acc.Feed(ev);
            Assert.IsNull(rec);
        }

        [Test]
        public void Feed_ArgsComplete_ReturnsRecordWithAssembledArgs()
        {
            _acc.Feed(ChatEvent.ToolStart("get_hierarchy", "", "id1"));
            _acc.Feed(ChatEvent.ToolStart(null, "{\"path\""));
            _acc.Feed(ChatEvent.ToolStart(null, ":\"/Cube\"}"));
            var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("get_hierarchy",      rec.Value.Name);
            Assert.AreEqual("{\"path\":\"/Cube\"}", rec.Value.ArgsJson);
        }

        [Test]
        public void Feed_ArgsComplete_NoCurrentTool_ReturnsNull()
        {
            // content_block_stop for a non-tool block (text block)
            var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.IsNull(rec);
        }

        [Test]
        public void Feed_MultipleDeltas_ConcatenatesAll()
        {
            _acc.Feed(ChatEvent.ToolStart("t", "", "id2"));
            _acc.Feed(ChatEvent.ToolStart(null, "ab"));
            _acc.Feed(ChatEvent.ToolStart(null, "cd"));
            _acc.Feed(ChatEvent.ToolStart(null, "ef"));
            var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.AreEqual("abcdef", rec.Value.ArgsJson);
        }

        [Test]
        public void Feed_ToolResult_MatchesPendingById()
        {
            _acc.Feed(ChatEvent.ToolStart("get_hierarchy", "", "id3"));
            _acc.Feed(ChatEvent.ToolArgsComplete());
            var rec = _acc.Feed(ChatEvent.ToolResult("id3", "Root/Cube", true));
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("get_hierarchy", rec.Value.Name);
            Assert.AreEqual("Root/Cube",     rec.Value.ResultText);
            Assert.IsTrue(rec.Value.IsOk);
        }

        [Test]
        public void Feed_ToolResult_OrphanId_ReturnsMinimalRecord()
        {
            var rec = _acc.Feed(ChatEvent.ToolResult("orphan_id", "some text", true));
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("?",         rec.Value.Name);
            Assert.AreEqual("orphan_id", rec.Value.Id);
            Assert.AreEqual("some text", rec.Value.ResultText);
        }

        [Test]
        public void Feed_ToolResult_Error_SetsIsOkFalse()
        {
            _acc.Feed(ChatEvent.ToolStart("cmd", "", "id4"));
            _acc.Feed(ChatEvent.ToolArgsComplete());
            var rec = _acc.Feed(ChatEvent.ToolResult("id4", "boom", false));
            Assert.IsTrue(rec.HasValue);
            Assert.IsFalse(rec.Value.IsOk);
        }

        [Test]
        public void Feed_TwoToolCalls_TracksIndependently()
        {
            _acc.Feed(ChatEvent.ToolStart("toolA", "", "idA"));
            _acc.Feed(ChatEvent.ToolStart(null, "aaa"));
            _acc.Feed(ChatEvent.ToolArgsComplete());

            _acc.Feed(ChatEvent.ToolStart("toolB", "", "idB"));
            _acc.Feed(ChatEvent.ToolStart(null, "bbb"));
            _acc.Feed(ChatEvent.ToolArgsComplete());

            var recB = _acc.Feed(ChatEvent.ToolResult("idB", "resB", true));
            var recA = _acc.Feed(ChatEvent.ToolResult("idA", "resA", true));

            Assert.AreEqual("toolB", recB.Value.Name);
            Assert.AreEqual("bbb",   recB.Value.ArgsJson);
            Assert.AreEqual("resB",  recB.Value.ResultText);

            Assert.AreEqual("toolA", recA.Value.Name);
            Assert.AreEqual("aaa",   recA.Value.ArgsJson);
            Assert.AreEqual("resA",  recA.Value.ResultText);
        }

        [Test]
        public void Feed_TextDelta_ReturnsNull()
        {
            var rec = _acc.Feed(ChatEvent.TextDelta("hello"));
            Assert.IsNull(rec);
        }

        [Test]
        public void Feed_TurnDone_ReturnsNull()
        {
            var rec = _acc.Feed(ChatEvent.TurnDone("sid", 0f, 0, 0));
            Assert.IsNull(rec);
        }

        [Test]
        public void Reset_ClearsPendingState()
        {
            _acc.Feed(ChatEvent.ToolStart("tool", "", "id5"));
            _acc.Feed(ChatEvent.ToolArgsComplete());
            _acc.Reset();
            // After reset the old id is an orphan
            var rec = _acc.Feed(ChatEvent.ToolResult("id5", "result", true));
            Assert.AreEqual("?", rec.Value.Name);
        }

        // ── FIX 2: null vs "" discriminator for zero-delta tools ──────────────

        [Test]
        public void Feed_ZeroDeltaTool_ChipRecordHasNullArgs_UpdateRecordHasEmptyArgs()
        {
            // ToolStart emits the chip-creation record (ArgsJson == null).
            var chipRec = _acc.Feed(ChatEvent.ToolStart("get_hierarchy", "", "id6"));
            Assert.IsTrue(chipRec.HasValue, "ToolStart must return a record");
            Assert.IsNull(chipRec.Value.ArgsJson, "chip-creation record must have null ArgsJson");
            Assert.IsFalse(chipRec.Value.HasResult);

            // ToolArgsComplete with NO deltas emits the args-complete record (ArgsJson == "").
            var argsRec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.IsTrue(argsRec.HasValue, "ToolArgsComplete must return a record");
            Assert.AreEqual("", argsRec.Value.ArgsJson, "args-complete record must have empty string ArgsJson");
            Assert.IsFalse(argsRec.Value.HasResult);
        }

        // ── Result-before-complete ordering ───────────────────────────────────

        [Test]
        public void Feed_ResultBeforeArgsComplete_TreatedAsOrphan()
        {
            // ToolStart received, but ToolArgsComplete hasn't fired yet.
            // A ToolResult for the same id arrives out of order → orphan path ("?").
            _acc.Feed(ChatEvent.ToolStart("my_tool", "", "id_early"));

            var rec = _acc.Feed(ChatEvent.ToolResult("id_early", "early result", true));

            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("?",            rec.Value.Name,       "out-of-order result is an orphan");
            Assert.AreEqual("id_early",     rec.Value.Id);
            Assert.AreEqual("early result", rec.Value.ResultText);
            Assert.IsTrue(rec.Value.IsOk);
        }

        [Test]
        public void Feed_ResultBeforeArgsComplete_SubsequentArgsCompleteStillWorks()
        {
            // After the orphan result, ToolArgsComplete still fires (stream can be
            // spec-non-conformant). The accumulator should return the assembled record
            // without throwing, but the pending slot is now dangling.
            _acc.Feed(ChatEvent.ToolStart("t2", "", "id_dangling"));
            _acc.Feed(ChatEvent.ToolResult("id_dangling", "res", true)); // orphan
            // ToolArgsComplete fires late — _currentId is "id_dangling", assembles normally.
            var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.IsTrue(rec.HasValue, "ToolArgsComplete must still produce a record");
            Assert.AreEqual("t2", rec.Value.Name);
        }
    }
}
