// NUnit tests for increment 3b: merge-write reloadPort in SavePorts + ServerReloadPort property.
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PortResolverReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private TempDirScope _scope;
        private string _tmpFile;

        [SetUp]
        public void SetUp()
        {
            _scope = new TempDirScope("mcp_port_test");
            _tmpFile = Path.Combine(_scope.Path, "port.json");
        }

        [TearDown]
        public void TearDown()
        {
            _scope.Dispose();
        }

        [Test]
        public void SavePorts_PreservesExistingReloadPort()
        {
            // Arrange: write file that already has reloadPort from reload-package
            File.WriteAllText(_tmpFile, "{\"port\":9500,\"chatPort\":9502,\"reloadPort\":9601}");

            // Act: main-side SavePorts overwrites port/chatPort
            PortResolver.SavePorts(_tmpFile, 9500, 9502);

            // Assert: reloadPort must survive
            var json = File.ReadAllText(_tmpFile);
            var reloadPort = PortResolver.ParsePortFromJson(json, "reloadPort");
            Assert.AreEqual(9601, reloadPort);
        }

        [Test]
        public void SavePorts_WithoutExistingReloadPort_WritesNoReloadPort()
        {
            // Arrange: file without reloadPort (old format)
            File.WriteAllText(_tmpFile, "{\"port\":9500,\"chatPort\":9502}");

            // Act
            PortResolver.SavePorts(_tmpFile, 9500, 9502);

            // Assert: no reloadPort key added
            var json = File.ReadAllText(_tmpFile);
            var reloadPort = PortResolver.ParsePortFromJson(json, "reloadPort");
            Assert.IsNull(reloadPort);
        }

        [Test]
        public void ParsePortFromJson_ReloadPort_ReturnsValue()
        {
            var json = "{\"port\":9500,\"chatPort\":9502,\"reloadPort\":9601}";
            var result = PortResolver.ParsePortFromJson(json, "reloadPort");
            Assert.AreEqual(9601, result);
        }

        [Test]
        public void ParsePortFromJson_MissingReloadPort_ReturnsNull()
        {
            var json = "{\"port\":9500,\"chatPort\":9502}";
            var result = PortResolver.ParsePortFromJson(json, "reloadPort");
            Assert.IsNull(result);
        }
    }

    [TestFixture]
    public class MCPServerReloadPortTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private TempDirScope _scope;
        private string _tmpFile;

        [SetUp]
        public void SetUp()
        {
            _scope = new TempDirScope("mcp_reload_test");
            _tmpFile = Path.Combine(_scope.Path, "port.json");
        }

        [TearDown]
        public void TearDown()
        {
            _scope.Dispose();
        }

        [Test]
        public void ReadReloadPort_FileHasReloadPort_ReturnsValue()
        {
            File.WriteAllText(_tmpFile, "{\"port\":9500,\"chatPort\":9502,\"reloadPort\":9601}");
            var result = PortResolver.ReadReloadPort(_tmpFile);
            Assert.AreEqual(9601, result);
        }

        [Test]
        public void ReadReloadPort_FileMissingReloadPort_ReturnsZero()
        {
            File.WriteAllText(_tmpFile, "{\"port\":9500,\"chatPort\":9502}");
            var result = PortResolver.ReadReloadPort(_tmpFile);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void ReadReloadPort_FileNotExist_ReturnsZero()
        {
            var result = PortResolver.ReadReloadPort("/nonexistent/path/file.json");
            Assert.AreEqual(0, result);
        }
    }
}
