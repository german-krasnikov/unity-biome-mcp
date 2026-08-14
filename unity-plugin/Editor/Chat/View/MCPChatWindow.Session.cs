// Partial MCPChatWindow — session management (P6: New Session / Clear).
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        /// <summary>
        /// Reset everything for a fresh session. Called from the session menu after confirm.
        /// </summary>
        internal void NewSession() => ResetSession(resumeId: null);

        // Shared reset: kills backend, clears transcript/state, then creates backend with optional resume ID.
        private void ResetSession(string resumeId)
        {
            ReloadGuard.OnTurnFinished();
            _backend?.Stop();
            if (resumeId == null) CreateBackend();
            else                  CreateBackendWithSession(resumeId);
            _transcript?.Clear();
            if (_chipField != null) { _chipField.ClearChips(); _chipField.Text = ""; }
            ReloadGuard.ClearPendingState();
            _sentTextCache.Set("");
            _heightCalc.Reset();
            ResetInputAreaHeight();
            _sessionAllowlist.ClearSession();
            _autoFix.Disarm();
            ResetTurnFlags();
            if (_activity.Phase != ActivityPhase.Idle) { _activity.Done(); OnActivityChanged(); }
            ResetTokenCounters();
            _undoTracker.Invalidate();
        }

        /// <summary>
        /// Build the session menu button for the footer bar.
        /// Click → GenericMenu with "New Session" and "Resume CLI Session..." options.
        /// </summary>
        internal Button BuildSessionMenuButton()
        {
            var btn = new Button(OnSessionMenu) { text = "☰" };
            btn.AddToClassList("chat-btn");
            btn.tooltip = "Session menu";
            return btn;
        }

        private void OnSessionMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("New Session"), false, () =>
            {
                if (EditorUtility.DisplayDialog(
                    "New Session",
                    "Clear the transcript and start a fresh session?\n\nThis kills the current backend process and cannot be undone.",
                    "Clear", "Cancel"))
                    NewSession();
            });

            menu.AddSeparator("");

            var supportsSessionDir = SessionScanner.GetSessionDir(_selectedKind) != null;
            if (supportsSessionDir)
            {
                menu.AddItem(new GUIContent("Resume CLI Session..."), false,
                    () => SessionPickerPopup.Show(_selectedKind, OnCliSessionSelected));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Resume CLI Session... (unsupported)"));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Browse Biome History..."), false, OnBrowseHistory);

            menu.AddSeparator("");
            var hasSession = !string.IsNullOrEmpty(_backend?.SessionId);
            if (hasSession)
                menu.AddItem(new GUIContent("→ CLI  (copy resume command)"), false, OnCopyCliResume);
            else
                menu.AddDisabledItem(new GUIContent("→ CLI  (no session yet)"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Attach Image"), false, OnAttachImage);
            foreach (var p in ToolbarButtonRegistry.All)
            {
                if (!p.MenuOnly) continue;
                var cap = p;
                menu.AddItem(new GUIContent(cap.ButtonLabel), false, () => cap.OnClick(this));
            }

            menu.ShowAsContext();
        }

        private void OnCliSessionSelected(SessionEntry entry) => ResetSession(entry.Id);
    }
}
