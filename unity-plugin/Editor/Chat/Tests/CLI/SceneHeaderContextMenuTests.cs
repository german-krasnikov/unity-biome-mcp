// TDD tests for SceneHeaderContextMenu — seam-injectable scene path finder.
// C1-C3: FindScenePath logic + seam injection.
// C4: OnHierarchyItemGUI is null-safe (no Event.current in EditMode).
using System;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SceneHeaderContextMenuTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        Func<int, string> _savedFinder;

        [SetUp]
        public void SetUp() => _savedFinder = SceneHeaderContextMenu.ScenePathFinder;

        [TearDown]
        public void TearDown() => SceneHeaderContextMenu.ScenePathFinder = _savedFinder;

        // C1: non-matching instanceId returns null via real FindScenePath
        [Test]
        public void FindScenePath_NonMatchingId_ReturnsNull()
        {
            // -999999 is extremely unlikely to match any real scene handle
            var result = SceneHeaderContextMenu.FindScenePath(-999999);
            Assert.IsNull(result);
        }

        // C2: seam injection — returns path for matching id
        [Test]
        public void ScenePathFinder_Seam_CanBeInjected()
        {
            SceneHeaderContextMenu.ScenePathFinder = id => id == 42 ? "Assets/A.unity" : null;
            Assert.AreEqual("Assets/A.unity", SceneHeaderContextMenu.ScenePathFinder(42));
        }

        // C3: seam injection — returns null for non-matching id
        [Test]
        public void ScenePathFinder_Seam_ReturnsNullForMismatch()
        {
            SceneHeaderContextMenu.ScenePathFinder = id => id == 42 ? "Assets/A.unity" : null;
            Assert.IsNull(SceneHeaderContextMenu.ScenePathFinder(999));
        }

        // C4: OnHierarchyItemGUI is null-safe when Event.current == null (EditMode)
        [Test]
        public void OnHierarchyItemGUI_NullEvent_DoesNotThrow()
        {
            // Event.current is null outside a GUI loop in EditMode tests.
            Assert.DoesNotThrow(() =>
                SceneHeaderContextMenu.OnHierarchyItemGUI(42, new UnityEngine.Rect(0, 0, 100, 20)));
        }
    }
}
