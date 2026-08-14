// Approve & Execute partial — resumes the same session in Agent mode after Ask-mode plan.
// T9: mode switch is now acknowledged by Python relay before dispatching the turn.
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
#if UNITY_INCLUDE_TESTS
        // Seam: notified with the ok value when SetModeAsync's onDone fires — lets tests
        // wait for callback completion without Task.Delay.
        internal static Action<bool> OnModeSetCallback;
#endif

        /// <summary>
        /// One-click plan approval: switches to Agent mode via relay ack, then dispatches
        /// the execute prompt. DispatchTurn is only called if the mode switch succeeded.
        /// </summary>
        internal void ApproveAndExecute()
        {
            var sessionId = _backend?.SessionId;
            var prompt    = ApproveHelper.BuildPromptOrNull(sessionId);
            if (prompt == null) return;

            var rb = _backend as RelayBackend;
            if (rb == null)
            {
                // No relay backend (test seam or null) — original synchronous behavior.
                _agentMode = true;
                _askBtn?.EnableInClassList("mode-toggle-btn--active",   false);
                _agentBtn?.EnableInClassList("mode-toggle-btn--active", true);
                try { DispatchTurn(UserTurnBuilder.Build(prompt), prompt); }
                catch (Exception e)
                { _transcript?.AppendToolChip("Approve failed: " + e.Message, ok: false); }
                return;
            }

            rb.SetModeAsync("agent", ok =>
            {
#if UNITY_INCLUDE_TESTS
                OnModeSetCallback?.Invoke(ok);
#endif
                if (!ok)
                {
                    _transcript?.AppendToolChip(
                        $"{BiomeLabel.Tag} Mode switch failed — cannot execute in agent mode",
                        ok: false);
                    return;
                }
                _agentMode = true;
                _askBtn?.EnableInClassList("mode-toggle-btn--active",   false);
                _agentBtn?.EnableInClassList("mode-toggle-btn--active", true);
                try { DispatchTurn(UserTurnBuilder.Build(prompt), prompt); }
                catch (Exception e)
                { _transcript?.AppendToolChip("Approve failed: " + e.Message, ok: false); }
            });
        }
    }
}
