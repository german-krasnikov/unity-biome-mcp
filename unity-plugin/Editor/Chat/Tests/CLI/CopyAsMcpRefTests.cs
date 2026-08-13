// TDD — CopyAsMcpRef: FormatAsRef + CopySelection logic tests.
// Tests FormatAsRef pipeline and CopySelection behavior without invoking real MenuItems.
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CopyAsMcpRefTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private System.Collections.Generic.List<GameObject> _created;

        [SetUp]
        public void SetUp()
        {
            _created = new System.Collections.Generic.List<GameObject>();
            ChipKindRegistry.ResetForTests();
            // Reset clipboard to a known state
            EditorGUIUtility.systemCopyBuffer = "";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            ChipKindRegistry.ResetForTests();
        }

        private GameObject MakeGo(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // 1. Hierarchy GO produces bracket ref with transient EntityId in $HEX format
        [Test]
        public void FormatAsRef_HierarchyGO_ReturnsBracketWithId()
        {
            var go = MakeGo("Player");
            var result = ChipContextResolver.FormatAsRef(go);
            Assert.IsNotNull(result);
            StringAssert.StartsWith("[hierarchy:/Player$", result);
            StringAssert.EndsWith("]", result);
            StringAssert.Contains(TransientObjectId.GetHexRef(go), result);
        }

        // 2. Null returns null — no throw
        [Test]
        public void FormatAsRef_Null_ReturnsNull()
        {
            Assert.IsNull(ChipContextResolver.FormatAsRef(null));
        }

        // 3. DRY proof — output matches FormatChipRef with same data
        [Test]
        public void FormatAsRef_MatchesFormatChipRef()
        {
            var go = MakeGo("Hero");
            var result = ChipContextResolver.FormatAsRef(go);
            var expected = ChipContextResolver.FormatChipRef(
                ChipKindKeys.Hierarchy, "/" + go.name, TransientObjectId.GetHexRef(go));
            Assert.AreEqual(expected, result);
        }

        // 4. Multiple objects → newline-separated refs in clipboard
        [Test]
        public void CopySelection_MultipleObjects_NewlineSeparated()
        {
            var go1 = MakeGo("A");
            var go2 = MakeGo("B");
            CopyAsMcpRef.CopySelection(new Object[] { go1, go2 });

            var clipboard = EditorGUIUtility.systemCopyBuffer;
            var lines = clipboard.Split('\n');
            Assert.AreEqual(2, lines.Length);
            StringAssert.Contains("hierarchy:/A", lines[0]);
            StringAssert.Contains("hierarchy:/B", lines[1]);
        }

        // 5. Empty array — clipboard unchanged
        [Test]
        public void CopySelection_EmptyArray_NoClipboardChange()
        {
            EditorGUIUtility.systemCopyBuffer = "ORIGINAL";
            CopyAsMcpRef.CopySelection(new Object[0]);
            Assert.AreEqual("ORIGINAL", EditorGUIUtility.systemCopyBuffer);
        }

        // 6. Component resolves to parent GO
        [Test]
        public void CopySelection_ComponentResolvesToParentGO()
        {
            var go = MakeGo("Tank");
            var comp = go.AddComponent<BoxCollider>();
            CopyAsMcpRef.CopySelection(new Object[] { comp });

            var clipboard = EditorGUIUtility.systemCopyBuffer;
            StringAssert.Contains("hierarchy:/Tank", clipboard);
        }

        // 8. Shortcut method exists and carries [Shortcut] attribute
        [Test]
        public void CopyRefShortcut_HasShortcutAttribute()
        {
            var method = typeof(CopyAsMcpRef).GetMethod("CopyRefShortcut",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "CopyRefShortcut method must exist");
            var attr = method.GetCustomAttribute<ShortcutAttribute>();
            Assert.IsNotNull(attr, "[Shortcut] attribute must be present");
        }

        // 7. MonoBehaviour: GO ref always present; MonoScript ref included when available.
        // TestDummyMB is a real MonoBehaviour. MonoScript.FromMonoBehaviour returns null
        // for test-assembly scripts not indexed by AssetDatabase, so we assert the GO ref
        // is produced and no exception is thrown (script ref is best-effort).
        [Test]
        public void CopySelection_MonoBehaviour_IncludesGORef()
        {
            var go = MakeGo("Hero");
            go.AddComponent<TestDummyMB>();
            var mb = go.GetComponent<TestDummyMB>();
            Assert.IsNotNull(mb, "AddComponent<TestDummyMB> must succeed — check TestDummyMB is in a runtime assembly");
            CopyAsMcpRef.CopySelection(new Object[] { mb });

            var clipboard = EditorGUIUtility.systemCopyBuffer;
            Assert.IsNotEmpty(clipboard);
            StringAssert.Contains("hierarchy:/Hero", clipboard);
        }
    }
}
