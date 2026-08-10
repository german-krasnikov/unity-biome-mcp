// Captures a Unity view screenshot and stores it in Library/MCPChat/Attachments/.
// CaptureFunc seam allows injection in tests — avoids real Editor API calls.
using System;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    internal static class ScreenshotService
    {
        // Test seam: injectable capture function (width, height, cameraName) → png bytes
        internal static Func<int, int, string, byte[]> CaptureFunc;

        /// <summary>Capture current view and return the stored file path, or null on failure.</summary>
        internal static string Capture(int width = 640, int height = 480)
        {
            try
            {
                var camera = EditorApplication.isPlaying ? "game" : "scene_view";
                byte[] pngBytes;

                if (CaptureFunc != null)
                    pngBytes = CaptureFunc(width, height, camera);
                else
                {
                    var b64 = ScreenshotCapture.Capture(width, height, camera);
                    pngBytes = Convert.FromBase64String(b64);
                }

                if (pngBytes == null || pngBytes.Length == 0)
                {
                    Debug.LogWarning($"{BiomeLabel.Tag} Screenshot capture returned empty data");
                    return null;
                }

                var ts   = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var path = ImageAttachmentStore.ImportBytes(pngBytes, baseName: $"screenshot_{ts}");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Screenshot failed: {e.Message}");
                return null;
            }
        }
    }
}
