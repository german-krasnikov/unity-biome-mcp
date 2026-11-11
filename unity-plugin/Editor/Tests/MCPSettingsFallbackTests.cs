using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    // ── CS5.test.3 — MCPSettings.GetCatalogCategories fallback ───────────────

    [TestFixture]
    public class MCPSettingsFallbackTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string KeyCatalog = "UnityMCP_Catalog";

        [SetUp]
        public void SetUp() => DeleteEditorPrefString(KeyCatalog);

        [Test]
        public void GetCatalogCategories_CorruptStoredCatalog_ReturnsFallbackDefault()
        {
            MCPSettings.SetCatalog("<<<BAD_DATA>>>");

            var cats = MCPSettings.GetCatalogCategories();

            Assert.IsNotNull(cats);
            Assert.IsTrue(cats.ContainsKey("CORE"),
                "Fallback catalog must contain the CORE key");
        }

        [Test]
        public void GetCatalogCategories_EmptyCatalog_ReturnsFallbackDefault()
        {
            MCPSettings.SetCatalog("");

            var cats = MCPSettings.GetCatalogCategories();

            Assert.IsNotNull(cats);
            Assert.IsTrue(cats.ContainsKey("CORE"));
        }

        // B1: GetCatalogCategories() is generated from ToolRoster.g.cs (dsl/tools.biome).
        // This is a drift guard — it would have caught the pre-DSL hand-written
        // _defaultCatalog silently missing 33 real tool names.
        [Test]
        public void GetCatalogCategories_ExactlyMatchesGeneratedToolRoster()
        {
            var cats = MCPSettings.GetCatalogCategories();
            var expected = ToolRoster.Rows
                .GroupBy(row => row.Category)
                .ToDictionary(g => g.Key, g => g.Select(row => row.Name).ToArray());

            Assert.AreEqual(expected.Count, cats.Count, "category count mismatch");
            foreach (var kv in expected)
            {
                Assert.IsTrue(cats.ContainsKey(kv.Key), $"missing category {kv.Key}");
                CollectionAssert.AreEquivalent(kv.Value, cats[kv.Key],
                    $"category {kv.Key} does not match ToolRoster.Rows");
            }
        }
    }
}
