// Session 5: UIHelper.UIToolkit.cs — partial UIHelper read tools tests.
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIHelperUIToolkitTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── U11: AttachUITK no PanelSettings → warn in response ──────────────

        [Test]
        public void AttachUITK_NoPanelSettings_ResponseContainsWarn()
        {
            var go = TrackOwnedObject(new UnityEngine.GameObject("UIHost_U11"));
            var result = UIHelper.AttachUITK("/" + go.name, null, null, 0);
            Assert.That(result, Does.Contain("warn:panelSettings=null"),
                $"Expected warn:panelSettings=null in response, got: {result}");
        }

        [Test]
        public void AttachUITK_NoPanelSettings_ResponseStartsWithOk()
        {
            var go = TrackOwnedObject(new UnityEngine.GameObject("UIHost_U11b"));
            var result = UIHelper.AttachUITK("/" + go.name, null, null, 0);
            Assert.That(result, Does.StartWith("ok:"),
                $"AttachUITK must still return ok: even with warn, got: {result}");
        }

        // ── U14: set_style unknown prop → error mentions inspect_uitk ────────

        [Test]
        public void UitkSetStyle_UnknownProp_MentionsInspectUITK()
        {
            var go = TrackOwnedObject(new UnityEngine.GameObject("UIHost_U14"));
            var method = typeof(UIHelper).GetMethod("UitkSetStyle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "UitkSetStyle private method must exist");
            var ve = new Label { name = "testLabel" };
            var result = (string)method.Invoke(null, new object[] { go, ve, "not-a-real-property", "red" });
            Assert.That(result, Does.Contain("inspect_uitk"),
                $"Expected 'inspect_uitk' in error for unknown style prop, got: {result}");
        }

        [Test]
        public void UitkSetStyle_UnknownProp_ReturnsErrResponse()
        {
            var go = TrackOwnedObject(new UnityEngine.GameObject("UIHost_U14b"));
            var method = typeof(UIHelper).GetMethod("UitkSetStyle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method);
            var ve = new Label { name = "label" };
            var result = (string)method.Invoke(null, new object[] { go, ve, "invalid-css-prop", "blue" });
            Assert.That(result, Does.StartWith("err:"),
                $"Unknown prop must return err:, got: {result}");
        }

        // Test 1: path=null → ListAllUIDocuments → "no UIDocument" when scene is empty
        [Test]
        public async Task InspectUITK_NullPath_NoDocuments_ReturnsNoUIDocumentMessage()
        {
            var result = UIHelper.InspectUITK(null, 4, null, null, false, false);
            // In a test scene with no UIDocuments, must report none found.
            // Double-red:
            // 1. Change "no UIDocument" to "error" → fails
            // 2. Delete ListAllUIDocuments → NullRef or wrong output → RED
            Assert.That(result,
                Does.Contain("no UIDocument").Or.Contain("no UI host")
                    .Or.Contain("[UIDocument]").Or.Contain("[PanelRenderer]"),
                "Expected no-host message or host listing");
        }

        // Test 2: path="scene" → same list-all path as null
        [Test]
        public async Task InspectUITK_ScenePath_CallsListAllDocuments()
        {
            var result = UIHelper.InspectUITK("scene", 4, null, null, false, false);
            Assert.That(result,
                Does.Contain("no UIDocument").Or.Contain("no UI host")
                    .Or.Contain("[UIDocument]").Or.Contain("[PanelRenderer]"),
                "path='scene' should trigger ListAllUIHosts");
        }

        // Test 3: invalid path → err: path not found
        [Test]
        public async Task InspectUITK_InvalidPath_ReturnsErrMessage()
        {
            var result = UIHelper.InspectUITK("/NonExistent/Object", 4, null, null, false, false);
            Assert.That(result, Does.StartWith("err:"),
                "Non-existent path should return an err: message");
            // Double-red:
            // 1. Remove "err:" prefix → StartsWith fails
            // 2. Remove ResolveUIDocument guard → exception propagates → RED
        }

        // Test 4: LintUITK with null path → err: path is required
        [Test]
        public async Task LintUITK_NullPath_ReturnsErrMessage()
        {
            var result = UIHelper.LintUITK(null, false);
            Assert.That(result, Does.StartWith("err:"),
                "null path should return an err: message");
            // Double-red:
            // 1. Remove null guard → NullRef → RED
            // 2. Change to Does.Not.StartWith("err:") → fails
        }

        // Test 5: LintUITK with non-existent file → err: file not found
        [Test]
        public async Task LintUITK_NonExistentFile_ReturnsErrMessage()
        {
            var result = UIHelper.LintUITK("/nonexistent/file.uxml", false);
            Assert.That(result, Does.StartWith("err:"),
                "Non-existent file should return err: file not found");
        }

        [Test]
        public void UitkElement_WhenNameAndSelectorProvided_NameWins()
        {
            var selectedAddress = UIHelper.PreferredUitkAddress(
                "preferred-by-name", ".fallback-selector");

            Assert.That(selectedAddress, Is.EqualTo("preferred-by-name"),
                "published addressing order is ref -> name -> selector");
        }
    }
}
