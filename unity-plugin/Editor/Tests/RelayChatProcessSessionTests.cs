// TDD: RED — tests for session_token field and v2 protocol negotiation in RelayChatProcess.
// Uses the test constructor (Func<string,string> sendCommand) to capture the start JSON.
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Chat.CLI;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RelayChatProcessSessionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string FakeOk = "{\"ok\":true,\"data\":\"spawned\"}";

        [Test]
        public void StartViaRelay_WithNonNullToken_IncludesSessionTokenInJson()
        {
            string firstCmd = null;
            using var proc = new RelayChatProcess(cmd =>
            {
                Interlocked.CompareExchange(ref firstCmd, cmd, null);
                return FakeOk;
            });
            proc.StartViaRelay(0, "claude", "ask", null, 9500, null,
                sessionToken: "aabbccdd" + new string('0', 56));
            proc.Kill();

            Assert.That(firstCmd, Does.Contain("\"session_token\""));
        }

        [Test]
        public void StartViaRelay_NullToken_OmitsSessionTokenField()
        {
            string firstCmd = null;
            using var proc = new RelayChatProcess(cmd =>
            {
                Interlocked.CompareExchange(ref firstCmd, cmd, null);
                return FakeOk;
            });
            proc.StartViaRelay(0, "claude", "ask", null, 9500, null,
                sessionToken: null);
            proc.Kill();

            Assert.That(firstCmd, Does.Not.Contain("\"session_token\""));
        }

        // ── Protocol version (always v2, no negotiation) ─────────────────────

        [Test]
        public void StartViaRelay_AlwaysSendsProtocolVersion2()
        {
            string capturedCmd = null;
            using var proc = new RelayChatProcess(cmd =>
            {
                Interlocked.CompareExchange(ref capturedCmd, cmd, null);
                return FakeOk;
            });
            proc.StartViaRelay(0, "claude", "ask", null, 9500, null);
            proc.Kill();

            Assert.That(capturedCmd, Does.Contain("\"protocol_version\":2"));
        }

        // ── C1: project_id wiring ────────────────────────────────────────────

        [Test]
        public void StartViaRelay_IncludesProjectId_InStartJson()
        {
            // C1: StartViaRelay must send project_id so Python sha256(project_id)[:12]
            // matches C# ProjectFingerprint.Compute() and history is found.
            string capturedCmd = null;
            var savedGetProjectId = ProjectFingerprint.GetProjectId;
            try
            {
                ProjectFingerprint.GetProjectId = () => "test-project-raw-id";
                using var proc = new RelayChatProcess(cmd =>
                {
                    System.Threading.Interlocked.CompareExchange(ref capturedCmd, cmd, null);
                    return FakeOk;
                });
                proc.StartViaRelay(0, "claude", "ask", null, 9500, null);
                proc.Kill();
            }
            finally
            {
                ProjectFingerprint.GetProjectId = savedGetProjectId;
            }

            Assert.That(capturedCmd, Does.Contain("\"project_id\""));
            Assert.That(capturedCmd, Does.Contain("test-project-raw-id"));
        }

        [Test]
        public void ParseV2Events_DoesNotUnescapeNewlines()
        {
            // Relay buffer stores JSON with \n as the escape sequence (two chars: \ + n).
            // v2 parse must NOT convert \\n → real newline (that would break JSON).
            // Format: "seq\nline\n..." where \\n inside line is JSON escape, not a separator.
            var data = "0\n{\"kind\":\"assistant_delta\",\"payload\":{\"text\":\"hello\\nworld\"}}\n";
            using var proc = new RelayChatProcess(_ => FakeOk);
            proc.ParseV2Events(data);

            var lines = new List<string>();
            proc.DrainLines(lines);
            Assert.That(lines, Has.Count.EqualTo(1));
            // Must contain backslash-n (JSON escape), not a real newline character.
            Assert.That(lines[0], Does.Not.Contain("\n"),
                "v2 must preserve \\n as JSON escape, not convert to newline");
        }
    }
}
