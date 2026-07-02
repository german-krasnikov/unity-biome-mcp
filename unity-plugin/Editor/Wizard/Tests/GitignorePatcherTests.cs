using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GitignorePatcherTests
    {
        private static readonly string[] Paths =
        {
            ".mcp.json", ".cursor/mcp.json", ".vscode/mcp.json",
            ".windsurf/mcp.json", ".codex/config.toml", ".junie/mcp/mcp.json"
        };

        [Test]
        public void EnsureEntries_EmptyFile_AddsAllEntriesUnderMarker()
        {
            var result = GitignorePatcher.EnsureEntries("", Paths);
            StringAssert.Contains(GitignorePatcher.MarkerLine, result);
            foreach (var p in Paths)
                StringAssert.Contains(p, result);
        }

        [Test]
        public void EnsureEntries_AllEntriesAlreadyPresent_ReturnsUnchangedText()
        {
            var first = GitignorePatcher.EnsureEntries("", Paths);
            var second = GitignorePatcher.EnsureEntries(first, Paths);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void EnsureEntries_PartialEntriesPresent_AddsOnlyMissing()
        {
            var existing = GitignorePatcher.MarkerLine + "\n.mcp.json\n";
            var result = GitignorePatcher.EnsureEntries(existing, Paths);
            foreach (var p in Paths)
                StringAssert.Contains(p, result);
            // ".mcp.json" line must still appear only once.
            var count = 0;
            var idx = 0;
            while ((idx = result.IndexOf(".mcp.json", idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += ".mcp.json".Length;
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void EnsureEntries_EntryWithTrailingSlashVariant_NotDuplicated()
        {
            var existing = GitignorePatcher.MarkerLine + "\n.cursor/mcp.json/\n";
            var result = GitignorePatcher.EnsureEntries(existing, new[] { ".cursor/mcp.json" });
            Assert.AreEqual(existing, result);
        }

        [Test]
        public void EnsureEntries_CalledTwiceWithSameInput_Idempotent()
        {
            var once = GitignorePatcher.EnsureEntries("node_modules/\n", Paths);
            var twice = GitignorePatcher.EnsureEntries(once, Paths);
            Assert.AreEqual(once, twice);
        }

        [Test]
        public void EnsureEntries_PreservesUnrelatedExistingLines()
        {
            var result = GitignorePatcher.EnsureEntries("node_modules/\n", Paths);
            StringAssert.Contains("node_modules/", result);
        }
    }
}
