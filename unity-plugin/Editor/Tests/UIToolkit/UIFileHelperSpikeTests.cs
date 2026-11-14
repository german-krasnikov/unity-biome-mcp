// Phase 3 Spike: prove four platform invariants before UIFileHelper production code.
// All tests are [BiomeWorkerOnly] — run only in a disposable worker.
// These tests use Unity APIs directly (no UIFileHelper) to confirm:
//   C4: UTF8Encoding(false) writes no BOM
//   C2: in-place overwrite preserves .meta GUID
//   D04: USS written before UXML → CloneTree succeeds
//   C3/F6: on Unity 6000.0.83f1, a UXML naming an unregistered element type never fails the
//   vta==null or CloneTree()==null gates — it logs an Error and clones one placeholder Label
//   instead (re-probed dsl-bottlenecks-2026-11; production detection moved to an Error-log
//   capture, see UIFileHelper.ValidateImportedUxml and UIFileHelperRollbackTests)
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIFileHelperSpikeTests : UnityMcpTestBase
    {
        private const string TempDir = "Assets/TestsTemp/UIFileHelperSpike";

        // Resolve asset-relative path to absolute OS path.
        private static string Abs(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        [SetUp]
        public async Task SetUp()
        {
            Directory.CreateDirectory(Abs(TempDir));
            TrackOwnedAsset(TempDir);
            await Task.CompletedTask;
        }

        // Spike invariant 1 — C4: UTF8Encoding(false) must NOT write a BOM.
        // Breaks if BOM is written → Unity import warnings on Windows; also
        // validates that JsonHelper.Utf8NoBom is the right encoding constant.
        [Test]
        [BiomeWorkerOnly("Phase3 spike C4: BOM-free UXML write")]
        public async Task WriteUxml_BomFree_FirstByteNotEF()
        {
            var assetPath = TempDir + "/NoBom.uxml";
            var absPath = Abs(assetPath);
            TrackOwnedAsset(assetPath);

            File.WriteAllText(absPath, MinimalUxml(), JsonHelper.Utf8NoBom);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            await Task.CompletedTask;

            var bytes = File.ReadAllBytes(absPath);
            Assert.AreNotEqual(0xEF, bytes[0],
                "First byte must not be 0xEF (UTF-8 BOM) — use UTF8Encoding(false)");
            // Double-red:
            // 1. Change to AreEqual(0xEF, bytes[0]) → fails when BOM is absent
            // 2. Change JsonHelper.Utf8NoBom to Encoding.UTF8 → BOM written → RED
        }

        // Spike invariant 2 — C2: in-place overwrite must preserve .meta GUID.
        // Breaks if UIFileHelper ever does File.Delete + recreate.
        [Test]
        [BiomeWorkerOnly("Phase3 spike C2: in-place overwrite preserves meta GUID")]
        public async Task WriteUxml_InPlace_PreservesMetaGuid()
        {
            var assetPath = TempDir + "/GuidTest.uxml";
            var absPath = Abs(assetPath);
            TrackOwnedAsset(assetPath);

            File.WriteAllText(absPath, MinimalUxml("v1"), JsonHelper.Utf8NoBom);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var guidBefore = AssetDatabase.GUIDFromAssetPath(assetPath).ToString();
            await Task.CompletedTask;

            // In-place overwrite — same path, different content
            File.WriteAllText(absPath, MinimalUxml("v2"), JsonHelper.Utf8NoBom);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var guidAfter = AssetDatabase.GUIDFromAssetPath(assetPath).ToString();
            await Task.CompletedTask;

            Assert.AreEqual(guidBefore, guidAfter,
                "In-place overwrite must not change .meta GUID — never delete+recreate");
            // Double-red:
            // 1. Change to AreNotEqual → fails when GUID is preserved (as it should be)
            // 2. Add File.Delete(absPath) before second WriteAllText → GUID changes → RED
        }

        // Spike invariant 3 — D04: USS must be written and imported BEFORE the UXML.
        // Breaks if write order is reversed (USS after UXML causes import error).
        [Test]
        [BiomeWorkerOnly("Phase3 spike D04: USS written before UXML → CloneTree succeeds")]
        public async Task WriteUss_BeforeUxml_CloneTreeSucceeds()
        {
            var ussAsset = TempDir + "/Spike.uss";
            var uxmlAsset = TempDir + "/SpikeWithUss.uxml";
            TrackOwnedAsset(ussAsset);
            TrackOwnedAsset(uxmlAsset);

            // Write USS first
            File.WriteAllText(Abs(ussAsset), ".root { color: red; }", JsonHelper.Utf8NoBom);
            AssetDatabase.ImportAsset(ussAsset, ImportAssetOptions.ForceUpdate);

            // Then UXML referencing the USS (relative src)
            var uxml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                       "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n" +
                       "  <Style src=\"Spike.uss\" />\n" +
                       "  <ui:VisualElement name=\"root\" />\n" +
                       "</ui:UXML>";
            File.WriteAllText(Abs(uxmlAsset), uxml, JsonHelper.Utf8NoBom);
            AssetDatabase.ImportAsset(uxmlAsset, ImportAssetOptions.ForceUpdate);
            await Task.CompletedTask;

            var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlAsset);
            Assert.IsNotNull(vta, "VisualTreeAsset must not be null when USS exists before UXML");
            Assert.IsNotNull(vta.CloneTree(),
                "CloneTree must not be null when USS is imported before the UXML that references it");
            // Double-red:
            // 1. Change to IsNull(vta.CloneTree()) → fails when CloneTree succeeds
            // 2. Import UXML before USS → CloneTree may return null → RED
        }

        // Spike invariant 4 (updated for Unity 6000.0.83f1) — a UXML naming an unregistered
        // element type does NOT fail the vta==null or CloneTree()==null gates on this Editor
        // version: CloneTree() logs an Error and substitutes exactly one placeholder Label
        // instead of returning null. This documents real, probed engine behaviour, so the
        // assertion below must stay true, not a desired invariant (that invariant moved to
        // UIFileHelperRollbackTests, which pins the production fix using an Error-log capture
        // around CloneTree() rather than its return value — see UIFileHelper.ValidateImportedUxml).
        [Test]
        [BiomeWorkerOnly("Phase3 spike C3/F6, re-probed on 6000.0.83f1: CloneTree() never returns null for this content")]
        public async Task BrokenUxml_UnregisteredElement_ClonesPlaceholderNeverNull()
        {
            var assetPath = TempDir + "/Broken.uxml";
            TrackOwnedAsset(assetPath);

            // Syntactically valid XML but semantically invalid UXML (unknown element type)
            var broken = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                         "<ui:NOTEXISTENT_ELEMENT_12345 />" +
                         "</ui:UXML>";
            File.WriteAllText(Abs(assetPath), broken, JsonHelper.Utf8NoBom);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            await Task.CompletedTask;

            var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            // CloneTree() logs this [Error] for the unregistered element type -- declare it so UTF's
            // default "unhandled error log fails the test" gate does not fail this test for an error
            // the test itself provoked on purpose (AI/testing.md: declare, don't ignoreFailingMessages).
            LogAssert.Expect(LogType.Error, new Regex(
                "Element 'UnityEngine\\.UIElements\\.NOTEXISTENT_ELEMENT_12345' is missing a UxmlElementAttribute"));
            var clone = vta?.CloneTree();

            Assert.IsNotNull(vta, "on 6000.0.83f1 the asset still imports despite the unregistered element");
            Assert.IsNotNull(clone,
                "on 6000.0.83f1 CloneTree() substitutes a placeholder instead of returning null");
            Assert.AreEqual(1, clone.childCount,
                "on 6000.0.83f1 CloneTree() substitutes exactly one placeholder Label child");
            // Double-red:
            // 1. Change the 1 to any other value → fails while the probed placeholder shape holds
            // 2. Write valid UXML instead of broken → no Error is logged, LogAssert.Expect goes
            //    unmet → RED (this test is only true for genuinely broken content)
        }

        // Helpers

        private static string MinimalUxml(string name = "root") =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n" +
            $"  <ui:VisualElement name=\"{name}\" />\n" +
            "</ui:UXML>";
    }
}
