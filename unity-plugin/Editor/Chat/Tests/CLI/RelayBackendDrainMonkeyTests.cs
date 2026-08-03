// RelayBackendDrainMonkeyTests — 25 DrainEvents tests (tests 126-150).
// Uses ProcessFactory seam + RelayChatProcess(Func) test ctor.
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
    public class RelayBackendDrainMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]  public void SetUp()  => RelaySpawner.EnsureRunningOverride = () => 19800;
        [TearDown] public void TearDown()
        {
            RelayBackend.ProcessFactory = null; RelaySpawner.EnsureRunningOverride = null;
            RelaySpawner.StopForTests();
        }

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

        [Test] public async Task OneTextDelta_ProducesOneEvent()
            => Assert.AreEqual(1, (await DAsync("t|hello")).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        [Test] public async Task TextDelta_HasCorrectText()
            => Assert.AreEqual("world", (await DAsync("t|world")).Find(e => e.Kind == ChatEventKind.TextDelta).Text);

        [Test] public async Task Heartbeat_ProducesHeartbeat()
            => Assert.AreEqual(1, (await DAsync("hb|")).FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count);

        [Test] public async Task RateLimit_ProducesRateLimit()
            => Assert.AreEqual("please wait", (await DAsync("rl|please wait")).Find(e => e.Kind == ChatEventKind.RateLimit).Text);

        [Test] public async Task Error_ProducesError()
            => Assert.AreEqual("boom", (await DAsync("e|boom")).Find(e => e.Kind == ChatEventKind.Error).Text);

        [Test] public async Task SessionInit_ProducesSessionInitEvent()
            => Assert.AreEqual("sess-abc", (await DAsync("si|sess-abc")).Find(e => e.Kind == ChatEventKind.SessionInit).SessionId);

        [Test] public async Task SessionInit_UpdatesBackendSessionId()
        { var b = await StartAsync(ED("si|sess-xyz")); Drain(b); Assert.AreEqual("sess-xyz", b.SessionId); }

        [Test] public async Task TurnDone_ProducesTurnDoneEvent()
            => Assert.AreEqual(1, (await DAsync("d|s|0.01|500|200")).FindAll(e => e.Kind == ChatEventKind.TurnDone).Count);

        [Test] public async Task TurnDone_UpdatesSessionId()
        { var b = await StartAsync(ED("d|sess-td|0.02|100|50")); Drain(b); Assert.AreEqual("sess-td", b.SessionId); }

        [Test] public async Task TurnDone_CostParsed()
            => Assert.AreEqual(0.05f, (await DAsync("d|sid|0.05|100|50")).Find(e => e.Kind == ChatEventKind.TurnDone).CostUsd, 0.001f);

        [Test] public async Task TurnDone_TokensParsed()
        { var e = (await DAsync("d|sid|0.01|333|222")).Find(e2 => e2.Kind == ChatEventKind.TurnDone); Assert.AreEqual(333, e.InputTokens); Assert.AreEqual(222, e.OutputTokens); }

        [Test] public async Task ToolCall_ProducesEvents()
        { var r = new List<ToolCallRecord>(); var ev = await DAsync("tc|MyTool|id-1|{\"a\":1}", r); Assert.Greater(ev.Count, 0); }

        [Test] public async Task ToolCall_ProducesRecord()
        { var r = new List<ToolCallRecord>(); await DAsync("tc|SearchTool|id-2|{}", r); Assert.IsTrue(r.Exists(x => x.Name == "SearchTool")); }

        [Test] public async Task SessionState_ProducesEvent()
            => Assert.AreEqual("active", (await DAsync("ss|active")).Find(e => e.Kind == ChatEventKind.SessionState).State);

        [Test] public async Task MalformedLine_ProducesZeroEvents()
            => Assert.AreEqual(0, (await DAsync("GARBAGE_NO_PIPE")).Count);

        [Test] public async Task EmptyLine_ProducesZeroEvents()
            => Assert.AreEqual(0, (await DAsync("")).Count);

        [Test] public async Task MultipleTextDeltas_AllPresent()
            => Assert.AreEqual(3, Drain(await StartAsync(ED("t|a", "t|b", "t|c"))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        [Test] public async Task TextAndHeartbeat_BothPresent()
        { var ev = Drain(await StartAsync(ED("t|hello", "hb|"))); Assert.AreEqual(1, ev.FindAll(e => e.Kind == ChatEventKind.TextDelta).Count); Assert.AreEqual(1, ev.FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count); }

        [Test] public async Task TurnDone_ZeroTokens_Parses()
        { var e = (await DAsync("d|s|0.0|0|0")).Find(e2 => e2.Kind == ChatEventKind.TurnDone); Assert.AreEqual(0, e.InputTokens); Assert.AreEqual(0, e.OutputTokens); }

        [Test] public async Task Error_IsOkFalse()
            => Assert.IsFalse((await DAsync("e|boom")).Find(e => e.Kind == ChatEventKind.Error).IsOk);

        [Test] public async Task ToolProgress_ProducesEvent()
            => Assert.AreEqual(50f, (await DAsync("tp|50|step 1")).Find(e => e.Kind == ChatEventKind.ToolProgress).Percentage, 0.1f);

        [Test] public async Task PermissionPrompt_ProducesEvent()
            => Assert.AreEqual(1, (await DAsync("pp|bash|req-1|{\"cmd\":\"ls\"}")).FindAll(e => e.Kind == ChatEventKind.PermissionPrompt).Count);

        [Test] public async Task TextDelta_PipesInText_Preserved()
            => Assert.AreEqual("a|b|c", (await DAsync("t|a|b|c")).Find(e => e.Kind == ChatEventKind.TextDelta).Text);

        [Test] public async Task Stop_ThenDrain_ProducesZero()
        { var b = await StartAsync(ED("t|x")); b.Stop(); var ev = new List<ChatEvent>(); b.DrainEvents(ev); Assert.AreEqual(0, ev.Count); }

        [Test] public async Task AskUser_ProducesAskUserEvent()
            => Assert.AreEqual(1, (await DAsync("au|req-99|[{\"label\":\"q\"}]")).FindAll(e => e.Kind == ChatEventKind.AskUser).Count);
    }
}
