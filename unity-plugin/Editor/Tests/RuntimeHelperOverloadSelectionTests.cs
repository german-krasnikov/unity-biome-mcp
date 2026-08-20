// TDD — RED first. Overload selection and argument coercion edge cases in
// RuntimeHelper.InvokeMethod: ambiguous same-arg-count, different-arg-count
// disambiguation, zero-arg selection, and int/float coercion round-trips.
// EditMode only — no Play Mode dependency.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperOverloadSelectionTests : SceneTestBase
    {
        // ── Inner component classes — one class per overload pattern ─────────────

        private class AmbiguousBehaviour : MonoBehaviour
        {
            public void SetValue(int v) { }
            public void SetValue(string v) { }
        }

        private class MultiArgBehaviour : MonoBehaviour
        {
            public void SetValue(int v) { }
            public void SetValue(int a, int b) { }
        }

        private class InitOverloadBehaviour : MonoBehaviour
        {
            public bool ZeroArgCalled;
            public void Init() { ZeroArgCalled = true; }
            public void Init(int v) { }
        }

        private class CoerceBehaviour : MonoBehaviour
        {
            public int  IntValue;
            public float FloatValue;
            public void SetInt(int v)     { IntValue   = v; }
            public void SetFloat(float v) { FloatValue = v; }
        }

        // ── 1. Same arg count → Ambiguous ────────────────────────────────────────

        [Test]
        public void InvokeMethod_TwoOverloads_SameArgCount_ThrowsAmbiguous()
        {
            var go = new GameObject("RH_Amb");
            go.AddComponent<AmbiguousBehaviour>();
            TrackOwnedObject(go);
            var path = ComponentSerializer.GetPath(go);

            var ex = Assert.Throws<System.ArgumentException>(() =>
                RuntimeHelper.InvokeMethod(path, "AmbiguousBehaviour", "SetValue", "42"));

            StringAssert.Contains("Ambiguous", ex.Message);
        }

        // ── 2. Different arg count → selects correct overload ────────────────────

        [Test]
        public void InvokeMethod_TwoOverloads_DifferentArgCount_SelectsCorrect()
        {
            var go = new GameObject("RH_MultiArg");
            go.AddComponent<MultiArgBehaviour>();
            TrackOwnedObject(go);
            var path = ComponentSerializer.GetPath(go);

            // "42" → suppliedParts=1 → selects SetValue(int), not SetValue(int,int)
            var result = RuntimeHelper.InvokeMethod(path, "MultiArgBehaviour", "SetValue", "42");

            Assert.AreEqual("void", result);
        }

        // ── 3. Zero args → selects no-arg overload ───────────────────────────────

        [Test]
        public void InvokeMethod_ZeroArgs_SelectsNoArgOverload()
        {
            var go = new GameObject("RH_Init");
            var comp = go.AddComponent<InitOverloadBehaviour>();
            TrackOwnedObject(go);
            var path = ComponentSerializer.GetPath(go);

            RuntimeHelper.InvokeMethod(path, "InitOverloadBehaviour", "Init", null);

            Assert.IsTrue(comp.ZeroArgCalled,
                "Zero-arg Init() should be selected when no args supplied");
        }

        // ── 4. String "99" coerced to int ────────────────────────────────────────

        [Test]
        public void InvokeMethod_ParseArgs_IntCoercedCorrectly()
        {
            var go = new GameObject("RH_IntCoerce");
            var comp = go.AddComponent<CoerceBehaviour>();
            TrackOwnedObject(go);
            var path = ComponentSerializer.GetPath(go);

            var result = RuntimeHelper.InvokeMethod(path, "CoerceBehaviour", "SetInt", "99");

            Assert.AreEqual("void", result);
            Assert.AreEqual(99, comp.IntValue);
        }

        // ── 5. String "3.14" coerced to float ────────────────────────────────────

        [Test]
        public void InvokeMethod_ParseArgs_FloatCoercedCorrectly()
        {
            var go = new GameObject("RH_FloatCoerce");
            var comp = go.AddComponent<CoerceBehaviour>();
            TrackOwnedObject(go);
            var path = ComponentSerializer.GetPath(go);

            var result = RuntimeHelper.InvokeMethod(path, "CoerceBehaviour", "SetFloat", "3.14");

            Assert.AreEqual("void", result);
            Assert.AreEqual(3.14f, comp.FloatValue, 0.001f);
        }
    }
}
