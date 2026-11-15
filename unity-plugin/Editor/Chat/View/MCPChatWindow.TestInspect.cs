using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Public test-inspection API for live test harness (execute_code).
    /// Avoids reflection in submitted snippets so security scanner stays strict.</summary>
    public partial class MCPChatWindow
    {
        // ── State reads ──────────────────────────────────────────────────────
        public bool   TestAgentMode          => _agentMode;
        public string TestInputValue         => _input?.value ?? "null";
        public string TestBackendTypeName    => _backend?.GetType().Name ?? "null";
        public bool   TestBackendIsRunning   => _backend?.IsRunning ?? false;
        public string TestActivityPhase      => _turn?.Phase.ToString() ?? "null";
        public string TestAskBtnState        =>
            _askBtn?.ClassListContains("mode-toggle-btn--active") == true ? "active" : "inactive";
        public string TestTranscriptTypeName => _transcript?.GetType().Name ?? "null";
        public string TestModelDropdown      => _modelDropdown?.value ?? "null";
        public int    TestModelDropdownCount => _modelDropdown?.choices?.Count ?? 0;
        public string TestAgentDropdown      => _agentDropdown?.value ?? "null";

        // ── Actions ──────────────────────────────────────────────────────────
        public void   TestSetMode(bool a)    => SetMode(a);
        public string TestSetInput(string v) { if (_input != null) _input.value = v; return _input?.value ?? "null"; }
        public void   TestStopBackend()      => _backend?.Stop();

        // ── Static helpers ───────────────────────────────────────────────────
        public static string TestIsImageExtension(string ext) => IsImageExtension(ext).ToString();
    }
}
