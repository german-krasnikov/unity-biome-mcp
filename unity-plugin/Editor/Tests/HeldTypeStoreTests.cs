using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class HeldTypeStoreTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => HeldTypeStore.Clear();

        [TearDown]
        public void TearDown() => HeldTypeStore.Clear();

        [Test]
        public void Register_AddsEntry()
        {
            HeldTypeStore.Register("MyType", new byte[] { 1, 2, 3 });
            Assert.AreEqual(1, HeldTypeStore.Count);
        }

        [Test]
        public void Register_RefreshExisting_NoGrowth()
        {
            HeldTypeStore.Register("MyType", new byte[] { 1, 2, 3 });
            HeldTypeStore.Register("MyType", new byte[] { 4, 5, 6 });
            Assert.AreEqual(1, HeldTypeStore.Count);
            // Bytes should be updated to latest
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, HeldTypeStore.GetAll()["MyType"]);
        }

        [Test]
        public void Register_LRU_EvictsOldestAt21()
        {
            for (int i = 0; i <= 20; i++)
                HeldTypeStore.Register($"label{i}", new byte[] { (byte)i });

            Assert.AreEqual(20, HeldTypeStore.Count);
            Assert.IsFalse(HeldTypeStore.GetAll().ContainsKey("label0"),
                "label0 (oldest) should be evicted");
            Assert.IsTrue(HeldTypeStore.GetAll().ContainsKey("label20"),
                "label20 (newest) should be present");
        }

        [Test]
        public void Clear_EmptiesStore()
        {
            HeldTypeStore.Register("A", new byte[] { 1 });
            HeldTypeStore.Register("B", new byte[] { 2 });
            HeldTypeStore.Register("C", new byte[] { 3 });

            HeldTypeStore.Clear();

            Assert.AreEqual(0, HeldTypeStore.Count);
        }
    }
}
