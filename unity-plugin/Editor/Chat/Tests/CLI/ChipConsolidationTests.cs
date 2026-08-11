// TDD tests for P1: chip consolidation — all chips go through InlineChipModel only.
// Tests are headless pure-logic assertions.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipConsolidationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetForTests();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetForTests();

        // Verify InlineChipModel.SerializePayload produces [kind:ref] text (regression guard).
        [Test]
        public void Test_SerializePayload_HierarchyChip_CorrectFormat()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/World/Player", "Player", 1));
            var payload = m.SerializePayload(new ChipConfig());
            StringAssert.Contains("[hierarchy:/World/Player", payload);
        }

        [Test]
        public void Test_SerializePayload_ScriptChip_CorrectFormat()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0));
            var payload = m.SerializePayload(new ChipConfig());
            StringAssert.Contains("[script:Assets/Foo.cs]", payload);
        }

        // Core test: empty model → text unchanged (AppendChipContext no-op).
        [Test]
        public void Test_AppendChipContext_EmptyModel_NoAppend()
        {
            var m = new InlineChipModel();
            Assert.AreEqual(0, m.Count, "empty model has 0 chips");
            var payload = m.SerializePayload(new ChipConfig());
            Assert.IsEmpty(payload, "empty model produces no payload");
        }
    }
}
