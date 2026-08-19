// Session 9: AttachUITK — add UIDocument component to a GameObject.
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AttachUITKTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string AssetFolder = "Assets/TestsTemp/AttachUITK";
        private GameObject _testGO;

        [SetUp]
        public async Task SetUpTest()
        {
            TestPaths.EnsureFolder(AssetFolder);
            TrackOwnedAsset(AssetFolder);
            _testGO = new GameObject("AttachUITK_TestGO");
            TrackOwnedObject(_testGO);
        }

        private static PanelSettings CreatePanelSettingsAsset(string path)
        {
            var theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            AssetDatabase.CreateAsset(theme, AssetFolder + "/TestTheme.asset");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static VisualTreeAsset CreateUxmlAsset(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var absolutePath = Path.Combine(projectRoot, assetPath);
            File.WriteAllText(absolutePath,
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><ui:VisualElement name=\"root\" /></ui:UXML>");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
        }

        // Test 1: bare GO gets UIDocument (or PanelRenderer on 6.4+) added
        [Test]
        public async Task AttachUITK_BareGO_AddsUIDocumentComponent()
        {
            var path = ComponentSerializer.GetPath(_testGO);
            var result = UIHelper.AttachUITK(path, null, null, 0);

            Assert.That(result, Does.StartWith("ok:"),
                "Expected ok: response when UI host component successfully added");
#if UNITY_6000_5_OR_NEWER
            Assert.That(_testGO.GetComponent<PanelRenderer>(), Is.Not.Null,
                "On 6.4+, attach_uitk must add PanelRenderer");
            Assert.That(_testGO.GetComponent<UIDocument>(), Is.Null,
                "On 6.4+, UIDocument must not be added");
#else
            Assert.That(_testGO.GetComponent<UIDocument>(), Is.Not.Null,
                "UIDocument must be present after AttachUITK");
            Assert.That(_testGO.GetComponent<UIDocument>().visualTreeAsset, Is.Null);
            Assert.That(_testGO.GetComponent<UIDocument>().panelSettings, Is.Null);
#endif
        }

        // Test 2: duplicate guard — GO already has UIDocument
        [Test]
        public async Task AttachUITK_AlreadyHasUIDocument_ReturnsError()
        {
            Undo.AddComponent<UIDocument>(_testGO);
            var path = ComponentSerializer.GetPath(_testGO);

            var result = UIHelper.AttachUITK(path, null, null, 0);

            Assert.That(result, Does.StartWith("err:").And.Contains("already has a UI host component"),
                "Must return err: when a UI host is already present");
        }

        // Test 3: missing path → err
        [Test]
        public async Task AttachUITK_MissingPath_ReturnsError()
        {
            var result = UIHelper.AttachUITK("/NonExistentPath/GO", null, null, 0);

            // Double-red:
            // 1. Change check to Does.StartWith("ok:") → fails when path missing
            // 2. Remove path-not-found guard → NullRef → RED
            Assert.That(result, Does.StartWith("err:").And.Contains("not found"),
                "Must return err: when path not found");
        }

        // Test 4: missing uxml path → err
        [Test]
        public async Task AttachUITK_MissingUxmlPath_ReturnsError()
        {
            var path = ComponentSerializer.GetPath(_testGO);
            var result = UIHelper.AttachUITK(path, "Assets/NonExistent/HUD.uxml", null, 0);

            // Double-red:
            // 1. Change check to Does.StartWith("ok:") → wrong
            // 2. Remove uxml-not-found guard → NullRef → RED
            Assert.That(result, Does.StartWith("err:").And.Contains("uxml not found"),
                "Must return err: when uxml asset does not exist");
            Assert.That(_testGO.GetComponent<UIDocument>(), Is.Null,
                "Invalid UXML must not leave a partially configured UIDocument");
        }

        [Test]
        public async Task AttachUITK_MissingPanelSettings_ReturnsErrorWithoutMutation()
        {
            var path = ComponentSerializer.GetPath(_testGO);
            var result = UIHelper.AttachUITK(path, null,
                AssetFolder + "/MissingPanelSettings.asset", 0);

            Assert.That(result, Does.StartWith("err:").And.Contains("PanelSettings not found"));
            Assert.That(_testGO.GetComponent<UIDocument>(), Is.Null,
                "Invalid PanelSettings must not leave a partially configured UIDocument");
        }

        [Test]
        public async Task AttachUITK_ValidAssets_AssignsExactReferencesAndSortingOrder()
        {
            var uxmlPath = AssetFolder + "/Document.uxml";
            var panelPath = AssetFolder + "/PanelSettings.asset";
            var uxml = CreateUxmlAsset(uxmlPath);
            var panel = CreatePanelSettingsAsset(panelPath);

            Assert.That(uxml, Is.Not.Null, "UXML fixture must import as VisualTreeAsset");

            var result = UIHelper.AttachUITK(
                ComponentSerializer.GetPath(_testGO), uxmlPath, panelPath, 17);

            Assert.That(result, Does.StartWith("ok:"));
#if UNITY_6000_5_OR_NEWER
            var renderer = _testGO.GetComponent<PanelRenderer>();
            Assert.That(renderer, Is.Not.Null, "On 6.4+ expect PanelRenderer");
            Assert.That(renderer.visualTreeAsset, Is.SameAs(uxml));
            Assert.That(renderer.panelSettings, Is.SameAs(panel));
#else
            var document = _testGO.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.visualTreeAsset, Is.SameAs(uxml));
            Assert.That(document.panelSettings, Is.SameAs(panel));
            Assert.That(document.sortingOrder, Is.EqualTo(17));
#endif
        }

        // U11: When panelSettings arg is omitted, ok response includes warn:panelSettings=null
        [Test]
        public async Task AttachUITK_NoPanelSettings_WarnsSuffix()
        {
            // Double-red:
            // 1. Change "warn:panelSettings=null" to "warn:nothere" → assertion fails
            // 2. Remove the warn suffix code from AttachUITK → no warning → RED
            var path = ComponentSerializer.GetPath(_testGO);
            var result = UIHelper.AttachUITK(path, null, null, 0);

            Assert.That(result, Does.StartWith("ok:"),
                "AttachUITK without panelSettings must succeed");
            Assert.That(result, Does.Contain("warn:panelSettings=null"),
                "Response must contain warn:panelSettings=null when no PanelSettings provided");
        }

        // Test 5: Undo restores state — UIDocument/PanelRenderer removed after undo
        [Test]
        public async Task AttachUITK_UndoCreatesRestorePoint()
        {
            var path = ComponentSerializer.GetPath(_testGO);
            UIHelper.AttachUITK(path, null, null, 0);
            Assert.That(UIPanelHost.HasHost(_testGO), Is.True,
                "A UI host must be present after AttachUITK");

            Undo.PerformUndo();

            Assert.That(UIPanelHost.HasHost(_testGO), Is.False,
                "UI host must be absent after Undo — Undo.AddComponent must have been used");
        }

        // Test: new duplicate guard message text
        [Test]
        public void AttachUITK_AlreadyHasUIDocument_ReturnsErrWithNewText()
        {
            var go = TrackOwnedObject(new GameObject("AttachUITK_DupGuard"));
            go.AddComponent<UIDocument>();
            var path = ComponentSerializer.GetPath(go);
            var result = UIHelper.AttachUITK(path, null, null, 0);
            Assert.That(result, Does.StartWith("err:"));
            Assert.That(result, Does.Contain("already has a UI host component"));
        }

#if UNITY_6000_5_OR_NEWER
        [Test]
        public void AttachUITK_AlreadyHasPanelRenderer_ReturnsError()
        {
            var go = TrackOwnedObject(new GameObject("AttachUITK_PRDupGuard"));
            go.AddComponent<PanelRenderer>();
            var path = ComponentSerializer.GetPath(go);
            var result = UIHelper.AttachUITK(path, null, null, 0);
            Assert.That(result, Does.StartWith("err:"));
            Assert.That(result, Does.Contain("already has a UI host component"));
        }

        [Test]
        public void AttachUITK_SortingOrderNonZero_ResponseContainsWarn()
        {
            var go = TrackOwnedObject(new GameObject("AttachUITK_SortWarn"));
            var path = ComponentSerializer.GetPath(go);
            var result = UIHelper.AttachUITK(path, null, null, 5);
            Assert.That(result, Does.Contain("warn: sorting_order ignored"),
                "Non-zero sorting_order must warn user when PanelRenderer used");
        }
#endif
    }
}
