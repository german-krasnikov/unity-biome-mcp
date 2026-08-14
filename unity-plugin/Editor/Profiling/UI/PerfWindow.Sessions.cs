using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor
{
    internal sealed partial class PerfWindow
    {
        private void AnalyzeSelectedInChat()
        {
            if (_selectedSessions.Count > 1) { _compareVerdict.text = "Select one session to analyze."; return; }
            string sid = _selectedSessions.Count == 1
                ? System.Linq.Enumerable.First(_selectedSessions)
                : ProfileContextSerializer.GetLatestSessionId();
            if (sid == null) { _compareVerdict.text = "No session to analyze."; return; }
            ProfileContextBridge.RequestAnalyzeInChat(sid);
        }

        private VisualElement _sessionList;
        private Label _compareVerdict;
        private readonly HashSet<string> _selectedSessions = new();

        private VisualElement BuildSessionsTab()
        {
            var root = new VisualElement();
            root.AddToClassList("perf-section");

            root.Add(MakeSectionLabel("Recorded Sessions"));

            _sessionList = new VisualElement();
            _sessionList.AddToClassList("perf-session-list");
            root.Add(_sessionList);

            // Refresh / Compare controls
            var ctrlRow = new VisualElement();
            ctrlRow.AddToClassList("perf-ctrl-row");
            var refreshBtn = new Button(RefreshSessions) { text = "Refresh" };
            refreshBtn.AddToClassList("perf-btn");
            ctrlRow.Add(refreshBtn);
            var compareBtn = new Button(CompareSelected) { text = "Compare Selected" };
            compareBtn.AddToClassList("perf-btn");
            ctrlRow.Add(compareBtn);
            var analyzeBtn = new Button(AnalyzeSelectedInChat) { text = "Analyze in Chat" };
            analyzeBtn.AddToClassList("perf-btn");
            ctrlRow.Add(analyzeBtn);
            root.Add(ctrlRow);

            // Verdict panel
            _compareVerdict = new Label("");
            _compareVerdict.AddToClassList("perf-verdict-panel");
            root.Add(_compareVerdict);

            // Auto-capture toggle
            var autoToggle = new Toggle("Auto-capture on Play");
            autoToggle.AddToClassList("perf-auto-toggle");
            autoToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    EditorApplication.playModeStateChanged += OnPlayModeChanged;
                else
                    EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            });
            root.Add(autoToggle);

            RefreshSessions();
            return root;
        }

        private void RefreshSessions()
        {
            _sessionList.Clear();
            string raw = ProfileRecorder.Dispatch("list_sessions", "{}");
            if (raw == "no sessions") return;

            foreach (string line in raw.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                // First token is the session ID: "p1 12:34:56 Burst 300f 5.0s fps=60"
                string sid = line.Trim().Split(' ')[0];
                _sessionList.Add(MakeSessionRow(sid, line.Trim()));
            }
        }

        private VisualElement MakeSessionRow(string sid, string summary)
        {
            var row = new VisualElement();
            row.AddToClassList("perf-session-row");

            var check = new Toggle { value = _selectedSessions.Contains(sid) };
            check.AddToClassList("perf-session-check");
            check.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) _selectedSessions.Add(sid);
                else _selectedSessions.Remove(sid);
            });
            row.Add(check);

            var lbl = new Label(summary);
            lbl.AddToClassList("perf-session-label");
            row.Add(lbl);
            return row;
        }

        private void CompareSelected()
        {
            if (_selectedSessions.Count < 2)
            {
                _compareVerdict.text = "Select 2 sessions to compare.";
                return;
            }
            string[] ids = new string[_selectedSessions.Count];
            _selectedSessions.CopyTo(ids);
            string args = $"{{\"compare_with\":\"{ids[0]}\",\"session\":\"{ids[1]}\"}}";
            string result = ProfileRecorder.Dispatch("compare", args);
            _compareVerdict.text = result;

            string verdict = result.Contains("REGRESSED") ? "regressed"
                : result.Contains("IMPROVED") ? "improved" : "stable";
            _compareVerdict.RemoveFromClassList("perf-verdict--regressed");
            _compareVerdict.RemoveFromClassList("perf-verdict--improved");
            _compareVerdict.RemoveFromClassList("perf-verdict--stable");
            _compareVerdict.AddToClassList($"perf-verdict--{verdict}");
            ArcadeAnim.PulseOnce(_compareVerdict);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                ProfileRecorder.Dispatch("start", "{\"mode\":\"burst\"}");
            else if (state == PlayModeStateChange.ExitingPlayMode)
                ProfileRecorder.Dispatch("stop", "{}");
        }
    }
}
