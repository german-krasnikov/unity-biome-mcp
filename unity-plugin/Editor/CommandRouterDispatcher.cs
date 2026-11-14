using System.Threading.Tasks;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Non-static ICommandDispatcher adapter over the static CommandRouter. CommandRouter itself
    /// cannot implement an interface (C# forbids interfaces/base types on `static class`), so this
    /// thin forwarder is the injectable seam Phase 2.3's consumers (ClientConnectionHandler,
    /// PlaytestRunner) will take instead of calling CommandRouter directly. Zero behavior change —
    /// pure 1:1 delegation to the existing static methods.
    /// </summary>
    public sealed class CommandRouterDispatcher : ICommandDispatcher
    {
        public static readonly CommandRouterDispatcher Instance = new CommandRouterDispatcher();

        public void ProcessAsync(string json, TaskCompletionSource<string> tcs, string chatMode = "")
            => CommandRouter.ProcessAsync(json, tcs, chatMode);

        public string ExecGetEnabledToolsCached() => CommandRouter.ExecGetEnabledToolsCached();
    }
}
