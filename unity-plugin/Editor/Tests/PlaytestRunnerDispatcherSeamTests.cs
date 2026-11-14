// TDD: PlaytestRunner → ICommandDispatcher seam (Phase 5.1, boundary-hardening).
// Proves the MCP step call site in PlaytestRunner.Steps.cs (phase = Phase.WaitingMcp) routes
// through the injected PlaytestRunner.Dispatcher seam rather than calling the static
// CommandRouter directly. A fake dispatcher returns a sentinel the real CommandRouter would
// never produce for an unregistered command — so this test fails both if the seam is missing
// (compile error) and if the call site quietly reverts to calling CommandRouter directly
// (sentinel never observed, real "Command not registered" error seen instead).
//
// Mirrors ClientConnectionHandlerDispatcherSeamTests.cs's FakeDispatcher pattern exactly.
using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunnerDispatcherSeamTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeDispatcher : ICommandDispatcher
        {
            internal const string DataSentinel = "__playtest_dispatcher_seam_sentinel__";

            public string ExecGetEnabledToolsCached() => DataSentinel;

            public void ProcessAsync(string json, TaskCompletionSource<string> tcs, string chatMode = "")
                => tcs.TrySetResult("{\"id\":\"fake\",\"ok\":true,\"data\":\"" + DataSentinel + "\"}");
        }

        // Shared bounded-wait helper (mirrors PlaytestRunnerEditModeTests.AwaitBoundedAsync) —
        // races the TCS against a fixed timeout rather than spinning unbounded, since completion
        // here rides EditorApplication.update (Phase.WaitingMcp is polled, not awaited directly).
        private static async Task<string> AwaitBoundedAsync(TaskCompletionSource<string> tcs, double timeoutSeconds = 5.0)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        [Test]
        public async Task Run_McpStep_RoutesThroughInjectedDispatcher()
        {
            var fake = new FakeDispatcher();
            var original = PlaytestRunner.Dispatcher;
            PlaytestRunner.Dispatcher = fake;
            try
            {
                // BuildReport collapses an all-passed run to just "PLAYTEST: N/N (..s) OK" (no
                // per-step detail), so the sentinel itself never reaches the returned text. The
                // MCP step instead captures it INTO $result and a follow-up ASSERT consumes it
                // (same round-trip PlaytestRunnerEditModeTests.Run_McpStep_ReadCommand_
                // CapturesDataAndAssertConsumesIt uses) — "2/2 OK" is only reachable if the MCP
                // step actually received the fake's sentinel data, which is only possible if the
                // call routed through the injected Dispatcher rather than the real CommandRouter
                // (which would fail the unregistered command and leave $result uncaptured).
                var tcs = new TaskCompletionSource<string>();
                PlaytestRunner.Run(
                    $"MCP __dispatcher_seam_probe__ INTO $result\nASSERT $result contains {FakeDispatcher.DataSentinel}\n",
                    5f, tcs, requiresPlayMode: false);
                var result = await AwaitBoundedAsync(tcs);

                StringAssert.Contains("PLAYTEST: 2/2", result, $"Expected both steps to pass, got: {result}");
            }
            finally
            {
                PlaytestRunner.Dispatcher = original;
            }
        }
    }
}
