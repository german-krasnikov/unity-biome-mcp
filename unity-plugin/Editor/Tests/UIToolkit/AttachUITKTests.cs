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

        // Test 1: bare GO gets UIDocument added
        [Test]
        public async Task AttachUITK_BareGO_AddsUIDocumentComponent()
        {
            var path = ComponentSerializer.GetPath(_testGO);
            var result = UIHelper.AttachUITK(path, null, null, 0);

            // Must succeed and GO must have UIDocument
            // Double-red:
            // 1. Change Assert.That check to "fail" → assertion fails
            // 2. Remove Undo.AddComponent in AttachUITK → null → RED
            Assert.That(result, Does.StartWith("ok:"),
                "Expected ok: response when UIDocument successfully added");
            Assert.That(_testGO.GetComponent<UIDocument>(), Is.Not.Null,
                "UIDocument must be present after AttachUITK");
            Assert.That(_testGO.GetComponent<UIDocument>().visualTreeAsset, Is.Null);
            Assert.That(_testGO.GetComponent<UIDocument>().panelSettings, Is.Null);
        }

        // Test 2: duplicate guard — GO already has UIDocument
        [Test]
        public async Task AttachUITK_AlreadyHasUIDocument_ReturnsError()
        {
            Undo.AddComponent<UIDocument>(_testGO);
            var path = ComponentSerializer.GetPath(_testGO);

            var result = UIHelper.AttachUITK(path, null, null, 0);

            // Double-red:
            // 1. Change check to Does.StartWith("ok:") → passes when it should fail
            // 2. Remove the duplicate guard in AttachUITK → returns ok: → RED
            Assert.That(result, Does.StartWith("err:").And.Contains("already has UIDocument"),
                "Must return err: when UIDocument already present");
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
            var document = _testGO.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.visualTreeAsset, Is.SameAs(uxml));
            Assert.That(document.panelSettings, Is.SameAs(panel));
            Assert.That(document.sortingOrder, Is.EqualTo(17));
        }

        // Test 5: Undo restores state — UIDocument removed after undo
        [Test]
        public async Task AttachUITK_UndoCreatesRestorePoint()
        {
            var path = ComponentSerializer.GetPath(_testGO);
            UIHelper.AttachUITK(path, null, null, 0);
            Assert.That(_testGO.GetComponent<UIDocument>(), Is.Not.Null,
                "UIDocument must be present after AttachUITK");

            Undo.PerformUndo();

            // Double-red:
            // 1. Invert assertion → fails when UIDocument is absent after undo
            // 2. Use AddComponent instead of Undo.AddComponent → undo doesn't remove it → RED
            Assert.That(_testGO.GetComponent<UIDocument>(), Is.Null,
                "UIDocument must be absent after Undo — Undo.AddComponent must have been used");
        }
    }
}
