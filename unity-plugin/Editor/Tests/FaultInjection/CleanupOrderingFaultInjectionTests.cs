using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests.FaultInjection
{
    internal static class CleanupOrderingSentinel
    {
        internal const string Category = "UnityMCP.FaultInjection";
        internal const string SyncScenario = "sync-teardown";
        internal const string AsyncScenario = "async-teardown";
        internal const string SetupScenario = "setup-failure";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);

        internal static string DirectoryPath => Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Library", "UnityMCP", "FaultInjection"));

        internal static void Reset(string scenario)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(PathFor(scenario), string.Empty, Utf8NoBom);
        }

        internal static void Append(string scenario, string step)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.AppendAllText(PathFor(scenario), step + "\n", Utf8NoBom);
        }

        internal static void AssertExact(string scenario, params string[] expected)
        {
            var path = PathFor(scenario);
            Assert.That(File.Exists(path), Is.True,
                $"Fault-injection sentinel is missing: {path}");
            var actual = new List<string>(File.ReadAllLines(path, Utf8NoBom));
            CollectionAssert.AreEqual(expected, actual,
                "Cleanup ordering changed or a cleanup boundary did not execute.");
        }

        private static string PathFor(string scenario)
        {
            if (string.IsNullOrWhiteSpace(scenario) ||
                scenario.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("A safe scenario name is required.", nameof(scenario));
            return Path.Combine(DirectoryPath, scenario + ".sentinel");
        }
    }

    /// <summary>
    /// Adds a teardown level between the deliberately failing derived teardown and
    /// UnityMcpTestBase. UTF must continue to both this teardown and the common
    /// registered-cleanup boundary after recording the derived failure.
    /// </summary>
    internal abstract class CleanupOrderingFaultFixtureBase : SceneTestBase
    {
        private string _scenario;

        protected void StartProbe(string scenario, string firstStep)
        {
            _scenario = scenario;
            CleanupOrderingSentinel.Reset(scenario);
            CleanupOrderingSentinel.Append(scenario, firstStep);
            RegisterCleanup(() =>
                CleanupOrderingSentinel.Append(scenario, "registered-cleanup"));
        }

        protected void DirtyActiveScene(string objectName)
        {
            new GameObject(objectName);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        protected void Record(string step)
        {
            if (string.IsNullOrEmpty(_scenario))
                throw new InvalidOperationException("The fault probe was not initialized.");
            CleanupOrderingSentinel.Append(_scenario, step);
        }

        [TearDown]
        public void RecordLocalBaseTearDown()
        {
            Record("local-base-teardown");
        }

        protected override void OnBeforeIsolationCleanup()
        {
            Record("isolation-hook");
        }
    }

    [TestFixture]
    [Category(CleanupOrderingSentinel.Category)]
    [Category(TestCategories.FaultInjection)]
    internal sealed class SyncTearDownFailureProbe : CleanupOrderingFaultFixtureBase
    {
        internal const string FailureMarker =
            "UNITYMCP_EXPECTED_SYNC_TEARDOWN_FAILURE";

        [Test]
        [BiomeWorkerOnly("Fault injection: this test must fail in teardown.")]
        public void FailsInDerivedTearDownAfterDirtyingScene()
        {
            StartProbe(CleanupOrderingSentinel.SyncScenario, "test-body");
        }

        [TearDown]
        public void FailDerivedTearDown()
        {
            DirtyActiveScene("sync-teardown-pollution");
            Record("derived-teardown");
            throw new InvalidOperationException(FailureMarker);
        }
    }

    [TestFixture]
    [Category(CleanupOrderingSentinel.Category)]
    [Category(TestCategories.FaultInjection)]
    internal sealed class SyncTearDownCleanupCanary : SceneTestBase
    {
        [Test]
        [BiomeWorkerOnly("Canary for the sync teardown fault probe.")]
        public void SentinelAndSceneAreClean()
        {
            CleanupOrderingSentinel.AssertExact(
                CleanupOrderingSentinel.SyncScenario,
                "test-body",
                "derived-teardown",
                "local-base-teardown",
                "isolation-hook",
                "registered-cleanup");
            AssertCleanEmptyScene();
        }

        private static void AssertCleanEmptyScene()
        {
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(scene.isDirty, Is.False);
            Assert.That(scene.GetRootGameObjects(), Is.Empty);
        }
    }

    [TestFixture]
    [Category(CleanupOrderingSentinel.Category)]
    [Category(TestCategories.FaultInjection)]
    internal sealed class AsyncTearDownFailureProbe : CleanupOrderingFaultFixtureBase
    {
        internal const string FailureMarker =
            "UNITYMCP_EXPECTED_ASYNC_TEARDOWN_FAILURE";

        [Test]
        [BiomeWorkerOnly("Fault injection: this test must fault a Task teardown.")]
        public void FaultsTaskTearDownAfterDirtyingScene()
        {
            StartProbe(CleanupOrderingSentinel.AsyncScenario, "test-body");
        }

        [TearDown]
        public async Task FailDerivedTaskTearDown()
        {
            await Task.Yield();
            DirtyActiveScene("async-teardown-pollution");
            Record("derived-task-teardown");
            throw new InvalidOperationException(FailureMarker);
        }
    }

    [TestFixture]
    [Category(CleanupOrderingSentinel.Category)]
    [Category(TestCategories.FaultInjection)]
    internal sealed class AsyncTearDownCleanupCanary : SceneTestBase
    {
        [Test]
        [BiomeWorkerOnly("Canary for the Task teardown fault probe.")]
        public void SentinelAndSceneAreClean()
        {
            CleanupOrderingSentinel.AssertExact(
                CleanupOrderingSentinel.AsyncScenario,
                "test-body",
                "derived-task-teardown",
                "local-base-teardown",
                "isolation-hook",
                "registered-cleanup");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(scene.isDirty, Is.False);
            Assert.That(scene.GetRootGameObjects(), Is.Empty);
        }
    }

    [TestFixture]
    [Category(CleanupOrderingSentinel.Category)]
    [Category(TestCategories.FaultInjection)]
    internal sealed class SetUpFailureProbe : CleanupOrderingFaultFixtureBase
    {
        internal const string FailureMarker =
            "UNITYMCP_EXPECTED_SETUP_FAILURE";

        [SetUp]
        public void FailDerivedSetUp()
        {
            StartProbe(CleanupOrderingSentinel.SetupScenario, "derived-setup");
            DirtyActiveScene("setup-failure-pollution");
            throw new InvalidOperationException(FailureMarker);
        }

        [Test]
        [BiomeWorkerOnly("Fault injection: this test must fail in setup.")]
        public void BodyMustNotRun()
        {
            Assert.Fail("The test body ran after the expected setup failure.");
        }
    }

    [TestFixture]
    [Category(CleanupOrderingSentinel.Category)]
    [Category(TestCategories.FaultInjection)]
    internal sealed class SetUpFailureCleanupCanary : SceneTestBase
    {
        [Test]
        [BiomeWorkerOnly("Canary for the setup failure probe.")]
        public void SentinelAndSceneAreClean()
        {
            CleanupOrderingSentinel.AssertExact(
                CleanupOrderingSentinel.SetupScenario,
                "derived-setup",
                "local-base-teardown",
                "isolation-hook",
                "registered-cleanup");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(scene.isDirty, Is.False);
            Assert.That(scene.GetRootGameObjects(), Is.Empty);
        }
    }
}
