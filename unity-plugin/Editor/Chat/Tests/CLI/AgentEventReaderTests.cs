// T23: AgentEventReader unit tests.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat.CLI;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgentEventReaderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static string MakeEvent(string kind, string payloadJson = "{}")
            => $"{{\"kind\":\"{kind}\",\"payload\":{payloadJson}}}";

        [Test]
        public void TurnStarted_CreatesUserEntry()
        {
            var lines = new[]
            {
                MakeEvent("turn_started", "{\"text\":\"Hello world\"}"),
            };
            var entries = AgentEventReader.ReadEntries(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(TranscriptEntry.Kind.User, entries[0].EntryKind);
            Assert.AreEqual("Hello world",             entries[0].Text);
        }

        [Test]
        public void AssistantDelta_AccumulatesIntoOneEntryAtTurnCompleted()
        {
            var lines = new[]
            {
                MakeEvent("turn_started",    "{\"text\":\"Q\"}"),
                MakeEvent("assistant_delta", "{\"text\":\"Hello \"}"),
                MakeEvent("assistant_delta", "{\"text\":\"world\"}"),
                MakeEvent("turn_completed",  "{}"),
            };
            var entries = AgentEventReader.ReadEntries(lines);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(TranscriptEntry.Kind.Assistant, entries[1].EntryKind);
            Assert.AreEqual("Hello world",                  entries[1].Text);
        }

        [Test]
        public void ToolCallStartedAndCompleted_ProducesToolEntryWithOkFlag()
        {
            var lines = new[]
            {
                MakeEvent("tool_call_started",   "{\"name\":\"get_hierarchy\",\"id\":\"tc1\"}"),
                MakeEvent("tool_call_completed", "{}"),
            };
            var entries = AgentEventReader.ReadEntries(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(TranscriptEntry.Kind.Tool, entries[0].EntryKind);
            Assert.AreEqual("get_hierarchy",            entries[0].Text);
            Assert.AreEqual("1",                        entries[0].ChipsData);
        }

        [Test]
        public void ToolCallFailed_SetsChipsDataToZero()
        {
            var lines = new[]
            {
                MakeEvent("tool_call_started", "{\"name\":\"bad_tool\",\"id\":\"tc2\"}"),
                MakeEvent("tool_call_failed",  "{}"),
            };
            var entries = AgentEventReader.ReadEntries(lines);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("0", entries[0].ChipsData);
        }

        [Test]
        public void EmptyAndCorruptLines_SkippedWithoutException()
        {
            var lines = new[] { "", "   ", "not json at all {{{", "{}" };
            var entries = AgentEventReader.ReadEntries(lines);
            Assert.IsNotNull(entries);
            Assert.AreEqual(0, entries.Count);
        }

        [Test]
        public void MultiTurn_ProducesEntriesInOrder()
        {
            var lines = new[]
            {
                MakeEvent("turn_started",    "{\"text\":\"Q1\"}"),
                MakeEvent("assistant_delta", "{\"text\":\"A1\"}"),
                MakeEvent("turn_completed",  "{}"),
                MakeEvent("turn_started",    "{\"text\":\"Q2\"}"),
                MakeEvent("assistant_delta", "{\"text\":\"A2\"}"),
                MakeEvent("turn_completed",  "{}"),
            };
            var entries = AgentEventReader.ReadEntries(lines);
            Assert.AreEqual(4, entries.Count);
            Assert.AreEqual(TranscriptEntry.Kind.User,      entries[0].EntryKind);
            Assert.AreEqual("Q1",                           entries[0].Text);
            Assert.AreEqual(TranscriptEntry.Kind.Assistant, entries[1].EntryKind);
            Assert.AreEqual("A1",                           entries[1].Text);
            Assert.AreEqual(TranscriptEntry.Kind.User,      entries[2].EntryKind);
            Assert.AreEqual("Q2",                           entries[2].Text);
            Assert.AreEqual(TranscriptEntry.Kind.Assistant, entries[3].EntryKind);
            Assert.AreEqual("A2",                           entries[3].Text);
        }
    }
}
