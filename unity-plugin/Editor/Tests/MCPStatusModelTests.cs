using NUnit.Framework;
using UnityMCP.Editor;
using static UnityMCP.Editor.MCPStatusModel;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPStatusModelTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void PinTextMode() => SetEditorPrefBool("MCPPlugin_UseEmojiLabel", false);

        // ── GetState ────────────────────────────────────────────────────────
        [Test] public void GetState_NotRunning_ReturnsDown()
            => Assert.AreEqual(State.Down, GetState(false, false));

        [Test] public void GetState_NotRunning_ClientConnected_StillDown()
            => Assert.AreEqual(State.Down, GetState(false, true));

        [Test] public void GetState_Running_NoClient_ReturnsListen()
            => Assert.AreEqual(State.Listen, GetState(true, false));

        [Test] public void GetState_Running_WithClient_ReturnsUp()
            => Assert.AreEqual(State.Up, GetState(true, true));

        // ── GetCssKey ───────────────────────────────────────────────────────
        [Test] public void GetCssKey_Down_ReturnsDown()
            => Assert.AreEqual("down", GetCssKey(State.Down));

        [Test] public void GetCssKey_Listen_ReturnsListen()
            => Assert.AreEqual("listen", GetCssKey(State.Listen));

        [Test] public void GetCssKey_Up_ReturnsUp()
            => Assert.AreEqual("up", GetCssKey(State.Up));

        // ── GetLabel ────────────────────────────────────────────────────────
        [Test] public void GetLabel_NotRunning_ReturnsOffline()
            => Assert.AreEqual("OFFLINE", GetLabel(false, false, 9500));

        [Test] public void GetLabel_Running_NoClient_ReturnsListening()
            => Assert.AreEqual("LISTENING", GetLabel(true, false, 9500));

        [Test] public void GetLabel_Running_WithClient_ReturnsOnlineWithPort()
            => Assert.AreEqual("ONLINE :9500", GetLabel(true, true, 9500));

        [Test] public void GetLabel_Running_WithClient_CustomPort()
            => Assert.AreEqual("ONLINE :9999", GetLabel(true, true, 9999));

        // ── GetSub ──────────────────────────────────────────────────────────
        [Test] public void GetSub_NotRunning_ReturnsServerStopped()
            => Assert.AreEqual("server stopped", GetSub(false, false));

        [Test] public void GetSub_Running_NoClient_ReturnsNoClient()
            => Assert.AreEqual("no client", GetSub(true, false));

        [Test] public void GetSub_Running_WithClient_ReturnsClientConnected()
            => Assert.AreEqual("client connected", GetSub(true, true));

        // ── GetPill ─────────────────────────────────────────────────────────
        [Test] public void GetPill_Down_ReturnsBiomeOff()
            => Assert.AreEqual("Biome off", GetPill(State.Down, 9500));

        [Test] public void GetPill_Listen_ReturnsBiomeDots()
            => Assert.AreEqual("Biome ...", GetPill(State.Listen, 9500));

        [Test] public void GetPill_Up_ReturnsBiomeWithPort()
            => Assert.AreEqual("Biome :9500", GetPill(State.Up, 9500));

        // ── F7: ChatActive state ─────────────────────────────────────────────

        [Test]
        public void GetState_Running_NoClient_ChatRunning_ReturnsChatActive()
            => Assert.AreEqual(State.ChatActive, GetState(true, false, true));

        [Test]
        public void GetState_Running_NoClient_NoChatRunning_ReturnsListen()
            => Assert.AreEqual(State.Listen, GetState(true, false, false));

        [Test]
        public void GetState_Running_ClientConnected_ChatRunning_ReturnsUp()
            => Assert.AreEqual(State.Up, GetState(true, true, true));

        [Test]
        public void GetLabel_ChatActive_ReturnsChatMode()
            => Assert.AreEqual("CHAT MODE", MCPStatusModel.GetLabel(State.ChatActive, 9500));

        [Test]
        public void GetPill_ChatActive_ReturnsBiomeChat()
            => Assert.AreEqual("Biome Chat", GetPill(State.ChatActive, 9500));

        [Test]
        public void GetCssKey_ChatActive_ReturnsChat()
            => Assert.AreEqual("chat", GetCssKey(State.ChatActive));

        // ── GetSubState priority ─────────────────────────────────────────────

        [Test]
        public void GetSubState_BindFailed_WinsOverAll()
        {
            var sub = MCPStatusModel.GetSubState(
                isCompiling: true, portMismatch: true, bindFailed: true, compileFailed: true);
            Assert.AreEqual(MCPStatusModel.SubState.BindFailed, sub);
        }

        [Test]
        public void GetSubState_CompileFailed_WinsOverCompilingAndMismatch()
        {
            var sub = MCPStatusModel.GetSubState(
                isCompiling: true, portMismatch: true, bindFailed: false, compileFailed: true);
            Assert.AreEqual(MCPStatusModel.SubState.CompileFailed, sub);
        }

        [Test]
        public void GetSubState_Compiling_WinsOverMismatch()
        {
            var sub = MCPStatusModel.GetSubState(
                isCompiling: true, portMismatch: true, bindFailed: false, compileFailed: false);
            Assert.AreEqual(MCPStatusModel.SubState.Compiling, sub);
        }

        [Test]
        public void GetSubState_PortMismatch_WhenOnlyMismatch()
        {
            var sub = MCPStatusModel.GetSubState(
                isCompiling: false, portMismatch: true, bindFailed: false, compileFailed: false);
            Assert.AreEqual(MCPStatusModel.SubState.PortMismatch, sub);
        }

        [Test]
        public void GetSubState_None_WhenAllFalse()
        {
            var sub = MCPStatusModel.GetSubState(
                isCompiling: false, portMismatch: false, bindFailed: false, compileFailed: false);
            Assert.AreEqual(MCPStatusModel.SubState.None, sub);
        }

        // ── GetLabel(State, SubState, int) ────────────────────────────────────

        [Test]
        public void GetLabel_BindFailed_ReturnsBINDFAILED()
        {
            var label = MCPStatusModel.GetLabel(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.BindFailed, 9500);
            Assert.AreEqual("BIND FAILED", label);
        }

        [Test]
        public void GetLabel_CompileFailed_ReturnsCOMPILEERROR()
        {
            var label = MCPStatusModel.GetLabel(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.CompileFailed, 9500);
            Assert.AreEqual("COMPILE ERROR", label);
        }

        [Test]
        public void GetLabel_None_FallsBackToExistingLabel()
        {
            var label = MCPStatusModel.GetLabel(
                MCPStatusModel.State.Up, MCPStatusModel.SubState.None, 9500);
            Assert.AreEqual("ONLINE :9500", label);
        }

        [Test]
        public void GetLabel_Listen_None_ReturnsLISTENING()
        {
            var label = MCPStatusModel.GetLabel(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.None, 9500);
            Assert.AreEqual("LISTENING", label);
        }

        // ── GetSub(State, SubState) ───────────────────────────────────────────

        [Test]
        public void GetSub_BindFailed_ContainsBindKeyword()
        {
            var text = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.BindFailed);
            StringAssert.Contains("bind", text);
        }

        [Test]
        public void GetSub_CompileFailed_ContainsCompileKeyword()
        {
            var text = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.CompileFailed);
            StringAssert.Contains("compile", text);
        }

        [Test]
        public void GetSub_Compiling_ContainsCompilingKeyword()
        {
            var text = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.Compiling);
            StringAssert.Contains("compil", text);
        }

        [Test]
        public void GetSub_PortMismatch_ContainsPortKeyword()
        {
            var text = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.PortMismatch);
            StringAssert.Contains("port", text);
        }

        [Test]
        public void GetSub_None_FallsBackToExistingGetSub()
        {
            var text = MCPStatusModel.GetSub(
                MCPStatusModel.State.Up, MCPStatusModel.SubState.None);
            Assert.AreEqual("client connected", text);
        }

        // ── GetSub(State, SubState) exact-string coverage ─────────────────────

        [Test]
        public void GetSub_PortMismatch_ReturnsPortFallbackMessage()
        {
            var sub = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.PortMismatch);
            Assert.AreEqual("port fallback — check config", sub);
        }

        [Test]
        public void GetSub_Compiling_ReturnsCompilingMessage()
        {
            var sub = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.Compiling);
            Assert.AreEqual("compiling — clients wait", sub);
        }

        [Test]
        public void GetSub_BindFailed_ReturnsBindFailedMessage()
        {
            var sub = MCPStatusModel.GetSub(
                MCPStatusModel.State.Down, MCPStatusModel.SubState.BindFailed);
            Assert.AreEqual("bind failed — port in use", sub);
        }

        [Test]
        public void GetSub_None_DelegatesToState_Listen()
        {
            // SubState.None → falls through to GetSub(State) → "no client" for Listen
            var sub = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.None);
            Assert.AreEqual("no client", sub);
        }

        // ── GetPill(State, SubState, port) coverage ───────────────────────────

        [Test]
        public void GetPill_BindFailed_ReturnsErrSuffix()
        {
            var pill = MCPStatusModel.GetPill(
                MCPStatusModel.State.Down, MCPStatusModel.SubState.BindFailed, 9500);
            StringAssert.EndsWith(" err", pill,
                "BindFailed pill must end with ' err' to indicate error state");
        }

        [Test]
        public void GetPill_None_DelegatesToStatePill_ContainsPort()
        {
            var pill = MCPStatusModel.GetPill(
                MCPStatusModel.State.Up, MCPStatusModel.SubState.None, 9500);
            StringAssert.Contains(":9500", pill,
                "None sub-state must fall through to GetPill(State, port) which includes the port");
        }

        // ── GetSub(State, SubState, double compileElapsed) ────────────────────

        [Test]
        public void GetSub_Compiling_WithElapsed_IncludesSeconds()
        {
            var text = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.Compiling, 3.2);
            Assert.AreEqual("compiling — 3.2s", text);
        }

        [Test]
        public void GetSub_Compiling_ZeroElapsed_FallsBackToWait()
        {
            var text = MCPStatusModel.GetSub(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.Compiling, 0.0);
            Assert.AreEqual("compiling — clients wait", text);
        }

        // ── GetPill(State, SubState, int) — Compiling and PortMismatch ────────

        [Test]
        public void GetPill_Compiling_ReturnsArrow()
        {
            var pill = MCPStatusModel.GetPill(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.Compiling, 9500);
            StringAssert.Contains("⟳", pill);
        }

        [Test]
        public void GetPill_PortMismatch_IncludesPort()
        {
            var pill = MCPStatusModel.GetPill(
                MCPStatusModel.State.Listen, MCPStatusModel.SubState.PortMismatch, 9501);
            StringAssert.Contains(":9501", pill);
        }
    }
}
