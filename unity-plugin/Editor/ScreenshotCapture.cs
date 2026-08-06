using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    public static class ScreenshotCapture
    {
        public static string Capture(int width = 640, int height = 480, string cameraName = null)
        {
            var pngData = RenderPng(width, height, cameraName);
            return Convert.ToBase64String(pngData);
        }

        public static string CaptureToFile(int width = 640, int height = 480, string cameraName = null, string outputPath = null)
        {
            var pngData = RenderPng(width, height, cameraName);
            return FileOutputHelper.WritePng(pngData, outputPath: outputPath);
        }

        private static byte[] RenderPng(int width, int height, string cameraName)
        {
            // Play Mode: capture composited Game View (includes all URP effects)
            bool isGameCamera = string.IsNullOrEmpty(cameraName) || cameraName == "game";
            if (EditorApplication.isPlaying && isGameCamera)
                return CaptureGameViewPng();

            // Scene View: read already-URP-rendered buffer from scene camera
            if (cameraName != null && cameraName.StartsWith("scene_view"))
                return CaptureSceneViewPng(cameraName, width, height);

            return RenderCameraPng(FindCamera(cameraName), width, height);
        }

        private static void FocusGameView()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            var gameView = EditorWindow.GetWindow(gameViewType, false, null, true);
            if (gameView != null) gameView.Focus();
        }

        private static byte[] CaptureGameViewPng()
        {
            FocusGameView();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try { return tex.EncodeToPNG(); }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        private static byte[] CaptureSceneViewPng(string cameraName, int width, int height)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) throw new ArgumentException("No active Scene View");
            if (cameraName == "scene_view_frame" && Selection.activeGameObject != null)
                sv.FrameSelected();
            sv.Focus();
            sv.Repaint();

            RenderTexture rt = null;
            Texture2D tex = null;
            var prevActive = RenderTexture.active;
            try
            {
                // Render directly into our own RT (avoids black-buffer from unrendered frame)
                rt = new RenderTexture(width, height, 24);
                sv.camera.targetTexture = rt;
                RenderOffscreen(sv.camera);
                sv.camera.targetTexture = null;

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                // If result is all black, fall back to already-rendered URP buffer
                bool allBlack = IsAllBlack(tex);

                if (allBlack)
                {
                    UnityEngine.Object.DestroyImmediate(tex); tex = null;
                    var active = sv.camera.activeTexture;
                    if (active != null)
                    {
                        tex = new Texture2D(active.width, active.height, TextureFormat.RGB24, false);
                        RenderTexture.active = active;
                        tex.ReadPixels(new Rect(0, 0, active.width, active.height), 0, 0);
                        tex.Apply();
                        return tex.EncodeToPNG();
                        // tex is cleaned up by the outer finally block
                    }
                    return RenderCameraPng(sv.camera, width, height);
                }

                return tex.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static byte[] RenderCameraPng(Camera camera, int width, int height)
        {
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            var previousActive = RenderTexture.active;

            try
            {
                renderTexture = new RenderTexture(width, height, 24);
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);

                var previousTarget = camera.targetTexture;
                camera.targetTexture = renderTexture;
                RenderOffscreen(camera);
                camera.targetTexture = previousTarget;

                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                return texture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null) UnityEngine.Object.DestroyImmediate(renderTexture);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Render camera to its current targetTexture, using SRP path when available.</summary>
        internal static void RenderOffscreen(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                try
                {
                    var req = new RenderPipeline.StandardRequest();
                    if (RenderPipeline.SupportsRenderRequest(camera, req))
                    {
                        RenderPipeline.SubmitRenderRequest(camera, req);
                        return;
                    }
                }
                catch { /* fallback to Camera.Render for pipelines that don't support StandardRequest */ }
            }
            camera.Render();
        }

        private static bool IsAllBlack(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var corners = tex.GetPixels(0, 0, Math.Min(4, w), Math.Min(4, h));
            var center  = tex.GetPixels(w / 2 - Math.Min(2, w / 2), h / 2 - Math.Min(2, h / 2),
                                        Math.Min(4, w), Math.Min(4, h));
            float max = 0f;
            foreach (var p in corners) max = Math.Max(max, p.r + p.g + p.b);
            foreach (var p in center)  max = Math.Max(max, p.r + p.g + p.b);
            return max < 0.01f;
        }

        internal static Camera FindCamera(string cameraName)
        {
            if (!string.IsNullOrEmpty(cameraName))
            {
                var cameraObj = ComponentSerializer.FindObject(cameraName);
                if (cameraObj != null)
                {
                    var cam = cameraObj.GetComponent<Camera>();
                    if (cam != null) return cam;
                }
                // Named camera not found — log warning before falling back
                Debug.LogWarning($"[Screenshot] Camera '{cameraName}' not found; falling back to Main Camera.");
            }

            if (Camera.main != null) return Camera.main;

            var allCameras = Camera.allCameras;
            if (allCameras.Length > 0) return allCameras[0];

            // Camera.allCameras excludes inactive — search including inactive as last resort
            var allInclInactive = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allInclInactive.Length > 0) return allInclInactive[0];

            throw new ArgumentException("No camera found in scene");
        }
    }
}
