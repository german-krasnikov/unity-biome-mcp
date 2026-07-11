// Amber "Restore" button injected into transcript after each turn (F6). Chat asmdef only.
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public static class RestoreButton
    {
        /// <summary>
        /// Creates an amber "Restore" button tied to <paramref name="tracker"/>.
        /// Only the LAST turn's button stays enabled — captured generation check
        /// disables old buttons the moment a new turn starts (architecture §risk #6).
        /// </summary>
        public static VisualElement Create(TurnUndoTracker tracker, Action onRestored = null)
        {
            // Capture the index of the turn this button belongs to.
            int capturedTurnIndex = tracker.TurnCount - 1;

            Button btn = null;
            btn = new Button(() =>
            {
                tracker.RestoreFromIndex(capturedTurnIndex);
                btn.SetEnabled(false);
                btn.text = "Restored";
                onRestored?.Invoke();
            });

            btn.text    = "Restore";
            btn.tooltip = "Revert this turn's scene changes";
            btn.AddToClassList("chat-btn");
            btn.AddToClassList("chat-btn--restore");

            // Refresh enabled state on geometry changes (covers repaint-equivalent).
            btn.RegisterCallback<GeometryChangedEvent>(_ => RefreshEnabled(btn, tracker, capturedTurnIndex));
            // Also refresh when the panel ticks (schedule-based refresh).
            btn.schedule.Execute(() => RefreshEnabled(btn, tracker, capturedTurnIndex)).Every(200);

            RefreshEnabled(btn, tracker, capturedTurnIndex);
            return btn;
        }

        /// <summary>
        /// Enabled when the tracker still has the captured turn (index in range).
        /// Exposed as internal static so tests can assert without a live UIElements panel.
        /// </summary>
        internal static void RefreshEnabled(Button btn, TurnUndoTracker tracker, int capturedTurnIndex)
        {
            var shouldEnable =
                tracker.HasRestorableGroup &&
                capturedTurnIndex < tracker.TurnCount;
            btn.SetEnabled(shouldEnable);
        }
    }
}
