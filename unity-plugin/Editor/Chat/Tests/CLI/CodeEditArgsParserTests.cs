// TDD Phase 2.3 — CodeEditArgsParser. 10 tests (2 via TestCase), all RED before implementation.
// Keys verified empirically (Plans/Reviews/EMPIRICAL-argsJson.md):
//   file_path (primary), path (fallback), old_string, new_string, content, edits[].
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests.CLI
{
    [TestFixture]
    public class CodeEditArgsParserTests : UnityMcpTestBase
    {
        // === Edit tool: field extraction (parameterized to cover path/file_path synonyms) ===

        [TestCase("{\"path\":\"/A.cs\",\"old_string\":\"x\",\"new_string\":\"y\"}",
            "/A.cs", "x", "y")]
        [TestCase("{\"file_path\":\"/B.cs\",\"old_string\":\"a\",\"new_string\":\"b\"}",
            "/B.cs", "a", "b")]
        public void ParseEdit_ValidJson_ExtractsFields(
            string json, string expectedPath, string expectedOld, string expectedNew)
        {
            var r = CodeEditArgsParser.Parse(json);
            Assert.AreEqual(expectedPath, r.FilePath);
            Assert.AreEqual(expectedOld,  r.OldString);
            Assert.AreEqual(expectedNew,  r.NewString);
            Assert.IsTrue(r.IsValid);
        }

        [Test]
        public void ParseEdit_EmptyOldString_IsValid()
        {
            var r = CodeEditArgsParser.Parse("{\"path\":\"A.cs\",\"old_string\":\"\",\"new_string\":\"x\"}");
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("", r.OldString);
        }

        // === Write tool: content field ===

        [Test]
        public void ParseWrite_ContentField_Extracted()
        {
            var r = CodeEditArgsParser.Parse("{\"path\":\"/X.cs\",\"content\":\"full file\"}");
            Assert.AreEqual("full file", r.Content);
            Assert.IsNull(r.OldString);
        }

        // === Multi-edit (edits array) ===

        [Test]
        public void ParseMultiEdit_TwoEdits_BothExtracted()
        {
            var json = "{\"file_path\":\"/X.cs\"," +
                       "\"edits\":[{\"old_string\":\"a\",\"new_string\":\"b\"}," +
                       "{\"old_string\":\"c\",\"new_string\":\"d\"}]}";
            var r = CodeEditArgsParser.Parse(json);
            Assert.IsNotNull(r.Edits);
            Assert.AreEqual(2, r.Edits.Length);
        }

        // === Multi-edit: values containing braces ===

        [Test]
        public void ParseMultiEdit_OldStringContainsBraces_Correct()
        {
            var json = "{\"file_path\":\"/A.cs\",\"edits\":[{\"old_string\":\"void F(){}\",\"new_string\":\"void F() { }\"}]}";
            var r = CodeEditArgsParser.Parse(json);
            Assert.AreEqual(1, r.Edits.Length);
            Assert.AreEqual("void F(){}", r.Edits[0].OldString);
        }

        // === Error cases ===

        [Test]
        public void Parse_Null_IsValidFalse()
        {
            var r = CodeEditArgsParser.Parse(null);
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void Parse_InvalidJson_IsValidFalse()
        {
            var r = CodeEditArgsParser.Parse("not json");
            Assert.IsFalse(r.IsValid);
        }

        // === Language detection ===

        [Test]
        public void DetectLang_CsFile_ReturnsCsharp()
        {
            Assert.AreEqual("csharp", CodeEditArgsParser.DetectLang("/Foo.cs"));
        }

        [Test]
        public void DetectLang_PyFile_ReturnsPython()
        {
            Assert.AreEqual("python", CodeEditArgsParser.DetectLang("/bar.py"));
        }

        [Test]
        public void DetectLang_UnknownExt_ReturnsEmpty()
        {
            Assert.AreEqual("", CodeEditArgsParser.DetectLang("/x.xyz"));
        }
    }
}
