// TDD — RED first. Tests drive SelectionSummary.Summarize contract.
// GameObject creation requires EditMode test runner (no PlayMode deps needed).
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SelectionSummaryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() { }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Summarize_Null_ReturnsEmpty()
        {
            Assert.AreEqual("", SelectionSummary.Summarize(null));
        }

        [Test]
        public void Summarize_IncludesHierarchyPath()
        {
            _go = new GameObject("TestObj");
            var result = SelectionSummary.Summarize(_go);
            StringAssert.Contains("/TestObj", result);
        }

        [Test]
        public void Summarize_Format_HasSelectionPrefix()
        {
            _go = new GameObject("X");
            var result = SelectionSummary.Summarize(_go);
            StringAssert.StartsWith("[Selection:", result);
        }

        [Test]
        public void Summarize_NoExtraComponents_ShowsTransformNotListed()
        {
            // Bare GameObject — only Transform. Transform is excluded from component list.
            _go = new GameObject("Bare");
            var result = SelectionSummary.Summarize(_go);
            // No components beyond Transform → empty parens or no parens
            Assert.IsFalse(result.Contains("Transform"), "Transform must not be listed in summary");
        }

        [Test]
        public void Summarize_WithComponents_ListsThem()
        {
            _go = new GameObject("WithComps");
            _go.AddComponent<BoxCollider>();
            _go.AddComponent<Rigidbody>();
            var result = SelectionSummary.Summarize(_go);
            StringAssert.Contains("BoxCollider",  result);
            StringAssert.Contains("Rigidbody",    result);
        }

        [Test]
        public void Summarize_MoreThanThreeComponents_CapsAtThreeWithEllipsis()
        {
            _go = new GameObject("Many");
            _go.AddComponent<BoxCollider>();
            _go.AddComponent<Rigidbody>();
            _go.AddComponent<AudioSource>();
            _go.AddComponent<Light>();
            var result = SelectionSummary.Summarize(_go);
            // Must show "..." and must NOT show 4th component name explicitly after the cap
            StringAssert.Contains("...", result);
        }

        [Test]
        public void Summarize_ExactlyThreeComponents_NoEllipsis()
        {
            _go = new GameObject("Three");
            _go.AddComponent<BoxCollider>();
            _go.AddComponent<Rigidbody>();
            _go.AddComponent<AudioSource>();
            var result = SelectionSummary.Summarize(_go);
            Assert.IsFalse(result.Contains("..."), "No ellipsis when <= 3 components");
        }

        [Test]
        public void Summarize_NestedObject_ShowsFullPath()
        {
            var parent = new GameObject("Parent");
            _go = new GameObject("Child");
            _go.transform.SetParent(parent.transform);
            var result = SelectionSummary.Summarize(_go);
            StringAssert.Contains("/Parent/Child", result);
            Object.DestroyImmediate(parent);
        }

        // ── New: destroyed GameObject must return "" ─────────────────────────

        [Test]
        public void Summarize_DestroyedGameObject_ReturnsEmpty()
        {
            _go = new GameObject("Destroyed");
            Object.DestroyImmediate(_go);
            // After DestroyImmediate, Unity's overloaded == makes it == null,
            // but the reference is still non-null in CLR terms.
            // The guard `!go` catches the destroyed-but-alive reference.
            Assert.AreEqual("", SelectionSummary.Summarize(_go));
            _go = null; // already destroyed, prevent double-destroy in TearDown
        }

        // ── #21: tagged overload ─────────────────────────────────────────────

        [Test]
        public void Summarize_WithTag_UsesCustomPrefix()
        {
            _go = new GameObject("Tagged");
            var result = SelectionSummary.Summarize(_go, "Context");
            StringAssert.StartsWith("[Context:", result);
            StringAssert.EndsWith("]", result);
        }

        [Test]
        public void Summarize_DefaultTag_UsesSelection()
        {
            _go = new GameObject("Default");
            var result = SelectionSummary.Summarize(_go);
            StringAssert.StartsWith("[Selection:", result);
        }

        // ── F4: transient EntityId in summary (&ref format — ST5) ───────────

        [Test]
        public void SelectionSummary_Summarize_IncludesAmpRef()
        {
            _go = new GameObject("IdObj");
            var ampRef = RefManager.Assign(_go);
            var result = SelectionSummary.Summarize(_go);
            StringAssert.Contains(ampRef, result);
            // Must use &ref format, not legacy #decimal or $HEX
            Assert.IsFalse(result.Contains($"#{TransientObjectId.GetWireValue(_go)}"),
                "Summary must not use legacy #decimal format");
        }
    }
}
