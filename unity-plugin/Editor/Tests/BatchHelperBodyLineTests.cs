// TDD (D7 Defect 2): every batch body line must carry the "ok:"/"err:"
// prefix the Python-side contract (_BODY_LINE_RE = ^\[(\d+)\] (ok|err):)
// already assumes — bare "ok" and raw handler data were previously emitted
// without it, making the batch summary/body incoherent for 36 tools.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BatchHelperBodyLineTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            CommandRegistry.Register("test_body_bareok", _ => "ok",
                required: "", alwaysAllowed: true);
            CommandRegistry.Register("test_body_rawdata", _ => "Main Camera &MKa",
                required: "", alwaysAllowed: true);
            CommandRegistry.Register("test_body_okprefixed", _ => "ok: already prefixed",
                required: "", alwaysAllowed: true);
            CommandRegistry.Register("test_body_errline", _ => "err: boom",
                required: "", alwaysAllowed: true);
        }

        [Test]
        public void BatchBodyLine_BareOk_WritesOkColon()
        {
            var result = BatchHelper.Execute("test_body_bareok", "continue");
            StringAssert.Contains("[0] ok:", result);
            Assert.IsFalse(BatchHelper.IsFailureResult("ok"));
        }

        [Test]
        public void BatchBodyLine_RawData_PrefixesWithOk()
        {
            var result = BatchHelper.Execute("test_body_rawdata", "continue");
            StringAssert.Contains("[0] ok: Main Camera &MKa", result);
        }

        [Test]
        public void BatchBodyLine_AlreadyOkPrefixed_NotDoublePrefixed()
        {
            var result = BatchHelper.Execute("test_body_okprefixed", "continue");
            StringAssert.Contains("[0] ok: already prefixed", result);
            StringAssert.DoesNotContain("ok: ok:", result);
        }

        [Test]
        public void BatchBodyLine_ErrorResult_Unchanged()
        {
            var result = BatchHelper.Execute("test_body_errline", "continue");
            StringAssert.Contains("[0] err: boom", result);
            StringAssert.DoesNotContain("ok:", result.Split('\n')[0]);
        }

        [Test]
        public void BatchBodyLine_MixedCommands_EveryIndexHasOkOrErrPrefix()
        {
            var result = BatchHelper.Execute(
                "test_body_bareok\ntest_body_rawdata\ntest_body_errline", "continue");
            StringAssert.Contains("[0] ok:", result);
            StringAssert.Contains("[1] ok: Main Camera &MKa", result);
            StringAssert.Contains("[2] err: boom", result);
        }
    }
}
