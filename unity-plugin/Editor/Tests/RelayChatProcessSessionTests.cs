// TDD: RED — tests for session_token field and v2 protocol negotiation in RelayChatProcess.
// Uses the test constructor (Func<string,string> sendCommand) to capture the start JSON.
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

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

        // ── V2 protocol negotiation ──────────────────────────────────────────

        [Test]
        public void StartViaRelay_Sends_ProtocolVersion_2_When_Flag_Enabled()
        {
            ProtectEditorPrefBool("UnityMCP.Chat.ProtocolV2");
            // EditorPrefs.GetBool("UnityMCP.Chat.ProtocolV2", true) defaults true.
            string firstCmd = null;
            using var proc = new RelayChatProcess(cmd =>
            {
                Interlocked.CompareExchange(ref firstCmd, cmd, null);
                return FakeOk;
            });
            proc.StartViaRelay(0, "claude", "ask", null, 9500, null);
            proc.Kill();

            Assert.That(firstCmd, Does.Contain("\"protocol_version\":2"));
        }

        [Test]
        public void StartViaRelay_NegotiatedVersion_Set_To_2_On_V2_Response()
        {
            const string V2Resp = "{\"ok\":true,\"data\":\"spawned pid=1\",\"negotiated_version\":2}";
            using var proc = new RelayChatProcess(_ => V2Resp);
            proc.StartViaRelay(0, "claude", "ask", null, 9500, null);
            proc.Kill();

            Assert.That(proc.NegotiatedVersion, Is.EqualTo(2));
        }

        [Test]
        public void StartViaRelay_NegotiatedVersion_Defaults_To_1_On_Missing_Field()
        {
            using var proc = new RelayChatProcess(_ => FakeOk);
            proc.StartViaRelay(0, "claude", "ask", null, 9500, null);
            proc.Kill();

            Assert.That(proc.NegotiatedVersion, Is.EqualTo(1));
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
