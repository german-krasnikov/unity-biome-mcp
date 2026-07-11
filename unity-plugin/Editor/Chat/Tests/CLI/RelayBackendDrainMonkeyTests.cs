// RelayBackendDrainMonkeyTests — 25 DrainEvents tests (tests 126-150).
// Uses ProcessFactory seam + RelayChatProcess(Func) test ctor. Thread.Sleep(200) for poll.
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayBackendDrainMonkeyTests
    {
        [SetUp]  public void SetUp()  => RelaySpawner.EnsureRunningOverride = () => 19800;
        [TearDown] public void TearDown()
        {
            RelayBackend.ProcessFactory = null; RelaySpawner.EnsureRunningOverride = null;
            RelaySpawner.Stop();
            SessionState.EraseInt(RelaySpawner.PortKey); SessionState.EraseInt(RelaySpawner.PidKey);
        }

        static string ED(params string[] lines)
        { var sb = new StringBuilder(); for (int i=0;i<lines.Length;i++) sb.Append(i).Append('\n').Append(lines[i]).Append('\n'); return sb.ToString(); }
        static string JE(string s) => s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n");
        static RelayChatProcess Proc(string d) =>
            new RelayChatProcess(j => j.Contains("\"cmd\":\"events\"") ? $"{{\"ok\":true,\"data\":\"{JE(d)}\"}}" : "{\"ok\":true,\"data\":\"\"}");
        RelayBackend Start(string d) { RelayBackend.ProcessFactory = () => Proc(d); var b = new RelayBackend("claude","ask","m",0); b.Start(); System.Threading.Thread.Sleep(200); return b; }
        List<ChatEvent> Drain(RelayBackend b, List<ToolCallRecord> r = null) { var ev = new List<ChatEvent>(); b.DrainEvents(ev, r); b.Stop(); return ev; }
        List<ChatEvent> D(string line, List<ToolCallRecord> r = null) => Drain(Start(ED(line)), r);

        [Test] public void OneTextDelta_ProducesOneEvent()
            => Assert.AreEqual(1, D("t|hello").FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        [Test] public void TextDelta_HasCorrectText()
            => Assert.AreEqual("world", D("t|world").Find(e => e.Kind == ChatEventKind.TextDelta).Text);

        [Test] public void Heartbeat_ProducesHeartbeat()
            => Assert.AreEqual(1, D("hb|").FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count);

        [Test] public void RateLimit_ProducesRateLimit()
            => Assert.AreEqual("please wait", D("rl|please wait").Find(e => e.Kind == ChatEventKind.RateLimit).Text);

        [Test] public void Error_ProducesError()
            => Assert.AreEqual("boom", D("e|boom").Find(e => e.Kind == ChatEventKind.Error).Text);

        [Test] public void SessionInit_ProducesSessionInitEvent()
            => Assert.AreEqual("sess-abc", D("si|sess-abc").Find(e => e.Kind == ChatEventKind.SessionInit).SessionId);

        [Test] public void SessionInit_UpdatesBackendSessionId()
        { var b = Start(ED("si|sess-xyz")); Drain(b); Assert.AreEqual("sess-xyz", b.SessionId); }

        [Test] public void TurnDone_ProducesTurnDoneEvent()
            => Assert.AreEqual(1, D("d|s|0.01|500|200").FindAll(e => e.Kind == ChatEventKind.TurnDone).Count);

        [Test] public void TurnDone_UpdatesSessionId()
        { var b = Start(ED("d|sess-td|0.02|100|50")); Drain(b); Assert.AreEqual("sess-td", b.SessionId); }

        [Test] public void TurnDone_CostParsed()
            => Assert.AreEqual(0.05f, D("d|sid|0.05|100|50").Find(e => e.Kind == ChatEventKind.TurnDone).CostUsd, 0.001f);

        [Test] public void TurnDone_TokensParsed()
        { var e = D("d|sid|0.01|333|222").Find(e2 => e2.Kind == ChatEventKind.TurnDone); Assert.AreEqual(333, e.InputTokens); Assert.AreEqual(222, e.OutputTokens); }

        [Test] public void ToolCall_ProducesEvents()
        { var r = new List<ToolCallRecord>(); var ev = D("tc|MyTool|id-1|{\"a\":1}", r); Assert.Greater(ev.Count, 0); }

        [Test] public void ToolCall_ProducesRecord()
        { var r = new List<ToolCallRecord>(); D("tc|SearchTool|id-2|{}", r); Assert.IsTrue(r.Exists(x => x.Name == "SearchTool")); }

        [Test] public void SessionState_ProducesEvent()
            => Assert.AreEqual("active", D("ss|active").Find(e => e.Kind == ChatEventKind.SessionState).State);

        [Test] public void MalformedLine_ProducesZeroEvents()
            => Assert.AreEqual(0, D("GARBAGE_NO_PIPE").Count);

        [Test] public void EmptyLine_ProducesZeroEvents()
            => Assert.AreEqual(0, D("").Count);

        [Test] public void MultipleTextDeltas_AllPresent()
            => Assert.AreEqual(3, Drain(Start(ED("t|a", "t|b", "t|c"))).FindAll(e => e.Kind == ChatEventKind.TextDelta).Count);

        [Test] public void TextAndHeartbeat_BothPresent()
        { var ev = Drain(Start(ED("t|hello", "hb|"))); Assert.AreEqual(1, ev.FindAll(e => e.Kind == ChatEventKind.TextDelta).Count); Assert.AreEqual(1, ev.FindAll(e => e.Kind == ChatEventKind.Heartbeat).Count); }

        [Test] public void TurnDone_ZeroTokens_Parses()
        { var e = D("d|s|0.0|0|0").Find(e2 => e2.Kind == ChatEventKind.TurnDone); Assert.AreEqual(0, e.InputTokens); Assert.AreEqual(0, e.OutputTokens); }

        [Test] public void Error_IsOkFalse()
            => Assert.IsFalse(D("e|boom").Find(e => e.Kind == ChatEventKind.Error).IsOk);

        [Test] public void ToolProgress_ProducesEvent()
            => Assert.AreEqual(50f, D("tp|50|step 1").Find(e => e.Kind == ChatEventKind.ToolProgress).Percentage, 0.1f);

        [Test] public void PermissionPrompt_ProducesEvent()
            => Assert.AreEqual(1, D("pp|bash|req-1|{\"cmd\":\"ls\"}").FindAll(e => e.Kind == ChatEventKind.PermissionPrompt).Count);

        [Test] public void TextDelta_PipesInText_Preserved()
            => Assert.AreEqual("a|b|c", D("t|a|b|c").Find(e => e.Kind == ChatEventKind.TextDelta).Text);

        [Test] public void Stop_ThenDrain_ProducesZero()
        { var b = Start(ED("t|x")); b.Stop(); var ev = new List<ChatEvent>(); b.DrainEvents(ev); Assert.AreEqual(0, ev.Count); }

        [Test] public void AskUser_ProducesAskUserEvent()
            => Assert.AreEqual(1, D("au|req-99|[{\"label\":\"q\"}]").FindAll(e => e.Kind == ChatEventKind.AskUser).Count);
    }
}
