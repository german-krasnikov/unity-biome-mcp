using NUnit.Framework;
using UnityMCP.Editor.Decisions;

namespace UnityMCP.Editor.Tests
{
    // UI-1/Q16: the one DECISION isNewer (dsl/decisions/versionorder.logic), tested directly instead of through
    // each of the three C# copies it replaces (UpdateChecker.IsNewer, ChangelogReader.IsNewerThan, ReleaseDiff.IsNewer).
    [TestFixture]
    public class VersionFactsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TestCase("1.52.0", "1.51.0", true)]
        [TestCase("1.51.0", "1.51.0", false)]
        [TestCase("1.50.0", "1.51.0", false)]
        [TestCase("not-a-version", "1.51.0", false)]
        public void IsNewer_StableVersions(string candidate, string current, bool expected)
        {
            Assert.AreEqual(expected, VersionFacts.IsNewer(candidate, current));
        }

        [Test]
        public void IsNewer_StableUserNeverSeesPreReleaseBanner()
        {
            // semver 2.0 §11 + Q16: even for a numerically newer triple. Old UpdateChecker/ChangelogReader/
            // ReleaseDiff all failed to parse "-rc.1" and returned false here too, but for the wrong reason.
            Assert.IsFalse(VersionFacts.IsNewer("1.52.0-rc.1", "1.51.0"));
        }

        [Test]
        public void IsNewer_PreReleaseUserSeesNewerStableRelease()
        {
            // closes EVAL3_UNITY_MCP_UI.md F4: old C# failed to parse "1.51.0-rc.1" as `current` and said false.
            Assert.IsTrue(VersionFacts.IsNewer("1.52.0", "1.51.0-rc.1"));
        }

        [Test]
        public void IsNewer_PreReleaseUserSeesReleaseOfTheirOwnLine()
        {
            Assert.IsTrue(VersionFacts.IsNewer("1.52.0", "1.52.0-rc.1"));
        }

        [Test]
        public void IsNewer_PreReleaseUserDoesNotSeeSiblingPreRelease()
        {
            Assert.IsFalse(VersionFacts.IsNewer("1.52.0-rc.2", "1.52.0-rc.1"));
        }
    }
}
