// TDD: PortResolver ReadOnly support tests.
// Covers ParseBoolFromJson and TrySaveProjectSettings readOnly preservation.
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PortResolverReadOnlyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── ParseBoolFromJson ─────────────────────────────────────────────────

        [Test]
        public void ParseBoolFromJson_TrueValue_ReturnsTrue()
            => Assert.AreEqual(true, PortResolver.ParseBoolFromJson("{\"readOnly\":true}", "readOnly"));

        [Test]
        public void ParseBoolFromJson_FalseValue_ReturnsFalse()
            => Assert.AreEqual(false, PortResolver.ParseBoolFromJson("{\"readOnly\":false}", "readOnly"));

        [Test]
        public void ParseBoolFromJson_Missing_ReturnsNull()
            => Assert.IsNull(PortResolver.ParseBoolFromJson("{\"port\":9500}", "readOnly"));

        [Test]
        public void ParseBoolFromJson_NullJson_ReturnsNull()
            => Assert.IsNull(PortResolver.ParseBoolFromJson(null, "readOnly"));

        [Test]
        public void ParseBoolFromJson_EmptyJson_ReturnsNull()
            => Assert.IsNull(PortResolver.ParseBoolFromJson("", "readOnly"));

        [Test]
        public void ParseBoolFromJson_WithWhitespace_Works()
            => Assert.AreEqual(true, PortResolver.ParseBoolFromJson("{\"readOnly\" : true}", "readOnly"));

        // ── TrySaveProjectSettings: readOnly preservation ─────────────────────

        [Test]
        public void TrySaveProjectSettings_PreservesReadOnlyTrue_WhenPresent()
        {
            var path = Path.Combine(Path.GetTempPath(), "mcp_ro_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "{\"port\":9600,\"chatPort\":9601,\"readOnly\":true}");
                var result = PortResolver.TrySaveProjectSettings(path, 9700, 9701, File.WriteAllText);
                Assert.IsTrue(result);
                var json = File.ReadAllText(path);
                Assert.AreEqual(true, PortResolver.ParseBoolFromJson(json, "readOnly"));
                Assert.AreEqual(9700, PortResolver.ParsePortFromJson(json, "port"));
                Assert.AreEqual(9701, PortResolver.ParsePortFromJson(json, "chatPort"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void TrySaveProjectSettings_PreservesReadOnlyFalse_WhenPresent()
        {
            var path = Path.Combine(Path.GetTempPath(), "mcp_ro_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "{\"port\":9600,\"chatPort\":9601,\"readOnly\":false}");
                PortResolver.TrySaveProjectSettings(path, 9700, 9701, File.WriteAllText);
                var json = File.ReadAllText(path);
                Assert.AreEqual(false, PortResolver.ParseBoolFromJson(json, "readOnly"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void TrySaveProjectSettings_NoReadOnly_WhenAbsent()
        {
            var path = Path.Combine(Path.GetTempPath(), "mcp_ro_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "{\"port\":9600,\"chatPort\":9601}");
                PortResolver.TrySaveProjectSettings(path, 9700, 9701, File.WriteAllText);
                var json = File.ReadAllText(path);
                Assert.IsNull(PortResolver.ParseBoolFromJson(json, "readOnly"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
