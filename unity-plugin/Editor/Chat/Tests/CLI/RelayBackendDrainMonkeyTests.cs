// RelayBackendDrainMonkeyTests — DrainEvents tests (ACP v2 protocol).
// Uses ProcessFactory seam + RelayChatProcess(Func) test ctor.
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
    public class RelayBackendDrainMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]  public void SetUp()  => RelaySpawner.EnsureRunningOverride = () => 19800;
        [TearDown] public void TearDown()
        {
            RelayBackend.ProcessFactory = null; RelaySpawner.EnsureRunningOverride = null;
            RelaySpawner.StopForTests();
        }

        // ── ACP JSON helpers ─────────────────────────────────────────────────
        static string Q(string s) => "\"" + s.Replace("\\","\\\\").Replace("\"","\\\"") + "\"";
        static string AsstDelta(string t) => $"{{\"kind\":\"assistant_delta\",\"payload\":{{\"text\":{Q(t)}}}}}";
        static string SessInit(string id) => $"{{\"kind\":\"session_started\",\"payload\":{{\"provider_session_id\":{Q(id)}}},\"session_id\":{Q(id)}}}";
        static string CostUpd(string cost, string inTok, string outTok) =>
            $"{{\"kind\":\"cost_update\",\"payload\":{{\"cost_usd\":{Q(cost)},\"input_tokens\":{Q(inTok)},\"output_tokens\":{Q(outTok)}}}}}";
        static string AcpTD(string sid) => $"{{\"kind\":\"turn_completed\",\"payload\":{{}},\"session_id\":{Q(sid)}}}";
        static string AcpHB() => "{\"kind\":\"heartbeat\",\"payload\":{}}";
        static string AcpRL(string msg) => $"{{\"kind\":\"warning\",\"payload\":{{\"code\":\"rate_limit\",\"message\":{Q(msg)}}}}}";
        static string AcpErr(string msg) => $"{{\"kind\":\"error\",\"payload\":{{\"message\":{Q(msg)}}}}}";
        static string TStart(string name, string id, string args) =>
            $"{{\"kind\":\"tool_call_started\",\"payload\":{{\"name\":{Q(name)},\"id\":{Q(id)},\"args\":{args}}}}}";
        static string PermReq(string tool, string reqId, string inputJson) =>
            $"{{\"kind\":\"permission_requested\",\"payload\":{{\"tool_name\":{Q(tool)},\"request_id\":{Q(reqId)},\"input\":{inputJson}}}}}";

        // ── Infrastructure ───────────────────────────────────────────────────
        static string ED(params string[] lines)
        { var sb = new StringBuilder(); for (int i=0;i<lines.Length;i++) sb.Append(i).Append('\n').Append(lines[i]).Append('\n'); return sb.ToString(); }
        static string JE(string s) => s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n");
        async Task<RelayBackend> StartAsync(string d)
        {
            var expectedAfterSeq = Math.Max(-1, (d.Split('\n').Length - 1) / 2 - 1);
            var polled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RelayBackend.ProcessFactory = () => new RelayChatProcess(j =>
            {
                if (!j.Contains("\"cmd\":\"events\"")) return "{\"ok\":true,\"data\":\"\"}";
                if (expectedAfterSeq < 0 || j.Contains($"\"after_seq\":{expectedAfterSeq}"))
                    polled.TrySetResult(true);
                return $"{{\"ok\":true,\"data\":\"{JE(d)}\"}}";
            });
            var b = new RelayBackend("claude","ask","m",0);
            RegisterCleanup(b.Stop);
            b.Start();
            var completed = await Task.WhenAny(polled.Task, Task.Delay(2000));
            Assert.AreSame(polled.Task, completed, "Relay event stream was not polled");
            await polled.Task;
            return b;
        }
        List<ChatEvent> Drain(RelayBackend b, List<ToolCallRecord> r = null) { var ev = new List<ChatEvent>(); b.DrainEvents(ev, r); b.Stop(); return ev; }
        async Task<List<ChatEvent>> DAsync(string line, List<ToolCallRecord> r = null) => Drain(await StartAsync(ED(line)), r);

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test] public async Task OneTextDelta_ProducesOneEvent()
            => Assert.AreEqual(1, (await DAsync(AsstDelta("hello"))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        [Test] public async Task TextDelta_HasCorrectText()
            => Assert.AreEqual("world", (await DAsync(AsstDelta("world"))).Find(e => e.Kind == ChatEventKind.TextDelta).Text);

        [Test] public async Task Heartbeat_ProducesHeartbeat()
            => Assert.AreEqual(1, (await DAsync(AcpHB())).FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count);

        [Test] public async Task RateLimit_ProducesRateLimit()
            => Assert.AreEqual("please wait", (await DAsync(AcpRL("please wait"))).Find(e => e.Kind == ChatEventKind.RateLimit).Text);

        [Test] public async Task Error_ProducesError()
            => Assert.AreEqual("boom", (await DAsync(AcpErr("boom"))).Find(e => e.Kind == ChatEventKind.Error).Text);

        [Test] public async Task SessionInit_ProducesSessionInitEvent()
            => Assert.AreEqual("sess-abc", (await DAsync(SessInit("sess-abc"))).Find(e => e.Kind == ChatEventKind.SessionInit).SessionId);

        [Test] public async Task SessionInit_UpdatesBackendSessionId()
        { var b = await StartAsync(ED(SessInit("sess-xyz"))); Drain(b); Assert.AreEqual("sess-xyz", b.SessionId); }

        [Test] public async Task TurnDone_ProducesTurnDoneEvent()
            => Assert.AreEqual(1, Drain(await StartAsync(ED(CostUpd("0.01","500","200"), AcpTD("s")))).FindAll(e => e.Kind == ChatEventKind.TurnDone).Count);

        [Test] public async Task TurnDone_UpdatesSessionId()
        { var b = await StartAsync(ED(CostUpd("0.02","100","50"), AcpTD("sess-td"))); Drain(b); Assert.AreEqual("sess-td", b.SessionId); }

        [Test] public async Task TurnDone_CostParsed()
            => Assert.AreEqual(0.05f, Drain(await StartAsync(ED(CostUpd("0.05","100","50"), AcpTD("sid")))).Find(e => e.Kind == ChatEventKind.TurnDone).CostUsd, 0.001f);

        [Test] public async Task TurnDone_TokensParsed()
        { var e = Drain(await StartAsync(ED(CostUpd("0.01","333","222"), AcpTD("sid")))).Find(e2 => e2.Kind == ChatEventKind.TurnDone); Assert.AreEqual(333, e.InputTokens); Assert.AreEqual(222, e.OutputTokens); }

        [Test] public async Task ToolCall_ProducesEvents()
        { var r = new List<ToolCallRecord>(); var ev = await DAsync(TStart("MyTool","id-1","{\"a\":1}"), r); Assert.Greater(ev.Count, 0); }

        [Test] public async Task ToolCall_ProducesRecord()
        { var r = new List<ToolCallRecord>(); await DAsync(TStart("SearchTool","id-2","{}"), r); Assert.IsTrue(r.Exists(x => x.Name == "SearchTool")); }

        [Test] public async Task MalformedLine_ProducesZeroEvents()
            => Assert.AreEqual(0, (await DAsync("GARBAGE_NO_PIPE")).Count);

        [Test] public async Task EmptyLine_ProducesZeroEvents()
            => Assert.AreEqual(0, (await DAsync("")).Count);

        [Test] public async Task MultipleTextDeltas_AllPresent()
            => Assert.AreEqual(3, Drain(await StartAsync(ED(AsstDelta("a"), AsstDelta("b"), AsstDelta("c")))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        [Test] public async Task TextAndHeartbeat_BothPresent()
        { var ev = Drain(await StartAsync(ED(AsstDelta("hello"), AcpHB()))); Assert.AreEqual(1, ev.FindAll(e => e.Kind == ChatEventKind.TextDelta).Count); Assert.AreEqual(1, ev.FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count); }

        [Test] public async Task TurnDone_ZeroTokens_Parses()
        { var e = Drain(await StartAsync(ED(CostUpd("0.0","0","0"), AcpTD("s")))).Find(e2 => e2.Kind == ChatEventKind.TurnDone); Assert.AreEqual(0, e.InputTokens); Assert.AreEqual(0, e.OutputTokens); }

        [Test] public async Task Error_IsOkFalse()
            => Assert.IsFalse((await DAsync(AcpErr("boom"))).Find(e => e.Kind == ChatEventKind.Error).IsOk);

        [Test] public async Task PermissionPrompt_ProducesEvent()
            => Assert.AreEqual(1, (await DAsync(PermReq("bash","req-1","{\"cmd\":\"ls\"}"))).FindAll(e => e.Kind == ChatEventKind.PermissionPrompt).Count);

        [Test] public async Task TextDelta_PipesInText_Preserved()
            => Assert.AreEqual("a|b|c", (await DAsync(AsstDelta("a|b|c"))).Find(e => e.Kind == ChatEventKind.TextDelta).Text);

        [Test] public async Task Stop_ThenDrain_ProducesZero()
        { var b = await StartAsync(ED(AsstDelta("x"))); b.Stop(); var ev = new List<ChatEvent>(); b.DrainEvents(ev); Assert.AreEqual(0, ev.Count); }
    }
}
