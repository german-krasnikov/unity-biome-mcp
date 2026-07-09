// Alias Manager EditorWindow — manage PlaytestConfig.aliases via UIElements.
// Menu: MCP / Alias Manager (Shift+Alt+A)
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal class PlaytestAliasWindow : EditorWindow
    {
        PlaytestConfig _config;
        SerializedObject _so;
        ScrollView _scrollView;
        VisualElement _listContainer;
        Label _tokenLabel;
        Label _previewLabel;

        [MenuItem("MCP/Alias Manager #&a")]
        internal static void Open()
        {
            var win = GetWindow<PlaytestAliasWindow>("Alias Manager");
            win.minSize = new Vector2(600, 400);
        }

        void OnEnable()
        {
            _config = LoadOrCreateConfig();
            if (_config != null) _so = new SerializedObject(_config);
        }

        void CreateGUI()
        {
            if (_config == null) { rootVisualElement.Add(new Label("No PlaytestConfig found.")); return; }

            var ss = MCPEditorUtils.LoadStyleSheet("PlaytestAliasWindow.uss");
            if (ss != null) rootVisualElement.styleSheets.Add(ss);
            rootVisualElement.AddToClassList("alias-root");

            rootVisualElement.Add(BuildToolbar());
            BuildDropZone(rootVisualElement);

            // TODO: P1.2 — replace ScrollView+manual loop with ListView { reorderable = true } (see PlaytestComposerWindow.cs:L55-65)
            _scrollView = new ScrollView();
            _scrollView.AddToClassList("alias-scroll-view");
            _listContainer = new VisualElement();
            _scrollView.Add(_listContainer);
            rootVisualElement.Add(_scrollView);

            rootVisualElement.Add(BuildPreviewFoldout());
            Refresh();
        }

        VisualElement BuildToolbar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("alias-toolbar");
            bar.Add(new Button(OnAddClicked) { text = "+ Add" });
            bar.Add(new Button(OnCopyBlockClicked) { text = "Copy defs block" });
            _tokenLabel = new Label();
            _tokenLabel.AddToClassList("alias-token-label");
            bar.Add(_tokenLabel);
            return bar;
        }

        void BuildDropZone(VisualElement root)
        {
            var zone = new Label("Drop GameObject(s) here to auto-create aliases");
            zone.AddToClassList("alias-drop-zone");
            PlaytestDropHelper.AttachMultiDnD(zone, drops => {
                zone.RemoveFromClassList("alias-drop-zone--hover");
                foreach (var (_, go, _) in drops)
                    if (go != null && go.scene.IsValid()) CreateAliasFromGo(go);
            });
            zone.RegisterCallback<DragUpdatedEvent>(e => { zone.AddToClassList("alias-drop-zone--hover"); e.StopPropagation(); });
            zone.RegisterCallback<DragLeaveEvent>(_ => zone.RemoveFromClassList("alias-drop-zone--hover"));
            root.Add(zone);
        }

        VisualElement BuildPreviewFoldout()
        {
            var foldout = new Foldout { text = "DSL Preview", value = false };
            foldout.AddToClassList("alias-preview-foldout");
            _previewLabel = new Label();
            _previewLabel.AddToClassList("alias-preview-label");
            foldout.Add(_previewLabel);
            foldout.Add(new Button(OnExportClicked) { text = "Export .defs" });
            return foldout;
        }

        void Refresh()
        {
            if (_config == null) return;
            _listContainer.Clear();
            _so.UpdateIfRequiredOrScript();

            if (_config.aliases.Count == 0)
            {
                var hint = new Label("No aliases yet — click '+ Add' or drop a GameObject above.");
                hint.AddToClassList("alias-empty-state");
                _listContainer.Add(hint);
            }
            else
            {
                for (int i = 0; i < _config.aliases.Count; i++)
                {
                    void OnCardChanged() { UpdateTokenLabel(); UpdatePreview(); Refresh(); }
                    _listContainer.Add(PlaytestAliasCardBuilder.BuildCard(i, _config, _so, OnCardChanged, DeleteAlias));
                }
            }

            UpdateTokenLabel();
            UpdatePreview();
        }

        void ScrollToBottom() => _scrollView?.schedule
            .Execute(() => _scrollView.scrollOffset = new Vector2(0, float.MaxValue))
            .StartingIn(50);

        void HighlightCard(int idx)
        {
            if (idx < 0 || idx >= _listContainer.childCount) return;
            var card = _listContainer.ElementAt(idx);
            card.AddToClassList("alias-card--new");
            card.schedule.Execute(() => card.RemoveFromClassList("alias-card--new")).StartingIn(800);
        }

        void OnAddClicked()
        {
            _so.UpdateIfRequiredOrScript();
            var arr = _so.FindProperty("aliases");
            arr.InsertArrayElementAtIndex(arr.arraySize);
            var el = arr.GetArrayElementAtIndex(arr.arraySize - 1);
            el.FindPropertyRelative("alias").stringValue        = "";
            el.FindPropertyRelative("type").enumValueIndex      = 0;
            el.FindPropertyRelative("path").stringValue         = "";
            el.FindPropertyRelative("component").stringValue    = "";
            el.FindPropertyRelative("field").stringValue        = "";
            el.FindPropertyRelative("constValue").stringValue   = "";
            _so.ApplyModifiedProperties();
            Refresh();
            ScrollToBottom();
            HighlightCard(_config.aliases.Count - 1);
        }

        void DeleteAlias(int idx)
        {
            _so.UpdateIfRequiredOrScript();
            _so.FindProperty("aliases").DeleteArrayElementAtIndex(idx);
            _so.ApplyModifiedProperties();
            Refresh();
        }

        void CreateAliasFromGo(GameObject go)
        {
            _so.UpdateIfRequiredOrScript();
            var arr = _so.FindProperty("aliases");
            arr.InsertArrayElementAtIndex(arr.arraySize);
            var el = arr.GetArrayElementAtIndex(arr.arraySize - 1);
            el.FindPropertyRelative("alias").stringValue        = PlaytestAliasHelpers.SuggestName(go.name);
            el.FindPropertyRelative("type").enumValueIndex      = 0;
            el.FindPropertyRelative("path").stringValue         = ComponentSerializer.GetPath(go);
            el.FindPropertyRelative("component").stringValue    = "";
            el.FindPropertyRelative("field").stringValue        = "";
            el.FindPropertyRelative("constValue").stringValue   = "";
            _so.ApplyModifiedProperties();
            Refresh();
            ScrollToBottom();
            HighlightCard(_config.aliases.Count - 1);
        }

        void OnExportClicked()
        {
            var path = PlaytestAliasHelpers.ExportToDefs(_config.aliases);
            EditorUtility.DisplayDialog("Exported", $"Written to:\n{path}", "OK");
        }

        void OnCopyBlockClicked()
            => GUIUtility.systemCopyBuffer = PlaytestAliasHelpers.FormatVALBlock(_config.aliases);

        void UpdateTokenLabel()
        {
            if (_tokenLabel == null) return;
            _tokenLabel.text = $"~{PlaytestAliasHelpers.TokenSavingsEstimate(_config.aliases)} tokens/call";
            _tokenLabel.tooltip = "Savings = (full_path - $alias) × 3 uses − block overhead";
        }

        void UpdatePreview()
        {
            if (_previewLabel == null) return;
            var block = PlaytestAliasHelpers.FormatVALBlock(_config.aliases);
            _previewLabel.text = string.IsNullOrEmpty(block) ? "(empty)" : block;
        }

        PlaytestConfig LoadOrCreateConfig()
        {
            var guids = AssetDatabase.FindAssets("t:PlaytestConfig");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<PlaytestConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            const string folder = "Assets/PlaytestDefs";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "PlaytestDefs");
            var cfg = ScriptableObject.CreateInstance<PlaytestConfig>();
            AssetDatabase.CreateAsset(cfg, folder + "/PlaytestConfig.asset");
            AssetDatabase.SaveAssets();
            return cfg;
        }
    }
}
