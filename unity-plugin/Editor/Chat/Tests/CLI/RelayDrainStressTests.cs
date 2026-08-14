// Monkey tests: RelayBackend.DrainEvents stress — large volumes, repeated drains,
// accumulator invariants, null-safety. No real relay process — ProcessFactory seam.
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
    public class RelayDrainStressTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]  public void SetUp()    => RelaySpawner.EnsureRunningOverride = () => 19750;
        [TearDown] public void TearDown()
        {
            RelayBackend.ProcessFactory        = null;
            RelaySpawner.EnsureRunningOverride  = null;
            RelaySpawner.TcpAliveOverride       = null;
            RelaySpawner.StopForTests();
        }

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
        async Task<RelayBackend> StartAsync(string data)
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
            var b = Own(new RelayBackend("claude","agent","m",0));
            b.Start();
            var completed = await Task.WhenAny(polled.Task, Task.Delay(2000));
            Assert.AreSame(polled.Task, completed, "Relay event stream was not polled");
            await polled.Task;
            return b;
        }
        List<ChatEvent> Drain(RelayBackend b, List<ToolCallRecord> recs = null)
        { var ev = new List<ChatEvent>(); b.DrainEvents(ev, recs); b.Stop(); return ev; }
        RelayBackend Own(RelayBackend backend) { RegisterCleanup(backend.Stop); return backend; }

        // ── ACP JSON helpers ─────────────────────────────────────────────────
        static string Q(string s) => "\"" + s.Replace("\\","\\\\").Replace("\"","\\\"") + "\"";
        static string AsstDelta(string t) => $"{{\"kind\":\"assistant_delta\",\"payload\":{{\"text\":{Q(t)}}}}}";
        static string AcpHB() => "{\"kind\":\"heartbeat\",\"payload\":{}}";
        static string AcpRL(string msg) => $"{{\"kind\":\"warning\",\"payload\":{{\"code\":\"rate_limit\",\"message\":{Q(msg)}}}}}";
        static string AcpErr(string msg) => $"{{\"kind\":\"error\",\"payload\":{{\"message\":{Q(msg)}}}}}";
        static string SessInit(string id) => $"{{\"kind\":\"session_started\",\"payload\":{{\"provider_session_id\":{Q(id)}}},\"session_id\":{Q(id)}}}";
        static string CostUpd(string cost, string inTok, string outTok) => $"{{\"kind\":\"cost_update\",\"payload\":{{\"cost_usd\":{Q(cost)},\"input_tokens\":{Q(inTok)},\"output_tokens\":{Q(outTok)}}}}}";
        static string AcpTD(string sid) => $"{{\"kind\":\"turn_completed\",\"payload\":{{}},\"session_id\":{Q(sid)}}}";
        static string TStart(string name, string id, string args) => $"{{\"kind\":\"tool_call_started\",\"payload\":{{\"name\":{Q(name)},\"id\":{Q(id)},\"args\":{args}}}}}";

        // Volume
        [Test] public async Task Drain_500TextDeltas_AllArrive()
        {
            var lines = new string[500]; for (int i = 0; i < 500; i++) lines[i] = AsstDelta($"w{i}");
            Assert.AreEqual(500, Drain(await StartAsync(ED(lines))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
        }

        [Test] public async Task Drain_200ToolCalls_AllRecordsProduced()
        {
            var lines = new string[200]; for (int i = 0; i < 200; i++) lines[i] = TStart("create_object", $"t{i}", $"{{\"n\":{i}}}");
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(lines)); b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 200, $"Expected ≥200 records, got {recs.Count}");
        }

        [Test] public async Task Drain_50Heartbeats_AllPresent()
        {
            var lines = new string[50]; for (int i = 0; i < 50; i++) lines[i] = AcpHB();
            Assert.AreEqual(50, Drain(await StartAsync(ED(lines))).FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count);
        }

        [Test] public async Task Drain_100RateLimits_AllPresent()
        {
            var lines = new string[100]; for (int i = 0; i < 100; i++) lines[i] = AcpRL("wait");
            Assert.AreEqual(100, Drain(await StartAsync(ED(lines))).FindAll(e => e.Kind == ChatEventKind.RateLimit).Count);
        }

        [Test] public async Task Drain_MixedEvents_TextAndToolCounts()
        {
            var lines = new string[20];
            for (int i = 0; i < 10; i++) lines[i]   = AsstDelta($"tok{i}");
            for (int i = 10; i < 20; i++) lines[i]  = TStart("set_property", $"m{i}", "{}");
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(lines)); b.DrainEvents(ev, recs); b.Stop();
            Assert.AreEqual(10, ev.FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);
            Assert.IsTrue(recs.Count >= 10, $"Expected ≥10 tool records, got {recs.Count}");
        }

        // Repeated drains
        [Test] public async Task Drain_3x_SecondAndThirdEmpty()
        {
            var b = await StartAsync(ED(AsstDelta("a"), AsstDelta("b")));
            var e1 = new List<ChatEvent>(); var e2 = new List<ChatEvent>(); var e3 = new List<ChatEvent>();
            b.DrainEvents(e1); b.DrainEvents(e2); b.DrainEvents(e3); b.Stop();
            Assert.IsTrue(e1.Count > 0); Assert.AreEqual(0, e2.Count); Assert.AreEqual(0, e3.Count);
        }

        [Test] public async Task Drain_AfterStop_AlwaysEmpty()
        {
            var b = await StartAsync(ED("t|hello")); b.Stop();
            var ev = new List<ChatEvent>(); Assert.DoesNotThrow(() => b.DrainEvents(ev)); Assert.AreEqual(0, ev.Count);
        }

        [Test] public void Drain_BeforeStart_ReturnsEmptyNoThrow()
        {
            RelayBackend.ProcessFactory = () => Proc("");
            var b = Own(new RelayBackend("claude","agent","m",0));
            var ev = new List<ChatEvent>(); Assert.DoesNotThrow(() => b.DrainEvents(ev)); Assert.AreEqual(0, ev.Count);
        }

        [Test] public async Task Drain_NullOutput_WhenProcNull_DoesNotThrow()
        {
            // After Stop: _proc==null → guard returns before touching output
            var b = await StartAsync(ED("t|hello")); b.Stop();
            Assert.DoesNotThrow(() => b.DrainEvents(null));
        }

        [Test] public async Task Drain_NullToolOutput_DoesNotThrow()
        {
            var b = await StartAsync(ED("tc|bash|t1|{}","tr|t1|true|ok")); var ev = new List<ChatEvent>();
            Assert.DoesNotThrow(() => { b.DrainEvents(ev, null); b.Stop(); });
        }

        // Accumulator state
        [Test] public async Task Drain_ToolCallWithEmptyArgs_RecordStillPresent()
        {
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(TStart("get_hierarchy","id","{}"))); b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 1, $"Expected ≥1 record even with empty args, got {recs.Count}");
        }

        [Test] public async Task Drain_5MatchedPairs_AtLeast5Records()
        {
            var lines = new string[5];
            for (int i = 0; i < 5; i++) lines[i] = TStart("bash", $"t{i}", "{\"cmd\":\"ls\"}");
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(lines)); b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 5, $"Expected ≥5 records, got {recs.Count}");
        }

        [Test] public async Task Drain_LargeTextDelta_ContentPreserved()
        {
            var big = new string('Z', 10_000);
            Assert.IsTrue(Drain(await StartAsync(ED(AsstDelta(big)))).Exists(e => e.Kind == ChatEventKind.TextDelta && e.Text.Length == 10_000));
        }

        [Test] public async Task Drain_SessionId_UpdatedViaTurnDoneEvent()
        { var b = await StartAsync(ED(SessInit("s1"), CostUpd("0","0","0"), AcpTD("s2"))); Drain(b); Assert.AreEqual("s2", b.SessionId); }

        [Test] public async Task Drain_SessionIdFromSi_CapturedWhenNoDone()
        { var b = await StartAsync(ED(SessInit("init-only"))); Drain(b); Assert.AreEqual("init-only", b.SessionId); }

        // Error and recovery
        [Test] public async Task Drain_ErrorMidStream_EventCaptured()
        {
            var ev = Drain(await StartAsync(ED(AsstDelta("before"), AcpErr("boom"), AsstDelta("after"))));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.Error && e.Text == "boom"));
            Assert.IsTrue(ev.Exists(e => e.Kind == ChatEventKind.TextDelta && e.Text == "before"));
        }

        [Test] public async Task Drain_StopThenDrainAgain_AlwaysEmpty()
        {
            var b = await StartAsync(ED("t|a")); var e1 = new List<ChatEvent>(); b.DrainEvents(e1); b.Stop();
            var e2 = new List<ChatEvent>(); b.DrainEvents(e2); Assert.AreEqual(0, e2.Count);
        }

        [Test] public void Drain_MultipleStopStart_StillDrains()
        {
            RelayBackend.ProcessFactory = () => Proc(ED("t|ok"));
            var b = Own(new RelayBackend("claude","agent","m",0));
            for (int i = 0; i < 3; i++) { b.Start(); b.Stop(); }
            Assert.DoesNotThrow(() => b.DrainEvents(new List<ChatEvent>()));
        }

        [Test] public async Task Drain_ToolCallUnicodeName_RecordProduced()
        {
            var recs = new List<ToolCallRecord>(); var ev = new List<ChatEvent>();
            var b = await StartAsync(ED(TStart("ツール","uid","{\"n\":\"obj\"}"))); b.DrainEvents(ev, recs); b.Stop();
            Assert.IsTrue(recs.Count >= 1, $"Unicode tool name should produce record, got {recs.Count}");
        }
    }
}
