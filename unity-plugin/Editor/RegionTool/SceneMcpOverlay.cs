using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.RegionTool
{
    [Overlay(typeof(SceneView), id: "🧬MCP", displayName: "🧬MCP", defaultDisplay: false)]
    internal sealed partial class SceneMcpOverlay : Overlay
    {
        Label  _statsLabel;
        Label  _warnLabel;
        Button _confirmBtn;
        Button _commitBtn;
        Button _cancelBtn;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.minWidth    = 220;
            root.style.paddingLeft = root.style.paddingRight  = 6;
            root.style.paddingTop  = root.style.paddingBottom = 4;

            root.Add(new Label("🧬MCP") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            root.Add(BuildRegionModeRow());
            root.Add(BuildAnnotationTypeRow());
            root.Add(BuildDetailRow());
            root.Add(BuildSnapRow());
            root.Add(BuildLabelField());

            _statsLabel = new Label { style = { marginTop = 6, whiteSpace = WhiteSpace.Normal } };
            root.Add(_statsLabel);

            _warnLabel = new Label("Warning: >128 verts — high token cost")
                { style = { color = Color.red, display = DisplayStyle.None, whiteSpace = WhiteSpace.Normal } };
            root.Add(_warnLabel);

            root.Add(BuildActionRow());
            root.schedule.Execute(UpdateStats).Every(150);
            return root;
        }

        VisualElement BuildActionRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };

            _confirmBtn = new Button { text = "Confirm Point", tooltip = "Place vertex at cursor" };
            _confirmBtn.style.flexGrow = 1;
            _confirmBtn.clicked += OnConfirmClicked;
            row.Add(_confirmBtn);

            _commitBtn = new Button { text = "+ Add to Chat", tooltip = "Commit to chat chip (Enter)" };
            _commitBtn.style.flexGrow = 1;
            _commitBtn.clicked += OnCommitClicked;
            row.Add(_commitBtn);

            _cancelBtn = new Button { text = "Cancel", tooltip = "Cancel drawing (Esc)" };
            _cancelBtn.style.flexGrow = 1;
            _cancelBtn.clicked += OnCancelClicked;
            row.Add(_cancelBtn);

            return row;
        }

        void OnConfirmClicked()
        {
            if (ActiveTool() == typeof(SceneRegionTool))
                SceneRegionTool.ConfirmPointAction?.Invoke();
            else if (ActiveTool() == typeof(SceneAnnotationTool))
                SceneAnnotationTool.ConfirmPointAction?.Invoke();
        }

        void OnCommitClicked()
        {
            if (ActiveTool() == typeof(SceneRegionTool))
                SceneRegionTool.CommitAction?.Invoke();
            else if (ActiveTool() == typeof(SceneAnnotationTool))
                SceneAnnotationTool.CommitAction?.Invoke();
        }

        void OnCancelClicked()
        {
            if (ActiveTool() == typeof(SceneRegionTool))
                SceneRegionTool.CancelAction?.Invoke();
            else if (ActiveTool() == typeof(SceneAnnotationTool))
                SceneAnnotationTool.CancelAction?.Invoke();
        }

        static Type ActiveTool() => ToolManager.activeToolType;
    }
}
