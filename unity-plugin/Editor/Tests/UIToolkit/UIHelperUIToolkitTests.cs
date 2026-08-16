// Session 5: UIHelper.UIToolkit.cs — partial UIHelper read tools tests.
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIHelperUIToolkitTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Test 1: path=null → ListAllUIDocuments → "no UIDocument" when scene is empty
        [Test]
        public async Task InspectUITK_NullPath_NoDocuments_ReturnsNoUIDocumentMessage()
        {
            var result = UIHelper.InspectUITK(null, 4, null, null, false, false);
            // In a test scene with no UIDocuments, must report none found.
            // Double-red:
            // 1. Change "no UIDocument" to "error" → fails
            // 2. Delete ListAllUIDocuments → NullRef or wrong output → RED
            Assert.That(result, Does.Contain("no UIDocument").Or.Contain("[UIDocument]"),
                "Expected either 'no UIDocument found' or a list of UIDocument paths");
        }

        // Test 2: path="scene" → same list-all path as null
        [Test]
        public async Task InspectUITK_ScenePath_CallsListAllDocuments()
        {
            var result = UIHelper.InspectUITK("scene", 4, null, null, false, false);
            Assert.That(result, Does.Contain("no UIDocument").Or.Contain("[UIDocument]"),
                "path='scene' should trigger ListAllUIDocuments");
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
