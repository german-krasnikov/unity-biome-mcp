using NUnit.Framework;
using System.Collections.Generic;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChangelogReaderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        const string Sample = @"# Changelog

## [Unreleased]

## [v0.38.0] — 2026-06-19 <!-- comment -->

Added foo.

## [v0.35.0] — 2026-05-01

Fixed bar.

## [v0.10.0] — 2025-01-01

Initial release.
";

        [Test]
        public void Parse_ExtractsVersionAndContent()
        {
            var entries = ChangelogReader.Parse(Sample, "0.35.0");
            Assert.AreEqual(4, entries.Count);
            Assert.AreEqual("Unreleased", entries[0].Version);
            Assert.AreEqual("0.38.0",     entries[1].Version);
            Assert.AreEqual("0.35.0",     entries[2].Version);
            Assert.AreEqual("0.10.0",     entries[3].Version);
        }

        [Test]
        public void Parse_ExtractsDate()
        {
            var entries = ChangelogReader.Parse(Sample, "0.0.0");
            Assert.AreEqual("2026-06-19", entries[1].Date);
            Assert.AreEqual("2026-05-01", entries[2].Date);
        }

        [Test]
        public void Parse_MarksNewerEntries()
        {
            var entries = ChangelogReader.Parse(Sample, "0.35.0");
            Assert.IsFalse(entries[0].IsNewer, "Unreleased should not be marked IsNewer");
            Assert.IsTrue (entries[1].IsNewer, "0.38.0 > 0.35.0 → IsNewer");
            Assert.IsFalse(entries[2].IsNewer, "0.35.0 == current → not IsNewer");
            Assert.IsFalse(entries[3].IsNewer, "0.10.0 < current → not IsNewer");
        }

        [Test]
        public void Parse_HandlesEmptyContent()
        {
            var entries = ChangelogReader.Parse("", "1.0.0");
            Assert.AreEqual(0, entries.Count);
        }

        [Test]
        public void Parse_HandlesNullContent()
        {
            Assert.DoesNotThrow(() => ChangelogReader.Parse(null, "1.0.0"));
        }

        [Test]
        public void Parse_HandlesUnreleasedSection()
        {
            const string md = "## [Unreleased]\n\nSome pending work.\n\n## [v1.0.0] — 2025-01-01\n\nFirst release.";
            var entries = ChangelogReader.Parse(md, "1.0.0");
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("Unreleased", entries[0].Version);
            StringAssert.Contains("Some pending work", entries[0].Content);
        }

        [Test]
        public void Parse_ExtractsBodyContent()
        {
            var entries = ChangelogReader.Parse(Sample, "0.0.0");
            StringAssert.Contains("Added foo", entries[1].Content);
            StringAssert.Contains("Fixed bar", entries[2].Content);
        }

        [Test]
        public void LocatePath_ReturnsNull_WhenNotFound()
        {
            // This just verifies it doesn't throw; in test environment it may or may not find a file
            Assert.DoesNotThrow(() => ChangelogReader.LocatePath());
        }
    }
}
