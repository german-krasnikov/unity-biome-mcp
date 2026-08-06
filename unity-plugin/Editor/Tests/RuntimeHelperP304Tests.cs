// TDD P-304: @scene path argument syntax and overload ambiguity diagnostics in INVOKE.
// EditMode only — no TCP, no Play Mode required.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperP304Tests : SceneTestBase
    {
        private class OverloadTestBehaviour : MonoBehaviour
        {
            // Ambiguous: two overloads with same param count (ParamScore=1 each)
            public string AmbigFoo(int x) => $"int:{x}";
            public string AmbigFoo(string s) => $"str:{s}";

            // Unambiguous: different param counts (score 1 vs 2)
            public string UnambigBar(int x) => $"one:{x}";
            public string UnambigBar(int x, int y) => $"two:{x},{y}";
        }

        private class ReceiverBehaviour : MonoBehaviour
        {
            // Receives a component reference via @scene path syntax
            public string IdentifyCollider(BoxCollider col) => col != null ? "found" : "null";
        }

        // ── Overload ambiguity diagnostics ────────────────────────────────────

        [Test]
        public void InvokeMethod_AmbiguousOverload_ThrowsWithCandidateList()
        {
            var go = TrackOwnedObject(new GameObject("RHP304_Overload"));
            go.AddComponent<OverloadTestBehaviour>();
            var path = ComponentSerializer.GetPath(go);

            var ex = Assert.Throws<ArgumentException>(
                () => RuntimeHelper.InvokeMethod(path, "OverloadTestBehaviour", "AmbigFoo", "42"));
            Assert.That(ex.Message, Does.Contain("Ambiguous"));
            Assert.That(ex.Message, Does.Contain("AmbigFoo"));
        }

        [Test]
        public void InvokeMethod_UnambiguousOverload_UniqueParamCount_Succeeds()
        {
            var go = TrackOwnedObject(new GameObject("RHP304_Unambig"));
            go.AddComponent<OverloadTestBehaviour>();
            var path = ComponentSerializer.GetPath(go);

            // 1 arg → unique match on UnambigBar(int x)
            var result = RuntimeHelper.InvokeMethod(path, "OverloadTestBehaviour", "UnambigBar", "5");
            Assert.That(result, Is.EqualTo("one:5"));
        }

        // ── @scene path arg syntax (tested via InvokeMethod public API) ───────

        [Test]
        public void InvokeMethod_AtPathArg_ResolvesTargetComponent()
        {
            var targetGo = TrackOwnedObject(new GameObject("RHP304_Target"));
            targetGo.AddComponent<BoxCollider>();
            var receiverGo = TrackOwnedObject(new GameObject("RHP304_Receiver"));
            receiverGo.AddComponent<ReceiverBehaviour>();

            var receiverPath = ComponentSerializer.GetPath(receiverGo);
            var targetPath = ComponentSerializer.GetPath(targetGo);

            var result = RuntimeHelper.InvokeMethod(
                receiverPath, "ReceiverBehaviour", "IdentifyCollider",
                $"@{targetPath}|BoxCollider");
            Assert.That(result, Is.EqualTo("found"));
        }

        [Test]
        public void InvokeMethod_AtPathArgMissingObject_ThrowsNotFound()
        {
            var receiverGo = TrackOwnedObject(new GameObject("RHP304_Recv2"));
            receiverGo.AddComponent<ReceiverBehaviour>();
            var receiverPath = ComponentSerializer.GetPath(receiverGo);

            var ex = Assert.Throws<ArgumentException>(
                () => RuntimeHelper.InvokeMethod(
                    receiverPath, "ReceiverBehaviour", "IdentifyCollider",
                    "@/NonExistent_P304|BoxCollider"));
            Assert.That(ex.Message, Does.Contain("not found").IgnoreCase);
        }
    }
}
