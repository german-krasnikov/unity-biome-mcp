// Phase 3 Spike: prove four platform invariants before UIFileHelper production code.
// All tests are [BiomeWorkerOnly] — run only in a disposable worker.
// These tests use Unity APIs directly (no UIFileHelper) to confirm:
//   C4: UTF8Encoding(false) writes no BOM
//   C2: in-place overwrite preserves .meta GUID
//   D04: USS written before UXML → CloneTree succeeds
//   C3/F6: broken UXML → at least one gate (LoadAssetAtPath or CloneTree) returns null
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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

        // Spike invariant 4 — C3/F6: broken UXML must fail at least one gate.
        // Proves the two-gate strategy is needed (some broken UXML still loads as asset).
        [Test]
        [BiomeWorkerOnly("Phase3 spike C3/F6: broken UXML → LoadAssetAtPath or CloneTree returns null")]
        public async Task BrokenUxml_CloneTreeOrLoadReturnsNull()
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
            var clone = vta?.CloneTree();
            // At least one gate must detect the invalid UXML
            var isInvalid = vta == null || clone == null || clone.childCount == 0;
            Assert.IsTrue(isInvalid,
                "Broken UXML must fail at least one validation gate " +
                "(vta==null, clone==null, or clone.childCount==0)");
            // Double-red:
            // 1. Change to IsFalse(isInvalid) → fails when broken UXML is detected
            // 2. Write valid UXML instead of broken → all gates pass → isInvalid=false → RED
        }

        // Helpers

        private static string MinimalUxml(string name = "root") =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n" +
            $"  <ui:VisualElement name=\"{name}\" />\n" +
            "</ui:UXML>";
    }
}
