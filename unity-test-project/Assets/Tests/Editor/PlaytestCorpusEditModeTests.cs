// TDD: B21 — corpus carrier split (R-01/standards#2): the 9 Edit-capable MCPFeedbackFixture
// .playtest files each get a real EditMode EXECUTION test (B11/B20 only ever *parsed* them).
// This bypasses CommandRouter's run_playtest header gate entirely and calls
// PlaytestRunner.Run(..., requiresPlayMode: false) directly — the same pattern
// PlaytestRunnerEditModeTests.cs uses (PlaytestRunner is internal; visible here via
// [assembly: InternalsVisibleTo("UnityMCP.TestProject")] in unity-plugin/Editor/AssemblyInfo.cs).
// Per AI/testing.md's Disposable Worker Boundary list (reload/restart/recompile/write-source/
// asmdef/package/project-settings/refresh-imported-source/process-global-callback/crash-or-hang),
// a plain additive EditorSceneManager.OpenScene + TrackOwnedScene is NOT worker-only — these
// tests must stay ordinary (non-Explicit) so they count in the honest EditMode CI total.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Editor.Testing;
using UnityMCP.Editor.Tests;
using UnityMCP.Playtest.Core;

namespace UnityMCP.TestProject
{
    [TestFixture]
    public class PlaytestCorpusEditModeTests : SceneTestBase
    {
        private const string FixtureDir = "Assets/MCPFeedbackFixture/PlayTests";
        private const string ScenePath = "Assets/MCPFeedbackFixture/McpFeedbackFixture.unity";

        // ── Shared script-loading + PlaytestRunner harness ──────────────────────────

        private static string ReadProjectRelativeScript(string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(fullPath, Encoding.UTF8);
        }

        private static string ReadFixtureScript(string fileName) =>
            ReadProjectRelativeScript($"{FixtureDir}/{fileName}");

        // B22's counterpart carrier opens the same scene via the build-settings path for
        // PlayMode; here we open it additively so the loose ASSERT/INVOKE paths resolve
        // against real GameObjects (/Fixture/State, /Fixture/Actor, ...).
        private async Task OpenFixtureSceneAsync()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                $"McpFeedbackFixture scene not found at {ScenePath}");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            TrackOwnedScene(scene);
            await WaitForEditorUpdatesAsync(2, timeoutSeconds: 5.0);
        }

        // Bounded wait shared by every test: races the TCS against a fixed timeout rather
        // than an unbounded spin (mirrors PlaytestRunnerEditModeTests.AwaitBoundedAsync).
        private static async Task<string> AwaitBoundedAsync(TaskCompletionSource<string> tcs, double timeoutSeconds)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        private static async Task<string> RunScriptAsync(
            string script, float globalTimeout = 5f, double outerTimeoutSeconds = 5.0, string format = "text")
        {
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(script, globalTimeout, tcs, requiresPlayMode: false, format: format);
            return await AwaitBoundedAsync(tcs, outerTimeoutSeconds);
        }

        private static Task<string> RunFixtureAsync(
            string fileName, float globalTimeout = 5f, double outerTimeoutSeconds = 5.0) =>
            RunScriptAsync(ReadFixtureScript(fileName), globalTimeout, outerTimeoutSeconds);

        // ── Independent single-file tests ───────────────────────────────────────────

        [Test]
        public async Task Run_FIndependentFail_ReportsExactlyOneFailedStep()
        {
            await OpenFixtureSceneAsync();
            var result = await RunFixtureAsync("F_independent_fail.playtest");
            StringAssert.Contains("PLAYTEST: 1/2", result);
            StringAssert.Contains("FAIL", result);
            StringAssert.DoesNotContain("ABORTED", result);
        }

        [Test]
        public async Task Run_I1IndependentPass_AllStepsPass()
        {
            await OpenFixtureSceneAsync();
            var result = await RunFixtureAsync("I1_independent_pass.playtest");
            StringAssert.Contains("PLAYTEST: 5/5", result);
            StringAssert.Contains(" OK", result);
        }

        [Test]
        public async Task Run_I2IndependentPass_AllStepsPass()
        {
            await OpenFixtureSceneAsync();
            var result = await RunFixtureAsync("I2_independent_pass.playtest");
            StringAssert.Contains("PLAYTEST: 5/5", result);
            StringAssert.Contains(" OK", result);
        }

        [Test]
        public async Task Run_InvokeArguments_QuotedAndCommaArgumentsPass()
        {
            await OpenFixtureSceneAsync();
            var result = await RunFixtureAsync("INVOKE_arguments.playtest");
            StringAssert.Contains("PLAYTEST: 5/5", result);
            StringAssert.Contains(" OK", result);
        }

        [Test]
        [Category(TestCategories.Slow)]
        [Timeout(30000)]
        public async Task Run_LLongPass_CompletesAfterWait()
        {
            await OpenFixtureSceneAsync();
            var result = await RunFixtureAsync("L_long_pass.playtest", globalTimeout: 20f, outerTimeoutSeconds: 25.0);
            StringAssert.Contains("PLAYTEST: 3/3", result);
            StringAssert.Contains(" OK", result);
        }

        [Test]
        public async Task Run_MovementProfiles_TeleportAndMoveAsyncPass()
        {
            await OpenFixtureSceneAsync();
            var result = await RunFixtureAsync("MOVEMENT_profiles.playtest");
            StringAssert.Contains("PLAYTEST: 4/4", result);
            StringAssert.Contains(" OK", result);
        }

        [Test]
        public async Task Run_CiSmoke_ReportsConsoleClean()
        {
            // B23 gate: ASSERT_CONSOLE_CLEAN (PlaytestRunner.Steps.cs) reads the last 20
            // global error-level entries via ConsoleCapture.GetLogs(20, "error") — it is
            // not time-windowed like PlaytestRunner's per-step CONSOLE_ERR check (which
            // uses GetErrorsSince(stepStart, ...)). In the full-suite run this fixture is
            // one of 9000+ tests; many others (MaterialShaderTests, AssetTests,
            // CommandRouterErrorTests, UpmPluginUpdaterTests, ...) deliberately log
            // expected errors via LogAssert.Expect. Whichever land last before this test
            // runs would otherwise poison ASSERT_CONSOLE_CLEAN with unrelated history.
            // This test's contract is "nothing goes wrong during THIS playtest run" —
            // reset the console immediately beforehand so only new errors count.
            ConsoleCapture.Clear();

            // No /Fixture/... references in this file — the fixture scene is not needed.
            var script = ReadProjectRelativeScript("Playtests/ci_smoke.playtest");
            var result = await RunScriptAsync(script);
            // SECTION doesn't count toward the passed/failed ratio and its "--- ... ---"
            // line forces BuildReport off the terse " OK" branch — assert the ratio and
            // absence of failure text instead of the OK suffix.
            StringAssert.Contains("PLAYTEST: 3/3", result);
            StringAssert.DoesNotContain("FAIL", result);
            StringAssert.DoesNotContain("ERR", result);
        }

        // ── Shared chain: A -> B in one session, no reset between them ──────────────

        [Test]
        public async Task Run_AbSharedChain_StateAccumulates()
        {
            await OpenFixtureSceneAsync();

            var resultA = await RunFixtureAsync("A_shared_setup.playtest");
            StringAssert.Contains("PLAYTEST: 4/4", resultA);
            StringAssert.Contains(" OK", resultA);

            // B's own first step is "ASSERT $state == 101" — it only passes if A's final
            // Increment persisted with no reset in between. That is the effect assertion.
            var resultB = await RunFixtureAsync("B_shared_continue.playtest");
            StringAssert.Contains("PLAYTEST: 3/3", resultB);
            StringAssert.Contains(" OK", resultB);
        }

        // ── B22a: @suite-only excludes A/B/C from loose corpus iteration ────────────

        [Test]
        public void SuiteOnly_TaggedFiles_ExcludedFromLooseCorpusIteration()
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FixtureDir));
            var allFiles = Directory.GetFiles(dir, "*.playtest");
            var loose = allFiles
                .Where(f => !PlaytestHeaderScanner.Scan(File.ReadAllText(f, Encoding.UTF8)).SuiteOnly)
                .Select(Path.GetFileName)
                .ToList();

            Assert.AreEqual(allFiles.Length - 3, loose.Count,
                "@suite-only tagging must exclude exactly A/B/C from the loose corpus count");
            CollectionAssert.DoesNotContain(loose, "A_shared_setup.playtest");
            CollectionAssert.DoesNotContain(loose, "B_shared_continue.playtest");
            CollectionAssert.DoesNotContain(loose, "C_shared_finish.playtest");
            CollectionAssert.Contains(loose, "F_independent_fail.playtest");
            CollectionAssert.Contains(loose, "I1_independent_pass.playtest");
        }

        // ── C10: biome_smoke — the flagship self-hosting-Biome vertical slice ──────
        // [Repeat(20)] was verified against this exact UTF 1.6 pinned worker before use
        // (not assumed): a throwaway probe test proved the async Task body genuinely
        // re-executes on every repeat (20 distinct GUIDs appended to a scratch file
        // across 20 repeats), and a second probe with a deliberate failure on repeat #5
        // proved NUnit's RepeatAttribute stops immediately on the first failure and
        // reports a single aggregated "failed" outcome (only 5 of the 20 lines were
        // written) rather than silently swallowing it — so a single "passed" outcome
        // from this [Repeat(20)] test is equivalent evidentiary weight to 20 discrete
        // green runs, not a weaker signal. NUnit brackets [SetUp]/[TearDown] around the
        // whole repeated sequence exactly once, not once per repeat.
        private static readonly HashSet<string> _biomeSmokeRunIds = new HashSet<string>();

        // Idempotent: [Repeat(20)] re-invokes this whole method body 20x with SetUp/
        // TearDown bracketing only the outer sequence, so a plain OpenFixtureSceneAsync()
        // call here would reopen (and re-register via TrackOwnedScene) the same scene 20
        // times. Skip once it is already loaded.
        private async Task EnsureFixtureSceneOpenAsync()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded) return;
            await OpenFixtureSceneAsync();
        }

        [Test]
        [Repeat(20)]
        public async Task Run_BiomeSmoke_CreateCaptureAssertSetPropertyAssertDelete_Passes()
        {
            await EnsureFixtureSceneOpenAsync();

            var script = ReadProjectRelativeScript("Assets/Playtests/biome_smoke.playtest");
            var result = await RunScriptAsync(script, format: "json");

            // Canonical receipt proof, not scene-mutation-only (INV per C10's TESTS
            // FIRST spec): step index 1 is `MCP get_hierarchy ... INTO $tree` and index
            // 2 is the following `ASSERT $tree contains ...` that consumes the capture —
            // both must show ok:true, pinned to their exact step index so this proves
            // the specific read-and-capture pair ran, not merely that some assert in the
            // script happened to pass.
            StringAssert.Contains("\"index\":1,\"type\":\"Mcp\",\"ok\":true,", result);
            StringAssert.Contains("\"index\":2,\"type\":\"Assert\",\"ok\":true,", result);
            StringAssert.Contains("\"passed\":9,", result);
            StringAssert.Contains("\"failed\":0", result);

            var runId = JsonHelper.ExtractString(result, "run_id");
            Assert.IsNotEmpty(runId, "canonical receipt must carry a run_id");
            Assert.IsTrue(_biomeSmokeRunIds.Add(runId),
                $"run_id '{runId}' repeated — every iteration must get a unique run_id");

            // Effect-spy, not a text-only assertion: confirms TEARDOWN's MCP
            // delete_object actually removed the object (not merely that the receipt
            // self-reports ok:true), proving no state leak into the next repeat.
            Assert.IsNull(GameObject.Find($"pt_{runId}"),
                "biome_smoke's TEARDOWN must leave no state for the next iteration");
        }
    }
}
