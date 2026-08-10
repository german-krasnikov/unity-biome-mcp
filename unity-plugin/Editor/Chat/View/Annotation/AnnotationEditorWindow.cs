using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal sealed class AnnotationEditorWindow : EditorWindow
    {
        private AnnotationToolState _toolState;
        private AnnotationHistory   _history;
        private AnnotationToolbar   _toolbar;
        private AnnotationCanvas    _canvas;
        private byte[]              _originalPng;
        private CameraSnapshot      _cameraSnapshot;

        /// <summary>Set by MCPChatWindow.OnEnable. Invoked with (storedPath, displayName) on Send.</summary>
        internal static Action<string, string> OnAnnotationReady;

        internal static void Open(string screenshotPath)
        {
            if (string.IsNullOrEmpty(screenshotPath) || !File.Exists(screenshotPath))
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Annotation: screenshot file not found");
                return;
            }
            var w = GetWindow<AnnotationEditorWindow>("Annotate Screenshot");
            w.minSize = new Vector2(500, 400);
            w.Init(screenshotPath);
        }

        private void Init(string path)
        {
            _originalPng = File.ReadAllBytes(path);
            _toolState = new AnnotationToolState();
            _history = new AnnotationHistory();
            _toolbar = new AnnotationToolbar(_toolState, _history);
            _canvas = new AnnotationCanvas(_toolState, _history);

            var tex = new Texture2D(2, 2);
            tex.LoadImage(_originalPng);
            _canvas.SetBackground(tex);
            _cameraSnapshot = CaptureCameraSnapshot();
        }

        private static CameraSnapshot CaptureCameraSnapshot()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) return new CameraSnapshot(sv.camera);
            if (EditorApplication.isPlaying && Camera.main != null)
                return new CameraSnapshot(Camera.main);
            return default;
        }

        private void OnGUI()
        {
            if (_toolbar == null || _canvas == null)
            {
                EditorGUILayout.HelpBox("No screenshot loaded. Use Snap or Annotate button first.", MessageType.Info);
                return;
            }

            _toolbar.HandleHotkeys(Event.current);
            _toolbar.Draw();

            const float toolbarHeight = 46f; // 2 toolbar rows (21px each) + 4px gap
            var canvasRect = new Rect(0, toolbarHeight, position.width, position.height - toolbarHeight);
            _canvas.Draw(canvasRect);

            if (_toolbar.SendClicked)
                SendToChat();

            if (_toolbar.ClearClicked)
            {
                _history.Clear();
                Repaint();
            }

            if (Event.current.type == EventType.MouseDrag || Event.current.type == EventType.MouseMove)
                Repaint();
        }

        private void SendToChat()
        {
            if (_originalPng == null) return;

            var composited = AnnotationCompositor.Composite(_originalPng, _history.Active);
            if (composited == null)
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Annotation composite failed");
                return;
            }

            var ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var storedPath = ImageAttachmentStore.ImportBytes(composited, baseName: $"annotated_{ts}");
            if (string.IsNullOrEmpty(storedPath)) return;

            string annotationsText = null;
            if (_toolbar.CoordsEnabled)
            {
                var hits = AnnotationRaycaster.RaycastAll(_cameraSnapshot, _history.Active);
                annotationsText = AnnotationRaycaster.FormatAnnotations(hits);
            }
            AnnotationMetaWriter.Write(storedPath, annotationsText);

            var displayName = Path.GetFileNameWithoutExtension(storedPath);
            OnAnnotationReady?.Invoke(storedPath, displayName);
            Close();
        }

        private void OnDestroy()
        {
            _canvas?.Dispose();
        }
    }
}
