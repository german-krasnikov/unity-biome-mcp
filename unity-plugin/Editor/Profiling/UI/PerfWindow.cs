using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    // Entry point for the Performance EditorWindow. Partial — each tab lives in its own file.
    internal sealed partial class PerfWindow : EditorWindow
    {
        [MenuItem("🧬MCP/Performance", priority = 4)]
        static void ShowWindow() => GetWindow<PerfWindow>($"{BiomeLabel.DisplayName} Profiler");

        private int _activeTab;
        private VisualElement[] _tabContents;
        private Button[] _tabButtons;

        static readonly string[] k_TabNames = { "Performance", "Rendering", "Sessions", "Memory" };

        void CreateGUI()
        {
            var ss = MCPEditorUtils.LoadStyleSheet("Profiling/UI/PerfWindow.uss");
            if (ss != null) rootVisualElement.styleSheets.Add(ss);
            var animSS = MCPEditorUtils.LoadStyleSheet("ArcadeAnim.uss");
            if (animSS != null) rootVisualElement.styleSheets.Add(animSS);
            rootVisualElement.AddToClassList("perf-window");

            var tabBar = new VisualElement();
            tabBar.AddToClassList("perf-tab-bar");
            _tabButtons = new Button[k_TabNames.Length];
            for (int i = 0; i < k_TabNames.Length; i++)
            {
                int idx = i;
                var btn = new Button(() => SwitchTab(idx)) { text = k_TabNames[idx] };
                btn.AddToClassList("perf-tab-btn");
                tabBar.Add(btn);
                _tabButtons[i] = btn;
            }
            rootVisualElement.Add(tabBar);

            _tabContents = new[]
            {
                BuildPerformanceTab(),
                BuildRenderingTab(),
                BuildSessionsTab(),
                BuildMemoryTab(),
            };

            var contentArea = new VisualElement();
            contentArea.AddToClassList("perf-content-area");
            foreach (var tab in _tabContents)
            {
                tab.AddToClassList("perf-tab-content");
                contentArea.Add(tab);
            }
            rootVisualElement.Add(contentArea);

            SwitchTab(0);
        }

        void SwitchTab(int index)
        {
            for (int i = 0; i < _tabContents.Length; i++)
            {
                bool active = i == index;
                _tabContents[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                _tabButtons[i].EnableInClassList("perf-tab-btn--active", active);
            }
            ArcadeAnim.FadeIn(_tabContents[index]);
            _activeTab = index;
        }

        void OnDisable()
        {
            // Unsubscribe auto-capture if it was registered
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
    }
}
