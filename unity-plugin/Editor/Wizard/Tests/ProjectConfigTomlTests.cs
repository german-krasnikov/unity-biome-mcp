using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProjectConfigTomlTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void BuildFresh_ContainsSectionHeader()
        {
            var result = ProjectConfigToml.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3");
            StringAssert.Contains("[mcp_servers.unity-biome-mcp]", result);
        }

        [Test]
        public void BuildFresh_ContainsMarkerComment()
        {
            var result = ProjectConfigToml.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3");
            StringAssert.Contains("# unity-biome-mcp generated v", result);
        }

        [Test]
        public void BuildFresh_EnvTableHasPort()
        {
            var result = ProjectConfigToml.BuildFresh(9501, WizardConfigWriter.GitInstallUrl, "1.2.3");
            StringAssert.Contains("[mcp_servers.unity-biome-mcp.env]", result);
            StringAssert.Contains("UNITY_MCP_PORT = '9501'", result);
        }

        [Test]
        public void Merge_ReplacesStaleSection_PreservesOtherTables()
        {
            // Also covers migration: existing entry is the OLD "unity-mcp" section;
            // Merge must rewrite it to the new "unity-biome-mcp" name, never leave
            // the old key behind as a duplicate.
            var existing =
                "[some_other_tool]\n" +
                "key = 'value'\n" +
                "\n" +
                "# unity-mcp generated v0.1.0\n" +
                "[mcp_servers.unity-mcp]\n" +
                "command = 'uvx'\n" +
                "args = ['--from', 'old-url', 'unity-mcp']\n" +
                "\n" +
                "[mcp_servers.unity-mcp.env]\n" +
                "UNITY_MCP_PORT = '9000'\n";

            var result = ProjectConfigToml.Merge(existing, 9502, WizardConfigWriter.GitInstallUrl, "2.0.0");

            StringAssert.Contains("[some_other_tool]", result);
            StringAssert.Contains("key = 'value'", result);
            StringAssert.Contains("9502", result);
            StringAssert.Contains("# unity-biome-mcp generated v2.0.0", result);
            StringAssert.Contains("[mcp_servers.unity-biome-mcp]", result);
            StringAssert.DoesNotContain("[mcp_servers.unity-mcp]", result, "old key must be migrated, not duplicated");
            StringAssert.DoesNotContain("9000", result);
        }

        [Test]
        public void Merge_MultipleDottedSubsections_ReplacesAllOnRewrite()
        {
            // Regression: SectionRe previously hardcoded exactly one optional ".env"
            // subsection. A second dotted subsection (e.g. a hypothetical future
            // ".extra" table) would survive re-merge as orphaned stale content — this
            // mirrors Python's _UNITY_MCP_SECTION_RE, which matches any number of
            // dotted subsections.
            var existing =
                "# unity-mcp generated v0.1.0\n" +
                "[mcp_servers.unity-mcp]\n" +
                "command = 'uvx'\n" +
                "\n" +
                "[mcp_servers.unity-mcp.env]\n" +
                "UNITY_MCP_PORT = '9000'\n" +
                "\n" +
                "[mcp_servers.unity-mcp.extra]\n" +
                "some_key = 'stale'\n";

            var result = ProjectConfigToml.Merge(existing, 9502, WizardConfigWriter.GitInstallUrl, "2.0.0");

            StringAssert.DoesNotContain("some_key", result);
            StringAssert.DoesNotContain("[mcp_servers.unity-mcp.extra]", result);
            StringAssert.Contains("9502", result);
        }

        [Test]
        public void ExtractMarkerVersion_CommentPresent_ReturnsVersion()
        {
            var fresh = ProjectConfigToml.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3");
            Assert.AreEqual("1.2.3", ProjectConfigToml.ExtractMarkerVersion(fresh));
        }

        [Test]
        public void ExtractMarkerVersion_CommentAbsent_ReturnsNull()
        {
            // Hand-written Codex config — section present, no marker comment above it.
            var existing = "[mcp_servers.unity-mcp]\ncommand = 'uvx'\n";
            Assert.IsNull(ProjectConfigToml.ExtractMarkerVersion(existing));
        }

        [Test]
        public void Classify_MatchingMarkerAndPort_ReturnsOwnedCurrent()
        {
            var fresh = ProjectConfigToml.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3");
            var result = ProjectConfigToml.Classify(fresh, 9500, "1.2.3");
            Assert.AreEqual(EntryState.OwnedCurrent, result);
        }

        [Test]
        public void Classify_PinnedToml_ReturnsOwnedCurrent()
        {
            // ARC-0b Task 1: " pinned" suffix must win over both a stale version AND a
            // stale port — proves the pin check overrides the whole staleness condition,
            // not just the version half of it.
            var existing =
                "# unity-biome-mcp generated v1.49.0 pinned\n" +
                "[mcp_servers.unity-biome-mcp]\n" +
                "command = 'uvx'\n" +
                "\n" +
                "[mcp_servers.unity-biome-mcp.env]\n" +
                "UNITY_MCP_PORT = '9500'\n";

            var result = ProjectConfigToml.Classify(existing, 9600, "1.50.0");

            Assert.AreEqual(EntryState.OwnedCurrent, result);
        }

        [Test]
        public void Classify_NoMarkerComment_ReturnsForeign()
        {
            var existing = "[mcp_servers.unity-mcp]\ncommand = 'uvx'\n";
            var result = ProjectConfigToml.Classify(existing, 9500, "1.2.3");
            Assert.AreEqual(EntryState.Foreign, result);
        }

        // C1 r3 #6: marker/pin regex must accept a semver pre-release tag (e.g. an
        // RC build) -- before the fix, ExtractMarkerVersion never matched through
        // to the section header because "-rc.1" broke the required immediate
        // "\n[" continuation, so Classify permanently returned Foreign.
        [Test]
        public void Classify_PrereleaseVersionMarker_ReturnsOwnedCurrent()
        {
            var fresh = ProjectConfigToml.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.51.0-rc.1");
            var result = ProjectConfigToml.Classify(fresh, 9500, "1.51.0-rc.1");
            Assert.AreEqual(EntryState.OwnedCurrent, result);
        }

        // Reproduces the reported repro exactly: WriteOne only calls Adopt() when
        // Classify() reports Foreign. Under the old regex, an rc-tagged marker never
        // classified as OwnedCurrent, so every Editor restart re-adopted and stacked
        // another marker comment line. This mirrors WriteOne's own guard (without
        // touching ProjectConfigWriter.cs) across three simulated restarts.
        [Test]
        public void AdoptTwice_PrereleaseVersion_DoesNotDuplicateMarker()
        {
            const int port = 9500;
            const string version = "1.51.0-rc.1";
            var text = "[mcp_servers.unity-biome-mcp]\n" +
                       "command = 'uvx'\n" +
                       "\n" +
                       "[mcp_servers.unity-biome-mcp.env]\n" +
                       $"UNITY_MCP_PORT = '{port}'\n";

            string SimulateEditorRestart(string existing) =>
                ProjectConfigToml.Classify(existing, port, version) == EntryState.Foreign
                    ? ProjectConfigToml.Adopt(existing, version)
                    : existing;

            var afterRestart1 = SimulateEditorRestart(text);
            var afterRestart2 = SimulateEditorRestart(afterRestart1);
            var afterRestart3 = SimulateEditorRestart(afterRestart2);

            Assert.AreEqual(afterRestart1, afterRestart2);
            Assert.AreEqual(afterRestart1, afterRestart3);
            Assert.AreEqual(1, Regex.Matches(afterRestart3, "generated v").Count,
                "each Editor restart on an rc-tagged install must not re-Adopt and duplicate the marker");
        }

        [Test]
        public void Adopt_AddsCommentMarker()
        {
            var text = "[mcp_servers.unity-biome-mcp]\ncommand = 'uvx'\n";
            var result = ProjectConfigToml.Adopt(text, "1.2.3");
            Assert.AreEqual("1.2.3", ProjectConfigToml.ExtractMarkerVersion(result));
        }

        [Test]
        public void ClassifyAfterAdopt_ReturnsOwnedCurrent()
        {
            var text = "[mcp_servers.unity-biome-mcp]\ncommand = 'uvx'\n\n"
                + "[mcp_servers.unity-biome-mcp.env]\nUNITY_MCP_PORT = '9500'\n";
            var adopted = ProjectConfigToml.Adopt(text, "1.2.3");
            Assert.AreEqual(EntryState.OwnedCurrent, ProjectConfigToml.Classify(adopted, 9500, "1.2.3"));
        }

        [Test]
        public void Adopt_LegacyUnityMcpSection_AddsCommentMarker()
        {
            var text = "[mcp_servers.unity-mcp]\ncommand = 'uvx'\n";
            var result = ProjectConfigToml.Adopt(text, "2.0.0");
            Assert.AreEqual("2.0.0", ProjectConfigToml.ExtractMarkerVersion(result));
        }

        // ── ARC-11 T1: Pin() surgical marker insert (TOML mirror) ───────────

        [Test]
        public void Pin_TomlEntry_InsertsPinnedSuffixOnCommentLine()
        {
            var fresh = ProjectConfigToml.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3");

            var result = ProjectConfigToml.Pin(fresh);

            // Arm B forbidden: `result != null` would pass against a stub that
            // returns the input unchanged.
            StringAssert.Contains("# unity-biome-mcp generated v1.2.3 pinned", result);
        }

        [Test]
        public void Pin_ThenClassify_Toml_ReturnsOwnedCurrent()
        {
            var fresh = ProjectConfigToml.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3");

            var pinned = ProjectConfigToml.Pin(fresh);
            var result = ProjectConfigToml.Classify(pinned, 9600, "1.50.0");

            // Arm B forbidden: `AreNotEqual(Absent, ...)` would also pass for
            // OwnedStale.
            Assert.AreEqual(EntryState.OwnedCurrent, result);
        }

        [Test]
        public void Pin_TomlNoSectionFound_ReturnsOriginalUnchanged()
        {
            var text = "[some_other_tool]\nkey = 'value'\n";
            var result = ProjectConfigToml.Pin(text);
            Assert.IsTrue(ReferenceEquals(result, text));
        }

        [Test]
        public void Pin_AlreadyPinnedToml_IsIdempotent_NoDuplicateSuffix()
        {
            var fresh = ProjectConfigToml.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3");

            var pinnedOnce = ProjectConfigToml.Pin(fresh);
            var pinnedTwice = ProjectConfigToml.Pin(pinnedOnce);

            Assert.AreEqual(1, Regex.Matches(pinnedTwice, "pinned").Count,
                "a repeated Pin() must never duplicate the ' pinned' suffix");
            Assert.AreEqual(pinnedOnce, pinnedTwice);
        }

        [Test]
        public void Pin_TomlPreservesOtherTablesByteForByte()
        {
            var existing =
                "[some_other_tool]\n" +
                "key = 'value'\n" +
                "\n" +
                "# unity-biome-mcp generated v1.49.0\n" +
                "[mcp_servers.unity-biome-mcp]\n" +
                "command = 'uvx'\n" +
                "\n" +
                "[mcp_servers.unity-biome-mcp.env]\n" +
                "UNITY_MCP_PORT = '9500'\n";

            var result = ProjectConfigToml.Pin(existing);

            StringAssert.Contains("[some_other_tool]\nkey = 'value'\n", result);
            StringAssert.Contains("UNITY_MCP_PORT = '9500'", result);
        }
    }
}
