// TDD: ReloadPortResolver — FindFreePort, MergePersist, GetReloadPort env override.
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    public class ReloadPortResolverTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _origPortFilePath;
        private string _origPortsDir;
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _origPortFilePath = ReloadPortResolver.PortFilePath;
            _origPortsDir     = ReloadPortResolver.PortsDir;
            _tempDir = Path.Combine(Path.GetTempPath(), "ReloadPortResolverTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            ReloadPortResolver.PortFilePath = _origPortFilePath;
            ReloadPortResolver.PortsDir     = _origPortsDir;
            Environment.SetEnvironmentVariable("UNITY_MCP_RELOAD_PORT", null);
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Test]
        public void FindFreePort_ReturnsAvailablePort()
        {
            var port = ReloadPortResolver.FindFreePort(19800);
            Assert.Greater(port, 0, "port must be > 0");

            // Verify the returned port is actually bindable.
            var l = new TcpListener(IPAddress.Loopback, port);
            Assert.DoesNotThrow(() => { l.Start(); l.Stop(); },
                $"Port {port} must be bindable after FindFreePort returns it");
        }

        [Test]
        public void MergePersist_PreservesExistingPortAndChatPort()
        {
            var tempPath = Path.Combine(_tempDir, "MCP_Port.json");
            File.WriteAllText(tempPath, "{\"port\":9500,\"chatPort\":9501}");
            ReloadPortResolver.PortFilePath = tempPath;

            ReloadPortResolver.MergePersist(9600);

            var result = File.ReadAllText(tempPath);
            // All three keys must be present
            StringAssert.Contains("\"port\":9500", result);
            StringAssert.Contains("\"chatPort\":9501", result);
            StringAssert.Contains("\"reloadPort\":9600", result);
            Assert.AreEqual(1, result.Count(c => c == '{'), "JSON must have exactly one opening brace");
            Assert.AreEqual(1, result.Count(c => c == '}'), "JSON must have exactly one closing brace");
        }

        [Test]
        public void MergePersist_OverwritesExistingReloadPort()
        {
            var tempPath = Path.Combine(_tempDir, "MCP_Port.json");
            File.WriteAllText(tempPath, "{\"port\":9500,\"reloadPort\":9610}");
            ReloadPortResolver.PortFilePath = tempPath;

            ReloadPortResolver.MergePersist(9620);

            var result = File.ReadAllText(tempPath);
            StringAssert.Contains("\"reloadPort\":9620", result);
            Assert.IsFalse(result.Contains("9610"), "old reloadPort must be removed");
        }

        [Test]
        public void GetReloadPort_EnvOverride()
        {
            Environment.SetEnvironmentVariable("UNITY_MCP_RELOAD_PORT", "19999");
            // Point to non-existent file so it cannot accidentally use it
            ReloadPortResolver.PortFilePath = Path.Combine(_tempDir, "nonexistent.json");

            var port = ReloadPortResolver.GetReloadPort();

            Assert.AreEqual(19999, port);
        }

        [Test]
        public void WriteReloadPortFile_WritesMultilineFormat()
        {
            // F2: port file must be "port\nProjectDir\nProjectName" for Python CWD disambiguation.
            // Override PortsDir so SUT writes to our temp dir, then verify actual output.
            ReloadPortResolver.PortsDir = _tempDir;
            const int pid  = 12345;
            const int port = 9601;

            ReloadPortResolver.WriteReloadPortFile(pid, port, "/path/to/project", "MyProject");

            var written = File.ReadAllText(Path.Combine(_tempDir, $"{pid}.reload-port"));
            var lines   = written.Split('\n');
            Assert.AreEqual(3, lines.Length,         "must have exactly 3 lines");
            Assert.AreEqual("9601",             lines[0], "line0 must be port");
            Assert.AreEqual("/path/to/project", lines[1], "line1 must be ProjectDir");
            Assert.AreEqual("MyProject",        lines[2], "line2 must be ProjectName");
        }

        // ── merge-write correctness (bug: string-append produced invalid JSON) ──

        [Test]
        public void MergePersist_OnCorruptFile_RecreatesValidJson()
        {
            // Arrange: the known corrupt file produced by the old string-append bug.
            var tempPath = Path.Combine(_tempDir, "MCP_Port.json");
            File.WriteAllText(tempPath, "{\"port\":9500,\"chatPort\":9501}\"reloadPort\":9601}");
            ReloadPortResolver.PortFilePath = tempPath;

            // Act
            ReloadPortResolver.MergePersist(9620);

            // Assert: output must be parseable and contain reloadPort.
            var result = File.ReadAllText(tempPath);
            StringAssert.Contains("\"reloadPort\":9620", result);
            // Must not have duplicate closing braces or stray characters.
            Assert.AreEqual(result.Split('{').Length - 1, result.Split('}').Length - 1,
                "brace count must be balanced");
        }

        [Test]
        public void MergePersist_Idempotent_ProducesValidJson()
        {
            // Two consecutive MergePersist calls must not corrupt the file.
            var tempPath = Path.Combine(_tempDir, "MCP_Port.json");
            File.WriteAllText(tempPath, "{\"port\":9500,\"chatPort\":9501}");
            ReloadPortResolver.PortFilePath = tempPath;

            ReloadPortResolver.MergePersist(9600);
            ReloadPortResolver.MergePersist(9601);

            var result = File.ReadAllText(tempPath);
            // Only ONE occurrence of reloadPort key.
            var count = 0;
            var idx = 0;
            while ((idx = result.IndexOf("reloadPort", idx, System.StringComparison.Ordinal)) >= 0)
            { count++; idx++; }
            Assert.AreEqual(1, count, "reloadPort must appear exactly once");
            StringAssert.Contains("\"reloadPort\":9601", result);
        }

        [Test]
        public void MergePersist_OnEmptyFile_ProducesValidJson()
        {
            var tempPath = Path.Combine(_tempDir, "MCP_Port.json");
            File.WriteAllText(tempPath, "");
            ReloadPortResolver.PortFilePath = tempPath;

            ReloadPortResolver.MergePersist(9600);

            var result = File.ReadAllText(tempPath);
            StringAssert.Contains("\"reloadPort\":9600", result);
            // Must start/end with braces (valid JSON object).
            Assert.IsTrue(result.TrimStart().StartsWith("{"), "must be JSON object");
            Assert.IsTrue(result.TrimEnd().EndsWith("}"), "must be JSON object");
        }

        // ── Bug fix: stale reloadPort from a different worker ──

        [Test]
        public void GetReloadPort_StaleReloadPort_ReturnsPortAboveMainPort()
        {
            // reloadPort=9601 is stale (≤ mainPort=9699): must be rejected
            var tempPath = Path.Combine(_tempDir, "MCP_Port.json");
            File.WriteAllText(tempPath, "{\"port\":9699,\"reloadPort\":9601}");
            ReloadPortResolver.PortFilePath = tempPath;

            var port = ReloadPortResolver.GetReloadPort();

            Assert.Greater(port, 9699,
                "Stale reloadPort (9601 ≤ mainPort 9699) must be discarded; result must be > mainPort");
        }

        [Test]
        public void GetReloadPort_NoReloadPortInJson_MainPortPresent_ReturnsAtLeastMainPortPlusOne()
        {
            var tempPath = Path.Combine(_tempDir, "MCP_Port.json");
            File.WriteAllText(tempPath, "{\"port\":9699}");
            ReloadPortResolver.PortFilePath = tempPath;

            var port = ReloadPortResolver.GetReloadPort();

            Assert.GreaterOrEqual(port, 9700,
                "Fallback without cached reloadPort must start from mainPort+1");
        }

        [Test]
        public void GetReloadPort_ValidReloadPort_GreaterThanMainPort_ReturnsCachedPort()
        {
            // reloadPort=9701 > mainPort=9699 → valid, return directly
            var tempPath = Path.Combine(_tempDir, "MCP_Port.json");
            File.WriteAllText(tempPath, "{\"port\":9699,\"reloadPort\":9701}");
            ReloadPortResolver.PortFilePath = tempPath;

            var port = ReloadPortResolver.GetReloadPort();

            Assert.AreEqual(9701, port, "Valid cached reloadPort must be returned unchanged");
        }
    }
}
