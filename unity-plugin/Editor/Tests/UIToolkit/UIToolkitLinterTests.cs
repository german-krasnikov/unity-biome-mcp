// TDD — RED: these tests fail until UILinter.LintUITK is implemented.
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIToolkitLinterTests : UnityMcpTestBase
    {
        private string _tempDir;

        [SetUp]
        public void SetUpTempDir()
        {
            _tempDir = Path.Combine(Application.dataPath, "TestsTemp", "UIToolkitLinter");
            Directory.CreateDirectory(_tempDir);
        }

        // Write a temp file and track it for cleanup.
        private string WriteTempFile(string name, string content)
        {
            string absPath = Path.Combine(_tempDir, name);
            File.WriteAllText(absPath, content);
            TrackOwnedAsset("Assets/TestsTemp/UIToolkitLinter/" + name);
            return absPath;
        }

        // ── A1: well-formed XML ──────────────────────────────────────────────

        [Test]
        public async Task LintUITK_ValidUxml_ReturnsZeroIssues()
        {
            const string uxml =
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:VisualElement name=\"root\"/>" +
                "</ui:UXML>";
            string path = WriteTempFile("valid.uxml", uxml);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Is.EqualTo("ok: 0 issues"),
                $"Expected clean result but got: {result}");
        }

        [Test]
        public async Task LintUITK_MalformedXml_ReportsA1()
        {
            string path = WriteTempFile("malformed.uxml", "<ui:UXML><unclosed>");
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Does.Contain("[A1]"),
                $"Expected [A1] malformed-XML issue but got: {result}");
        }

        // ── A2: broken <Style src> ───────────────────────────────────────────

        [Test]
        public async Task LintUITK_BrokenStyleSrc_ReportsA2()
        {
            const string uxml =
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<Style src=\"nonexistent_xyz.uss\"/>" +
                "<ui:Label name=\"lbl\"/>" +
                "</ui:UXML>";
            string path = WriteTempFile("broken_style.uxml", uxml);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Does.Contain("[A2]"),
                $"Expected [A2] broken Style src issue but got: {result}");
        }

        // ── A3: missing <Template src> ───────────────────────────────────────

        [Test]
        public async Task LintUITK_MissingTemplateSrc_ReportsA3()
        {
            const string uxml =
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<Template src=\"missing_widget_xyz.uxml\" name=\"Widget\"/>" +
                "<ui:VisualElement name=\"root\"/>" +
                "</ui:UXML>";
            string path = WriteTempFile("missing_template.uxml", uxml);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Does.Contain("[A3]"),
                $"Expected [A3] missing Template src issue but got: {result}");
        }

        // ── A4: unnamed interactive elements ─────────────────────────────────

        [Test]
        public async Task LintUITK_UnnamedButton_ReportsA4()
        {
            const string uxml =
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:Button/>" +
                "</ui:UXML>";
            string path = WriteTempFile("unnamed_btn.uxml", uxml);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Does.Contain("[A4]"),
                $"Expected [A4] unnamed interactive element but got: {result}");
        }

        [Test]
        public async Task LintUITK_NamedButton_NoWarning()
        {
            const string uxml =
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:Button name=\"start-btn\"/>" +
                "</ui:UXML>";
            string path = WriteTempFile("named_btn.uxml", uxml);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Is.EqualTo("ok: 0 issues"),
                $"Named button must not trigger A4 but got: {result}");
        }

        // ── A5: duplicate selectors in USS ───────────────────────────────────

        [Test]
        public async Task LintUITK_DuplicateSelector_ReportsA5()
        {
            const string uss =
                ".btn { background-color: blue; }\n" +
                ".other { color: white; }\n" +
                ".btn { color: red; }\n";
            string path = WriteTempFile("dup_sel.uss", uss);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Does.Contain("[A5]"),
                $"Expected [A5] duplicate selector but got: {result}");
        }

        // ── A6: empty rules in USS ────────────────────────────────────────────

        [Test]
        public async Task LintUITK_EmptyRule_ReportsA6()
        {
            const string uss = ".empty {  }\n.ok { color: white; }\n";
            string path = WriteTempFile("empty_rule.uss", uss);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Does.Contain("[A6]"),
                $"Expected [A6] empty rule but got: {result}");
        }

        [Test]
        public async Task LintUITK_ValidUss_ReturnsZeroIssues()
        {
            const string uss =
                ".panel { background-color: black; }\n" +
                ".title { color: white; font-size: 20px; }\n";
            string path = WriteTempFile("valid.uss", uss);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Is.EqualTo("ok: 0 issues"),
                $"Expected clean USS result but got: {result}");
        }

        // ── Multiple issues ───────────────────────────────────────────────────

        [Test]
        public async Task LintUITK_MultipleIssues_ReportsAll()
        {
            // Test A5+A6 together in a USS file (A1 malformed prevents further parsing)
            const string uss =
                ".btn { color: blue; }\n" +
                ".btn { color: red; }\n" +   // A5: duplicate selector
                ".empty {  }\n";              // A6: empty rule
            string path = WriteTempFile("multi_issue.uss", uss);
            string result = UILinter.LintUITK(path, false);
            Assert.That(result, Does.Contain("[A5]"), $"Expected [A5] in: {result}");
            Assert.That(result, Does.Contain("[A6]"), $"Expected [A6] in: {result}");
        }
    }
}
