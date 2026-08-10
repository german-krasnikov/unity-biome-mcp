// TDD: P-335 — wire_event overload resolution fix.
// Verifies GetMethod(name) AmbiguousMatchException is replaced with structured error.
// EditMode NUnit tests. No TCP required.
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class EventWiringTests : SceneTestBase
    {
        private GameObject _srcGo;
        private GameObject _tgtGo;

        [SetUp]
        public void SetUp()
        {
            // Source: has Button with onClick UnityEvent
            _srcGo = TrackOwnedObject(new GameObject("EW_Source"));
            _srcGo.AddComponent<UnityEngine.UI.Button>();

            // Target: has Animator (SetTrigger overloads = the bug trigger)
            _tgtGo = TrackOwnedObject(new GameObject("EW_Target"));
            _tgtGo.AddComponent<Animator>();
        }

        // ── 1. Overloaded method without disambiguation → structured error ────

        [Test]
        public void WireEvent_AmbiguousMethod_ThrowsWithCandidateList()
        {
            // Animator.SetTrigger has SetTrigger(string) and SetTrigger(int) overloads.
            // Before fix: silently falls back to GameObject as target.
            // After fix: throws ArgumentException with candidate signatures.
            var ex = Assert.Throws<ArgumentException>(() =>
                ObjectManager.WireEvent(
                    "/EW_Source", "Button", "onClick",
                    "/EW_Target", "SetTrigger", "void", null));

            Assert.That(ex.Message, Does.Contain("Ambiguous"),
                "Error must say 'Ambiguous'");
            Assert.That(ex.Message, Does.Contain("SetTrigger"),
                "Error must name the method");
            Assert.That(ex.Message, Does.Contain("parameter_types"),
                "Error must suggest parameter_types to resolve");
        }

        // ── 2. Overloaded method + parameter_types → resolves to Animator ────

        [Test]
        public void WireEvent_AmbiguousMethod_WithParameterTypes_ResolvesAnimator()
        {
            // Disambiguate SetTrigger(string) vs SetTrigger(int) via parameter_types
            var result = ObjectManager.WireEvent(
                "/EW_Source", "Button", "onClick",
                "/EW_Target", "SetTrigger", "void", null,
                targetComponentType: "Animator", parameterTypes: "string");

            // Verify the persistent listener target is Animator, not GameObject
            var so = new SerializedObject(_srcGo.GetComponent<UnityEngine.UI.Button>());
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            Assert.Greater(calls.arraySize, 0, "Listener was not added");
            var call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            var typeName = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;

            Assert.That(typeName, Does.Contain("Animator"),
                $"m_TargetAssemblyTypeName should contain 'Animator', was: {typeName}");
            Assert.That(typeName, Does.Not.Contain("GameObject"),
                "Must not fall back to GameObject");
            // Return string should include readback of target type
            Assert.That(result, Does.Contain("target=UnityEngine.Animator"),
                "Result must include target type for verification");
        }

        // ── 3. Non-ambiguous method resolves correct component ───────────────

        [Test]
        public void WireEvent_SingleOverloadMethod_ResolvesComponentNotGameObject()
        {
            // AudioSource.Pause() is AudioSource-exclusive (Animator has no Pause method)
            // and has exactly one overload — ensures component, not GameObject, is resolved.
            // Note: Stop() is avoided because Unity 6 added Animator.Stop() and AudioSource.Stop(bool).
            _tgtGo.AddComponent<AudioSource>();

            var result = ObjectManager.WireEvent(
                "/EW_Source", "Button", "onClick",
                "/EW_Target", "Pause", "void", null);

            var so = new SerializedObject(_srcGo.GetComponent<UnityEngine.UI.Button>());
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            Assert.Greater(calls.arraySize, 0, "Listener was not added");
            var call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            var typeName = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;

            Assert.That(typeName, Does.Contain("AudioSource"),
                $"Target should be AudioSource, was: {typeName}");
        }

        // ── 4. DEF-3: Registration lambda must forward target_component_type/parameter_types ──

        [Test]
        public void WireEvent_ViaRegistry_ForwardsTargetComponentTypeAndParameterTypes()
        {
            // DEF-3: CommandRouter.Registration lambda was missing these 2 args.
            // Going through CommandRegistry.Execute proves the registration lambda extracts them.
            CommandRegistry.InitDefaults();

            var json = "{\"path\":\"/EW_Source\",\"component\":\"Button\",\"event\":\"onClick\","
                     + "\"target\":\"/EW_Target\",\"method\":\"SetTrigger\","
                     + "\"arg_type\":\"void\",\"arg_value\":null,"
                     + "\"target_component_type\":\"Animator\","
                     + "\"parameter_types\":\"string\"}";

            var result = CommandRegistry.Execute("wire_event", json);

            // If target_component_type/parameter_types were NOT forwarded,
            // this would throw "Ambiguous" (SetTrigger has string+int overloads).
            // Success means the args flowed through the registration lambda.
            var so = new SerializedObject(_srcGo.GetComponent<UnityEngine.UI.Button>());
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            Assert.Greater(calls.arraySize, 0, "Listener was not added");
            var call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            var typeName = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;

            Assert.That(typeName, Does.Contain("Animator"),
                "Registration must forward target_component_type to resolve Animator");
        }

        // ── 5. target_component_type narrows which component is searched ──────

        [Test]
        public void WireEvent_TargetComponentType_NarrowsToSpecifiedComponent()
        {
            // Target has Animator + AudioSource; request Pause() on AudioSource only.
            // Note: Stop() is avoided because Unity 6 added AudioSource.Stop(bool) overload.
            _tgtGo.AddComponent<AudioSource>();

            var result = ObjectManager.WireEvent(
                "/EW_Source", "Button", "onClick",
                "/EW_Target", "Pause", "void", null,
                targetComponentType: "AudioSource");

            var so = new SerializedObject(_srcGo.GetComponent<UnityEngine.UI.Button>());
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            var call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            var typeName = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;

            Assert.That(typeName, Does.Contain("AudioSource"));
        }
    }
}
