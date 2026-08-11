// TDD regression: T-6.3 miss-detector false positive.
// Defect: the detector checked only the FIRST tool in a turn; if Read/Bash fired
// before Agent, it immediately warned "delegation never occurred" even though
// Agent was called later in the same turn.
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgentMissDetectorWindowTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly FieldInfo s_transcript = typeof(MCPChatWindow)
            .GetField("_transcript", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_pendingAgent = typeof(MCPChatWindow)
            .GetField("_pendingAgentName", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo s_handleToolRecord = typeof(MCPChatWindow)
            .GetMethod("HandleToolRecord", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo s_handleEvent = typeof(MCPChatWindow)
            .GetMethod("HandleEvent", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void FireTool(MCPChatWindow w, string name, string id = "tid")
            => s_handleToolRecord.Invoke(w, new object[] { new ToolCallRecord(name, id, null) });

        private static void FireTurnDone(MCPChatWindow w)
            => s_handleEvent.Invoke(w, new object[] { ChatEvent.TurnDone("s1", 0f, 0, 0) });

        private static bool HasWarnChip(VisualElement container)
            => container.Query<Label>().ToList()
                .Any(l => l.text?.Contains("was mentioned but no delegation") == true);

        // ── Red test: Read fires first, Agent fires second — no warning ──────
        // Before fix: the check on first-tool immediately fires warning when Read is first.
        // After fix: check is deferred to TurnDone; Agent later in the same turn suppresses it.
        [Test]
        public void HandleToolRecord_ReadFirstThenAgent_InSameTurn_NoWarnChip()
        {
            var container  = new VisualElement();
            var transcript = new ChatTranscript(container, ChatBlockRendererFactory.CreateDefault(null, null));
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_transcript.SetValue(w, transcript);
                s_pendingAgent.SetValue(w, "junior-dev"); // @junior-dev mentioned

                FireTool(w, "Read",  "t1"); // first tool — NOT a delegation
                FireTool(w, "Agent", "t2"); // second tool — delegation
                FireTurnDone(w);

                Assert.IsFalse(HasWarnChip(container),
                    "Agent delegated later in the turn — no warning expected");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // Complementary: only non-Agent tools in the turn — warning must still appear
        [Test]
        public void HandleToolRecord_ReadOnly_NoAgent_WarnChipPresent()
        {
            var container  = new VisualElement();
            var transcript = new ChatTranscript(container, ChatBlockRendererFactory.CreateDefault(null, null));
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_transcript.SetValue(w, transcript);
                s_pendingAgent.SetValue(w, "junior-dev");

                FireTool(w, "Read", "t1"); // no Agent follows
                FireTurnDone(w);

                Assert.IsTrue(HasWarnChip(container),
                    "No Agent called — warning expected");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // Task also counts as delegation
        [Test]
        public void HandleToolRecord_BashThenTask_InSameTurn_NoWarnChip()
        {
            var container  = new VisualElement();
            var transcript = new ChatTranscript(container, ChatBlockRendererFactory.CreateDefault(null, null));
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_transcript.SetValue(w, transcript);
                s_pendingAgent.SetValue(w, "senior-dev");

                FireTool(w, "Bash", "t1");
                FireTool(w, "Task", "t2");
                FireTurnDone(w);

                Assert.IsFalse(HasWarnChip(container),
                    "Task delegated later in the turn — no warning expected");
            }
            finally { Object.DestroyImmediate(w); }
        }
    }
}
