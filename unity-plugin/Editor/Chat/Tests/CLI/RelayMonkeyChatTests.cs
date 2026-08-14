// Monkey tests: Chat UI flow, event accumulation, mode switching, backend construction.
// No real Python relay required — all mocked via ProcessFactory seam.
// ACP-only: all v1 pipe-format strings replaced with JSON AgentEvent equivalents.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    [Category(TestCategories.Stress)]
    public class RelayMonkeyChatTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]  public void SetUp()    => RelaySpawner.EnsureRunningOverride = () => 19800;
        [TearDown] public void TearDown()
        {
            RelayBackend.ProcessFactory        = null;
            RelaySpawner.EnsureRunningOverride  = null;
            RelaySpawner.StopForTests();
        }

        // ── Core helpers ──────────────────────────────────────────────────────

        static string ED(params string[] lines)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++) sb.Append(i).Append('\n').Append(lines[i]).Append('\n');
            return sb.ToString();
        }

        static string JE(string s) => s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n");

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
                var done = await Task.WhenAny(nextPoll, timeout);
                if (done == timeout) Assert.Fail(timeoutMessage);
                await nextPoll;
            }
        }

        // ── ACP JSON event builders ───────────────────────────────────────────

        static string Q(string s) => "\"" + s.Replace("\\","\\\\").Replace("\"","\\\"") + "\"";

        static string AsstDelta(string text) =>
            $"{{\"kind\":\"assistant_delta\",\"payload\":{{\"text\":{Q(text)}}}}}";

        static string SessInit(string id) =>
            $"{{\"kind\":\"session_started\",\"payload\":{{\"provider_session_id\":{Q(id)}}},\"session_id\":{Q(id)}}}";

        static string AcpTurnDone(string sid) =>
            $"{{\"kind\":\"turn_completed\",\"payload\":{{}},\"session_id\":{Q(sid)}}}";

        static string CostUpdate(string cost, string inTok, string outTok) =>
            $"{{\"kind\":\"cost_update\",\"payload\":{{\"cost_usd\":{Q(cost)},\"input_tokens\":{Q(inTok)},\"output_tokens\":{Q(outTok)}}}}}";

        static string AcpErr(string msg) =>
            $"{{\"kind\":\"error\",\"payload\":{{\"message\":{Q(msg)}}}}}";

        static string AcpHeartbeat() => "{\"kind\":\"heartbeat\",\"payload\":{}}";

        static string AcpRateLimit(string msg) =>
            $"{{\"kind\":\"warning\",\"payload\":{{\"code\":\"rate_limit\",\"message\":{Q(msg)}}}}}";

        static string ToolStart(string name, string id, string args) =>
            $"{{\"kind\":\"tool_call_started\",\"payload\":{{\"name\":{Q(name)},\"id\":{Q(id)},\"args\":{args}}}}}";

        static string ToolDone(string id, string result) =>
            $"{{\"kind\":\"tool_call_completed\",\"payload\":{{\"id\":{Q(id)},\"result\":{Q(result)}}}}}";

        static string PermReq(string tool, string reqId, string inputJson) =>
            $"{{\"kind\":\"permission_requested\",\"payload\":{{\"tool_name\":{Q(tool)},\"request_id\":{Q(reqId)},\"input\":{inputJson}}}}}";

        // ══════════════════════════════════════════════════════════════════════
        // B. Full-turn event sequences (17 tests)
        // Removed: B7 SessionState, B8 AutoReply, B9 ToolProgress, B15 AskUser
        // — these v1 event kinds have no ACP equivalent in AgentEventParser.
        // ══════════════════════════════════════════════════════════════════════

        // B1. si → t → d: all three kinds present, session captured
        [Test] public async Task Seq_SiTextDone_AllPresent()
        {
            var b = await StartAsync(ED(SessInit("s1"), AsstDelta("hi"), CostUpdate("0.01","10","5"), AcpTurnDone("s1")));
            var ev = Drain(b);
            Assert.AreEqual("s1", b.SessionId);
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.SessionInit));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.TextDelta && e.Text == "hi"));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.TurnDone));
        }

        // B2. 20 text deltas → exactly 20 TextDelta events
        [Test] public async Task Seq_20TextDeltas_All20()
        {
            var lines = new string[20]; for (int i = 0; i < 20; i++) lines[i] = AsstDelta($"w{i}");
            Assert.AreEqual(20, Drain(await StartAsync(ED(lines))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
        }

        // B3. tool start + complete → ToolCallRecord produced
        [Test] public async Task Seq_ToolCallAndResult_RecordProduced()
        {
            var recs = new List<ToolCallRecord>();
            var ev   = new List<ChatEvent>();
            var b    = await StartAsync(ED(ToolStart("create_object","t1","{\"n\":\"X\"}"), ToolDone("t1","ok")));
            b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 1, $"Expected records, got 0; events={ev.Count}");
        }

        // B4. Error mid-stream — captured, events before/after still processed
        [Test] public async Task Seq_ErrorMidStream_Captured()
        {
            var ev = Drain(await StartAsync(ED(AsstDelta("before"), AcpErr("boom"), AsstDelta("after"))));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.Error && e.Text == "boom"));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.TextDelta && e.Text == "before"));
        }

        // B5. Heartbeats interleaved — ≥2 heartbeats in output
        [Test] public async Task Seq_Heartbeats_Included() =>
            Assert.IsTrue(Drain(await StartAsync(ED(AcpHeartbeat(), AsstDelta("x"), AcpHeartbeat())))
                .FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count >= 2);

        // B6. RateLimit — text preserved
        [Test] public async Task Seq_RateLimit_TextPreserved() =>
            Assert.IsTrue(Drain(await StartAsync(ED(AcpRateLimit("wait 30s"))))
                .Exists(e => e.Kind == ChatEventKind.RateLimit && e.Text == "wait 30s"));

        // B10. TurnDone zero fields — cost_update omitted, defaults to zero
        [Test] public async Task Seq_TurnDone_ZeroFields() =>
            Assert.IsTrue(Drain(await StartAsync(ED(AcpTurnDone("s1")))).Exists(e =>
                e.Kind == ChatEventKind.TurnDone && e.SessionId == "s1" && e.InputTokens == 0));

        // B11. Multiple TurnDone → SessionId is last
        [Test] public async Task Seq_MultipleTurnDone_LastWins()
        {
            var b = await StartAsync(ED(AcpTurnDone("s1"), AcpTurnDone("s2")));
            Drain(b);
            Assert.AreEqual("s2", b.SessionId);
        }

        // B12. si then done → TurnDone session wins
        [Test] public async Task Seq_SiThenDone_DoneSessionWins()
        {
            var b = await StartAsync(ED(SessInit("init"), AcpTurnDone("done")));
            Drain(b);
            Assert.AreEqual("done", b.SessionId);
        }

        // B13. Full realistic turn: si → 3×t → tc → tr → d
        [Test] public async Task Seq_RealisticTurn_AllPresent()
        {
            var recs = new List<ToolCallRecord>();
            var ev   = new List<ChatEvent>();
            var b    = await StartAsync(ED(
                SessInit("r1"), AsstDelta("A"), AsstDelta("B"), AsstDelta("C"),
                ToolStart("create_object","t1","{\"n\":\"E\"}"), ToolDone("t1","ok"),
                CostUpdate("0.05","500","150"), AcpTurnDone("r1")));
            b.DrainEvents(ev, recs); b.Stop();
            Assert.AreEqual(3, ev.FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
            Assert.IsTrue(recs.Count >= 1);
            Assert.AreEqual("r1", b.SessionId);
        }

        // B14. PermissionPrompt in sequence
        [Test] public async Task Seq_PermissionPrompt_Captured() =>
            Assert.IsTrue(Drain(await StartAsync(ED(PermReq("bash","r1","{}")))).Exists(e =>
                e.Kind == ChatEventKind.PermissionPrompt && e.Text == "bash" && e.RequestId == "r1"));

        // B16. Empty data → 0 events, no crash
        [Test] public async Task Seq_Empty_NoEvents()
        {
            var ev = new List<ChatEvent>(); var b = await StartAsync("");
            Assert.DoesNotThrow(() => b.DrainEvents(ev)); b.Stop(); Assert.AreEqual(0, ev.Count);
        }

        // B17. Non-JSON lines → filtered, valid ACP lines still pass
        [Test] public async Task Seq_UnknownPrefix_Filtered() =>
            Assert.AreEqual(2, Drain(await StartAsync(ED(AsstDelta("a"), "JUNK|x", AsstDelta("b"))))
                .FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        // B18. 3 tool call pairs → ≥3 records
        [Test] public async Task Seq_ThreeToolCalls_ThreePlusRecords()
        {
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(
                ToolStart("set_property","t1","{}"), ToolDone("t1","ok"),
                ToolStart("set_property","t2","{}"), ToolDone("t2","ok"),
                ToolStart("set_property","t3","{}"), ToolDone("t3","ok")));
            b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 3, $"Got {recs.Count}");
        }

        // B19. Drain twice → second drain empty
        [Test] public async Task Seq_DrainTwice_SecondEmpty()
        {
            var b = await StartAsync(ED(AsstDelta("a"), AsstDelta("b")));
            var e1 = new List<ChatEvent>(); var e2 = new List<ChatEvent>();
            b.DrainEvents(e1); b.DrainEvents(e2); b.Stop();
            Assert.IsTrue(e1.Count > 0); Assert.AreEqual(0, e2.Count);
        }

        // B20. TurnDone large token counts preserved (cost_update accumulates, turn_completed emits)
        [Test] public async Task Seq_TurnDone_LargeTokens() =>
            Assert.IsTrue(Drain(await StartAsync(ED(CostUpdate("9.99","1000000","500000"), AcpTurnDone("big")))).Exists(e =>
                e.Kind == ChatEventKind.TurnDone && e.InputTokens == 1_000_000 && e.OutputTokens == 500_000));

        // ══════════════════════════════════════════════════════════════════════
        // C. Mode switching and backend construction (18 tests — unchanged)
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
        // D. Accumulation stress + edge cases (17 tests)
        // Removed: D18 ToolProgress boundaries, D19 AskUser nested JSON,
        // ══════════════════════════════════════════════════════════════════════

        // D1. 50 text deltas → exactly 50
        [Test] public async Task Stress_50TextDeltas_All50()
        {
            var lines = new string[50]; for (int i = 0; i < 50; i++) lines[i] = AsstDelta($"w{i}");
            Assert.AreEqual(50, Drain(await StartAsync(ED(lines))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
        }

        // D2. 10 tool call pairs → ≥10 records
        [Test] public async Task Stress_10ToolCalls_AtLeast10Records()
        {
            var lines = new string[20];
            for (int i = 0; i < 10; i++)
            {
                lines[i*2]   = ToolStart("create_object", $"t{i}", $"{{\"n\":{i}}}");
                lines[i*2+1] = ToolDone($"t{i}", "ok");
            }
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(lines)); b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 10, $"Got {recs.Count}");
        }

        // D3. Mix: 5 tool pairs + 5 text — both counts correct
        [Test] public async Task Stress_ToolsAndText_MixedCounts()
        {
            var lines = new string[15];
            for (int i = 0; i < 5; i++)
            {
                lines[i*2]   = ToolStart("set_property", $"m{i}", "{}");
                lines[i*2+1] = ToolDone($"m{i}", "ok");
            }
            for (int i = 5; i < 10; i++) lines[i+5] = AsstDelta($"tok{i}");
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(lines)); b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 5);
            Assert.AreEqual(5, ev.FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
        }

        // D4. DrainEvents with null toolOutput → no NullReferenceException
        [Test] public async Task Stress_NullToolOutput_NoException()
        {
            var b = await StartAsync(ED(ToolStart("create_object","t1","{}"), ToolDone("t1","ok")));
            Assert.DoesNotThrow(() => { var ev = new List<ChatEvent>(); b.DrainEvents(ev, null); b.Stop(); });
        }

        // D5. 10KB text delta preserved
        [Test] public async Task Stress_10KBTextDelta_FullyPreserved()
        {
            var big = new string('Z', 10_000);
            Assert.IsTrue(Drain(await StartAsync(ED(AsstDelta(big))))
                .Exists(e => e.Kind == ChatEventKind.TextDelta && e.Text.Length == 10_000));
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
            var b = await StartAsync(ED(CostUpdate("-0.5","100","50"), AcpTurnDone("s1")));
            Assert.DoesNotThrow(() => Drain(b));
        }

        // D8. Empty tool id — no crash
        [Test] public async Task Stress_EmptyToolId_NoException()
        {
            var b = await StartAsync(ED(ToolStart("get_hierarchy","","{}")));
            Assert.DoesNotThrow(() => { var ev = new List<ChatEvent>(); b.DrainEvents(ev); b.Stop(); });
        }

        // D9. SessionInit empty id — event produced, no crash
        [Test] public async Task Stress_SessionInit_EmptyId_EventProduced() =>
            Assert.IsTrue(Drain(await StartAsync(ED(SessInit(""))))
                .Exists(e => e.Kind == ChatEventKind.SessionInit));

        // D10. Error with empty message — event produced
        [Test] public async Task Stress_Error_EmptyMessage_EventProduced() =>
            Assert.IsTrue(Drain(await StartAsync(ED(AcpErr(""))))
                .Exists(e => e.Kind == ChatEventKind.Error && e.Text == ""));

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
            var b = await StartAsync(ED(SessInit("init-only")));
            Drain(b);
            Assert.AreEqual("init-only", b.SessionId);
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

        // ══════════════════════════════════════════════════════════════════════
        // E. RCP edge cases (4 tests)
        // Removed: E8 AutoReply v1 write-back, E9 BrokenJson args, E10 ControlChars.
        // ══════════════════════════════════════════════════════════════════════

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
            Assert.AreEqual(0, Drain(await StartAsync(ED("   ","\t")))
                .FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        [Test] public void Backend_Dispose_MultipleTimes_NoException()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = Own(new RelayBackend("claude","agent","m",0)); b.Start();
            Assert.DoesNotThrow(() => { b.Dispose(); b.Dispose(); b.Dispose(); });
        }
    }
}
