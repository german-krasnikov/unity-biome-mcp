// TDD: ReloadCommands — dispatch resolves all 6 commands, ping/get_version/unknown.
using NUnit.Framework;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    public class ReloadCommandsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Commands_ContainsAllSixCommands()
        {
            var cmds = ReloadCommands.Commands;
            Assert.IsTrue(cmds.ContainsKey("ping"),          "missing: ping");
            Assert.IsTrue(cmds.ContainsKey("get_version"),   "missing: get_version");
            Assert.IsTrue(cmds.ContainsKey("diagnose"),      "missing: diagnose");
            Assert.IsTrue(cmds.ContainsKey("sync_status"),   "missing: sync_status");
            Assert.IsTrue(cmds.ContainsKey("force_refresh"), "missing: force_refresh");
            Assert.IsTrue(cmds.ContainsKey("recompile"),     "missing: recompile");
        }

        [Test]
        public void Dispatch_Ping_ReturnsPong()
        {
            var result = ReloadCommands.Dispatch("ping");
            Assert.AreEqual("pong", result);
        }

        [Test]
        public void Dispatch_GetVersion_ReturnsNonEmpty()
        {
            var result = ReloadCommands.Dispatch("get_version");
            Assert.IsFalse(string.IsNullOrEmpty(result),
                "get_version must return stamp (non-empty)");
        }

        [Test]
        public void Dispatch_GetVersion_ContainsColon()
        {
            // stamp format: mvid:mtime_ticks
            var result = ReloadCommands.Dispatch("get_version");
            StringAssert.Contains(":", result);
        }

        [Test]
        public void Dispatch_UnknownCommand_ReturnsError()
        {
            var result = ReloadCommands.Dispatch("does_not_exist");
            StringAssert.StartsWith("error=", result);
        }

        [Test]
        public void Dispatch_Diagnose_ReturnsNonEmpty()
        {
            var result = ReloadCommands.Dispatch("diagnose");
            Assert.IsFalse(string.IsNullOrEmpty(result));
        }

        [Test]
        public void Dispatch_SyncStatus_ContainsStateField()
        {
            var result = ReloadCommands.Dispatch("sync_status");
            StringAssert.Contains("state=", result);
        }
    }
}
