// Issue #54: UIPanelHost compat layer — UIDocument (6.0) and PanelRenderer (6.4+).
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIPanelHostTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Cycle 1 — ResolveRoot with UIDocument: rootVisualElement may or may not be null
        // in Edit Mode depending on Unity version and editor state.
        [Test]
        public void ResolveRoot_WithUIDocument_EitherReturnsRootOrThrowsWithErrPrefix()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_1"));
            var doc = go.AddComponent<UIDocument>();
            if (doc.rootVisualElement == null)
            {
                var ex = Assert.Throws<InvalidOperationException>(() => UIPanelHost.ResolveRoot(go));
                Assert.That(ex.Message, Does.StartWith("err:"));
                Assert.That(ex.Message, Does.Contain("rootVisualElement is null"));
            }
            else
            {
                var root = UIPanelHost.ResolveRoot(go);
                Assert.That(root, Is.Not.Null);
                Assert.That(root, Is.SameAs(doc.rootVisualElement));
            }
        }

        // Cycle 2 — ResolveRoot with no host: ArgumentException with err: prefix
        [Test]
        public void ResolveRoot_NoHost_ThrowsArgumentExceptionWithErrPrefix()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_2"));
            var ex = Assert.Throws<ArgumentException>(() => UIPanelHost.ResolveRoot(go));
            Assert.That(ex.Message, Does.StartWith("err:"));
            Assert.That(ex.Message, Does.Contain("no UIDocument or PanelRenderer"));
        }

        // Cycle 3a — HasHost with UIDocument returns true
        [Test]
        public void HasHost_WithUIDocument_ReturnsTrue()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_3a"));
            go.AddComponent<UIDocument>();
            Assert.IsTrue(UIPanelHost.HasHost(go));
        }

        // Cycle 3b — HasHost with no host returns false
        [Test]
        public void HasHost_NoHost_ReturnsFalse()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_3b"));
            Assert.IsFalse(UIPanelHost.HasHost(go));
        }

        // Cycle 4a — HostLabel with UIDocument returns "[UIDocument]"
        [Test]
        public void HostLabel_WithUIDocument_ReturnsUIDocumentLabel()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_4a"));
            go.AddComponent<UIDocument>();
            Assert.That(UIPanelHost.HostLabel(go), Is.EqualTo("[UIDocument]"));
        }

        // Cycle 4b — HostLabel with no host returns "[no UI host]"
        [Test]
        public void HostLabel_NoHost_ReturnsNoUIHostLabel()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_4b"));
            Assert.That(UIPanelHost.HostLabel(go), Is.EqualTo("[no UI host]"));
        }

        // TryResolveRoot — returns null instead of throwing
        [Test]
        public void TryResolveRoot_NoHost_ReturnsNull()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_TryNull"));
            var result = UIPanelHost.TryResolveRoot(go);
            Assert.IsNull(result);
        }

#if UNITY_6000_5_OR_NEWER
        // Verify reflection cache is non-null on 6.4+ — if null, PanelRenderer paths silently fail.
        [Test]
        public void ReflectionCache_IsNotNull_On_6000_4()
        {
            var fi = typeof(UIPanelHost).GetField("_rootProp",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(fi, "_rootProp field must exist");
            var val = fi.GetValue(null);
            Assert.IsNotNull(val, "PropertyInfo must be non-null — PanelRenderer.rootVisualElement API changed");
        }

        [Test]
        public void HasHost_WithPanelRenderer_ReturnsTrue()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_PR"));
            go.AddComponent<PanelRenderer>();
            Assert.IsTrue(UIPanelHost.HasHost(go));
        }

        [Test]
        public void HostLabel_WithPanelRenderer_ReturnsPanelRendererLabel()
        {
            var go = TrackOwnedObject(new GameObject("UIPanelHostTest_PRLabel"));
            go.AddComponent<PanelRenderer>();
            Assert.That(UIPanelHost.HostLabel(go), Is.EqualTo("[PanelRenderer]"));
        }
#endif
    }
}
