// ToolCallAccumulatorMonkeyTests — 25 NEW edge-case tests for ToolCallAccumulator.
// Tests 151-175. Does NOT duplicate ToolCallAccumulatorTests.cs.
// New coverage: non-tool event kinds, unicode, large args, reset invariants.
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    [Category(TestCategories.Stress)]
    public class ToolCallAccumulatorMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ToolCallAccumulator _acc;
        [SetUp] public void SetUp() => _acc = new ToolCallAccumulator();

        [Test] public void Feed_Heartbeat_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.Heartbeat()));

        [Test] public void Feed_RateLimit_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.RateLimit("slow")));

        [Test] public void Feed_SessionInit_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.SessionInit("s")));

        [Test] public void Feed_Error_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.Error("boom")));

        [Test] public void Feed_PermissionPrompt_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.PermissionPrompt("req-1", "bash", "{}")));

        [Test] public void Feed_AskUser_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.AskUser("req-2", "[{}]")));

        [Test] public void Feed_ToolProgress_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.ToolProgress(50f)));

        [Test] public void Feed_SessionState_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.SessionState("active")));

        [Test] public void Feed_AutoReply_ReturnsNull()
            => Assert.IsNull(_acc.Feed(ChatEvent.AutoReply("{\"ok\":true}")));

        [Test] public void Reset_10x_NoException()
            => Assert.DoesNotThrow(() => { for (int i = 0; i < 10; i++) _acc.Reset(); });

        [Test] public void Feed_UnicodeToolName_PreservedInChipRecord()
        {
            var rec = _acc.Feed(ChatEvent.ToolStart("ツール名", "", "id-u"));
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("ツール名", rec.Value.Name);
        }

        [Test] public void Feed_EmptyToolName_ProducesChipRecord()
        {
            var rec = _acc.Feed(ChatEvent.ToolStart("", "", "id-e"));
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("", rec.Value.Name);
        }

        [Test] public void Feed_NullToolId_ChipRecordHasNullId()
        {
            var rec = _acc.Feed(ChatEvent.ToolStart("tool", "", null));
            Assert.IsTrue(rec.HasValue);
            Assert.IsNull(rec.Value.Id);
        }

        [Test] public void Feed_LargeArgsDelta_NoException()
        {
            _acc.Feed(ChatEvent.ToolStart("t", "", "id-L"));
            Assert.DoesNotThrow(() => _acc.Feed(ChatEvent.ToolStart(null, new string('x', 10_000), null)));
        }

        [Test] public void Feed_EmptyArgsDelta_DoesNotProduceRecord()
        {
            _acc.Feed(ChatEvent.ToolStart("t", "", "id-ea"));
            var rec = _acc.Feed(ChatEvent.ToolStart(null, "", null));
            Assert.IsNull(rec);
        }

        [Test] public void Feed_100ToolCallsSequential_AllProduceRecords()
        {
            int count = 0;
            for (int i = 0; i < 100; i++)
            {
                _acc.Feed(ChatEvent.ToolStart($"tool{i}", "", $"id{i}"));
                _acc.Feed(ChatEvent.ToolStart(null, $"{{\"n\":{i}}}", null));
                var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
                if (rec.HasValue) count++;
            }
            Assert.AreEqual(100, count);
        }

        [Test] public void Feed_Reset_ThenNewToolCall_GivesRecord()
        {
            _acc.Feed(ChatEvent.ToolStart("first", "", "id-f"));
            _acc.Reset();
            _acc.Feed(ChatEvent.ToolStart("second", "", "id-s"));
            var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("second", rec.Value.Name);
        }

        [Test] public void Feed_ArgsComplete_AfterReset_ReturnsNull()
        {
            _acc.Feed(ChatEvent.ToolStart("t", "", "id-r"));
            _acc.Reset(); // _currentId cleared
            var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.IsNull(rec); // _currentId == null → early return
        }

        [Test] public void Feed_ChipRecord_HasNullArgsJson()
        {
            var rec = _acc.Feed(ChatEvent.ToolStart("chip", "", "id-c"));
            Assert.IsTrue(rec.HasValue);
            Assert.IsNull(rec.Value.ArgsJson);
        }

        [Test] public void Feed_ArgsComplete_RecordHasAssembledArgs()
        {
            _acc.Feed(ChatEvent.ToolStart("t", "", "id-aa"));
            _acc.Feed(ChatEvent.ToolStart(null, "{\"x\":", null));
            _acc.Feed(ChatEvent.ToolStart(null, "1}",     null));
            var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("{\"x\":1}", rec.Value.ArgsJson);
        }

        [Test] public void Feed_SecondToolStart_OverridesCurrent()
        {
            // Two consecutive chip-create events before args-complete.
            _acc.Feed(ChatEvent.ToolStart("first",  "", "id-1"));
            _acc.Feed(ChatEvent.ToolStart("second", "", "id-2")); // overrides
            var rec = _acc.Feed(ChatEvent.ToolArgsComplete());
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("second", rec.Value.Name);
            Assert.AreEqual("id-2",   rec.Value.Id);
        }

        [Test] public void Feed_VeryLongToolName_NoException()
        {
            var name = new string('A', 1000);
            Assert.DoesNotThrow(() =>
            {
                _acc.Feed(ChatEvent.ToolStart(name, "", "id-ln"));
                _acc.Feed(ChatEvent.ToolArgsComplete());
            });
        }

        [Test] public void Feed_ToolResult_WhileCurrentPending_ProducesOrphan()
        {
            // Chip-create for id-1, then result for id-9 (unknown) → orphan
            _acc.Feed(ChatEvent.ToolStart("t", "", "id-1"));
            var rec = _acc.Feed(ChatEvent.ToolResult("id-9", "res", true));
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("?", rec.Value.Name); // orphan
        }

        [Test] public void Feed_Reset_ClearsPendingMap()
        {
            // Complete a tool call, then reset. The pending entry is gone.
            _acc.Feed(ChatEvent.ToolStart("t", "", "id-p"));
            _acc.Feed(ChatEvent.ToolArgsComplete()); // adds to pending
            _acc.Reset();
            // id-p should now be orphaned (not in pending)
            var rec = _acc.Feed(ChatEvent.ToolResult("id-p", "res", true));
            Assert.IsTrue(rec.HasValue);
            Assert.AreEqual("?", rec.Value.Name); // orphan after reset
        }

        [Test] public void Feed_ToolResult_IsOkTrue_WhenOk()
        {
            _acc.Feed(ChatEvent.ToolStart("t", "", "id-ok"));
            _acc.Feed(ChatEvent.ToolArgsComplete());
            var rec = _acc.Feed(ChatEvent.ToolResult("id-ok", "data", true));
            Assert.IsTrue(rec.HasValue);
            Assert.IsTrue(rec.Value.IsOk);
        }
    }
}
