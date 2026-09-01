using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProjectConfigFormatsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
        public void Classify_PinnedJson_ReturnsOwnedCurrent()
        {
            // ARC-0b Task 1: "_pin": true must win over a stale "_v" — the P7 fix.
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"_v\": \"1.49.0\","
                + "\"_pin\": true"
                + "}}}";

            var result = ProjectConfigFormats.Classify(existing, 9500, "1.50.0");

            Assert.AreEqual(EntryState.OwnedCurrent, result);
        }

        [Test]
        public void Classify_UnpinnedJson_StaleVersion_ReturnsOwnedStale()
        {
            // Existing behavior preserved: no "_pin" key, stale "_v" still stales.
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"_v\": \"1.49.0\""
                + "}}}";

            var result = ProjectConfigFormats.Classify(existing, 9500, "1.50.0");

            Assert.AreEqual(EntryState.OwnedStale, result);
        }

        [Test]
        public void IsPinned_SiblingServerHasPin_OurEntryDoesNot_ReturnsFalse()
        {
            // A sibling MCP server's own "_pin" must never leak into our classification —
            // same scoping guarantee ExtractMarkerVersion already has (FindOurEntry).
            var existing = "{\"mcpServers\":{"
                + "\"other-mcp\":{\"command\":\"uvx\",\"_v\":\"3.0.0\",\"_pin\": true},"
                + "\"unity-biome-mcp\":{\"command\":\"uvx\",\"_v\":\"1.49.0\"}"
                + "}}";

            Assert.IsFalse(ProjectConfigFormats.IsPinned(existing));
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

        [Test]
        public void Merge_PreservesUnknownEntryKeys_OnVersionBump()
        {
            // ARC-13 T1: a version bump (OwnedStale -> Merge) must point-splice only
            // command/args/_v, never whole-replace the entry — an unrelated "env" key
            // a user hand-added (e.g. UNITY_MCP_NO_GATING) must survive.
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"args\": [\"--from\", \"OLD_URL_MARKER\", \"unity-biome-mcp\"],"
                + "\"_v\": \"0.1.0\","
                + "\"env\": {\"UNITY_MCP_NO_GATING\": \"1\"}"
                + "}}}";

            var result = ProjectConfigFormats.Merge(existing, 9500, "NEW_URL_MARKER", "2.0.0", "mcpServers");

            StringAssert.Contains("UNITY_MCP_NO_GATING", result, "unknown env key must survive a version bump");
            StringAssert.Contains("\"_v\": \"2.0.0\"", result, "version marker must actually update");
            StringAssert.Contains("NEW_URL_MARKER", result, "args value must actually be replaced");
            StringAssert.DoesNotContain("OLD_URL_MARKER", result, "old args value must not survive");
        }

        [Test]
        public void Merge_NestedBracesInUnknownValue_DoesNotBreakFieldSearch()
        {
            // A user-added "env" key with its own nested object AND array must not
            // confuse the outer entry-bounds brace-counting or the "args" field's own
            // bracket-depth matching during point-splice.
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"args\": [\"--from\", \"OLD_URL\", \"unity-biome-mcp\"],"
                + "\"_v\": \"0.1.0\","
                + "\"env\": {\"FLAGS\": [\"a\", \"b\"], \"NESTED\": {\"deep\": \"1\"}}"
                + "}}}";

            var result = ProjectConfigFormats.Merge(existing, 9500, "NEW_URL", "2.0.0", "mcpServers");

            StringAssert.Contains("\"NESTED\": {\"deep\": \"1\"}", result, "deeply nested unknown value must survive intact");
            StringAssert.Contains("NEW_URL", result);
            StringAssert.DoesNotContain("OLD_URL", result);
        }

        [Test]
        public void Merge_OldEntryMissingArgsKey_InsertsArgsWithoutDroppingOtherKeys()
        {
            // ARC-13 T2: a field present in the fresh template but absent from the
            // old entry (e.g. a very old hand-edited entry with no "args") must be
            // inserted, not silently left missing forever — and inserting it must
            // not disturb an unrelated key. Two-part assertion is deliberate:
            // loosening to only check "env" would pass even with "args" missing.
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\":\"uvx\","
                + "\"_v\":\"0.1.0\","
                + "\"env\":{\"X\":\"1\"}"
                + "}}}";

            var result = ProjectConfigFormats.Merge(existing, 9500, WizardConfigWriter.GitInstallUrl, "2.0.0", "mcpServers");

            StringAssert.Contains("\"args\": [", result, "args must be inserted, not left missing");
            StringAssert.Contains("\"X\":\"1\"", result, "unrelated env key must survive the insert");
            AssertBalancedJson(result);
        }

        [Test]
        public void Merge_UserNestedObjectHasSameFieldNames_TopLevelFieldsUpdatedEnvUntouched()
        {
            // ARC-13 T2 review: FindFieldSegment must only match "command"/"args" at
            // depth 1 (a direct child of our own entry). Without a depth guard,
            // Regex.Match takes the FIRST textual occurrence — here that's inside a
            // user's nested "env" object using the same key names — and the splice
            // corrupts user data instead of touching our own top-level fields.
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"env\":{\"args\":\"x\",\"command\":\"y\"},"
                + "\"command\":\"uvx\","
                + "\"args\":[\"--from\",\"OLD_URL\",\"unity-biome-mcp\"]"
                + "}}}";

            var result = ProjectConfigFormats.Merge(existing, 9500, "NEW_URL", "2.0.0", "mcpServers");

            StringAssert.Contains("\"env\":{\"args\":\"x\",\"command\":\"y\"}", result,
                "nested user object with colliding field names must survive byte-identical");
            StringAssert.Contains("NEW_URL", result, "top-level args must still be updated");
            StringAssert.DoesNotContain("OLD_URL", result, "top-level args old value must not survive");
            AssertBalancedJson(result);
        }

        // Lightweight structural-validity check (no JSON parser in this codebase by
        // design, ARC-13 §2) — proves point-splice/insert never leaves an unbalanced
        // brace/bracket behind.
        private static void AssertBalancedJson(string json)
        {
            int braces = 0, brackets = 0;
            foreach (var c in json)
            {
                if (c == '{') braces++;
                else if (c == '}') braces--;
                else if (c == '[') brackets++;
                else if (c == ']') brackets--;
                Assert.GreaterOrEqual(braces, 0, "unbalanced '}' in: " + json);
                Assert.GreaterOrEqual(brackets, 0, "unbalanced ']' in: " + json);
            }
            Assert.AreEqual(0, braces, "unbalanced braces in: " + json);
            Assert.AreEqual(0, brackets, "unbalanced brackets in: " + json);
        }

        [Test]
        public void Adopt_NoEntry_ReturnsOriginalText()
        {
            var text = "{\"mcpServers\":{}}";
            var result = ProjectConfigFormats.Adopt(text, "1.2.3");
            Assert.IsTrue(ReferenceEquals(result, text));
        }

        [Test]
        public void Adopt_AddsMissingMarker_LeavesOtherContentIntact()
        {
            var text = "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"uvx\","
                + "\"env\":{\"UNITY_MCP_NO_GATING\":\"1\"}}}}";
            var result = ProjectConfigFormats.Adopt(text, "1.2.3");
            StringAssert.Contains("\"_v\": \"1.2.3\"", result);
            StringAssert.Contains("UNITY_MCP_NO_GATING", result);
        }

        [Test]
        public void ClassifyAfterAdopt_ReturnsOwnedCurrent()
        {
            var text = "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"uvx\"}}}";
            var adopted = ProjectConfigFormats.Adopt(text, "1.2.3");
            Assert.AreEqual(EntryState.OwnedCurrent, ProjectConfigFormats.Classify(adopted, 9500, "1.2.3"));
        }

        // ── ARC-11 T1: Pin() surgical marker insert ─────────────────────────

        [Test]
        public void Pin_OwnedStaleEntry_InsertsPinMarker_PreservesVersion()
        {
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"_v\": \"1.49.0\""
                + "}}}";

            var result = ProjectConfigFormats.Pin(existing);

            // Arm B forbidden: `result != null` would pass against a stub that
            // returns the input unchanged — assert the exact inserted substring
            // AND that the pre-existing version marker survives untouched.
            StringAssert.Contains("\"_pin\": true", result);
            StringAssert.Contains("\"_v\": \"1.49.0\"", result);
        }

        [Test]
        public void Pin_ThenClassify_ReturnsOwnedCurrent_RegardlessOfVersionMismatch()
        {
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"_v\": \"1.49.0\""
                + "}}}";

            var pinned = ProjectConfigFormats.Pin(existing);
            var result = ProjectConfigFormats.Classify(pinned, 9500, "1.50.0");

            // Arm B forbidden: `AreNotEqual(Absent, ...)` would also pass for
            // OwnedStale — the composition claim requires the exact value.
            Assert.AreEqual(EntryState.OwnedCurrent, result);
        }

        [Test]
        public void Pin_NoOurEntryFound_ReturnsOriginalUnchanged()
        {
            var text = "{\"mcpServers\":{}}";
            var result = ProjectConfigFormats.Pin(text);
            Assert.IsTrue(ReferenceEquals(result, text));
        }

        [Test]
        public void Pin_AlreadyPinned_IsIdempotent_NoDuplicateMarker()
        {
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"_v\": \"1.49.0\""
                + "}}}";

            var pinnedOnce = ProjectConfigFormats.Pin(existing);
            var pinnedTwice = ProjectConfigFormats.Pin(pinnedOnce);

            Assert.AreEqual(1, Regex.Matches(pinnedTwice, "\"_pin\"").Count,
                "a repeated Pin() must never duplicate the marker");
            Assert.AreEqual(pinnedOnce, pinnedTwice);
        }

        [Test]
        public void Pin_PreservesEnvAndSiblingKeys_ByteForByte()
        {
            var existing = "{\"mcpServers\":{"
                + "\"other-tool\":{\"command\":\"x\"},"
                + "\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"_v\": \"1.49.0\","
                + "\"env\": {\"UNITY_MCP_NO_GATING\": \"1\"}"
                + "}}}";

            var result = ProjectConfigFormats.Pin(existing);

            StringAssert.Contains("\"other-tool\":{\"command\":\"x\"}", result);
            StringAssert.Contains("\"env\": {\"UNITY_MCP_NO_GATING\": \"1\"}", result);
        }
    }
}
