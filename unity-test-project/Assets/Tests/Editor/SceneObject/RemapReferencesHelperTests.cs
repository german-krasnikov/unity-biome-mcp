using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.SceneObject
{
    [TestFixture]
    public class RemapReferencesHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static Dictionary<string, string> ParseMappings(string text)
        {
            var m = typeof(RemapReferencesHelper).GetMethod("ParseMappings",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (Dictionary<string, string>)m.Invoke(null, new object[] { text });
        }

        private static string AutoRemapPath(string refPath, string sourcePath, string targetPath)
        {
            var m = typeof(RemapReferencesHelper).GetMethod("AutoRemapPath",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (string)m.Invoke(null, new object[] { refPath, sourcePath, targetPath });
        }

        // ---------- ParseMappings ----------

        [Test]
        public void ParseMappings_SingleEntry_ParsedCorrectly()
        {
            var result = ParseMappings("old=new");
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result["old"], Is.EqualTo("new"));
        }

        [Test]
        public void ParseMappings_TwoNewlineSeparatedEntries_BothParsed()
        {
            var result = ParseMappings("a=b\nc=d");
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["a"], Is.EqualTo("b"));
            Assert.That(result["c"], Is.EqualTo("d"));
        }

        [Test]
        public void ParseMappings_EmptyString_ReturnsEmptyDictionary()
        {
            var result = ParseMappings("");
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void ParseMappings_MalformedEntryWithoutEquals_Skipped()
        {
            var result = ParseMappings("invalid_no_eq\ngood=val");
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result["good"], Is.EqualTo("val"));
        }

        [Test]
        public void ParseMappings_WhitespaceAroundKeyAndValue_Trimmed()
        {
            var result = ParseMappings("  key  =  value  ");
            Assert.That(result.ContainsKey("key"), Is.True);
            Assert.That(result["key"], Is.EqualTo("value"));
        }

        // ---------- AutoRemapPath ----------

        [Test]
        public void AutoRemapPath_ExactPathMatch_ReturnsTargetPath()
        {
            var result = AutoRemapPath("/Source", "/Source", "/Target");
            Assert.That(result, Is.EqualTo("/Target"));
        }

        [Test]
        public void AutoRemapPath_ChildPath_RemapsWithSuffix()
        {
            var result = AutoRemapPath("/Source/Child", "/Source", "/Target");
            Assert.That(result, Is.EqualTo("/Target/Child"));
        }

        [Test]
        public void AutoRemapPath_DeeperNesting_RemapsCorrectly()
        {
            var result = AutoRemapPath("/Root/A/B/C", "/Root/A", "/New/A");
            Assert.That(result, Is.EqualTo("/New/A/B/C"));
        }
    }
}
