// Byte-level encoding tests for UTF-8 no-BOM correctness.
// EditMode only — no scene load needed.
using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class EncodingTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private TempDirScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new TempDirScope("unity-biome-mcp-enc-test");
        }

        [TearDown]
        public void TearDown()
        {
            _scope.Dispose();
        }

        private static readonly byte[] CyrUtf8 = Encoding.UTF8.GetBytes("Привет");

        // ── CS1: JsonHelper.Utf8NoBom writes without BOM ─────────────────────

        [Test]
        public void Utf8NoBom_NoUtf8Bom()
        {
            var path = Path.Combine(_scope.Path, "test.txt");
            File.WriteAllText(path, "// Тест", JsonHelper.Utf8NoBom);

            var bytes = File.ReadAllBytes(path);
            Assert.IsTrue(bytes.Length >= 1, "File must not be empty");
            Assert.AreNotEqual(0xEF, bytes[0], "File must not start with UTF-8 BOM (0xEF 0xBB 0xBF)");
        }

        [Test]
        public void Utf8NoBom_CyrillicRoundtrip()
        {
            var path = Path.Combine(_scope.Path, "roundtrip.txt");
            var cyr  = "Shader \"Custom/Тест\" { }";
            File.WriteAllText(path, cyr, JsonHelper.Utf8NoBom);

            var readBack = File.ReadAllText(path, Encoding.UTF8);
            Assert.AreEqual(cyr, readBack, "Cyrillic must survive UTF-8 no-BOM round-trip");
        }

        [Test]
        public void Utf8NoBom_CyrillicBytesAreUtf8()
        {
            var path = Path.Combine(_scope.Path, "bytes.txt");
            File.WriteAllText(path, "// Привет", JsonHelper.Utf8NoBom);

            var raw = File.ReadAllBytes(path);
            Assert.IsTrue(ContainsSequence(raw, CyrUtf8),
                "Cyrillic must be stored as UTF-8 bytes (not escaped)");
        }

        // ── CS1b: ShaderHelper.WriteShaderFile uses Utf8NoBom ────────────────

        [Test]
        public void ShaderHelper_WriteShaderFile_NoUtf8Bom()
        {
            var path = Path.Combine(_scope.Path, "Test.shader");
            ShaderHelper.WriteShaderFile(path, "// Привет шейдер");
            var bytes = File.ReadAllBytes(path);
            Assert.AreNotEqual(0xEF, bytes[0], "File must not start with UTF-8 BOM");
        }

        // CS3: ChatProcess._stdin NewLine="\n" — covered by integration test.

        private static bool ContainsSequence(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                if (match) return true;
            }
            return false;
        }
    }
}
