// TDD (DEV-66): EditorTickOnce is the delayCall replacement shared by TestRunService
// and TestRunObserver post-reload recovery scheduling — a backgrounded Editor (no
// focus/render frames) keeps pumping EditorApplication.update but does not reliably
// drain delayCall (RELAY-FIX, commit 1bcc90b7).
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class EditorTickOnceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Schedule_RunsActionExactlyOnce_AcrossTwoTicks()
        {
            var before = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();
            var callCount = 0;

            EditorTickOnce.Schedule(() => callCount++);

            var added = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Except(before).ToArray();
            Assert.AreEqual(1, added.Length, "Schedule must add exactly one EditorApplication.update subscriber");

            added[0].DynamicInvoke(); // simulate the next Editor tick firing this handler

            Assert.AreEqual(1, callCount, "the action must fire on the next Editor tick");
            // Proves "a second tick cannot re-fire this handler" without invoking the
            // shared EditorApplication.update itself: that delegate carries every other
            // live subscriber too (e.g. the durable test-run driver that steps this very
            // suite), so firing it from inside a running test reenters that pump and can
            // corrupt its state instead of exercising EditorTickOnce in isolation.
            Assert.IsFalse(
                (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>()).Contains(added[0]),
                "the handler must unsubscribe itself once it has fired, so no later tick can invoke it again");
        }

        [Test]
        public void Schedule_NullAction_DoesNotThrowOrSubscribe()
        {
            var before = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();

            Assert.DoesNotThrow(() => EditorTickOnce.Schedule(null));

            var after = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();
            Assert.AreEqual(before.Length, after.Length, "a null action must not add an update subscriber");
        }
    }
}
