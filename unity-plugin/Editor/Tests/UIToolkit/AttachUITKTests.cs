// Session 9: AttachUITK — add UIDocument component to a GameObject.
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
        private GameObject _testGO;

        [SetUp]
        public async Task SetUpTest()
        {
            _testGO = new GameObject("AttachUITK_TestGO");
            TrackOwnedObject(_testGO);
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
