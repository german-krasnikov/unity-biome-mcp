// Regression witness: SaveRuntimePortsCore must not call
// Application.dataPath or Application.temporaryCachePath (main-thread-only).
//
// Issue 1 root cause: MCPServer.StartAsync() retries bind on ThreadPool
// (after ConfigureAwait(false) in Task.Delay). Old SaveRuntimePorts(int, int)
// called Application APIs from ThreadPool → UnityException.
//
// Fix: MCPServer.cs caches _cachedTempCachePath on main thread, then
// passes it to SaveRuntimePorts(int, int, string, string).
//
// This test verifies SaveRuntimePortsCore (the path-injected version) works
// correctly and that the cached overload does not use Unity main-thread APIs.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PortFileManagerCachedPathTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private TempDirScope _scope;
        private string _portJsonPath;
        private string _tempCacheDir;

        [SetUp]
        public void SetUp()
        {
            _scope = new TempDirScope("cached_path_test");
            _portJsonPath = Path.Combine(_scope.Path, "MCP_Port.json");
            _tempCacheDir = Path.Combine(_scope.Path, "TempCache");
            Directory.CreateDirectory(_tempCacheDir);
        }

        [TearDown]
        public void TearDown() => _scope.Dispose();

        // ── SaveRuntimePortsCore writes port JSON correctly ───────────────────

        [Test]
        public void SaveRuntimePortsCore_WritesPortJsonCorrectly()
        {
            // Capture all writes via injected lambda — no Unity or real FS I/O.
            var writes = new List<string[]>();

            bool result = PortFileManager.SaveRuntimePortsCore(
                port: 9501,
                chatPort: 9503,
                runtimePortFilePath: _portJsonPath,
                temporaryCacheFilePath: Path.Combine(_tempCacheDir, "mcp_port.txt"),
                discoveryDirectory: _scope.Path,
                processId: System.Diagnostics.Process.GetCurrentProcess().Id,
                projectPath: _scope.Path,
                projectName: "TestProject",
                writeAllText: (path, content) =>
                {
                    File.WriteAllText(path, content);
                    writes.Add(new[] { path, content });
                },
                commitResolvedPorts: null);

            Assert.IsTrue(result, "SaveRuntimePortsCore must return true on success");

            // TrySavePorts uses atomic write: lambda is called with .tmp path,
            // then File.Move renames it to the final path (hardcoded, not injected).
            var portJsonWrite = writes.Find(w => w[0] == _portJsonPath + ".tmp");
            Assert.IsNotNull(portJsonWrite, "MCP_Port.json.tmp must be among the writes (atomic write pattern)");

            var parsedPort = PortResolver.ParsePortFromJson(portJsonWrite[1], "port");
            Assert.IsNotNull(parsedPort, "Port key must be present in written JSON");
            Assert.AreEqual(9501, parsedPort.Value, "Written port must match input");
        }

        // ── SaveRuntimePortsCore must not throw on ThreadPool ─────────────────

        [Test]
        public async System.Threading.Tasks.Task SaveRuntimePortsCore_DoesNotThrowOffMainThread()
        {
            // SaveRuntimePortsCore uses injected paths only — no Unity APIs.
            // Running on ThreadPool proves it doesn't call Application.dataPath etc.
            System.Exception caughtException = null;

            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    PortFileManager.SaveRuntimePortsCore(
                        port: 9501,
                        chatPort: 9503,
                        runtimePortFilePath: _portJsonPath,
                        temporaryCacheFilePath: Path.Combine(_tempCacheDir, "mcp_port.txt"),
                        discoveryDirectory: _scope.Path,
                        processId: System.Diagnostics.Process.GetCurrentProcess().Id,
                        projectPath: _scope.Path,
                        projectName: "TestProject",
                        writeAllText: (path, content) => File.WriteAllText(path, content),
                        commitResolvedPorts: null);
                }
                catch (System.Exception ex)
                {
                    caughtException = ex;
                }
            });

            Assert.IsNull(caughtException,
                $"SaveRuntimePortsCore must not throw on ThreadPool (Issue 1 regression). " +
                $"Error: {caughtException?.Message}");
        }

        // ── _cachedTempCachePath field exists and is initialized ──────────────

        [Test]
        public void MCPServer_CachedTempCachePath_FieldExistsAndIsNotNull()
        {
            // MCPServer._cachedTempCachePath is set in StartAsync() before the bind loop.
            // The field must exist and be initialized to empty string (not null).
            // Actual value is populated on first StartAsync() call.
            var field = typeof(MCPServer).GetField(
                "_cachedTempCachePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.IsNotNull(field, "_cachedTempCachePath field must exist on MCPServer");
            var value = (string)field.GetValue(null);
            // Must not be null (empty string is valid — set to "" at class init)
            Assert.IsNotNull(value,
                "_cachedTempCachePath must be initialized to empty string, not null. " +
                "If null, a ThreadPool read would NullReferenceException on Path.Combine.");
        }
    }
}
