using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProjectConfigFormatsTests
    {
        [Test]
        public void BuildEntry_ContainsMarkerVersion()
        {
            var result = ProjectConfigFormats.BuildEntry(9500, WizardConfigWriter.GitInstallUrl, "1.2.3");
            StringAssert.Contains("\"_v\": \"1.2.3\"", result);
        }

        [Test]
        public void BuildEntry_DoesNotContainPort()
        {
            var result = ProjectConfigFormats.BuildEntry(9501, WizardConfigWriter.GitInstallUrl, "1.2.3");
            StringAssert.DoesNotContain("UNITY_MCP_PORT", result);
            StringAssert.DoesNotContain("9501", result);
        }

        [Test]
        public void BuildFresh_WrapsEntryUnderGivenRootKey_Servers()
        {
            var result = ProjectConfigFormats.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3", "servers");
            StringAssert.Contains("\"servers\"", result);
            StringAssert.DoesNotContain("\"mcpServers\"", result);
        }

        [Test]
        public void BuildFresh_WrapsEntryUnderGivenRootKey_McpServers()
        {
            var result = ProjectConfigFormats.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3", "mcpServers");
            StringAssert.Contains("\"mcpServers\"", result);
        }

        [Test]
        public void ExtractMarkerVersion_NoUnityMcpKey_ReturnsNull()
        {
            Assert.IsNull(ProjectConfigFormats.ExtractMarkerVersion("{\"mcpServers\":{}}"));
        }

        [Test]
        public void ExtractMarkerVersion_EntryWithoutMarker_ReturnsNull()
        {
            // Simulates a hand-written pre-Phase1A entry — no "_v" key.
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"uvx\"}}}";
            Assert.IsNull(ProjectConfigFormats.ExtractMarkerVersion(existing));
        }

        [Test]
        public void ExtractMarkerVersion_EntryWithMarker_ReturnsVersion()
        {
            var entry = ProjectConfigFormats.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3", "mcpServers");
            Assert.AreEqual("1.2.3", ProjectConfigFormats.ExtractMarkerVersion(entry));
        }

        [Test]
        public void ExtractMarkerPort_NoPortInEntry_ReturnsNull()
        {
            // Port is no longer written to JSON entries — discovery uses .port files.
            var entry = ProjectConfigFormats.BuildFresh(9501, WizardConfigWriter.GitInstallUrl, "1.2.3", "mcpServers");
            Assert.IsNull(ProjectConfigFormats.ExtractMarkerPort(entry));
        }

        [Test]
        public void Classify_NoUnityMcpEntry_ReturnsAbsent()
        {
            var result = ProjectConfigFormats.Classify("{\"mcpServers\":{}}", 9500, "1.2.3");
            Assert.AreEqual(EntryState.Absent, result);
        }

        [Test]
        public void Classify_EntryWithoutMarker_ReturnsForeign()
        {
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"uvx\"}}}";
            var result = ProjectConfigFormats.Classify(existing, 9500, "1.2.3");
            Assert.AreEqual(EntryState.Foreign, result);
        }

        [Test]
        public void Classify_MarkerMatchesCurrentVersion_ReturnsOwnedCurrent()
        {
            var fresh = ProjectConfigFormats.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3", "mcpServers");
            var result = ProjectConfigFormats.Classify(fresh, 9500, "1.2.3");
            Assert.AreEqual(EntryState.OwnedCurrent, result);
        }

        [Test]
        public void Classify_MarkerVersionDiffers_ReturnsOwnedStale()
        {
            var fresh = ProjectConfigFormats.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3", "mcpServers");
            var result = ProjectConfigFormats.Classify(fresh, 9500, "1.3.0");
            Assert.AreEqual(EntryState.OwnedStale, result);
        }

        [Test]
        public void Classify_MarkerPortDiffers_ReturnsOwnedCurrent()
        {
            // Port is no longer written to JSON entries — port difference is not a staleness signal.
            var fresh = ProjectConfigFormats.BuildFresh(9500, WizardConfigWriter.GitInstallUrl, "1.2.3", "mcpServers");
            var result = ProjectConfigFormats.Classify(fresh, 9501, "1.2.3");
            Assert.AreEqual(EntryState.OwnedCurrent, result);
        }

        [Test]
        public void Classify_SiblingEntryHasMarker_ForeignUnityMcpEntry_ReturnsForeign()
        {
            // Regression: a sibling MCP server's own "_v" key must never leak into
            // unity-mcp's classification. unity-mcp itself here is hand-edited (Foreign,
            // no "_v" of its own) — before the fix, MarkerVersionRe matched blender-mcp's
            // "_v" file-wide and misclassified this as OwnedStale, causing Merge to
            // overwrite the user's custom unity-mcp entry (data loss).
            var existing = "{\"mcpServers\":{"
                + "\"blender-mcp\":{\"command\":\"uvx\",\"args\":[\"blender-mcp\"],\"_v\":\"3.0.0\"},"
                + "\"unity-mcp\":{\"command\":\"custom-launcher\",\"args\":[\"--special\"]}"
                + "}}";

            Assert.AreEqual(EntryState.Foreign, ProjectConfigFormats.Classify(existing, 9500, "1.2.3"));
            Assert.IsNull(ProjectConfigFormats.ExtractMarkerVersion(existing));
        }

        [Test]
        public void Merge_ReplacesOnlyUnityMcpValue_PreservesSiblingServerEntries()
        {
            var existing = "{\"mcpServers\":{\"other-tool\":{\"command\":\"x\"},"
                + "\"unity-mcp\":{\"command\":\"uvx\",\"_v\":\"0.1.0\"}}}";
            var result = ProjectConfigFormats.Merge(existing, 9502, WizardConfigWriter.GitInstallUrl, "2.0.0", "mcpServers");
            StringAssert.Contains("other-tool", result);
            StringAssert.Contains("\"_v\": \"2.0.0\"", result);
        }
    }
}
