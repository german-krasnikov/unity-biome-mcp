using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor.TestRuns;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunIsolationSafetyTests : UnityMcpTestBase
    {
        private const string OwnedSceneSessionKey =
            "UnityMCP_active_owned_test_scene_v1";

        [Test]
        public void TornLedger_SweepsRawAssetAndPreservesRunScene()
        {
            GetActiveOwnership(out var runId, out var runScenePath);
            var rawAssetPath = TestRunAssetOwnership.Root +
                "/torn-ledger-" + Guid.NewGuid().ToString("N") + ".bin";
            var rawAbsolutePath = AbsoluteAssetPath(rawAssetPath);
            File.WriteAllText(rawAbsolutePath, "stale", Encoding.UTF8);

            var ledgerPath = LedgerPath(runId);
            Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath));
            File.WriteAllText(ledgerPath, "not-valid-base64***\n", Encoding.UTF8);
            var priorQuarantines = ExistingQuarantines(ledgerPath);
            RegisterCleanup(() => DeleteTestLedgerArtifacts(ledgerPath, priorQuarantines));

            var report = TestRunAssetOwnership.CleanupForRun(runId, runScenePath);

            Assert.That(report.HasWarning, Is.True);
            StringAssert.Contains("ledger was corrupt", report.Warning);
            Assert.That(report.QuarantinedLedgerPath, Is.Not.Empty);
            Assert.That(File.Exists(report.QuarantinedLedgerPath), Is.True);
            Assert.That(File.Exists(rawAbsolutePath), Is.False);
            AssertRunScenePreserved(runScenePath);
        }

        [Test]
        public void CleanupForRun_RejectsArbitraryPreserveBeforeMutation()
        {
            GetActiveOwnership(out var runId, out var runScenePath);
            var markerPath = TestRunAssetOwnership.Root +
                "/preserve-rejection-" + Guid.NewGuid().ToString("N") + ".bin";
            var markerAbsolutePath = AbsoluteAssetPath(markerPath);
            File.WriteAllText(markerAbsolutePath, "must-survive-rejected-call", Encoding.UTF8);
            var arbitraryPreserve = TestRunAssetOwnership.Root +
                "/not-the-run-scene-" + Guid.NewGuid().ToString("N") + ".unity";

            Assert.Throws<ArgumentException>(() =>
                TestRunAssetOwnership.CleanupForRun(runId, arbitraryPreserve));

            Assert.That(File.Exists(markerAbsolutePath), Is.True,
                "Validation must run before the reserved-root sweep.");
            AssertRunScenePreserved(runScenePath);
        }

        [Test]
        public void RegisterForActiveRun_RejectsCompileExtensionsBeforeLedgerWrite()
        {
            GetActiveOwnership(out var runId, out _);
            var ledgerPath = LedgerPath(runId);
            var ledgerBefore = File.Exists(ledgerPath)
                ? File.ReadAllBytes(ledgerPath)
                : null;
            var extensions = new[]
            {
                ".cs", ".asmdef", ".asmref", ".rsp", ".dll", ".mdb", ".pdb", ".aar", ".jar"
            };

            foreach (var extension in extensions)
            {
                var path = TestRunAssetOwnership.Root + "/rejected-" +
                    Guid.NewGuid().ToString("N") + extension;
                Assert.Throws<ArgumentException>(() =>
                    TestRunAssetOwnership.RegisterForActiveRun(path), extension);

                if (ledgerBefore == null)
                    Assert.That(File.Exists(ledgerPath), Is.False,
                        extension + " was written to the ledger before rejection.");
                else
                    CollectionAssert.AreEqual(ledgerBefore, File.ReadAllBytes(ledgerPath),
                        extension + " changed the ledger before rejection.");
            }
        }

        [Test]
        public void Utf16BootstrapFingerprint_AcceptsOnlyPristineKnownLayouts()
        {
            var empty = TrackOwnedScene(EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive));
            Assert.That(
                UnityTestRunEnvironmentController.HasExactUtf16BootstrapFingerprint(empty),
                Is.True);
            Assert.That(EditorSceneManager.CloseScene(empty, true), Is.True);

            var utfDefault = TrackOwnedScene(EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive));
            Assert.That(
                UnityTestRunEnvironmentController.HasExactUtf16BootstrapFingerprint(utfDefault),
                Is.True);
            Assert.That(EditorSceneManager.CloseScene(utfDefault, true), Is.True);
        }

        [Test]
        public void Utf16BootstrapFingerprint_RejectsNearMisses()
        {
            var extraRoot = TrackOwnedScene(EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive));
            var extra = new GameObject("near-miss-extra-root");
            SceneManager.MoveGameObjectToScene(extra, extraRoot);
            Assert.That(
                UnityTestRunEnvironmentController.HasExactUtf16BootstrapFingerprint(extraRoot),
                Is.False, "an additional root must invalidate the UTF bootstrap fingerprint");
            Assert.That(EditorSceneManager.CloseScene(extraRoot, true), Is.True);

            var changedCamera = TrackOwnedScene(EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive));
            changedCamera.GetRootGameObjects()[0].GetComponent<Camera>().orthographic = false;
            Assert.That(
                UnityTestRunEnvironmentController.HasExactUtf16BootstrapFingerprint(changedCamera),
                Is.False, "a modified default camera must invalidate the fingerprint");
            Assert.That(EditorSceneManager.CloseScene(changedCamera, true), Is.True);
        }

        [Test]
        public void EnvironmentPreflight_ToleratesExistingPreviewSceneWithoutMutation()
        {
            var activeBefore = SceneManager.GetActiveScene();
            var preview = CreateOwnedPreviewScene();
            var marker = new GameObject("existing-preview-scene-proof");
            SceneManager.MoveGameObjectToScene(marker, preview);
            var previewCountBefore = EditorSceneManager.previewSceneCount;
            var previewHandle = preview.handle;
            var rootIdsBefore = preview.GetRootGameObjects()
                .Select(root => root.GetInstanceID())
                .ToArray();

            var ordinaryScenes = OrdinarySceneInventory.CaptureLoaded(
                "test inventory encountered an invalid or unloaded scene");
            Assert.That(ordinaryScenes.Any(scene => scene.handle == previewHandle), Is.False);
            Assert.That(ordinaryScenes.Any(scene => scene.handle == activeBefore.handle), Is.True);
            Assert.DoesNotThrow(() =>
                UnityTestRunEnvironmentController.RequireMainStage("start"));
            Assert.DoesNotThrow(ResetManagedTestScene,
                "managed repair must ignore an ambient preview owned by the outer test scope");

            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCountBefore));
            Assert.That(preview.IsValid() && preview.isLoaded, Is.True);
            Assert.That(EditorSceneManager.IsPreviewScene(preview), Is.True);
            Assert.That(preview.handle, Is.EqualTo(previewHandle));
            CollectionAssert.AreEqual(rootIdsBefore, preview.GetRootGameObjects()
                .Select(root => root.GetInstanceID())
                .ToArray());
            Assert.That(marker.scene.handle, Is.EqualTo(previewHandle));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeBefore.handle));
        }

        [Test]
        public void RunPreviewBaseline_ReportsCountMismatchWithoutClosingAmbientState()
        {
            var baseline = PreviewSceneCountEvidence.Capture();
            var owned = CreateOwnedPreviewScene();

            var violation = PreviewSceneCountEvidence.RestoreRunBaseline(true, baseline);
            var error = Assert.Throws<InvalidOperationException>(() =>
                PreviewSceneCountEvidence.RequireRecordedBaseline(true, baseline));

            StringAssert.Contains("preview scene count changed", violation);
            StringAssert.Contains("Preview scene count changed", error.Message);
            Assert.That(owned.IsValid() && owned.isLoaded, Is.True,
                "Count evidence must never close a preview scene without its exact handle.");
        }

        [Test]
        public void LegacyRunPreviewBaseline_IsReportedAndCannotAdmitAnotherTest()
        {
            var violation = PreviewSceneCountEvidence.RestoreRunBaseline(false, 0);
            var error = Assert.Throws<InvalidOperationException>(() =>
                PreviewSceneCountEvidence.RequireRecordedBaseline(false, 0));

            StringAssert.Contains("was not captured", violation);
            StringAssert.Contains("predates preview-scene baseline evidence", error.Message);
        }

        [Test]
        public void Restore_TamperedOwnedScenePathFailsBeforeSceneMutation()
        {
            GetActiveOwnership(out var activeRunId, out var activeRunScenePath);
            var sceneBefore = SceneManager.GetActiveScene();
            var rootsBefore = sceneBefore.GetRootGameObjects();
            var dirtyBefore = sceneBefore.isDirty;
            var guidBefore = AssetDatabase.AssetPathToGUID(activeRunScenePath);
            var ownedHintBefore = SessionState.GetString(OwnedSceneSessionKey, "");
            var runHintBefore = SessionState.GetString(
                TestRunAssetOwnership.OwnedRunIdSessionKey, "");

            var storeRoot = Path.Combine(Path.GetTempPath(),
                "unity-mcp-tampered-environment-" + Guid.NewGuid().ToString("N"));
            RegisterCleanup(() =>
            {
                if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
            });
            var store = new TestRunStore(storeRoot);
            var tamperedRunId = "tampered-" + Guid.NewGuid().ToString("N");
            store.WriteRun(new TestRunRecord
            {
                run_id = tamperedRunId,
                lifecycle = TestRunProtocol.Lifecycle.Running,
                created_utc = DateTime.UtcNow.ToString("O"),
                build_coherent = true
            });
            store.WriteEnvironment(new TestRunEnvironmentRecord
            {
                run_id = tamperedRunId,
                restore_single_untitled = true,
                untitled_scene_setup = TestRunProtocol.UntitledSceneSetup.Empty,
                owned_scene_path = activeRunScenePath,
                prepared_utc = DateTime.UtcNow.ToString("O")
            });

            var controller = new UnityTestRunEnvironmentController();
            Assert.Throws<InvalidOperationException>(() => controller.Restore(
                store, tamperedRunId, DateTime.UtcNow.ToString("O")));

            var sceneAfter = SceneManager.GetActiveScene();
            Assert.That(sceneAfter.handle, Is.EqualTo(sceneBefore.handle));
            Assert.That(sceneAfter.path, Is.EqualTo(activeRunScenePath));
            Assert.That(sceneAfter.isDirty, Is.EqualTo(dirtyBefore));
            CollectionAssert.AreEqual(rootsBefore, sceneAfter.GetRootGameObjects());
            Assert.That(AssetDatabase.AssetPathToGUID(activeRunScenePath), Is.EqualTo(guidBefore));
            Assert.That(SessionState.GetString(OwnedSceneSessionKey, ""),
                Is.EqualTo(ownedHintBefore));
            Assert.That(SessionState.GetString(
                TestRunAssetOwnership.OwnedRunIdSessionKey, ""), Is.EqualTo(runHintBefore));
            Assert.That(runHintBefore, Is.EqualTo(activeRunId));
        }

        [Test]
        public void CleanupForRun_RemovesRawDirectoryFileAndOrphanMeta()
        {
            GetActiveOwnership(out var runId, out var runScenePath);
            var suffix = Guid.NewGuid().ToString("N");
            var rawFilePath = TestRunAssetOwnership.Root + "/unregistered-" + suffix + ".bin";
            var rawDirectoryPath = TestRunAssetOwnership.Root + "/unregistered-dir-" + suffix;
            var orphanMetaPath = TestRunAssetOwnership.Root + "/orphan-" + suffix + ".asset.meta";
            var rawFileAbsolute = AbsoluteAssetPath(rawFilePath);
            var rawDirectoryAbsolute = AbsoluteAssetPath(rawDirectoryPath);
            var orphanMetaAbsolute = AbsoluteAssetPath(orphanMetaPath);
            File.WriteAllText(rawFileAbsolute, "raw", Encoding.UTF8);
            Directory.CreateDirectory(rawDirectoryAbsolute);
            File.WriteAllText(Path.Combine(rawDirectoryAbsolute, "nested.bin"), "nested", Encoding.UTF8);
            File.WriteAllText(orphanMetaAbsolute,
                "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n",
                Encoding.UTF8);

            var report = TestRunAssetOwnership.CleanupForRun(runId, runScenePath);

            Assert.That(report.HasWarning, Is.False);
            Assert.That(File.Exists(rawFileAbsolute), Is.False);
            Assert.That(Directory.Exists(rawDirectoryAbsolute), Is.False);
            Assert.That(File.Exists(orphanMetaAbsolute), Is.False);
            AssertRunScenePreserved(runScenePath);

            var absoluteRoot = AbsoluteAssetPath(TestRunAssetOwnership.Root);
            var actualEntries = Directory.EnumerateFileSystemEntries(absoluteRoot)
                .Select(Path.GetFileName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var sceneFileName = Path.GetFileName(runScenePath);
            CollectionAssert.AreEquivalent(
                new[] { sceneFileName, sceneFileName + ".meta" }, actualEntries);
        }

        [Test]
        public void LegacyAutoSuffixedRootDetection_IsExact()
        {
            Assert.That(TestRunAssetOwnership.IsLegacyAutoSuffixedRootPath(
                "Assets/TestsTemp 1"), Is.True);
            Assert.That(TestRunAssetOwnership.IsLegacyAutoSuffixedRootPath(
                "Assets/TestsTemp 386"), Is.True);

            var rejected = new[]
            {
                "Assets/TestsTemp", "Assets/TestsTemp 0", "Assets/TestsTemp 01",
                "Assets/TestsTemp -1", "Assets/TestsTemp 1/file.asset",
                "Assets/Nested/TestsTemp 1", "Assets/TestsTemp copy"
            };
            foreach (var path in rejected)
                Assert.That(TestRunAssetOwnership.IsLegacyAutoSuffixedRootPath(path),
                    Is.False, path);
        }

        [Test]
        public void LegacyRootMigration_DeletesOnlyExactEmptyFolders_AndIsIdempotent()
        {
            var assetsDirectory = CreateTemporaryAssetsDirectory();
            var legacy = Path.Combine(assetsDirectory, "TestsTemp 987654321");
            var unrelated = Path.Combine(assetsDirectory, "TestsTemp backup");
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(unrelated);
            var deleted = new List<string>();

            bool Delete(string assetPath)
            {
                deleted.Add(assetPath);
                Directory.Delete(Path.Combine(
                    assetsDirectory, assetPath.Substring("Assets/".Length)));
                return true;
            }

            var first = TestRunAssetOwnership.MigrateLegacyAutoSuffixedRoots(
                assetsDirectory, _ => true, Delete);
            var second = TestRunAssetOwnership.MigrateLegacyAutoSuffixedRoots(
                assetsDirectory, _ => true, Delete);

            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.Zero);
            CollectionAssert.AreEqual(
                new[] { "Assets/TestsTemp 987654321" }, deleted);
            Assert.That(Directory.Exists(legacy), Is.False);
            Assert.That(Directory.Exists(unrelated), Is.True);
        }

        [Test]
        public void LegacyRootMigration_NonEmptyCandidateFailsBeforeAnyDeletion()
        {
            var assetsDirectory = CreateTemporaryAssetsDirectory();
            var empty = Path.Combine(assetsDirectory, "TestsTemp 987654322");
            var nonEmpty = Path.Combine(assetsDirectory, "TestsTemp 987654323");
            Directory.CreateDirectory(empty);
            Directory.CreateDirectory(nonEmpty);
            File.WriteAllText(Path.Combine(nonEmpty, ".orphan.meta"), "evidence");
            var deleted = new List<string>();

            var error = Assert.Throws<IOException>(() =>
                TestRunAssetOwnership.MigrateLegacyAutoSuffixedRoots(
                    assetsDirectory,
                    _ => true,
                    assetPath =>
                    {
                        deleted.Add(assetPath);
                        return true;
                    }));

            StringAssert.Contains("non-empty", error.Message);
            Assert.That(deleted, Is.Empty);
            Assert.That(Directory.Exists(empty), Is.True);
            Assert.That(Directory.Exists(nonEmpty), Is.True);
        }

        private static void GetActiveOwnership(out string runId, out string runScenePath)
        {
            runId = SessionState.GetString(TestRunAssetOwnership.OwnedRunIdSessionKey, "");
            runScenePath = SessionState.GetString(OwnedSceneSessionKey, "");
            Assert.That(runId, Is.Not.Empty, "The observer did not publish the active run id.");
            Assert.That(runScenePath, Is.EqualTo(
                TestRunAssetOwnership.ExpectedRunScenePath(runId)));
        }

        private static void AssertRunScenePreserved(string runScenePath)
        {
            var scene = SceneManager.GetSceneByPath(runScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(runScenePath), Is.Not.Empty);
            Assert.That(File.Exists(AbsoluteAssetPath(runScenePath)), Is.True);
        }

        private static string AbsoluteAssetPath(string assetPath) =>
            Path.Combine(ProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar));

        private static string LedgerPath(string runId) => Path.Combine(
            ProjectRoot(), "Library", "UnityMCP", "TestOwnership", runId + ".assets");

        private static string ProjectRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static HashSet<string> ExistingQuarantines(string ledgerPath)
        {
            var directory = Path.GetDirectoryName(ledgerPath);
            if (!Directory.Exists(directory))
                return new HashSet<string>(StringComparer.Ordinal);
            return new HashSet<string>(Directory.GetFiles(
                directory, Path.GetFileName(ledgerPath) + ".corrupt-*"),
                StringComparer.Ordinal);
        }

        private static void DeleteTestLedgerArtifacts(
            string ledgerPath,
            ISet<string> priorQuarantines)
        {
            if (File.Exists(ledgerPath)) File.Delete(ledgerPath);
            var directory = Path.GetDirectoryName(ledgerPath);
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.GetFiles(
                         directory, Path.GetFileName(ledgerPath) + ".corrupt-*"))
                    if (!priorQuarantines.Contains(path)) File.Delete(path);
        }

        private string CreateTemporaryAssetsDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "unity-mcp-legacy-root-tests-" + Guid.NewGuid().ToString("N"));
            var assets = Path.Combine(root, "Assets");
            RegisterCleanup(() =>
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            });
            Directory.CreateDirectory(assets);
            return assets;
        }
    }
}
