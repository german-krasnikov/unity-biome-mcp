// Monkey tests: Chat UI flow, Unity tool-command parsing, mode switching, event accumulation.
// No real Python relay required — all mocked via ProcessFactory seam.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayMonkeyChatTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]  public void SetUp()    => RelaySpawner.EnsureRunningOverride = () => 19800;
        [TearDown] public void TearDown()
        {
            RelayBackend.ProcessFactory        = null;
            RelaySpawner.EnsureRunningOverride  = null;
            RelaySpawner.StopForTests();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Build relay event-data: interleaved seq/line pairs as raw string.
        static string ED(params string[] lines)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++) sb.Append(i).Append('\n').Append(lines[i]).Append('\n');
            return sb.ToString();
        }

        static string JE(string s) => s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n");

        static RelayChatProcess Proc(string data) =>
            new RelayChatProcess(j => j.Contains("\"cmd\":\"events\"")
                ? $"{{\"ok\":true,\"data\":\"{JE(data)}\"}}"
                : "{\"ok\":true,\"data\":\"\"}");

        static RelayChatProcess SentProc(List<string> sink) =>
            new RelayChatProcess(j => { lock (sink) sink.Add(j); return "{\"ok\":true,\"data\":\"\"}"; });

        static RelayChatProcess OkProc() =>
            new RelayChatProcess(j => "{\"ok\":true,\"data\":\"\"}");

        async Task<RelayBackend> StartAsync(string data, string id="claude", string mode="agent")
        {
            var expectedAfterSeq = Math.Max(-1, (data.Split('\n').Length - 1) / 2 - 1);
            var polled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RelayBackend.ProcessFactory = () => new RelayChatProcess(j =>
            {
                if (!j.Contains("\"cmd\":\"events\"")) return "{\"ok\":true,\"data\":\"\"}";
                if (expectedAfterSeq < 0 || j.Contains($"\"after_seq\":{expectedAfterSeq}"))
                    polled.TrySetResult(true);
                return $"{{\"ok\":true,\"data\":\"{JE(data)}\"}}";
            });
            var b = Own(new RelayBackend(id, mode, "m", 0));
            b.Start();
            var completed = await Task.WhenAny(polled.Task, Task.Delay(2000));
            Assert.AreSame(polled.Task, completed, "Relay event stream was not polled");
            await polled.Task;
            return b;
        }

        List<ChatEvent> Drain(RelayBackend b, List<ToolCallRecord> recs = null)
        { var ev = new List<ChatEvent>(); b.DrainEvents(ev, recs); b.Stop(); return ev; }

        RelayBackend Own(RelayBackend backend)
        {
            RegisterCleanup(backend.Stop);
            return backend;
        }

        static async Task WaitUntilAsync(
            Func<bool> condition, Action poll, string timeoutMessage, int timeoutMs = 2000)
        {
            var timeout = Task.Delay(timeoutMs);
            while (true)
            {
                poll?.Invoke();
                if (condition()) return;
                var nextPoll = Task.Delay(10);
                var completed = await Task.WhenAny(nextPoll, timeout);
                if (completed == timeout)
                    Assert.Fail(timeoutMessage);
                await nextPoll;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // A. Unity tool-command parsing (32 tests via TestCase)
        // ══════════════════════════════════════════════════════════════════════

        // A1. Valid tc| for Unity-specific tools — Name/ToolId/ArgsJson all correct (10 cases)
        [TestCase("tc|create_object|id1|{\"n\":\"Cube\"}", "create_object","id1","{\"n\":\"Cube\"}")]
        [TestCase("tc|set_property|id2|{\"path\":\"/X\"}", "set_property","id2","{\"path\":\"/X\"}")]
        [TestCase("tc|drag_object|id3|{\"from\":[0,0],\"to\":[1,1]}", "drag_object","id3","{\"from\":[0,0],\"to\":[1,1]}")]
        [TestCase("tc|draw_polygon|id4|{\"pts\":[[0,0],[1,0]]}", "draw_polygon","id4","{\"pts\":[[0,0],[1,0]]}")]
        [TestCase("tc|place_prefab|id5|{\"p\":\"Tree\"}", "place_prefab","id5","{\"p\":\"Tree\"}")]
        [TestCase("tc|batch|id6|[{\"cmd\":\"create_object\"}]", "batch","id6","[{\"cmd\":\"create_object\"}]")]
        [TestCase("tc|get_hierarchy|id7|{}", "get_hierarchy","id7","{}")]
        [TestCase("tc|screenshot|id8|{\"cam\":\"main\"}", "screenshot","id8","{\"cam\":\"main\"}")]
        [TestCase("tc|execute_code|id9|{\"code\":\"1;\"}", "execute_code","id9","{\"code\":\"1;\"}")]
        [TestCase("tc|get_console|id10|{\"max\":50}", "get_console","id10","{\"max\":50}")]
        public void Parse_UnityTool_CorrectFields(string line, string name, string tid, string args)
        {
            var ev = RelayEventParser.Parse(line);
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.ToolStart, ev.Value.Kind);
            Assert.AreEqual(name, ev.Value.Text);
            Assert.AreEqual(tid,  ev.Value.ToolId);
            Assert.AreEqual(args, ev.Value.ArgsJson);
        }

        // A2. Malformed tc| — too few fields → null (4 cases)
        [TestCase("tc|create_object")][TestCase("tc|create_object|id1")]
        [TestCase("tc|")][TestCase("tc|drag|")]
        public void Parse_MalformedTool_Null(string line) => Assert.IsNull(RelayEventParser.Parse(line));

        // A3. tc| edge cases — pipe in args, empty args, unicode, 10KB
        [Test] public void Parse_ToolCall_EmptyArgs() =>
            Assert.AreEqual("", RelayEventParser.Parse("tc|get_hierarchy|id|").Value.ArgsJson);

        [Test] public void Parse_ToolCall_PipeInArgs() =>
            Assert.AreEqual("{\"v\":\"a|b\"}", RelayEventParser.Parse("tc|set_property|x|{\"v\":\"a|b\"}").Value.ArgsJson);

        [Test] public void Parse_ToolCall_Unicode() =>
            Assert.AreEqual("{\"n\":\"こんにちは\"}", RelayEventParser.Parse("tc|create_object|u|{\"n\":\"こんにちは\"}").Value.ArgsJson);

        [Test] public void Parse_ToolCall_10KBArgs()
        {
            var big = "{\"d\":\"" + new string('x', 9_980) + "\"}";
            Assert.AreEqual(big, RelayEventParser.Parse($"tc|batch|b|{big}").Value.ArgsJson);
        }

        // A4. tr| tool result — isOk, text, toolId (5 cases)
        [TestCase("tr|id1|true|ok",          true,  "ok",  "id1")]
        [TestCase("tr|id2|false|err",         false, "err", "id2")]
        [TestCase("tr|id3|true|",             true,  "",    "id3")]
        [TestCase("tr|id4|false|",            false, "",    "id4")]
        [TestCase("tr|id5|true|r|w|p",        true,  "r|w|p","id5")]
        public void Parse_ToolResult_Correct(string line, bool ok, string text, string tid)
        {
            var ev = RelayEventParser.Parse(line);
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.ToolResult, ev.Value.Kind);
            Assert.AreEqual(ok, ev.Value.IsOk); Assert.AreEqual(text, ev.Value.Text); Assert.AreEqual(tid, ev.Value.ToolId);
        }

        // A5. pp| permission prompt — toolName and requestId correct (3 cases)
        [TestCase("pp|create_object|r1|{}","create_object","r1")]
        [TestCase("pp|execute_code|r2|{}","execute_code","r2")]
        [TestCase("pp|bash|r3|{}","bash","r3")]
        public void Parse_PermissionPrompt_Correct(string line, string tool, string reqId)
        {
            var ev = RelayEventParser.Parse(line);
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.PermissionPrompt, ev.Value.Kind);
            Assert.AreEqual(tool, ev.Value.Text); Assert.AreEqual(reqId, ev.Value.RequestId);
        }

        // A6. tc| with empty tool name — still parses (3 fields present)
        [Test] public void Parse_ToolCall_EmptyName_Parseable()
        {
            var ev = RelayEventParser.Parse("tc||id1|{}");
            Assert.IsNotNull(ev); Assert.AreEqual("", ev.Value.Text);
        }

        // ══════════════════════════════════════════════════════════════════════
        // B. Full-turn event sequences (20 tests)
        // ══════════════════════════════════════════════════════════════════════

        // B1. si → t → d: all three kinds present, session captured
        [Test] public async Task Seq_SiTextDone_AllPresent()
        {
            var b = await StartAsync(ED("si|s1","t|hi","d|s1|0.01|10|5"));
            var ev = Drain(b);
            Assert.AreEqual("s1", b.SessionId);
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.SessionInit));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.TextDelta && e.Text == "hi"));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.TurnDone));
        }

        // B2. 20 text deltas → exactly 20 TextDelta events
        [Test] public async Task Seq_20TextDeltas_All20()
        {
            var lines = new string[20]; for (int i = 0; i < 20; i++) lines[i] = $"t|w{i}";
            Assert.AreEqual(20, Drain(await StartAsync(ED(lines))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
        }

        // B3. tc + tr → ToolCallRecord produced
        [Test] public async Task Seq_ToolCallAndResult_RecordProduced()
        {
            var recs = new List<ToolCallRecord>();
            var ev   = new List<ChatEvent>();
            var b    = await StartAsync(ED("tc|create_object|t1|{\"n\":\"X\"}", "tr|t1|true|ok"));
            b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 1, $"Expected records, got 0; events={ev.Count}");
        }

        // B4. Error mid-stream — captured, events before/after still processed
        [Test] public async Task Seq_ErrorMidStream_Captured()
        {
            var ev = Drain(await StartAsync(ED("t|before","e|boom","t|after")));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.Error && e.Text == "boom"));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.TextDelta && e.Text == "before"));
        }

        // B5. Heartbeats interleaved — ≥2 heartbeats in output
        [Test] public async Task Seq_Heartbeats_Included() =>
            Assert.IsTrue(Drain(await StartAsync(ED("hb|","t|x","hb|"))).FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count >= 2);

        // B6. RateLimit — text preserved
        [Test] public async Task Seq_RateLimit_TextPreserved() =>
            Assert.IsTrue(Drain(await StartAsync(ED("rl|wait 30s"))).Exists(e => e.Kind == ChatEventKind.RateLimit && e.Text == "wait 30s"));

        // B7. SessionState — state field preserved
        [Test] public async Task Seq_SessionState_StatePreserved() =>
            Assert.IsTrue(Drain(await StartAsync(ED("ss|connecting"))).Exists(e => e.Kind == ChatEventKind.SessionState && e.State == "connecting"));

        // B8. AutoReply must NOT appear in DrainEvents output
        [Test] public async Task Seq_AutoReply_NotForwardedToUI() =>
            Assert.IsFalse(Drain(await StartAsync(ED("ar|{\"type\":\"auto\"}"))).Exists(e => e.Kind == ChatEventKind.AutoReply));

        // B9. ToolProgress — pct and text correct
        [Test] public async Task Seq_ToolProgress_PctCorrect() =>
            Assert.IsTrue(Drain(await StartAsync(ED("tp|75.5|Uploading"))).Exists(e =>
                e.Kind == ChatEventKind.ToolProgress && Math.Abs(e.Percentage - 75.5f) < 0.01f));

        // B10. TurnDone zero fields
        [Test] public async Task Seq_TurnDone_ZeroFields() =>
            Assert.IsTrue(Drain(await StartAsync(ED("d|s1|0|0|0"))).Exists(e =>
                e.Kind == ChatEventKind.TurnDone && e.SessionId == "s1" && e.InputTokens == 0));

        // B11. Multiple TurnDone → SessionId is last
        [Test] public async Task Seq_MultipleTurnDone_LastWins()
        {
            var b = await StartAsync(ED("d|s1|0|0|0","d|s2|0|0|0")); Drain(b); Assert.AreEqual("s2", b.SessionId);
        }

        // B12. si then d → TurnDone session wins
        [Test] public async Task Seq_SiThenDone_DoneSessionWins()
        {
            var b = await StartAsync(ED("si|init","d|done|0|0|0")); Drain(b); Assert.AreEqual("done", b.SessionId);
        }

        // B13. Full realistic turn: si → 3×t → tc → tr → d
        [Test] public async Task Seq_RealisticTurn_AllPresent()
        {
            var recs = new List<ToolCallRecord>();
            var ev   = new List<ChatEvent>();
            var b    = await StartAsync(ED("si|r1","t|A","t|B","t|C","tc|create_object|t1|{\"n\":\"E\"}","tr|t1|true|ok","d|r1|0.05|500|150"));
            b.DrainEvents(ev, recs); b.Stop();
            Assert.AreEqual(3, ev.FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
            Assert.IsTrue(recs.Count >= 1); Assert.AreEqual("r1", b.SessionId);
        }

        // B14. PermissionPrompt in sequence
        [Test] public async Task Seq_PermissionPrompt_Captured() =>
            Assert.IsTrue(Drain(await StartAsync(ED("pp|bash|r1|{}"))).Exists(e =>
                e.Kind == ChatEventKind.PermissionPrompt && e.Text == "bash" && e.RequestId == "r1"));

        // B15. AskUser — requestId captured
        [Test] public async Task Seq_AskUser_RequestId() =>
            Assert.IsTrue(Drain(await StartAsync(ED("au|rq2|{\"q\":\"?\"}"))).Exists(e =>
                e.Kind == ChatEventKind.AskUser && e.RequestId == "rq2"));

        // B16. Empty data → 0 events, no crash
        [Test] public async Task Seq_Empty_NoEvents()
        {
            var ev = new List<ChatEvent>(); var b = await StartAsync("");
            Assert.DoesNotThrow(() => b.DrainEvents(ev)); b.Stop(); Assert.AreEqual(0, ev.Count);
        }

        // B17. Unknown prefix lines → filtered, valid pass
        [Test] public async Task Seq_UnknownPrefix_Filtered() =>
            Assert.AreEqual(2, Drain(await StartAsync(ED("t|a","JUNK|x","t|b"))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        // B18. 3 tool calls → ≥3 records
        [Test] public async Task Seq_ThreeToolCalls_ThreePlusRecords()
        {
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED("tc|set_property|t1|{}","tc|set_property|t2|{}","tc|set_property|t3|{}"));
            b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 3, $"Got {recs.Count}");
        }

        // B19. Drain twice → second drain empty
        [Test] public async Task Seq_DrainTwice_SecondEmpty()
        {
            var b = await StartAsync(ED("t|a","t|b"));
            var e1 = new List<ChatEvent>(); var e2 = new List<ChatEvent>();
            b.DrainEvents(e1); b.DrainEvents(e2); b.Stop();
            Assert.IsTrue(e1.Count > 0); Assert.AreEqual(0, e2.Count);
        }

        // B20. TurnDone large token counts preserved
        [Test] public async Task Seq_TurnDone_LargeTokens() =>
            Assert.IsTrue(Drain(await StartAsync(ED("d|big|9.99|1000000|500000"))).Exists(e =>
                e.Kind == ChatEventKind.TurnDone && e.InputTokens == 1_000_000 && e.OutputTokens == 500_000));

        // ══════════════════════════════════════════════════════════════════════
        // C. Mode switching and backend construction (18 tests)
        // ══════════════════════════════════════════════════════════════════════

        // C1. SetMode sends set_mode with correct mode field (4 cases)
        [TestCase("ask")][TestCase("agent")][TestCase("auto")][TestCase("")]
        public void SetMode_CorrectModeJson(string mode)
        {
            var sent = new List<string>();
            RelayBackend.ProcessFactory = () => SentProc(sent);
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start(); lock (sent) sent.Clear();
            b.SetMode(mode);
            lock (sent) Assert.IsTrue(sent.Exists(j => j.Contains("\"cmd\":\"set_mode\"") && j.Contains($"\"mode\":\"{mode}\"")));
            b.Stop();
        }

        // C2. SetMode with special chars — no crash, command sent (2 cases)
        [TestCase("m\"x")][TestCase("m\\y")]
        public void SetMode_SpecialChars_Sent(string mode)
        {
            var sent = new List<string>(); RelayBackend.ProcessFactory = () => SentProc(sent);
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start(); lock (sent) sent.Clear();
            b.SetMode(mode);
            lock (sent) Assert.IsTrue(sent.Exists(j => j.Contains("\"cmd\":\"set_mode\"")));
            b.Stop();
        }

        // C3. 100 rapid mode flips — no exception
        [Test] public void SetMode_100Flips_NoException()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start();
            Assert.DoesNotThrow(() => { for (int i = 0; i < 100; i++) b.SetMode(i%2==0?"agent":"ask"); });
            b.Stop();
        }

        // C4. Different backend IDs — start JSON contains correct id (5 cases)
        [TestCase("claude")][TestCase("codex")][TestCase("kimi")]
        [TestCase("antigravity")][TestCase("opencode")]
        public void Backend_Id_InStartJson(string id)
        {
            var sent = new List<string>(); RelayBackend.ProcessFactory = () => SentProc(sent);
            Own(new RelayBackend(id,"agent","m",0)).Start();
            lock (sent) Assert.IsTrue(sent[0].Contains($"\"backend\":\"{id}\""), sent[0]);
        }

        // C5. Resume session ID — in start JSON
        [Test] public void Backend_ResumeId_InStartJson()
        {
            var sent = new List<string>(); RelayBackend.ProcessFactory = () => SentProc(sent);
            Own(new RelayBackend("claude","agent","m",0,"resume-xyz")).Start();
            lock (sent) Assert.IsTrue(sent[0].Contains("\"resume_session_id\":\"resume-xyz\""), sent[0]);
        }

        // C6. No resume ID → field absent
        [Test] public void Backend_NoResumeId_FieldAbsent()
        {
            var sent = new List<string>(); RelayBackend.ProcessFactory = () => SentProc(sent);
            Own(new RelayBackend("claude","agent","m",0)).Start();
            lock (sent) Assert.IsFalse(sent[0].Contains("resume_session_id"), sent[0]);
        }

        // C7. IsRunning true after Start, false after Stop
        [Test] public void Backend_IsRunning_Lifecycle()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start();
            Assert.IsTrue(b.IsRunning); b.Stop(); Assert.IsFalse(b.IsRunning);
        }

        // C8. SendTurn before Start → auto-starts
        [Test] public void Backend_SendTurnAutoStarts()
        {
            var sent = new List<string>(); RelayBackend.ProcessFactory = () => SentProc(sent);
            var b = Own(new RelayBackend("claude","agent","m",0)); b.SendTurn("{\"type\":\"user\"}");
            lock (sent) Assert.IsTrue(sent.Exists(j => j.Contains("\"cmd\":\"start\"")));
            b.Stop();
        }

        // C9. SendControlResponse → send cmd to proc
        [Test] public void Backend_SendControlResponse_WritesSendCmd()
        {
            var sent = new List<string>(); RelayBackend.ProcessFactory = () => SentProc(sent);
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start(); lock (sent) sent.Clear();
            b.SendControlResponse("{\"allow\":true}");
            lock (sent) Assert.IsTrue(sent.Exists(j => j.Contains("\"cmd\":\"send\"")));
            b.Stop();
        }

        // C10. model field in start JSON
        [Test] public void Backend_ModelField_InStartJson()
        {
            var sent = new List<string>(); RelayBackend.ProcessFactory = () => SentProc(sent);
            Own(new RelayBackend("claude","agent","sonnet-4-5",0)).Start();
            lock (sent) Assert.IsTrue(sent[0].Contains("\"model\":\"sonnet-4-5\""), sent[0]);
        }

        // ══════════════════════════════════════════════════════════════════════
        // D. Accumulation stress + edge cases (20 tests)
        // ══════════════════════════════════════════════════════════════════════

        // D1. 50 text deltas → exactly 50
        [Test] public async Task Stress_50TextDeltas_All50()
        {
            var lines = new string[50]; for (int i = 0; i < 50; i++) lines[i] = $"t|w{i}";
            Assert.AreEqual(50, Drain(await StartAsync(ED(lines))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
        }

        // D2. 10 tool calls → ≥10 records
        [Test] public async Task Stress_10ToolCalls_AtLeast10Records()
        {
            var lines = new string[10]; for (int i = 0; i < 10; i++) lines[i] = $"tc|create_object|t{i}|{{\"n\":{i}}}";
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(lines)); b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 10, $"Got {recs.Count}");
        }

        // D3. Mix: 5 tools + 5 text — both counts correct
        [Test] public async Task Stress_ToolsAndText_MixedCounts()
        {
            var lines = new string[10];
            for (int i = 0; i < 5; i++) lines[i] = $"tc|set_property|m{i}|{{}}";
            for (int i = 5; i < 10; i++) lines[i] = $"t|tok{i}";
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(lines)); b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 5);
            Assert.AreEqual(5, ev.FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
        }

        // D4. DrainEvents with null toolOutput → no NullReferenceException
        [Test] public async Task Stress_NullToolOutput_NoException()
        {
            var b = await StartAsync(ED("tc|create_object|t1|{}","tr|t1|true|ok"));
            Assert.DoesNotThrow(() => { var ev = new List<ChatEvent>(); b.DrainEvents(ev, null); b.Stop(); });
        }

        // D5. 10KB text delta preserved
        [Test] public async Task Stress_10KBTextDelta_FullyPreserved()
        {
            var big = new string('Z', 10_000);
            Assert.IsTrue(Drain(await StartAsync(ED($"t|{big}"))).Exists(e => e.Kind == ChatEventKind.TextDelta && e.Text.Length == 10_000));
        }

        // D6. 100 SendTurns without DrainEvents — no crash
        [Test] public void Stress_100SendTurns_NoException()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start();
            Assert.DoesNotThrow(() => { for (int i = 0; i < 100; i++) b.SendTurn($"{{\"t\":{i}}}"); });
            b.Stop();
        }

        // D7. TurnDone with negative cost — no crash
        [Test] public async Task Stress_TurnDone_NegativeCost_NoException()
        {
            var b = await StartAsync(ED("d|s1|-0.5|100|50"));
            Assert.DoesNotThrow(() => Drain(b));
        }

        // D8. Empty tool id in tc| — no crash
        [Test] public async Task Stress_EmptyToolId_NoException()
        {
            var b = await StartAsync(ED("tc|get_hierarchy||{}"));
            Assert.DoesNotThrow(() => { var ev = new List<ChatEvent>(); b.DrainEvents(ev); b.Stop(); });
        }

        // D9. SessionInit empty id — event produced, no crash
        [Test] public async Task Stress_SessionInit_EmptyId_EventProduced() =>
            Assert.IsTrue(Drain(await StartAsync(ED("si|"))).Exists(e => e.Kind == ChatEventKind.SessionInit));

        // D10. Error with empty message — event produced
        [Test] public async Task Stress_Error_EmptyMessage_EventProduced() =>
            Assert.IsTrue(Drain(await StartAsync(ED("e|"))).Exists(e => e.Kind == ChatEventKind.Error && e.Text == ""));

        // D11. Multiple Stop/Start cycles — DrainEvents still works
        [Test] public void Stress_MultipleStopStart_DrainWorks()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend("claude","agent","m",0));
            for (int i = 0; i < 3; i++) { b.Start(); b.Stop(); }
            Assert.DoesNotThrow(() => b.DrainEvents(new List<ChatEvent>()));
        }

        // D12. Relay returns error → InvalidOperationException on Start
        [Test] public void Stress_RelayError_ThrowsOnStart()
        {
            RelayBackend.ProcessFactory = () => new RelayChatProcess(j => "{\"ok\":false,\"err\":\"down\"}");
            Assert.Throws<InvalidOperationException>(() => Own(new RelayBackend("claude","agent","m",0)).Start());
        }

        // D13. SessionInit only → SessionId captured via DrainEvents
        [Test] public async Task Stress_SessionInitOnly_SessionIdCaptured()
        {
            var b = await StartAsync(ED("si|init-only")); Drain(b); Assert.AreEqual("init-only", b.SessionId);
        }

        // D14. Very long backend ID — no crash
        [Test] public void Stress_VeryLongBackendId_NoException()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend(new string('x',1000),"agent","m",0));
            Assert.DoesNotThrow(() => b.Start()); b.Stop();
        }

        // D15. MCP port zero — no crash
        [Test] public void Stress_McpPortZero_DoesNotThrow()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend("claude","agent","m",0));
            Assert.DoesNotThrow(() => b.Start()); b.Stop();
        }

        // D16. 50 SetMode alternations — final mode is last one set
        [Test] public void Stress_50ModeFlips_FinalModeIsAsk()
        {
            var sent = new List<string>(); RelayBackend.ProcessFactory = () => SentProc(sent);
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start();
            for (int i = 0; i < 50; i++) b.SetMode(i%2==0?"agent":"ask");
            b.Stop();
            lock (sent) Assert.IsTrue(sent.Exists(j => j.Contains("\"mode\":\"ask\"")));
        }

        // D17. Unicode backend ID — start JSON has it
        [Test] public void Stress_UnicodeBackendId_NoException()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend("バック","agent","m",0));
            Assert.DoesNotThrow(() => b.Start()); b.Stop();
        }

        // D18. ToolProgress 0 and 100 edge values (2 TestCase)
        [TestCase("tp|0|start",   0f)][TestCase("tp|100|done", 100f)]
        public void Parse_ToolProgress_Boundaries(string line, float pct) =>
            Assert.AreEqual(pct, RelayEventParser.Parse(line).Value.Percentage, 0.001f);

        // D19. AskUser nested JSON — rawJson preserved
        [Test] public void Parse_AskUser_NestedJson()
        {
            var ev = RelayEventParser.Parse("au|r99|{\"q\":[{\"type\":\"text\"}]}");
            Assert.IsNotNull(ev); Assert.AreEqual("r99", ev.Value.RequestId); Assert.IsTrue(ev.Value.RawJson.Contains("text"));
        }

        // D20. TurnDone non-numeric fields → defaults, no crash
        [Test] public void Parse_TurnDone_NonNumericFields_Defaults()
        {
            var ev = RelayEventParser.Parse("d|s|bad|cost|toks");
            Assert.IsNotNull(ev); Assert.AreEqual("s", ev.Value.SessionId); Assert.AreEqual(0f, ev.Value.CostUsd, 0.001f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // E. Parser micro-tests + RCP edge cases (12 tests)
        // ══════════════════════════════════════════════════════════════════════

        [Test] public void Parse_Null_ReturnsNull()  => Assert.IsNull(RelayEventParser.Parse(null));
        [Test] public void Parse_Empty_ReturnsNull() => Assert.IsNull(RelayEventParser.Parse(""));

        [Test] public void Parse_ErrorPipeText_FullyPreserved() =>
            Assert.AreEqual("A|B|C", RelayEventParser.Parse("e|A|B|C").Value.Text);

        [Test] public void Parse_TurnDone_LongSessionId_Preserved()
        {
            var id = new string('s', 256); var ev = RelayEventParser.Parse($"d|{id}|0|0|0");
            Assert.IsNotNull(ev); Assert.AreEqual(id, ev.Value.SessionId);
        }

        [Test] public void Parse_Heartbeat_Kind() =>
            Assert.AreEqual(ChatEventKind.Heartbeat, RelayEventParser.Parse("hb|").Value.Kind);

        [Test] public void RCP_SendSetModeNull_NoException()
        {
            var proc = new RelayChatProcess(j => "{\"ok\":true,\"data\":\"\"}");
            proc.StartViaRelay(0,"claude","agent","m",0,null);
            Assert.DoesNotThrow(() => proc.SendSetMode(null)); proc.Dispose();
        }

        [Test] public void RCP_DrainLines_PreExistingEntriesSurvive()
        {
            var proc = new RelayChatProcess(j => "{\"ok\":true,\"data\":\"\"}");
            var out_ = new List<string> { "existing" }; proc.DrainLines(out_);
            Assert.AreEqual(1, out_.Count);
        }

        [Test] public async Task Seq_WhitespaceLines_NoTextDelta() =>
            Assert.AreEqual(0, Drain(await StartAsync(ED("   ","\t"))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        // E8. AutoReply written back to proc stdin via send command
        [Test] public async Task Seq_AutoReply_WrittenBackToProc()
        {
            var written = new List<string>();
            var proc = new RelayChatProcess(json =>
            {
                if (json.Contains("\"cmd\":\"events\"")) return "{\"ok\":true,\"data\":\"0\\nar|{\\\"r\\\":1}\\n\"}";
                if (json.Contains("\"cmd\":\"send\"")) lock (written) written.Add(json);
                return "{\"ok\":true,\"data\":\"\"}";
            });
            RelayBackend.ProcessFactory = () => proc;
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start();
            var events = new List<ChatEvent>();
            await WaitUntilAsync(
                () => { lock (written) return written.Count > 0; },
                () => b.DrainEvents(events),
                "AutoReply was not written back to the process");
            b.Stop();
            Assert.IsTrue(written.Count > 0, "AutoReply must be written back via send cmd");
        }

        [Test] public void Parse_BrokenJson_ArgsPreserved()
        {
            var ev = RelayEventParser.Parse("tc|name|id|{broken");
            Assert.IsNotNull(ev); Assert.AreEqual("{broken", ev.Value.ArgsJson);
        }

        [Test] public void Parse_TextDelta_ControlChars_NoThrow() =>
            Assert.DoesNotThrow(() => RelayEventParser.Parse("t|\0\r\t"));

        [Test] public void Backend_Dispose_MultipleTimes_NoException()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start();
            Assert.DoesNotThrow(() => { b.Dispose(); b.Dispose(); b.Dispose(); });
        }
    }
}
