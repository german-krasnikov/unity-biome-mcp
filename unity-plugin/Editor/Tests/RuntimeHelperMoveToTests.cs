// TDD (DEV-66 Part A): RuntimeHelper.MoveTo's movement callback resolved its
// TaskCompletionSource via EditorApplication.delayCall, which a backgrounded Editor
// (no focus/render frames) does not reliably drain (RELAY-FIX, commit 1bcc90b7).
// completed=true is set synchronously inside the callback, so the sibling
// TimeoutCheck() (update-driven, reliable) unsubscribed without ever calling
// tcs.TrySetResult — a move that actually finished in Unity was reported to the
// caller as a timeout. EditMode only — no Play Mode required (the callback path
// under test does not gate on EditorApplication.isPlaying).
using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperMoveToTests : SceneTestBase
    {
        // Matches FindMoveComponent's (Vector3, Action<bool>) signature convention;
        // invokes the callback synchronously, exactly like a real movement component
        // that finishes in the same frame it starts.
        private sealed class SynchronousMoveComponent : MonoBehaviour
        {
            public void MoveTo(Vector3 target, Action<bool> onArrived) => onArrived(true);
        }

        [Test]
        public async Task MoveTo_ResolvesResult_WhenDelayCallNeverDrains()
        {
            var go = TrackOwnedObject(new GameObject("RuntimeHelperMoveToTarget"));
            go.AddComponent<SynchronousMoveComponent>();
            var path = ComponentSerializer.GetPath(go);

            var before = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();
            var tcs = new TaskCompletionSource<string>();

            RuntimeHelper.MoveTo(path, "1,2,3", timeout: 5f, tcs);

            Assert.IsFalse(tcs.Task.IsCompleted,
                "the movement callback ran synchronously but must defer tcs resolution to the " +
                "next Editor tick, never inline");

            var added = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Except(before).ToArray();
            RegisterCleanup(() =>
            {
                foreach (var d in added) EditorApplication.update -= (EditorApplication.CallbackFunction)d;
            });

            Assert.GreaterOrEqual(added.Length, 1,
                "MoveTo must arm at least one EditorApplication.update subscriber to resolve later");

            // Simulate exactly the next Editor tick for each newly-armed subscriber
            // (the tcs-resolving one-shot and RuntimeHelper's own TimeoutCheck).
            // delayCall is never pumped anywhere in this test.
            foreach (var d in added) d.DynamicInvoke();

            Assert.IsTrue(tcs.Task.IsCompleted,
                "MoveTo result must resolve from an EditorApplication.update tick, not an undrained delayCall");
            var result = await tcs.Task; // already completed — await returns immediately, no block
            Assert.That(result, Does.StartWith("MoveTo arrived"), $"got: {result}");
        }
    }
}
