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

        [Test]
        public void DefaultCatalog_DoesNotContain_DirectOnlyTools()
        {
            var cats = MCPSettings.GetCatalogCategories();
            CollectionAssert.DoesNotContain(cats["UGUI"], "ui_intent");
            CollectionAssert.DoesNotContain(cats["MEDIA"], "vfx_intent");
            CollectionAssert.DoesNotContain(cats["UITOOLKIT"], "uitk_intent");
        }
    }
}
