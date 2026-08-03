using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CatalogParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Parse_EmptyString_ReturnsEmptyDict()
        {
            var result = CatalogParser.Parse("");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Parse_SingleLine_ReturnsSingleCategory()
        {
            var result = CatalogParser.Parse("CORE:get_hierarchy,batch");
            Assert.IsTrue(result.ContainsKey("CORE"));
            Assert.AreEqual(2, result["CORE"].Length);
            Assert.AreEqual("get_hierarchy", result["CORE"][0]);
            Assert.AreEqual("batch", result["CORE"][1]);
        }

        [Test]
        public void Parse_MultiLine_ReturnsAllCategories()
        {
            var input = "CORE:get_hierarchy,batch\nSCENE_EDIT:find_objects,set_active\n";
            var result = CatalogParser.Parse(input);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(2, result["CORE"].Length);
            Assert.AreEqual(2, result["SCENE_EDIT"].Length);
            Assert.AreEqual("find_objects", result["SCENE_EDIT"][0]);
        }

        [Test]
        public void Parse_EmptyCategory_ReturnsEmptyArray()
        {
            var input = "CONNECTION:\nCORE:batch";
            var result = CatalogParser.Parse(input);
            Assert.IsTrue(result.ContainsKey("CONNECTION"));
            Assert.AreEqual(0, result["CONNECTION"].Length);
            Assert.AreEqual(1, result["CORE"].Length);
        }

        [Test]
        public void Parse_TrailingNewlineAndWhitespace_NoCrash()
        {
            var input = "CORE:get_hierarchy\n  \n\n";
            Assert.DoesNotThrow(() => CatalogParser.Parse(input));
            var result = CatalogParser.Parse(input);
            Assert.IsTrue(result.ContainsKey("CORE"));
        }
    }
}
