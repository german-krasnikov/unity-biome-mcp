// DEV-66: a delayCall replacement for one-shot deferred work. A backgrounded Editor
// (no focus/render frames — this plugin's normal MCP-driven posture) keeps pumping
// EditorApplication.update but does not reliably drain EditorApplication.delayCall
// (RELAY-FIX, commit 1bcc90b7) — so a callback scheduled via delayCall can silently
// never run for the whole session. Schedule() instead fires on the next update tick
// and self-unsubscribes, regardless of window focus.
using System;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class EditorTickOnce
    {
        /// <summary>Runs <paramref name="action"/> once on the next EditorApplication.update tick.</summary>
        internal static void Schedule(Action action)
        {
            if (action == null) return;
            new OneShot(action).Arm();
        }

        private sealed class OneShot
        {
            private readonly Action _action;

            internal OneShot(Action action) => _action = action;

            internal void Arm() => EditorApplication.update += Tick;

            private void Tick()
            {
                // Unsubscribe before invoking — a throwing action can never leave this
                // handler re-armed for the next tick.
                EditorApplication.update -= Tick;
                _action();
            }
        }
    }
}
