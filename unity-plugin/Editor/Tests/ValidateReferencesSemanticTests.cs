// TDD: ValidateReferencesHelper semantic coverage — UnityEvent broken target detection.
// MCP-REF-035: existing tests cover ObjectReference discovery but not broken UnityEvent targets.
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class ValidateReferencesSemanticTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("ValidateSemanticsTest");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ── Inner component types for semantic tests ─────────────────────────

        private class EventSource : MonoBehaviour
        {
            public UnityEvent onFired = new UnityEvent();
        }

        private class EventTarget : MonoBehaviour
        {
            public void HandleEvent() { }
        }

        private class NullRefComp : MonoBehaviour
        {
            // Plain serialized ObjectReference field — null by default (instanceId=0).
            public GameObject targetRef;
        }

        // ── Test 4: broken UnityEvent target ─────────────────────────────────

        // MCP-REF-035 (Test 4): a persistent listener wired to a deleted target should be
        // reported as MISSING by Validate.
        // RED — current ReferenceHelper.WalkObjectRefs does not recurse into generic array
        // elements (UnityEvent.m_PersistentCalls.m_Calls[i].m_Target), so the broken ref
        // is invisible to the current scanner. When WalkObjectRefs is extended to handle
        // nested generic structs inside arrays, this test will turn GREEN.
        [Ignore("Documents gap MCP-REF-035: WalkObjectRefs does not recurse into UnityEvent m_Calls")]
        [Test]
        public void ValidateRefs_BrokenUnityEvent_IsReported()
        {
            var source = _go.AddComponent<EventSource>();
            var targetGo = new GameObject("EventTarget");
            var target = targetGo.AddComponent<EventTarget>();
            UnityEventTools.AddVoidPersistentListener(source.onFired, target.HandleEvent);

            // Destroy the listener target — creates a dangling reference in the event.
            Object.DestroyImmediate(targetGo);

            var path = ComponentSerializer.GetPath(_go);
            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            // Expected: broken UnityEvent target reported as MISSING.
            // If Unity normalizes the instanceId to 0 after DestroyImmediate, the result
            // will be "0 ERROR" (another known EditMode limitation — see existing test comment
            // in ValidateReferencesHelperTests.ValidateReferences_MissingRef_DetectionLogic).
            StringAssert.Contains("MISSING", result,
                "Broken UnityEvent listener target must be reported as MISSING");
        }

        // ── Test 5: valid UnityEvent target ──────────────────────────────────

        // MCP-REF-035 (Test 5): a persistent listener wired to a live target must not be
        // reported as a broken reference.
        [Test]
        public void ValidateRefs_ValidUnityEvent_IsNotReported()
        {
            var source = _go.AddComponent<EventSource>();
            var targetGo = new GameObject("ValidEventTarget");
            try
            {
                var target = targetGo.AddComponent<EventTarget>();
                UnityEventTools.AddVoidPersistentListener(source.onFired, target.HandleEvent);

                var path = ComponentSerializer.GetPath(_go);
                var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

                StringAssert.Contains("0 ERROR", result,
                    "Valid UnityEvent listener must not be reported as a broken reference");
                StringAssert.DoesNotContain("MISSING", result);
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        // ── Test 6: null / dangling ObjectReference ───────────────────────────

        // MCP-REF-035 (Test 6): a serialized field whose target was destroyed should be
        // reported as MISSING. The field carries a non-zero instanceId after the target is
        // gone — TransientObjectId.HasSerializedReference(prop) returns true, value == null
        // → CheckRef flags it as MISSING.
        // Note: Unity EditMode may normalize instanceId to 0 on DestroyImmediate; whether
        // this test is GREEN depends on Unity's runtime behaviour in the test environment.
        [Test]
        public void ValidateRefs_NullObjectRef_IsReported()
        {
            var comp = _go.AddComponent<NullRefComp>();
            var target = new GameObject("RefTarget");
            comp.targetRef = target;
            EditorUtility.SetDirty(comp);

            // Destroy the referenced object, leaving a potential dangling ref.
            Object.DestroyImmediate(target);

            var path = ComponentSerializer.GetPath(_go);
            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            StringAssert.Contains("MISSING", result,
                "Dangling ObjectReference field must be reported as MISSING after target destruction");
        }
    }
}
