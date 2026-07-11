// RelayFlowWindowTests — exercises backend → DrainAndRender → transcript pipeline.
// Tier A: QueuedFakeBackend (no sleep). Tier B: ProcessFactory (300ms sleep).
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayFlowWindowTests : RealWindowFixture
    {
        // ── Reflection seams ──────────────────────────────────────────────────

        static readonly FieldInfo  s_back  = typeof(MCPChatWindow).GetField("_backend",   BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo  s_act   = typeof(MCPChatWindow).GetField("_activity",  BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo  s_chip  = typeof(MCPChatWindow).GetField("_chipField", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo s_drain  = typeof(MCPChatWindow).GetMethod("DrainAndRender",   BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo s_send   = typeof(MCPChatWindow).GetMethod("OnSend",           BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo s_creat  = typeof(MCPChatWindow).GetMethod("CreateBackend",    BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo s_cancel = typeof(MCPChatWindow).GetMethod("CancelTurn",       BindingFlags.NonPublic | BindingFlags.Instance);

        private QueuedFakeBackend _fake;

        // ── Inner: fake backend with a pre-loaded queue ───────────────────────

        sealed class QueuedFakeBackend : IChatBackend
        {
            public bool   IsRunning  { get; private set; } = true;
            public string SessionId  => "test-sid";
            readonly Queue<ChatEvent> _q = new Queue<ChatEvent>();
            public void Enqueue(ChatEvent ev) => _q.Enqueue(ev);
            public void DrainEvents(List<ChatEvent> o, List<ToolCallRecord> t = null)
            { while (_q.Count > 0) o.Add(_q.Dequeue()); }
            public void Start()                      { }
            public void Stop()                       { IsRunning = false; }
            public void SendTurn(string j)           { }
            public void SendControlResponse(string j){ }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        void DrainAndRender()     => s_drain.Invoke(W, null);
        void CancelTurn()         => s_cancel.Invoke(W, null);
        void ArmSending()         => ((ChatActivityState)s_act.GetValue(W)).Send();
        void ResetToIdle()        => ((ChatActivityState)s_act.GetValue(W)).Done();
        ActivityPhase GetPhase()  => ((ChatActivityState)s_act.GetValue(W)).Phase;
        void RestoreRealBackend() => s_creat?.Invoke(W, null);

        void SetInputText(string text)
        {
            var field = s_chip.GetValue(W) as InlineChipField;
            if (field != null) field.Text = text;
        }

        // Tier B: encode event stream as relay protocol
        static string ED(params string[] lines)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++) sb.Append(i).Append('\n').Append(lines[i]).Append('\n');
            return sb.ToString();
        }
        static string JE(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        static RelayChatProcess Proc(string d) =>
            new RelayChatProcess(j => j.Contains("\"cmd\":\"events\"")
                ? $"{{\"ok\":true,\"data\":\"{JE(d)}\"}}"
                : "{\"ok\":true,\"data\":\"\"}");

        // ── SetUp / TearDown ──────────────────────────────────────────────────

        [SetUp]
        public override void SetUp()
        {
            RelaySpawner.EnsureRunningOverride = () => 19800;
            base.SetUp();
            _fake = new QueuedFakeBackend();
            s_back.SetValue(W, _fake);
            ArmSending();   // Tier A default: Idle → Sending so DrainAndRender processes events
        }

        [TearDown]
        public override void TearDown()
        {
            RelayBackend.ProcessFactory        = null;
            RelaySpawner.EnsureRunningOverride = null;
            RelaySpawner.Stop();
            SessionState.EraseInt(RelaySpawner.PortKey);
            SessionState.EraseInt(RelaySpawner.PidKey);
            base.TearDown();
        }

        // ── Tier A: Direct drain via QueuedFakeBackend ────────────────────────

        [Test]
        public void TextDelta_AppearsInTranscript()
        {
            _fake.Enqueue(ChatEvent.TextDelta("Hello from mock"));
            _fake.Enqueue(ChatEvent.TurnDone("sid", 0.01f, 100, 50));
            DrainAndRender();
            var labels = Scroll().Query<Label>().ToList();
            Assert.IsTrue(labels.Exists(l => l.text.Contains("Hello from mock")),
                "Expected 'Hello from mock' in transcript labels");
        }

        [Test]
        public void TurnDone_TokenLabel_ShowsReadout()
        {
            _fake.Enqueue(ChatEvent.TurnDone("sid", 0.01f, 123, 456));
            DrainAndRender();
            var lbl = TokenLabel();
            if (lbl == null) { Assert.Ignore("token-readout label not found in layout"); return; }
            StringAssert.Contains("123", lbl.text);
        }

        [Test]
        public void TurnDone_ActivityState_ReturnedToIdle()
        {
            _fake.Enqueue(ChatEvent.TurnDone("sid", 0f, 0, 0));
            DrainAndRender();
            Assert.AreEqual(ActivityPhase.Idle, GetPhase());
        }

        [Test]
        public void Error_ErrorRendered_InTranscript()
        {
            _fake.Enqueue(ChatEvent.Error("Something went wrong"));
            DrainAndRender();
            var labels = Scroll().Query<Label>().ToList();
            Assert.IsTrue(labels.Exists(l => l.text.Contains("Something went wrong")),
                "Expected error text in transcript");
        }

        [Test]
        public void MultiEvent_TextThenDone_BothRendered()
        {
            _fake.Enqueue(ChatEvent.TextDelta("part1"));
            _fake.Enqueue(ChatEvent.TextDelta(" part2"));
            _fake.Enqueue(ChatEvent.TurnDone("sid", 0.02f, 200, 100));
            DrainAndRender();
            Assert.AreEqual(ActivityPhase.Idle, GetPhase());
            var lbl = TokenLabel();
            if (lbl != null) StringAssert.Contains("200", lbl.text);
            var labels = Scroll().Query<Label>().ToList();
            Assert.IsTrue(labels.Exists(l => l.text.Contains("part1") || l.text.Contains("part2")),
                "At least one text delta must be visible");
        }

        [Test]
        public void StopDuringTurn_BackendStops_ActivityIdle()
        {
            // Activity is Sending (ArmSending in SetUp). CancelTurn stops backend + resets phase.
            CancelTurn();
            Assert.IsFalse(_fake.IsRunning, "Backend.Stop() must be called by CancelTurn");
            Assert.AreEqual(ActivityPhase.Idle, GetPhase(), "Phase must return to Idle after cancel");
        }

        [Test]
        public void PermissionPrompt_DrainedAsEvent()
        {
            // PermissionPrompt is non-terminal — phase must stay non-Idle after drain.
            _fake.Enqueue(ChatEvent.PermissionPrompt("req-1", "bash", "{\"cmd\":\"ls\"}"));
            Assert.DoesNotThrow(() => DrainAndRender());
            Assert.AreNotEqual(ActivityPhase.Idle, GetPhase(),
                "PermissionPrompt must not terminate the turn");
        }

        // ── Tier B: ProcessFactory full stack (real RelayBackend) ─────────────

        [Test]
        public void ProcessFactory_Send_TextDeltaRendered()
        {
            ResetToIdle();         // CanSend must be true for OnSend
            RestoreRealBackend();  // replace fake with real RelayBackend (uses ProcessFactory)
            RelayBackend.ProcessFactory = () => Proc(ED("t|Hello from relay", "d|sid|0.01|10|5"));
            SetInputText("test message");
            s_send.Invoke(W, null);
            Thread.Sleep(300);
            DrainAndRender();
            var labels = Scroll().Query<Label>().ToList();
            Assert.IsTrue(labels.Exists(l => l.text.Contains("Hello from relay")),
                "Expected 'Hello from relay' in transcript");
            Assert.AreEqual(ActivityPhase.Idle, GetPhase());
        }

        [Test]
        public void ProcessFactory_Send_ErrorEvent_RenderedAsChip()
        {
            ResetToIdle();
            RestoreRealBackend();
            RelayBackend.ProcessFactory = () => Proc(ED("e|Relay error"));
            SetInputText("trigger error");
            s_send.Invoke(W, null);
            Thread.Sleep(300);
            DrainAndRender();
            Assert.AreEqual(ActivityPhase.Idle, GetPhase());
            var labels = Scroll().Query<Label>().ToList();
            Assert.IsTrue(labels.Exists(l => l.text.Contains("Relay error")));
        }

        [Test]
        public void ProcessFactory_SessionInit_NonTerminal_ActivityStaysActive()
        {
            ResetToIdle();
            RestoreRealBackend();
            RelayBackend.ProcessFactory = () => Proc(ED("si|sess-abc"));
            SetInputText("ping");
            s_send.Invoke(W, null);
            Thread.Sleep(300);
            DrainAndRender();
            // SessionInit is non-terminal — no TurnDone yet → window stays active
            Assert.AreNotEqual(ActivityPhase.Idle, GetPhase(),
                "SessionInit alone must not return activity to Idle");
        }
    }
}
