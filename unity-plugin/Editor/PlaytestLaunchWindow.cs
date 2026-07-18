// PlaytestLaunchWindow — run .playtest files from Edit Mode or Play Mode.
// Menu: MCP / Playtest Launcher
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal class PlaytestLaunchWindow : EditorWindow
    {
        [MenuItem("MCP/Playtest Launcher")]
        internal static void Open() => GetWindow<PlaytestLaunchWindow>("Playtest Launcher");

        string[] _files = Array.Empty<string>();
        int _selected = -1;
        float _timeout = 120f;
        Label _statusLabel;
        ListView _listView;

        void OnEnable()
        {
            // Clear stale pending key left from a previous crash
            if (!EditorApplication.isPlaying)
                EditorPrefs.DeleteKey(PlaytestAutoLaunch.PrefKey);
            Refresh();
        }

        void Refresh()
        {
            var folder = PlaytestFileHelper.EnsurePlaytestsFolder();
            _files = Directory.GetFiles(folder, "*.playtest", SearchOption.AllDirectories);
            Array.Sort(_files);
            _selected = -1;
            if (_listView != null) _listView.itemsSource = _files;
            _listView?.Rebuild();
            if (_statusLabel != null) _statusLabel.text = $"{_files.Length} file(s) found";
        }

        void CreateGUI()
        {
            // Toolbar
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            toolbar.Add(new Button(Refresh) { text = "Refresh" });
            _statusLabel = new Label($"{_files.Length} file(s) found");
            _statusLabel.style.flexGrow = 1;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            toolbar.Add(_statusLabel);
            rootVisualElement.Add(toolbar);

            // File list
            _listView = new ListView(_files) { style = { flexGrow = 1 } };
            _listView.makeItem = () => new Label();
            _listView.bindItem = (el, i) => ((Label)el).text = Path.GetFileName(_files[i]);
            _listView.selectionChanged += _ => _selected = _listView.selectedIndex;
            rootVisualElement.Add(_listView);

            // Footer: timeout + run button
            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var timeoutField = new FloatField("Timeout (s)") { value = _timeout };
            timeoutField.style.flexGrow = 1;
            timeoutField.RegisterValueChangedCallback(e => _timeout = e.newValue);
            footer.Add(timeoutField);
            footer.Add(new Button(OnRunClicked) { text = "Run" });
            rootVisualElement.Add(footer);
        }

        void OnRunClicked()
        {
            if (_selected < 0 || _selected >= _files.Length) return;
            var path = _files[_selected];

            if (EditorApplication.isPlaying)
            {
                var script = File.ReadAllText(path);
                var tcs = new TaskCompletionSource<string>();
                tcs.Task.ContinueWith(t =>
                    EditorApplication.delayCall += () =>
                    {
                        if (_statusLabel != null) _statusLabel.text = $"Done: {t.Result}";
                    }, TaskScheduler.Default);
                PlaytestRunner.Run(script, _timeout, tcs);
                if (_statusLabel != null) _statusLabel.text = "Running…";
            }
            else
            {
                EditorPrefs.SetString(PlaytestAutoLaunch.PrefKey, path);
                EditorPrefs.SetFloat(PlaytestAutoLaunch.TimeoutKey, _timeout);
                EditorApplication.isPlaying = true;
                if (_statusLabel != null) _statusLabel.text = "Entering Play Mode…";
            }
        }
    }

    [InitializeOnLoad]
    internal static class PlaytestAutoLaunch
    {
        internal const string PrefKey = "UnityMCP.PendingPlaytestPath";
        internal const string TimeoutKey = "UnityMCP.PendingPlaytestTimeout";

        static PlaytestAutoLaunch()
            => EditorApplication.playModeStateChanged += OnStateChanged;

        static void OnStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!TryGetPendingTest(out var path, out var timeout)) return;
            if (!File.Exists(path)) return;

            var script = File.ReadAllText(path);
            var fileName = Path.GetFileName(path);
            var tcs = new TaskCompletionSource<string>();
            tcs.Task.ContinueWith(t =>
                EditorApplication.delayCall += () =>
                    Debug.Log($"[Playtest] {fileName}: {t.Result}"),
                TaskScheduler.Default);
            PlaytestRunner.Run(script, timeout, tcs);
        }

        /// <summary>Reads + clears pending EditorPrefs. Testable without Play Mode.</summary>
        internal static bool TryGetPendingTest(out string path, out float timeout)
        {
            path = EditorPrefs.GetString(PrefKey, "");
            timeout = EditorPrefs.GetFloat(TimeoutKey, 120f);
            if (string.IsNullOrEmpty(path)) return false;
            EditorPrefs.DeleteKey(PrefKey);
            EditorPrefs.DeleteKey(TimeoutKey);
            return true;
        }
    }
}
