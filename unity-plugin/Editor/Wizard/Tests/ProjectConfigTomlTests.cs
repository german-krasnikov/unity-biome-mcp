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
        public void Classify_NoMarkerComment_ReturnsForeign()
        {
            var existing = "[mcp_servers.unity-mcp]\ncommand = 'uvx'\n";
            var result = ProjectConfigToml.Classify(existing, 9500, "1.2.3");
            Assert.AreEqual(EntryState.Foreign, result);
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
    }
}
