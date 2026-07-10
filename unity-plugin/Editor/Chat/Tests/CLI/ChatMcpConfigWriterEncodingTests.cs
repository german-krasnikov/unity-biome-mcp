// Byte-level encoding tests for ChatMcpConfigWriter.
// Verifies that the production write chain (BuildClaudeConfigJson + File.WriteAllText(Utf8NoBom))
// produces no BOM — BOM in JSON causes Node.js JSON.parse to fail.
// Discriminating: swap JsonHelper.Utf8NoBom → Encoding.UTF8 → bytes[0] becomes 0xEF → FAIL.
// GetOrCreateConfigPath() is not exercised directly: it depends on Packages/com.unity-mcp.editor
// (UPM path, not available in EditMode isolation). We exercise the same 2 prod lines (L109-111).
using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatMcpConfigWriterEncodingTests
    {
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"unity-mcp-chatenc-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tmpDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tmpDir, true); } catch { }
        }

        [Test]
        public void WriteChain_NoBomOnDisk()
        {
            // Mirror exact production chain from GetOrCreateConfigPath lines 109-111
            var json       = ChatMcpConfigWriter.BuildClaudeConfigJson("python3", new[] { "-m", "unity_mcp.server" });
            var configPath = Path.Combine(_tmpDir, "unity-mcp-config.json");
            File.WriteAllText(configPath, json, JsonHelper.Utf8NoBom);  // ← same call as production

            var bytes = File.ReadAllBytes(configPath);
            Assert.IsTrue(bytes.Length >= 1, "File must not be empty");
            Assert.AreNotEqual(0xEF, bytes[0], "BOM detected — would break Node JSON.parse");
            Assert.AreEqual((byte)'{', bytes[0], "JSON must start with '{', not BOM");
        }

        [Test]
        public void WriteChain_BytesMatchNoBomEncoding()
        {
            // Byte-level: on-disk bytes must equal UTF8NoBom.GetBytes(json), not UTF8(BOM).GetBytes
            var json       = ChatMcpConfigWriter.BuildClaudeConfigJson("uv", new[] { "run", "unity-mcp" });
            var configPath = Path.Combine(_tmpDir, "config-bytes.json");
            File.WriteAllText(configPath, json, JsonHelper.Utf8NoBom);

            var onDisk   = File.ReadAllBytes(configPath);
            var expected = new UTF8Encoding(false).GetBytes(json);  // no BOM reference
            CollectionAssert.AreEqual(expected, onDisk, "On-disk bytes must match UTF-8 no-BOM encoding");
        }

        [Test]
        public void BuildClaudeConfigJson_Structure()
        {
            var json = ChatMcpConfigWriter.BuildClaudeConfigJson("uv", new[] { "run", "unity-mcp" });
            Assert.IsTrue(json.StartsWith("{"), "Config JSON must start with '{'");
            Assert.IsTrue(json.Contains("\"mcpServers\""), "Must contain mcpServers key");
            Assert.IsTrue(json.Contains("\"unity-mcp\""), "Must contain unity-mcp server key");
        }
    }
}
