// TDD Phase 2.1 — TextDiffEngine (Myers diff). 18 tests, all RED before implementation.
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests.CLI
{
    [TestFixture]
    public class TextDiffEngineTests : UnityMcpTestBase
    {
        // === Identity ===

        [Test]
        public void Compute_IdenticalStrings_AllContextFlag()
        {
            var r = TextDiffEngine.Compute("a\nb", "a\nb");
            Assert.IsTrue(r.AllContext, "Identical strings must set AllContext=true");
        }

        [Test]
        public void Compute_IdenticalStrings_NoRemovedOrAdded()
        {
            var r = TextDiffEngine.Compute("a\nb", "a\nb");
            Assert.IsTrue(r.Lines.All(l => l.Kind == DiffLineKind.Context),
                "All lines must be Context for identical strings");
        }

        // === Pure additions / deletions ===

        [Test]
        public void Compute_EmptyOld_AllAdded()
        {
            var r = TextDiffEngine.Compute("", "a\nb");
            Assert.IsTrue(r.Lines.Length > 0);
            Assert.IsTrue(r.Lines.All(l => l.Kind == DiffLineKind.Added),
                "All lines must be Added when old is empty");
        }

        [Test]
        public void Compute_EmptyNew_AllRemoved()
        {
            var r = TextDiffEngine.Compute("a\nb", "");
            Assert.IsTrue(r.Lines.Length > 0);
            Assert.IsTrue(r.Lines.All(l => l.Kind == DiffLineKind.Removed),
                "All lines must be Removed when new is empty");
        }

        [Test]
        public void Compute_AddOneLine_OneAddedLine()
        {
            var r = TextDiffEngine.Compute("a", "a\nb");
            Assert.AreEqual(1, r.Lines.Count(l => l.Kind == DiffLineKind.Added));
            Assert.AreEqual("b", r.Lines.First(l => l.Kind == DiffLineKind.Added).Text);
        }

        [Test]
        public void Compute_RemoveOneLine_OneRemovedLine()
        {
            var r = TextDiffEngine.Compute("a\nb", "a");
            Assert.AreEqual(1, r.Lines.Count(l => l.Kind == DiffLineKind.Removed));
            Assert.AreEqual("b", r.Lines.First(l => l.Kind == DiffLineKind.Removed).Text);
        }

        // === Typical edits ===

        [Test]
        public void Compute_OneLine_Changed_RemovedThenAdded()
        {
            var r = TextDiffEngine.Compute("x", "y");
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Removed && l.Text == "x"));
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Added   && l.Text == "y"));
        }

        [Test]
        public void Compute_TwoChanges_ContextSurrounds()
        {
            // 5-line file, change lines 2 and 4
            var old = "line1\nchange2\nline3\nchange4\nline5";
            var nw  = "line1\nnew2\nline3\nnew4\nline5";
            var r = TextDiffEngine.Compute(old, nw);
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Context),
                "Context lines must appear around changes");
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Removed));
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Added));
        }

        [Test]
        public void Compute_AppendLine_ExistingIsContext()
        {
            var r = TextDiffEngine.Compute("a\nb", "a\nb\nc");
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Context),
                "Unchanged lines before addition must be Context");
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Added && l.Text == "c"));
        }

        // === Line count threshold ===

        [Test]
        public void Compute_OldExceeds80Lines_IsLargeFile()
        {
            var old = string.Join("\n", Enumerable.Repeat("a", 81));
            var r = TextDiffEngine.Compute(old, "b");
            Assert.IsTrue(r.IsLargeFile, "81-line old must set IsLargeFile=true");
        }

        [Test]
        public void Compute_NewExceeds80Lines_IsLargeFile()
        {
            var nw = string.Join("\n", Enumerable.Repeat("a", 81));
            var r = TextDiffEngine.Compute("b", nw);
            Assert.IsTrue(r.IsLargeFile, "81-line new must set IsLargeFile=true");
        }

        [Test]
        public void Compute_Exactly80Lines_NormalDiff()
        {
            var both = string.Join("\n", Enumerable.Repeat("a", 80));
            var r = TextDiffEngine.Compute(both, both);
            Assert.IsFalse(r.IsLargeFile, "Exactly 80 lines must NOT set IsLargeFile");
        }

        // === Edge cases ===

        [Test]
        public void Compute_EmptyBoth_ZeroLines()
        {
            var r = TextDiffEngine.Compute("", "");
            Assert.AreEqual(0, r.Lines.Length);
            Assert.IsTrue(r.AllContext);
        }

        [Test]
        public void Compute_SingleChar_Different()
        {
            var r = TextDiffEngine.Compute("a", "b");
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Removed));
            Assert.IsTrue(r.Lines.Any(l => l.Kind == DiffLineKind.Added));
        }

        [Test]
        public void Compute_WhitespaceOnlyChange_Detected()
        {
            var r = TextDiffEngine.Compute("x", "x ");
            Assert.IsFalse(r.AllContext, "Trailing space must be detected as a difference");
        }

        // === Line ending normalization ===

        [Test]
        public void Compute_CRLF_NormalizedToLF()
        {
            // CRLF vs LF for identical content → no difference
            var r = TextDiffEngine.Compute("a\r\nb", "a\nb");
            Assert.IsTrue(r.AllContext, "CRLF and LF must be treated as identical after normalization");
        }

        [Test]
        public void Compute_MixedLineEndings_Normalized()
        {
            var r = TextDiffEngine.Compute("a\r\nb\nc", "a\nb\nc");
            Assert.IsTrue(r.AllContext, "Mixed line endings must normalize correctly");
        }

        // === Unicode ===

        [Test]
        public void Compute_CyrillicContent_NoException()
        {
            Assert.DoesNotThrow(() =>
                TextDiffEngine.Compute("Привет мир", "Привет мир 2"),
                "Cyrillic content must not throw");
        }

        // === T1.3: trailing newline must not inflate line count ===

        [Test]
        public void Compute_TrailingNewline_NotTreatedAsExtraLine()
        {
            // 80 real C# lines + trailing newline (as any standard editor produces).
            // Split('\n') without TrimEnd produces 81 elements → IsLargeFile=true (wrong).
            // Fix: TrimEnd('\n') before Split gives exactly 80 elements → IsLargeFile=false.
            var line = "    private float _хpHealthPoints = 100f;";
            var old = string.Join("\n", Enumerable.Repeat(line, 80)) + "\n";
            var @new = old.Replace("_хpHealthPoints", "_currentHealthPoints");

            var result = TextDiffEngine.Compute(old, @new);

            Assert.IsFalse(result.IsLargeFile,
                "80 lines + trailing newline must NOT be treated as 81 lines (IsLargeFile must be false); " +
                "trailing '\\n' is standard editor output and must be stripped before counting");
        }

        [Test]
        public void Compute_TrailingNewline_ProducesCorrectDiff()
        {
            // Verify that with the fix the diff is actually computed (not TwoBlock).
            var line = "public class ИгровойОбъект { }";
            var old = string.Join("\n", Enumerable.Repeat(line, 10)) + "\n";
            var @new = old.Replace("ИгровойОбъект", "GameEntity");

            var result = TextDiffEngine.Compute(old, @new);

            Assert.IsFalse(result.IsLargeFile, "small file with trailing newline must use diff mode");
            Assert.IsTrue(result.Lines.Any(l => l.Kind == DiffLineKind.Removed),
                "removed lines must appear in the diff");
            Assert.IsTrue(result.Lines.Any(l => l.Kind == DiffLineKind.Added),
                "added lines must appear in the diff");
        }
    }
}
