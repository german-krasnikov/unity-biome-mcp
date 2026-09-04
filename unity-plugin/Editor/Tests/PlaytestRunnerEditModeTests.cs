// TDD: B05 — the Play-mode gate for run_playtest moved past parsing. Registration no longer
// flags run_playtest runtime:true (CommandRouterRegistrationTests carries that assertion);
// AsyncRunPlaytest now scans the script's `# @needs editmode` header itself and decides.
// Dispatched end-to-end through CommandRouter.ProcessAsync (same pattern as PlaytestPathTests.cs)
// since AsyncRunPlaytest is a private handler reachable only through the command dispatch path.
using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunnerEditModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private Func<bool> _savedIsCompiling;

        [SetUp]
        public void SetUp()
        {
            // Defensive parity with PlaytestPathTests.cs: isolate from real Editor compile state.
            _savedIsCompiling = CommandRouter.IsCompiling;
            CommandRouter.IsCompiling = () => false;
        }

        [TearDown]
        public void TearDown()
        {
            CommandRouter.IsCompiling = _savedIsCompiling;
        }

        private static async Task<string> GetResultAsync(string argsJson)
        {
            var json = $"{{\"id\":\"t\",\"cmd\":\"run_playtest\",\"args\":{{{argsJson}}}}}";
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CommandRouter.ProcessAsync(json, tcs);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        // ── Regression: header-less script keeps the exact legacy gate (INV-005) ────

        [Test]
        public async Task AsyncRunPlaytest_NoHeader_NotPlaying_ReturnsPlayModeError()
        {
            var result = await GetResultAsync("\"script\":\"# empty playtest\"");
            StringAssert.Contains("Not in Play Mode. Use editor(action='play') first.", result);
        }

        // ── New: @needs editmode opts out of the Play-mode gate ─────────────────────

        [Test]
        public async Task AsyncRunPlaytest_EditModeHeader_NotPlaying_DoesNotBlock()
        {
            // 0-step script completes synchronously — never touches Tick()/EditorApplication.update,
            // so this proves the gate alone without depending on B06's Tick() change.
            var result = await GetResultAsync("\"script\":\"# @needs editmode\"");
            StringAssert.DoesNotContain("Not in Play Mode", result);
            StringAssert.Contains("PLAYTEST: 0 steps", result);
        }

        // ── New: @needs editmode + fresh is rejected before Run() ───────────────────

        [Test]
        public async Task AsyncRunPlaytest_EditModeHeader_FreshTrue_ReturnsError()
        {
            var result = await GetResultAsync("\"script\":\"# @needs editmode\",\"fresh\":\"true\"");
            StringAssert.Contains("err:", result);
            StringAssert.Contains("fresh", result);
            StringAssert.Contains("editmode", result);
        }
    }
}
