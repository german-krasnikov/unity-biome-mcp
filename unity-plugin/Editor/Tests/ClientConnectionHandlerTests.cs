// TDD: RefManager.Invalidate deferred to first slow-path command.
// Fast-path commands (ping/get_version/status/get_enabled_tools) skip invalidation.
// Slow-path (any mutating command) triggers it exactly once per connection.
using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ClientConnectionHandlerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Fast-path commands must NOT trigger ref invalidation.
        [TestCase("ping")]
        [TestCase("get_version")]
        [TestCase("status")]
        [TestCase("get_enabled_tools")]
        // client_hello is a handshake fast-path — must not trigger invalidation.
        [TestCase("client_hello")]
        public void IsSlowPath_FastPathCommands_ReturnFalse(string cmd)
        {
            Assert.IsFalse(ClientConnectionHandler.IsSlowPath(cmd));
        }

        // Slow-path (non-probe) commands trigger ref invalidation.
        [TestCase("create_object")]
        [TestCase("set_property")]
        [TestCase("get_hierarchy")]
        [TestCase("batch")]
        [TestCase("run_tests")]
        public void IsSlowPath_SlowPathCommands_ReturnTrue(string cmd)
        {
            Assert.IsTrue(ClientConnectionHandler.IsSlowPath(cmd));
        }

        // Null / empty must be treated as slow-path (safe default — don't skip invalidate).
        [TestCase("")]
        [TestCase(null)]
        public void IsSlowPath_NullOrEmpty_ReturnTrue(string cmd)
        {
            Assert.IsTrue(ClientConnectionHandler.IsSlowPath(cmd));
        }

        // BuildClientHelloResponse: verify cross-language discriminant key and field presence.
        // Python checks hello.get("helloVersion") to select fast-path vs 3-RTT fallback.
        [Test]
        public void BuildClientHelloResponse_ContainsHelloVersion2Discriminant()
        {
            string resp = ClientConnectionHandler.BuildClientHelloResponse(
                "msg1", "proto:3|plugin:1.0|stamp:abc", "/proj/path");
            Assert.IsTrue(resp.Contains("\"helloVersion\":2"),
                $"helloVersion:2 discriminant missing from response: {resp}");
            Assert.AreEqual("msg1", JsonHelper.ExtractString(resp, "id"));
            Assert.AreEqual("/proj/path", JsonHelper.ExtractString(resp, "projectPath"));
            Assert.AreEqual("proto:3|plugin:1.0|stamp:abc", JsonHelper.ExtractString(resp, "version"));
        }

        [Test]
        public void BuildClientHelloResponse_EscapesJsonSpecialChars()
        {
            string resp = ClientConnectionHandler.BuildClientHelloResponse(
                "id\"1", "ver", "/path/\"proj\"");
            // Embedded quotes must be escaped so Python json.loads doesn't reject the frame.
            Assert.AreEqual("id\"1", JsonHelper.ExtractString(resp, "id"));
            Assert.AreEqual("/path/\"proj\"", JsonHelper.ExtractString(resp, "projectPath"));
        }

        // BuildCapacityRejectionResponse: Python discriminant is error==CLIENT_CAPACITY_BUSY.
        // All three numeric fields must be present so Python can extract retry_after_seconds.
        [Test]
        public void BuildCapacityRejectionResponse_ContainsRequiredFields()
        {
            string resp = ClientConnectionHandler.BuildCapacityRejectionResponse(8, 8);
            Assert.AreEqual("CLIENT_CAPACITY_BUSY", JsonHelper.ExtractString(resp, "error"),
                $"error field missing or wrong: {resp}");
            Assert.IsTrue(resp.Contains("\"capacity\":8"), $"capacity field missing: {resp}");
            Assert.IsTrue(resp.Contains("\"active\":8"), $"active field missing: {resp}");
            Assert.IsTrue(resp.Contains("\"retry_after_seconds\":"), $"retry_after_seconds missing: {resp}");
        }

        [Test]
        public void BuildCapacityRejectionResponse_ActiveLessThanCapacity()
        {
            string resp = ClientConnectionHandler.BuildCapacityRejectionResponse(8, 3);
            Assert.IsTrue(resp.Contains("\"capacity\":8"), $"capacity wrong: {resp}");
            Assert.IsTrue(resp.Contains("\"active\":3"), $"active wrong: {resp}");
        }

        // ARC-15 T1: pure classifier for known foreign-protocol probes (AV/EDR scanners,
        // health-checkers) that HTTP/TLS-probe the raw TCP port. The 4-byte header is normally
        // read as a BE length prefix; these bytes reinterpreted as ASCII/TLS all comfortably
        // exceed MaxMessageSize (verified in ARC-15-http-garbage-detect.md §1), so this
        // classifier is only ever consulted from the existing overflow branch — it never
        // misroutes a legitimate small frame. Exact bool equality only (ARC-0a Arm B: no `||`,
        // no "one of N" tautology) — negative rows live in the SAME table so an
        // unconditional-true stub fails them.
        [TestCase(0x47, 0x45, 0x54, 0x20, true)]  // "GET "
        [TestCase(0x50, 0x4F, 0x53, 0x54, true)]  // "POST"
        [TestCase(0x48, 0x45, 0x41, 0x44, true)]  // "HEAD"
        [TestCase(0x50, 0x55, 0x54, 0x20, true)]  // "PUT "
        [TestCase(0x44, 0x45, 0x4C, 0x45, true)]  // "DELE" (TE)
        [TestCase(0x4F, 0x50, 0x54, 0x49, true)]  // "OPTI" (ONS)
        [TestCase(0x48, 0x54, 0x54, 0x50, true)]  // "HTTP" (/1.x response)
        [TestCase(0x16, 0x03, 0x01, 0x00, true)]  // TLS ContentType.Handshake
        [TestCase(0x15, 0x03, 0x01, 0x00, false)] // TLS ContentType.Alert — near-miss, not Handshake
        [TestCase(0x00, 0x00, 0x00, 0x2A, false)] // small valid length (42)
        [TestCase(0x00, 0x98, 0x96, 0x81, false)] // legitimate large length (10,000,001) — no prefix match
        public void IsKnownForeignProtocolProbe_ClassifiesExactly(int b0, int b1, int b2, int b3, bool expected)
        {
            var header = new byte[] { (byte)b0, (byte)b1, (byte)b2, (byte)b3 };
            Assert.AreEqual(expected, ClientConnectionHandler.IsKnownForeignProtocolProbe(header));
        }

        [Test]
        public void IsKnownForeignProtocolProbe_EmptyArray_ReturnsFalseWithoutThrowing()
        {
            var result = true;
            Assert.DoesNotThrow(() => result = ClientConnectionHandler.IsKnownForeignProtocolProbe(Array.Empty<byte>()));
            Assert.IsFalse(result);
        }

        [Test]
        public void IsKnownForeignProtocolProbe_ShortArray_ReturnsFalseWithoutThrowing()
        {
            var result = true;
            Assert.DoesNotThrow(() => result = ClientConnectionHandler.IsKnownForeignProtocolProbe(new byte[] { 0x47, 0x45 }));
            Assert.IsFalse(result);
        }

        [Test]
        public void IsKnownForeignProtocolProbe_NullArray_ReturnsFalseWithoutThrowing()
        {
            var result = true;
            Assert.DoesNotThrow(() => result = ClientConnectionHandler.IsKnownForeignProtocolProbe(null));
            Assert.IsFalse(result);
        }
    }
}
