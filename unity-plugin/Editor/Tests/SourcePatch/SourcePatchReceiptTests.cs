// P0-70: SourcePatchDisableReceipt round-trip and fail-closed parsing.
// See §3.3/§6 in Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchReceiptTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void SerializeThenTryParse_RoundTripsExactFields()
        {
            var original = new SourcePatchDisableReceipt("abc123", 4242, "/Users/x/proj", 7);

            var ok = SourcePatchDisableReceipt.TryParse(original.Serialize(), out var parsed);

            Assert.IsTrue(ok);
            Assert.AreEqual(original.OpId, parsed.OpId);
            Assert.AreEqual(original.Pid, parsed.Pid);
            Assert.AreEqual(original.ProjectPath, parsed.ProjectPath);
            Assert.AreEqual(original.ExpectedEpochAfter, parsed.ExpectedEpochAfter);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json at all")]
        [TestCase("{}")]
        public void TryParse_EmptyOrGarbage_ReturnsFalse(string raw)
        {
            var ok = SourcePatchDisableReceipt.TryParse(raw, out var parsed);
            Assert.IsFalse(ok);
            Assert.IsNull(parsed);
        }

        [Test]
        public void TryParse_MissingOpId_ReturnsFalse()
        {
            var ok = SourcePatchDisableReceipt.TryParse(
                "{\"pid\":1,\"project_path\":\"/p\",\"expected_epoch_after\":2}", out var parsed);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParse_MissingProjectPath_ReturnsFalse()
        {
            var ok = SourcePatchDisableReceipt.TryParse(
                "{\"op_id\":\"x\",\"pid\":1,\"expected_epoch_after\":2}", out var parsed);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParse_MissingPid_ReturnsFalse()
        {
            var ok = SourcePatchDisableReceipt.TryParse(
                "{\"op_id\":\"x\",\"project_path\":\"/p\",\"expected_epoch_after\":2}", out var parsed);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParse_MissingExpectedEpochAfter_ReturnsFalse()
        {
            var ok = SourcePatchDisableReceipt.TryParse(
                "{\"op_id\":\"x\",\"pid\":1,\"project_path\":\"/p\"}", out var parsed);
            Assert.IsFalse(ok);
        }
    }
}
