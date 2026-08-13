// RED: DormantBridgeScanner tests (T7, IMPL-phase2-ports.md).
// All tests fail with compile errors until ConnectionSnapshot.cs is added
// with DormantInfo struct and DormantBridgeScanner static class.
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public sealed class DormantBridgeScannerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private TempDirScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new TempDirScope("mcp-dormant-scanner");
            DormantBridgeScanner.OverrideLockDir = _scope.Path;
        }

        [TearDown]
        public void TearDown()
        {
            DormantBridgeScanner.OverrideLockDir = null;
            _scope.Dispose();
        }

        [Test]
        public void Scan_NoLockFiles_ReturnsEmpty()
        {
            var result = DormantBridgeScanner.Scan(9500, new List<int>());
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Scan_LivePidNotInActivePids_ReturnsDormant()
        {
            var pid = Process.GetCurrentProcess().Id;
            File.WriteAllText(Path.Combine(_scope.Path, $"server-9500-{pid}.lock"), $"{pid}\n");

            var result = DormantBridgeScanner.Scan(9500, new List<int>());

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].BridgePid, Is.EqualTo(pid));
        }

        [Test]
        public void Scan_LivePidInActivePids_Excluded()
        {
            var pid = Process.GetCurrentProcess().Id;
            File.WriteAllText(Path.Combine(_scope.Path, $"server-9500-{pid}.lock"), $"{pid}\n");

            var result = DormantBridgeScanner.Scan(9500, new List<int> { pid });

            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Scan_DeadPid_Excluded()
        {
            const int deadPid = 99999999;
            Assume.That(!IsAlive(deadPid), "PID must not be alive for this test");
            File.WriteAllText(Path.Combine(_scope.Path, $"server-9500-{deadPid}.lock"), $"{deadPid}\n");

            var result = DormantBridgeScanner.Scan(9500, new List<int>());

            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Scan_WrongPort_Excluded()
        {
            var pid = Process.GetCurrentProcess().Id;
            File.WriteAllText(Path.Combine(_scope.Path, $"server-9501-{pid}.lock"), $"{pid}\n");

            var result = DormantBridgeScanner.Scan(9500, new List<int>());

            Assert.That(result.Count, Is.EqualTo(0));
        }

        private static bool IsAlive(int pid)
        {
            try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
            catch { return false; }
        }
    }
}
