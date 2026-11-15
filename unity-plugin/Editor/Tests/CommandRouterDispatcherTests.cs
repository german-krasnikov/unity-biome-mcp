// TDD: ICommandDispatcher seam (Phase 2.1-2.2, boundary-hardening). CommandRouter is a static
// class and cannot implement an interface directly, so CommandRouterDispatcher is the injectable
// adapter future Transport consumers (ClientConnectionHandler, PlaytestRunner) will take instead
// of calling CommandRouter directly. These tests prove the adapter delegates 1:1 — zero behavior
// change versus calling CommandRouter's static methods directly.
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterDispatcherTests : UnityMcpTestBase
    {
        [Test]
        public void ExecGetEnabledToolsCached_ThroughInterface_DelegatesToCommandRouter()
        {
            // Both sides call the same static method — ceiling of testing a static adapter
            ICommandDispatcher dispatcher = CommandRouterDispatcher.Instance;
            Assert.AreEqual(CommandRouter.ExecGetEnabledToolsCached(), dispatcher.ExecGetEnabledToolsCached());
        }

        [Test]
        public async Task ProcessAsync_ThroughInterface_SameResultAsDirectCommandRouterCall()
        {
            var savedIsCompiling = CommandRouter.IsCompiling;
            var savedIsPlayMode = CommandRouter.IsPlayMode;
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            try
            {
                ICommandDispatcher dispatcher = CommandRouterDispatcher.Instance;

                var directTcs = new TaskCompletionSource<string>();
                CommandRouter.ProcessAsync("{\"id\":\"disp1-direct\",\"cmd\":\"ping\",\"args\":{}}", directTcs);
                var direct = await directTcs.Task;

                // 2-arg call through the interface — proves the interface's default chatMode=""
                // matches PlaytestRunner.Steps.cs:754's call shape (no explicit chatMode passed).
                var viaInterfaceTcs = new TaskCompletionSource<string>();
                dispatcher.ProcessAsync("{\"id\":\"disp1-iface\",\"cmd\":\"ping\",\"args\":{}}", viaInterfaceTcs);
                var viaInterface = await viaInterfaceTcs.Task;

                // Requests use distinct ids (disp1-direct / disp1-iface) so this isn't comparing
                // a call against itself; strip the id substring before comparing the rest of the
                // response so the assertion proves ProcessAsync's behavior matches.
                Assert.AreEqual(
                    direct.Replace("disp1-direct", "ID"),
                    viaInterface.Replace("disp1-iface", "ID"));
            }
            finally
            {
                CommandRouter.IsCompiling = savedIsCompiling;
                CommandRouter.IsPlayMode = savedIsPlayMode;
            }
        }
    }
}
