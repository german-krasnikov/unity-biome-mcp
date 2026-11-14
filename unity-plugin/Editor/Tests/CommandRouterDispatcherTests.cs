// TDD: ICommandDispatcher seam (Phase 2.1-2.2, boundary-hardening). CommandRouter is a static
// class and cannot implement an interface directly, so CommandRouterDispatcher is the injectable
// adapter future Transport consumers (ClientConnectionHandler, PlaytestRunner) will take instead
// of calling CommandRouter directly. These tests prove the adapter delegates 1:1 — zero behavior
// change versus calling CommandRouter's static methods directly.
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterDispatcherTests : SceneTestBase
    {
        [Test]
        public void ExecGetEnabledToolsCached_ThroughInterface_DelegatesToCommandRouter()
        {
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
                const string envelope = "{\"id\":\"disp1\",\"cmd\":\"ping\",\"args\":{}}";

                var directTcs = new TaskCompletionSource<string>();
                CommandRouter.ProcessAsync(envelope, directTcs);
                var direct = await directTcs.Task;

                // 2-arg call through the interface — proves the interface's default chatMode=""
                // matches PlaytestRunner.Steps.cs:754's call shape (no explicit chatMode passed).
                var viaInterfaceTcs = new TaskCompletionSource<string>();
                dispatcher.ProcessAsync(envelope, viaInterfaceTcs);
                var viaInterface = await viaInterfaceTcs.Task;

                Assert.AreEqual(direct, viaInterface);
            }
            finally
            {
                CommandRouter.IsCompiling = savedIsCompiling;
                CommandRouter.IsPlayMode = savedIsPlayMode;
            }
        }
    }
}
