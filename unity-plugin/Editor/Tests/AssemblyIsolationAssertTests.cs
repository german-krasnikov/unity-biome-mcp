// TDD for D02 — meta-test for the reusable AssemblyIsolationAssert helper.
// Feeds it a known-clean assembly (UnityMCP.Editor.Chat.Parsers, noEngineReferences:true)
// and a known-dirty one (UnityMCP.Editor, references UnityEditor.TestRunner) and asserts
// the correct verdict for each.
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AssemblyIsolationAssertTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void HasNoEngineReferences_CleanAssembly_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                AssemblyIsolationAssert.HasNoEngineReferences("UnityMCP.Editor.Chat.Parsers"));
        }

        [Test]
        public void HasNoEngineReferences_DirtyAssembly_Throws()
        {
            Assert.Throws<AssertionException>(() =>
                AssemblyIsolationAssert.HasNoEngineReferences("UnityMCP.Editor"));
        }

        [Test]
        public void HasNoEngineReferences_MissingAssembly_ThrowsRatherThanVacuouslyPassing()
        {
            Assert.Throws<AssertionException>(() =>
                AssemblyIsolationAssert.HasNoEngineReferences("UnityMCP.DoesNotExist.Assembly"));
        }
    }
}
