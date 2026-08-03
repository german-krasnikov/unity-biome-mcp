// TDD: EditorStateHelper.PingObject / GetSelection — S4 ping_object + get_selection commands.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class EditorStateHelperTests : SceneTestBase
    {
        private GameObject _go;
        private GameObject _prevSelection;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("PingSelectionTarget");
            _prevSelection = Selection.activeGameObject;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeGameObject = _prevSelection;
            Object.DestroyImmediate(_go);
        }

        // ── PingObject ────────────────────────────────────────────────────────

        [Test]
        public void PingObject_ValidPath_PingsAndSelects()
        {
            var path = ComponentSerializer.GetPath(_go);

            var result = EditorStateHelper.PingObject(path);

            Assert.AreSame(_go, Selection.activeGameObject);
            StringAssert.Contains($"pinged:{path}", result);
        }

        [Test]
        public void PingObject_InvalidPath_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => EditorStateHelper.PingObject("/NoSuchObject"));
        }

        [Test]
        public void PingObject_IsRegistered()
            => Assert.IsTrue(CommandRegistry.IsRegistered("ping_object"));

        // ── GetSelection ──────────────────────────────────────────────────────

        [Test]
        public void GetSelection_NoSelection_ReturnsEmpty()
        {
            Selection.activeGameObject = null;

            Assert.AreEqual("none", EditorStateHelper.GetSelection());
        }

        [Test]
        public void GetSelection_WithSelection_ReturnsPath()
        {
            var path = ComponentSerializer.GetPath(_go);
            Selection.activeGameObject = _go;

            var result = EditorStateHelper.GetSelection();

            StringAssert.StartsWith($"path:{path}", result);
        }

        [Test]
        public void GetSelection_IsRegistered()
            => Assert.IsTrue(CommandRegistry.IsRegistered("get_selection"));

        // ── Multi-select (paths param) ────────────────────────────────────────

        [Test]
        public void Control_SelectMulti_TwoValidPaths_ReturnsOkSelected2()
        {
            var a = TrackOwnedObject(new GameObject("MultiSelectA"));
            var b = TrackOwnedObject(new GameObject("MultiSelectB"));

            var result = EditorStateHelper.Control("select", null,
                $"{{\"paths\":\"{ComponentSerializer.GetPath(a)},{ComponentSerializer.GetPath(b)}\"}}");

            StringAssert.StartsWith("ok:selected 2", result);
            Assert.AreEqual(2, UnityEditor.Selection.objects.Length);
        }
    }
}
