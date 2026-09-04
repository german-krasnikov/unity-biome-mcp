using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// TestRunService.Start() has no public parameter for categories/assemblies/tests
    /// yet (that plumbing is a separate concern — see the commit message for this
    /// file), so these tests pre-seed a Prepared request+run pair directly through
    /// TestRunStore (identical technique to
    /// TestRunProtocolTests.RequestIdempotencyKeyNeverRedirectsToSecondRun) and then
    /// call Start() with matching mode/group/filter so the pre-seeded run is resumed
    /// and dispatched, exercising the real Filter-building code path.
    /// </summary>
    [TestFixture]
    internal sealed class TestRunServiceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string Utc = "2026-08-02T12:00:00.0000000Z";
        private string _root;
        private TestRunStore _store;
        private FakeFrameworkDriver _framework;
        private FakeEnvironment _environment;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(),
                "unity-mcp-run-service-" + Guid.NewGuid().ToString("N"));
            _store = new TestRunStore(_root);
            _framework = new FakeFrameworkDriver();
            _environment = new FakeEnvironment();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void Dispatch_WithCategories_PopulatesFilterCategoryNames()
        {
            SeedPreparedRun("request-categories", "run-categories",
                categories: new[] { "Fast", "!Stress" });

            CreateService().Start("request-categories", "EditMode", "", "");

            CollectionAssert.AreEqual(new[] { "Fast", "!Stress" },
                _framework.LastSettings.filters.Single().categoryNames);
        }

        [Test]
        public void Dispatch_WithAssemblies_PopulatesFilterAssemblyNames()
        {
            SeedPreparedRun("request-assemblies", "run-assemblies",
                assemblies: new[] { "UnityMCP.Editor.Tests" });

            CreateService().Start("request-assemblies", "EditMode", "", "");

            CollectionAssert.AreEqual(new[] { "UnityMCP.Editor.Tests" },
                _framework.LastSettings.filters.Single().assemblyNames);
        }

        [Test]
        public void Dispatch_WithTests_PopulatesFilterTestNames()
        {
            SeedPreparedRun("request-tests", "run-tests",
                tests: new[] { "Suite.TestA", "Suite.TestB" });

            CreateService().Start("request-tests", "EditMode", "", "");

            CollectionAssert.AreEqual(new[] { "Suite.TestA", "Suite.TestB" },
                _framework.LastSettings.filters.Single().testNames);
        }

        [Test]
        public void Dispatch_WithEmptySelection_LeavesFilterArraysNull()
        {
            SeedPreparedRun("request-empty-selection", "run-empty-selection");

            CreateService().Start("request-empty-selection", "EditMode", "", "");

            var filter = _framework.LastSettings.filters.Single();
            Assert.IsNull(filter.categoryNames);
            Assert.IsNull(filter.assemblyNames);
            Assert.IsNull(filter.testNames);
        }

        [Test]
        public void Start_WithSelection_PersistsSelectionOnRequestAndRun()
        {
            var selection = new TestRunSelection(
                new[] { "Fast", "!Stress" },
                new[] { "UnityMCP.Editor.Tests" },
                new[] { "Suite.TestA" });
            var otherSelection = new TestRunSelection(
                new[] { "Slow" }, Array.Empty<string>(), Array.Empty<string>());

            CreateService().Start("request-selection", "EditMode", "", "", selection);
            CreateService().Start("request-selection-repeat", "EditMode", "", "", selection);
            CreateService().Start("request-selection-other", "EditMode", "", "", otherSelection);

            var request = _store.ReadRequest("request-selection");
            CollectionAssert.AreEqual(new[] { "Fast", "!Stress" }, request.categories);
            CollectionAssert.AreEqual(new[] { "UnityMCP.Editor.Tests" }, request.assemblies);
            CollectionAssert.AreEqual(new[] { "Suite.TestA" }, request.tests);
            Assert.IsNotEmpty(request.selection_sha256);

            var run = _store.ReadRun(request.run_id);
            CollectionAssert.AreEqual(new[] { "Fast", "!Stress" }, run.categories);
            CollectionAssert.AreEqual(new[] { "UnityMCP.Editor.Tests" }, run.assemblies);
            CollectionAssert.AreEqual(new[] { "Suite.TestA" }, run.tests);
            Assert.AreEqual(request.selection_sha256, run.selection_sha256);

            var repeatSha = _store.ReadRequest("request-selection-repeat").selection_sha256;
            var otherSha = _store.ReadRequest("request-selection-other").selection_sha256;
            Assert.AreEqual(request.selection_sha256, repeatSha);
            Assert.AreNotEqual(request.selection_sha256, otherSha);
        }

        [Test]
        public void ComputeSha256_KnownInput_MatchesFrozenVector()
        {
            // Canonical form: mode|filter|group|categories|assemblies|tests,
            // each array sorted ordinal + "\n"-joined (TestRunSelection.Canonicalize).
            // Frozen hex computed independently:
            // python3 -c "import hashlib; print(hashlib.sha256('<canonical>'.encode('utf-8')).hexdigest())"
            // canonical_a = "EditMode||||UnityMCP.Editor.Chat.Tests.View|"
            var assembliesOnly = new TestRunSelection(
                Array.Empty<string>(),
                new[] { "UnityMCP.Editor.Chat.Tests.View" },
                Array.Empty<string>());
            Assert.AreEqual(
                "e944be3f753540a8f201a310b10ba9ad6c6bc996d1f929fbaa97d03c382c6390",
                TestRunSelection.ComputeSha256("EditMode", "", "", assembliesOnly));

            // Non-ASCII + multi-item + unsorted input, proving ordinal sort and
            // UTF-8 canonicalization (not just an ASCII single-item happy path).
            // canonical_b = "PlayMode|Foo|Bar|Smoke|Zeta\nalpha\nÄrger||A.T1\nB.T2"
            var multi = new TestRunSelection(
                new[] { "Zeta", "Ärger", "alpha" },
                Array.Empty<string>(),
                new[] { "B.T2", "A.T1" });
            Assert.AreEqual(
                "6d3f0f555fb69d40d036b2dc3db99997ef0fded91c84d5caac1afc2126d9fba3",
                TestRunSelection.ComputeSha256("PlayMode", "Foo|Bar", "Smoke", multi));
        }

        private void SeedPreparedRun(
            string requestId,
            string runId,
            string[] categories = null,
            string[] assemblies = null,
            string[] tests = null)
        {
            _store.CreateRequestOnce(new TestRunRequestRecord
            {
                request_id = requestId,
                run_id = runId,
                intent_complete = true,
                mode = "EditMode",
                group = "",
                filter = "",
                state = TestRunProtocol.Lifecycle.Prepared,
                created_utc = Utc
            });
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                request_id = requestId,
                source = "mcp",
                lifecycle = TestRunProtocol.Lifecycle.Prepared,
                health = TestRunProtocol.Health.Healthy,
                created_utc = Utc,
                mode = "EditMode",
                group = "",
                filter = "",
                categories = categories ?? Array.Empty<string>(),
                assemblies = assemblies ?? Array.Empty<string>(),
                tests = tests ?? Array.Empty<string>()
            });
        }

        private TestRunService CreateService() =>
            new TestRunService(
                _store,
                _environment,
                _framework,
                CoherentBuild,
                () => false,
                () => false,
                () => true,
                () => Utc);

        private static TestRunBuildFingerprint CoherentBuild() =>
            new TestRunBuildFingerprint
            {
                ProjectIdentity = "/tmp/project",
                EditorProcessIdentity = TestRunBuildFingerprintProbe.EditorProcessIdentity(),
                Fingerprint = "mvid=fresh;utf=1.6.0",
                UtfVersion = "1.6.0",
                AssemblyPath = "/tmp/UnityMCP.Editor.dll",
                SourcePath = "/tmp/TestRunner.cs",
                IsCoherent = true
            };

        // FakeFrameworkDriver/FakeEnvironment moved to TestRunFakes.cs (shared with
        // TestRunnerTests.cs, A20/A21 review minor). This fixture only asserts on
        // LastSettings, which the shared fake still captures identically; its
        // default Activity=Active/AnyActivity=Inactive match what this fixture's
        // own hardcoded Probe/ProbeAny used to return.
    }
}
