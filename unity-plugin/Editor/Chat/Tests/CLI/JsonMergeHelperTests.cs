// TDD tests for JsonMergeHelper.
// Pure unit tests — no FS, no Unity API.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class JsonMergeHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── ReplaceEntry: key not found ───────────────────────────────────────

        [Test]
        public void ReplaceEntry_KeyNotFound_ReturnsNull()
        {
            var json = "{\"other\":{\"a\":1}}";
            var result = JsonMergeHelper.ReplaceEntry(json, "missing", "{\"x\":2}");
            Assert.IsNull(result, "Must return null when key is absent");
        }

        [Test]
        public void ReplaceEntry_EmptyJson_ReturnsNull()
        {
            Assert.IsNull(JsonMergeHelper.ReplaceEntry("", "key", "{}"));
        }

        [Test]
        public void ReplaceEntry_NullJson_ReturnsNull()
        {
            Assert.IsNull(JsonMergeHelper.ReplaceEntry(null, "key", "{}"));
        }

        // ── ReplaceEntry: basic replacement ──────────────────────────────────

        [Test]
        public void ReplaceEntry_ReplacesValue_PreservesKey()
        {
            var json = "{\"unity-biome-mcp\":{\"command\":\"python3\",\"port\":9501}}";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-biome-mcp", "{\"command\":\"python3\",\"port\":9900}");
            StringAssert.Contains("\"unity-biome-mcp\"", result);
            StringAssert.Contains("9900", result);
            StringAssert.DoesNotContain("9501", result);
        }

        [Test]
        public void ReplaceEntry_PreservesOtherEntries()
        {
            var json = "{\"blender\":{\"cmd\":\"blender-mcp\"},\"unity-biome-mcp\":{\"port\":9501}}";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-biome-mcp", "{\"port\":9900}");
            StringAssert.Contains("\"blender\"", result);
            StringAssert.Contains("blender-mcp", result);
            StringAssert.Contains("9900", result);
            StringAssert.DoesNotContain("9501", result);
        }

        // ── ReplaceEntry: brace balance ───────────────────────────────────────

        [Test]
        public void ReplaceEntry_OutputBracesAreBalanced()
        {
            var json = "{\"unity-biome-mcp\":{\"command\":\"python3\",\"env\":{\"UNITY_MCP_PORT\":\"9501\"}}}";
            var fresh = "{\"command\":\"python3\",\"env\":{\"UNITY_MCP_PORT\":\"9900\"}}";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-biome-mcp", fresh);
            Assert.AreEqual(result.Count(c => c == '{'), result.Count(c => c == '}'),
                "Output braces must be balanced");
        }

        [Test]
        public void ReplaceEntry_NestedBracesInValue_HandledCorrectly()
        {
            // Value contains nested braces — depth matching must not stop early
            var json = "{\"unity-biome-mcp\":{\"env\":{\"KEY\":\"old\"},\"args\":[]}}";
            var fresh = "{\"env\":{\"KEY\":\"new\"},\"args\":[]}";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-biome-mcp", fresh);
            StringAssert.Contains("\"new\"", result);
            StringAssert.DoesNotContain("\"old\"", result);
            Assert.AreEqual(result.Count(c => c == '{'), result.Count(c => c == '}'),
                "Brace count must be balanced with nested value braces");
        }

        [Test]
        public void ReplaceEntry_MultipleOtherEntries_AllPreserved()
        {
            var json =
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"blender\": { \"command\": \"blender-mcp\" },\n" +
                "    \"unity-biome-mcp\": { \"command\": \"python3\", \"env\": { \"UNITY_MCP_PORT\": \"9501\" } },\n" +
                "    \"figma\": { \"command\": \"figma-mcp\" }\n" +
                "  }\n" +
                "}\n";
            var fresh = "{ \"command\": \"python3\", \"env\": { \"UNITY_MCP_PORT\": \"9900\" } }";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-biome-mcp", fresh);

            StringAssert.Contains("blender-mcp", result);
            StringAssert.Contains("figma-mcp", result);
            StringAssert.Contains("9900", result);
            StringAssert.DoesNotContain("9501", result);
            Assert.AreEqual(result.Count(c => c == '{'), result.Count(c => c == '}'),
                "Brace count must be balanced");
        }

        // ── FindBlockClose ────────────────────────────────────────────────────

        [Test]
        public void FindBlockClose_FindsClosingBrace()
        {
            var json = "{ \"mcp\": { \"a\": 1 } }";
            var idx = JsonMergeHelper.FindBlockClose(json, "mcp");
            Assert.Greater(idx, -1);
            Assert.AreEqual('}', json[idx]);
        }

        [Test]
        public void FindBlockClose_MissingKey_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, JsonMergeHelper.FindBlockClose("{\"x\":{}}", "missing"));
        }

        [Test]
        public void FindBlockClose_NullOrEmpty_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, JsonMergeHelper.FindBlockClose(null, "k"));
            Assert.AreEqual(-1, JsonMergeHelper.FindBlockClose("", "k"));
        }

        // ── ExtractObjectEntries ──────────────────────────────────────────────

        [Test]
        public void ExtractObjectEntries_ObjectValue_Included()
        {
            var block = "\n  \"blender\": { \"cmd\": \"blender-mcp\" }\n";
            var result = JsonMergeHelper.ExtractObjectEntries(block, _ => true);
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("\"blender\"", result[0]);
            StringAssert.Contains("blender-mcp", result[0]);
        }

        [Test]
        public void ExtractObjectEntries_ArrayValue_Skipped()
        {
            // Array-valued entry must be silently skipped without corrupting parser position
            var block = "\n  \"settings\": [1, 2, 3],\n  \"blender\": { \"cmd\": \"blender-mcp\" }\n";
            var result = JsonMergeHelper.ExtractObjectEntries(block, _ => true);
            Assert.AreEqual(1, result.Count, "settings array must be skipped");
            StringAssert.Contains("\"blender\"", result[0]);
        }

        [Test]
        public void ExtractObjectEntries_FilterExcludesKey_EntryAbsent()
        {
            var block = "\"unity-biome-mcp\": { \"cmd\": \"python3\" }, \"blender\": { \"cmd\": \"blender-mcp\" }";
            var result = JsonMergeHelper.ExtractObjectEntries(block,
                key => !key.Equals("unity-biome-mcp", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("\"blender\"", result[0]);
            Assert.IsFalse(result.Exists(e => e.Contains("unity-biome-mcp")));
        }

        [Test]
        public void ExtractObjectEntries_EmptyBlock_ReturnsEmpty()
        {
            var result = JsonMergeHelper.ExtractObjectEntries("", _ => true);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ExtractObjectEntries_StringValueWithBrace_DoesNotDropSubsequentEntry()
        {
            // String value contains '}' — must not fool the parser into stopping early
            var block = "\"meta\": \"has } inside\", \"blender\": { \"cmd\": \"b\" }";
            var result = JsonMergeHelper.ExtractObjectEntries(block, _ => true);
            Assert.AreEqual(1, result.Count, "blender must survive after string-with-brace entry");
            StringAssert.Contains("\"blender\"", result[0]);
        }

        // ── InjectBeforeBlockClose ────────────────────────────────────────────

        [Test]
        public void InjectBeforeBlockClose_InjectsEntry()
        {
            var json = "{\n  \"mcp\": {\n    \"unity-biome-mcp\": {}\n  }\n}\n";
            var extra = new List<string> { "\"blender\": { \"cmd\": \"b\" }" };
            var result = JsonMergeHelper.InjectBeforeBlockClose(json, "mcp", extra);
            StringAssert.Contains("\"blender\"", result);
            StringAssert.Contains("unity-biome-mcp", result);
            Assert.AreEqual(result.Count(c => c == '{'), result.Count(c => c == '}'));
        }

        [Test]
        public void InjectBeforeBlockClose_EmptyExtra_ReturnsUnchanged()
        {
            var json = "{ \"mcp\": {} }";
            var result = JsonMergeHelper.InjectBeforeBlockClose(json, "mcp", new List<string>());
            Assert.AreEqual(json, result);
        }

        [Test]
        public void InjectBeforeBlockClose_MissingKey_ReturnsUnchanged()
        {
            var json = "{ \"other\": {} }";
            var extra = new List<string> { "\"x\": {}" };
            var result = JsonMergeHelper.InjectBeforeBlockClose(json, "mcp", extra);
            Assert.AreEqual(json, result);
        }
    }
}
