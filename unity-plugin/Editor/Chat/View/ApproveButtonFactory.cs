// Factory for the one-shot "Approve & Execute" button injected into the transcript.
// Kept separate from ChatTranscript to stay under the 200-line guard.
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public static class ApproveButtonFactory
    {
        /// <summary>
        /// Creates a one-shot approve button.
        /// The button removes itself from the hierarchy after the first click.
        /// <c>btn.userData</c> holds the click <see cref="Action"/> for test use.
        /// </summary>
        internal static Button MakeButton(VisualElement container, Action onApprove)
        {
            Button btn = null;
            Action clickAction = () =>
            {
                btn?.RemoveFromHierarchy();
                try { onApprove?.Invoke(); }
                catch (Exception e) { UnityEngine.Debug.LogError($"{BiomeLabel.Tag} Approve error: " + e.Message); }
            };
            btn = new Button(clickAction) { text = "Approve & Execute" };
            btn.AddToClassList("approve-btn");
            btn.userData = clickAction;   // seam for tests
            return btn;
        }

        /// <summary>
        /// Conditionally appends an approve button to <paramref name="container"/>
        /// only when in Ask mode with a valid session.
        /// </summary>
        internal static void MaybeAppend(VisualElement container, bool agentMode,
            string sessionId, Action onApprove)
        {
            if (agentMode || string.IsNullOrEmpty(sessionId)) return;
            container.Add(MakeButton(container, onApprove));
        }
    }
}
