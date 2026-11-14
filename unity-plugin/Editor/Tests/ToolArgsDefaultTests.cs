// 2E: Verify that generated ToolArgs int accessors return the DSL DEFAULT when the key is absent.
// Red: fails before tool.construct csargs template fix (accessors call ExtractInt with no default → 0).
// Green: passes after regeneration with the fixed template.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ToolArgsDefaultTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string Empty = "{}";

        // fingerprint: ARG depth int DEFAULT 3
        [Test]
        public void FingerprintArgs_Depth_WhenMissing_ReturnsThree()
            => Assert.AreEqual(3, FingerprintArgs.Depth(Empty));

        // validate_references: ARG depth int DEFAULT 3 RAW
        [Test]
        public void ValidateReferencesArgs_Depth_WhenMissing_ReturnsThree()
            => Assert.AreEqual(3, ValidateReferencesArgs.Depth(Empty));

        // undo_last: ARG turns int DEFAULT 1 RAW
        [Test]
        public void UndoLastArgs_Turns_WhenMissing_ReturnsOne()
            => Assert.AreEqual(1, UndoLastArgs.Turns(Empty));

        // list_test_runs: ARG limit int DEFAULT 20
        [Test]
        public void ListTestRunsArgs_Limit_WhenMissing_ReturnsTwenty()
            => Assert.AreEqual(20, ListTestRunsArgs.Limit(Empty));

        // references: ARG depth int DEFAULT 1 OMIT RAW
        [Test]
        public void ReferencesArgs_Depth_WhenMissing_ReturnsOne()
            => Assert.AreEqual(1, ReferencesArgs.Depth(Empty));

        // Explicit value still takes precedence over default
        [Test]
        public void FingerprintArgs_Depth_WhenPresent_ReturnsProvidedValue()
            => Assert.AreEqual(5, FingerprintArgs.Depth("{\"depth\":5}"));
    }
}
