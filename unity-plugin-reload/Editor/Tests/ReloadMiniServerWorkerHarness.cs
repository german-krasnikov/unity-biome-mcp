using UnityMCP.Editor.Testing;

namespace UnityMCP.Reload.Tests
{
    internal sealed class ReloadMiniServerWorkerHarness
    {
        private const string Operation =
            "reload mini-server lifecycle mutation from a test";

        private ReloadMiniServerWorkerHarness()
        {
        }

        internal static ReloadMiniServerWorkerHarness Create()
        {
            UnityMcpWorkerTestBoundary.Require(Operation);
            return new ReloadMiniServerWorkerHarness();
        }

        internal void Restart(int port)
        {
            ReloadMiniServer.Stop();
            ReloadMiniServer.Start(port);
        }

        internal void Stop()
        {
            ReloadMiniServer.Stop();
        }
    }
}
