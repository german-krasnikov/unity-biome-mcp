using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class MCPChatWindowTestIsolationTests :
        UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Scope_ClosesOwnedWindowsAndRestoresCallbacksQueueAndSessionState()
        {
            var backendFactory = MCPChatWindow.BackendFactoryForTest;
            var colorResolver = ChipPillFactory.ColorResolver;
            var addToContext = ChipPillFactory.AddToContextAction;
            var regionCommitted = RegionTool.SceneRegionTool.OnRegionCommitted;
            var annotationCommitted = RegionTool.SceneAnnotationTool.OnAnnotationCommitted;
            var screenshotCaptured = ScreenshotToolbarButton.OnScreenshotCaptured;
            var annotationReady = Annotation.AnnotationEditorWindow.OnAnnotationReady;
            var copyFlash = CopyFlash.ShowAction;
            var pending = ChipPillFactory.PendingChips.ToArray();
            var transcript = CaptureSessionString(PrefKeys.ChatTranscript);
            var backendSession = CaptureSessionString(PrefKeys.ChatBackendSessionId);
            MCPChatWindow window;

            using (MCPChatWindow.BeginTestIsolation(CurrentOwnerId()))
            {
                MCPChatWindow.BackendFactoryForTest = _ => null;
                ChipPillFactory.ColorResolver = _ => "#ffffff";
                ChipPillFactory.AddToContextAction = _ => { };
                RegionTool.SceneRegionTool.OnRegionCommitted = (_, __) => { };
                RegionTool.SceneAnnotationTool.OnAnnotationCommitted = (_, __) => { };
                ScreenshotToolbarButton.OnScreenshotCaptured = _ => { };
                Annotation.AnnotationEditorWindow.OnAnnotationReady = (_, __) => { };
                CopyFlash.ShowAction = () => { };
                ChipPillFactory.PendingChips.Clear();
                ChipPillFactory.PendingChips.Enqueue(
                    new ChipData(ChipKindKeys.Asset, "Assets/test.txt", "test", 0));
                SessionState.SetString(PrefKeys.ChatTranscript, "mutated transcript");
                SessionState.SetString(PrefKeys.ChatBackendSessionId, "mutated session");
                window = ScriptableObject.CreateInstance<MCPChatWindow>();
            }

            Assert.IsTrue(window == null, "The scope must destroy test-owned chat windows.");
            Assert.AreSame(backendFactory, MCPChatWindow.BackendFactoryForTest);
            Assert.AreSame(colorResolver, ChipPillFactory.ColorResolver);
            Assert.AreSame(addToContext, ChipPillFactory.AddToContextAction);
            Assert.AreSame(regionCommitted, RegionTool.SceneRegionTool.OnRegionCommitted);
            Assert.AreSame(annotationCommitted,
                RegionTool.SceneAnnotationTool.OnAnnotationCommitted);
            Assert.AreSame(screenshotCaptured, ScreenshotToolbarButton.OnScreenshotCaptured);
            Assert.AreSame(annotationReady,
                Annotation.AnnotationEditorWindow.OnAnnotationReady);
            Assert.AreSame(copyFlash, CopyFlash.ShowAction);
            CollectionAssert.AreEqual(pending, ChipPillFactory.PendingChips.ToArray());
            AssertSessionString(PrefKeys.ChatTranscript, transcript);
            AssertSessionString(PrefKeys.ChatBackendSessionId, backendSession);
        }

        [Test]
        public void NestedScope_PreservesBaselineWindowAndDestroysOwnedWindow()
        {
            MCPChatWindow.BackendFactoryForTest = _ => null;
            var baseline = ScriptableObject.CreateInstance<MCPChatWindow>();
            MCPChatWindow owned = null;

            try
            {
                using (MCPChatWindow.BeginTestIsolation(CurrentOwnerId()))
                    owned = ScriptableObject.CreateInstance<MCPChatWindow>();

                Assert.IsTrue(baseline != null,
                    "A window that predates the nested scope must survive it.");
                Assert.IsTrue(owned == null,
                    "A window created inside the nested scope must be destroyed.");
            }
            finally
            {
                if (baseline != null) UnityEngine.Object.DestroyImmediate(baseline);
            }
        }

        [Test]
        public void NestedScope_WhenBaselineWasDestroyed_FailsClosedAndUnwinds()
        {
            MCPChatWindow.BackendFactoryForTest = _ => null;
            var baseline = ScriptableObject.CreateInstance<MCPChatWindow>();
            var scope = MCPChatWindow.BeginTestIsolation(CurrentOwnerId());

            UnityEngine.Object.DestroyImmediate(baseline);
            var error = Assert.Throws<AggregateException>(() => scope.Dispose());

            StringAssert.Contains("existed before the test", error.ToString());
            Assert.IsTrue(MCPChatWindow.IsTestIsolationOwnedBy(CurrentOwnerId()),
                "The failed nested scope must still unwind to the base scope.");
        }

        [Test]
        public void NestedScopes_RequireSameOwnerAndReverseDisposalOrder()
        {
            var outer = MCPChatWindow.BeginTestIsolation(CurrentOwnerId());
            var inner = MCPChatWindow.BeginTestIsolation(CurrentOwnerId());

            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            Assert.Throws<InvalidOperationException>(() =>
                MCPChatWindow.BeginTestIsolation("another-test"));

            inner.Dispose();
            outer.Dispose();
            Assert.IsTrue(MCPChatWindow.IsTestIsolationOwnedBy(CurrentOwnerId()),
                "Disposing nested scopes must reveal the fixture's base scope.");
        }

        [Test]
        public void RepairOrphanedTestIsolation_UnwindsScopesAndDestroysOwnedWindows()
        {
            MCPChatWindow.BackendFactoryForTest = _ => null;
            var orphanedScope = MCPChatWindow.BeginTestIsolation(CurrentOwnerId());
            var owned = ScriptableObject.CreateInstance<MCPChatWindow>();

            Assert.IsTrue(MCPChatWindow.RepairOrphanedTestIsolation("next-test"));

            Assert.IsTrue(owned == null,
                "Repair must destroy windows owned by the orphaned scope.");
            Assert.IsFalse(MCPChatWindow.HasActiveTestIsolation,
                "Repair must unwind the entire orphaned scope stack.");
            orphanedScope.Dispose();
        }

        [Test]
        public void OnDisable_UnsubscribesPendingResumeDelayCall()
        {
            MCPChatWindow.BackendFactoryForTest = _ => null;
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            var scheduleMethod = typeof(MCPChatWindow).GetMethod(
                "SchedulePendingTurnResume",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(scheduleMethod);

            try
            {
                scheduleMethod.Invoke(window, null);
                scheduleMethod.Invoke(window, null);
                var scheduled = ResumeCallbacksFor(window);
                Assert.AreEqual(1, scheduled.Length,
                    "Repeated resume requests must share one delayed callback.");

                UnityEngine.Object.DestroyImmediate(window);
                Assert.IsEmpty(ResumeCallbacksFor(window),
                    "Destroyed windows must not retain a delayed resume callback.");
            }
            finally
            {
                foreach (var callback in ResumeCallbacksFor(window))
                    EditorApplication.delayCall -=
                        (EditorApplication.CallbackFunction)callback;
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static (bool exists, string value) CaptureSessionString(string key)
        {
            var sentinel = "UnityMCP.Test.Absent." + Guid.NewGuid().ToString("N");
            var value = SessionState.GetString(key, sentinel);
            return (value != sentinel, value == sentinel ? "" : value);
        }

        private static void AssertSessionString(
            string key,
            (bool exists, string value) expected)
        {
            var sentinel = "UnityMCP.Test.Absent." + Guid.NewGuid().ToString("N");
            var actual = SessionState.GetString(key, sentinel);
            Assert.AreEqual(expected.exists, actual != sentinel, key + " existence");
            if (expected.exists) Assert.AreEqual(expected.value, actual, key + " value");
        }

        private static string CurrentOwnerId()
        {
            var test = TestContext.CurrentContext.Test;
            return string.IsNullOrEmpty(test.ID) ? test.FullName : test.ID;
        }

        private static Delegate[] ResumeCallbacksFor(MCPChatWindow window) =>
            (EditorApplication.delayCall?.GetInvocationList() ?? Array.Empty<Delegate>())
            .Where(callback => ReferenceEquals(callback.Target, window) &&
                callback.Method.Name == "ResumePendingTurnAfterDelay")
            .ToArray();

    }
}
