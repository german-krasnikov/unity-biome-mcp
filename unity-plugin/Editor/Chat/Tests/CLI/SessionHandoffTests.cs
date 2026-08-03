using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SessionHandoffTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── GetResumeCommand ────────────────────────────────────────────────────

        [Test]
        public void GetResumeCommand_NullSessionId_ReturnsNull()
        {
            Assert.IsNull(SessionHandoff.GetResumeCommand(BackendKind.Claude, null, null));
        }

        [Test]
        public void GetResumeCommand_EmptySessionId_ReturnsNull()
        {
            Assert.IsNull(SessionHandoff.GetResumeCommand(BackendKind.Claude, "", null));
        }

        [Test]
        public void GetResumeCommand_Claude_ReturnsCorrectFormat()
        {
            var cmd = SessionHandoff.GetResumeCommand(BackendKind.Claude, "abc123", "/my/project");
            Assert.AreEqual("cd /my/project && claude --resume abc123", cmd);
        }

        [Test]
        public void GetResumeCommand_Claude_NullProjectDir_OmitsCd()
        {
            var cmd = SessionHandoff.GetResumeCommand(BackendKind.Claude, "abc123", null);
            Assert.AreEqual("claude --resume abc123", cmd);
        }

        [Test]
        public void GetResumeCommand_Codex_IgnoresProjectDir()
        {
            var cmd = SessionHandoff.GetResumeCommand(BackendKind.Codex, "sess-xyz", "/my/project");
            Assert.AreEqual("codex resume sess-xyz", cmd);
        }

        [Test]
        public void GetResumeCommand_Codex_ReturnsCorrectFormat()
        {
            var cmd = SessionHandoff.GetResumeCommand(BackendKind.Codex, "sess-xyz", null);
            Assert.AreEqual("codex resume sess-xyz", cmd);
        }

        [Test]
        public void GetResumeCommand_OpenCode_ReturnsCorrectFormat()
        {
            var cmd = SessionHandoff.GetResumeCommand(BackendKind.OpenCode, "s1", null);
            Assert.AreEqual("opencode -s s1", cmd);
        }

        [Test]
        public void GetResumeCommand_Kimi_ReturnsCorrectFormat()
        {
            var cmd = SessionHandoff.GetResumeCommand(BackendKind.Kimi, "k42", null);
            Assert.AreEqual("kimi -S k42", cmd);
        }

        [Test]
        public void GetResumeCommand_Antigravity_ReturnsCorrectFormat()
        {
            var cmd = SessionHandoff.GetResumeCommand(BackendKind.Antigravity, "g99", null);
            Assert.AreEqual("agy --conversation g99", cmd);
        }

        // ── GetBinaryName ───────────────────────────────────────────────────────

        [Test]
        public void GetBinaryName_Claude_ReturnsClaude()
        {
            Assert.AreEqual("claude", SessionHandoff.GetBinaryName(BackendKind.Claude));
        }

        [Test]
        public void GetBinaryName_Codex_ReturnsCodex()
        {
            Assert.AreEqual("codex", SessionHandoff.GetBinaryName(BackendKind.Codex));
        }

        [Test]
        public void GetBinaryName_OpenCode_ReturnsOpencode()
        {
            Assert.AreEqual("opencode", SessionHandoff.GetBinaryName(BackendKind.OpenCode));
        }

        [Test]
        public void GetBinaryName_Kimi_ReturnsKimi()
        {
            Assert.AreEqual("kimi", SessionHandoff.GetBinaryName(BackendKind.Kimi));
        }

        [Test]
        public void GetBinaryName_Antigravity_ReturnsAgy()
        {
            Assert.AreEqual("agy", SessionHandoff.GetBinaryName(BackendKind.Antigravity));
        }

        [Test]
        public void GetBinaryName_InvalidKind_ReturnsNull()
        {
            Assert.IsNull(SessionHandoff.GetBinaryName((BackendKind)999));
        }
    }
}
