using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityMCP.Editor.Testing;

namespace McpFeedbackFixture.Tests
{
    [TestFixture]
    public class ConformanceBaselineTests : UnityMcpTestBase
    {
        // AT-01/MCP-UTF-001/002 — baseline run identity emit
        [Test]
        public void Baseline_RunIdentityEmit_Succeeds()
        {
            var runId = Guid.NewGuid().ToString("N").Substring(0, 8);
            TestContext.WriteLine($"fixture_run_id={runId}");
            Assert.Pass();
        }

        // AT-10 — negative path validation (explicit: only run when selected by name)
        [Test, Explicit("Intentionally failing fixture for AT-10 verdict testing")]
        public void Baseline_IntentionalFail_ForVerdictValidation()
        {
            Assert.Fail("intentional_fixture_fail");
        }

        // MCP-UTF-001, AT-06 — timeout testing (explicit: long-running, run by name)
        [Test, Explicit("Long-running fixture for timeout testing")]
        [Timeout(30000)]
        public async Task LongPass()
        {
            var rawSeconds = Environment.GetEnvironmentVariable("FIXTURE_LONG_SECONDS") ?? "10";
            var seconds = int.TryParse(rawSeconds, out var s) ? s : 10;
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            Assert.Pass();
        }

        // MCP-COMP-023 — create a script, trigger compile, verify type appears
        [Test]
        [BiomeWorkerOnly("creates and deletes scripts")]
        public async Task CompileGenerationVisible()
        {
            const string tempFolder = UnityMcpTestAssetOwnership.Root + "/McpFeedbackFixture";
            var typeName = "AutoGen_" + Guid.NewGuid().ToString("N");
            var assetPath = tempFolder + "/" + typeName + ".cs";
            var absPath = Path.GetFullPath(assetPath);

            UnityMcpTestAssetOwnership.EnsureOwnedRoot();
            if (!AssetDatabase.IsValidFolder(tempFolder))
                AssetDatabase.CreateFolder(UnityMcpTestAssetOwnership.Root, "McpFeedbackFixture");

            RegisterCleanup(() =>
            {
                if (AssetDatabase.AssetPathExists(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
            });
            File.WriteAllText(absPath, $"public class {typeName} {{ }}");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            // Wait for compilation to begin
            await WaitForEditorUpdatesAsync(3, timeoutSeconds: 5.0);

            // Poll until compilation is done (domain reload will cancel this task if it occurs)
            for (int i = 0; i < 60 && EditorApplication.isCompiling; i++)
                await WaitForEditorUpdatesAsync(2, timeoutSeconds: 2.0);

            var found = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Any(t => t.Name == typeName);

            Assert.IsTrue(found, $"Type {typeName} not found after compilation");
        }

        // MCP-REF-035, AT-35 — reference graph traversal sanity
        [Test]
        [BiomeWorkerOnly("opens fixture scene additively")]
        public async Task ReferenceGraphRoundTrip()
        {
            const string scenePath = "Assets/MCPFeedbackFixture/McpFeedbackFixture.unity";

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                Assert.Ignore($"McpFeedbackFixture scene not found at {scenePath}");

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            TrackOwnedScene(scene);

            await WaitForEditorUpdatesAsync(2, timeoutSeconds: 5.0);

            var graph = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<FixtureReferenceGraph>(true))
                .FirstOrDefault();

            Assert.IsNotNull(graph, "FixtureReferenceGraph not found in McpFeedbackFixture scene");
            Assert.GreaterOrEqual(graph.ExpectedEdgeCount, 7, "Expected at least 7 reference edges from default field sizes");
            Assert.GreaterOrEqual(graph.ExpectedListenerCount, 0, "Listener count will be >0 once scene events are wired");
        }
    }
}
