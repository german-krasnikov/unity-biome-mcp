// TDD: B15 — PlaytestStepReceipt JSON round-trip + PlaytestReceiptStore path authority.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestStepReceiptTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ToJson_RoundTrips_AllFields()
        {
            var receipt = new PlaytestStepReceipt(
                index: 3, type: "Assert", ms: 12.5, sourceFile: "Playtests/foo.playtest",
                sourceLine: 7, rawPassed: true, expectedFail: false);

            var json = receipt.ToJson();

            StringAssert.Contains("\"index\":3", json);
            StringAssert.Contains("\"type\":\"Assert\"", json);
            StringAssert.Contains("\"ok\":true", json);
            StringAssert.Contains("\"ms\":12.500", json);
            StringAssert.Contains("\"source_file\":\"Playtests/foo.playtest\"", json);
            StringAssert.Contains("\"source_line\":7", json);
            StringAssert.Contains("\"raw_passed\":true", json);
            StringAssert.Contains("\"expected_fail\":false", json);
            StringAssert.Contains("\"console_errored\":false", json);
        }

        [Test]
        public void ToJson_ConsoleErroredTrue_SerializesField()
        {
            var receipt = new PlaytestStepReceipt(
                index: 0, type: "Assert", ms: 1.0, sourceFile: "f.playtest",
                sourceLine: 1, rawPassed: true, expectedFail: false, consoleErrored: true);

            StringAssert.Contains("\"console_errored\":true", receipt.ToJson());
        }

        // ── F7: ConsoleErrored must override a raw-passing/EXPECT_FAIL-inverted step ──

        [Test]
        public void Ok_ConsoleErrored_OverridesPassingRawPassed_ReturnsFalse()
        {
            var receipt = new PlaytestStepReceipt(
                index: 0, type: "Assert", ms: 1.0, sourceFile: "f.playtest",
                sourceLine: 1, rawPassed: true, expectedFail: false, consoleErrored: true);

            Assert.IsFalse(receipt.Ok);
        }

        [Test]
        public void Ok_ConsoleErrored_ExpectFailStepStillFails_ReturnsFalse()
        {
            // today's formula (RawPassed != ExpectedFail) would say true here — the
            // EXPECT_FAIL inversion must not absorb a genuine console error.
            var receipt = new PlaytestStepReceipt(
                index: 0, type: "MCP", ms: 1.0, sourceFile: "f.playtest",
                sourceLine: 1, rawPassed: false, expectedFail: true, consoleErrored: true);

            Assert.IsFalse(receipt.Ok);
        }

        [Test]
        public void Ok_NoConsoleError_Unaffected()
        {
            var receipt = new PlaytestStepReceipt(
                index: 0, type: "Assert", ms: 1.0, sourceFile: "f.playtest",
                sourceLine: 1, rawPassed: true, expectedFail: false, consoleErrored: false);

            Assert.IsTrue(receipt.Ok);
        }

        [Test]
        public void ReceiptStore_ReceiptAndSentinelPaths_ShareOneRoot()
        {
            const string runId = "cafebabe";
            var receipt = PlaytestReceiptStore.ReceiptPath(runId);
            var sentinel = PlaytestReceiptStore.SentinelPath(runId);

            StringAssert.StartsWith(PlaytestReceiptStore.Root, receipt);
            StringAssert.StartsWith(PlaytestReceiptStore.Root, sentinel);
            Assert.AreEqual(
                receipt.Substring(0, receipt.Length - ".json".Length),
                sentinel.Substring(0, sentinel.Length - ".running".Length));
        }
    }
}
