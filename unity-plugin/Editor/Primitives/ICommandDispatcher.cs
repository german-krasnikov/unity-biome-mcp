using System.Threading.Tasks;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Seam that decouples Transport-layer consumers (ClientConnectionHandler, PlaytestRunner)
    /// from the static CommandRouter. Minimal surface — only the two members those consumers
    /// actually call today (Phase 2.1, boundary-hardening).
    /// </summary>
    public interface ICommandDispatcher
    {
        /// <summary>Dispatches one command envelope; result is delivered via tcs, not the return
        /// value (mirrors CommandRouter.ProcessAsync's fire-and-forget + TaskCompletionSource
        /// pattern exactly — never throws past its own internal catch).</summary>
        void ProcessAsync(string json, TaskCompletionSource<string> tcs, string chatMode = "");

        /// <summary>Thread-safe fast-path read of the enabled-tools cache — never computes on the
        /// calling thread (mirrors CommandRouter.ExecGetEnabledToolsCached exactly).</summary>
        string ExecGetEnabledToolsCached();
    }
}
