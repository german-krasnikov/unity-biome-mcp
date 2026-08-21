using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class AnnotationMetaWriter
    {
        /// <summary>
        /// Write camera + visible objects metadata alongside the annotated PNG.
        /// Optional annotationsText is appended verbatim (pre-formatted by caller).
        /// Returns the meta file path, or null if write fails.
        /// </summary>
        internal static string Write(string pngPath, string annotationsText = null)
        {
            if (string.IsNullOrEmpty(pngPath)) return null;
            var metaPath = pngPath + ".meta.txt";
            try
            {
                var sb = new StringBuilder();
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    var cam = sv.camera;
                    var pos = cam.transform.position;
                    var rot = cam.transform.eulerAngles;
                    sb.AppendLine($"view=SceneView pos=({pos.x:F1},{pos.y:F1},{pos.z:F1}) rot=({rot.x:F0},{rot.y:F0},{rot.z:F0})");
                }
                else if (EditorApplication.isPlaying)
                {
                    sb.AppendLine("view=GameView");
                }

                AppendVisibleObjects(sb);
                if (!string.IsNullOrEmpty(annotationsText))
                    sb.Append(annotationsText);
                File.WriteAllText(metaPath, sb.ToString(), Encoding.UTF8);
                return metaPath;
            }
            catch { return null; }
        }

        /// <summary>Read the meta sidecar text. Returns "" if not found.</summary>
        internal static string Read(string pngPath)
        {
            if (string.IsNullOrEmpty(pngPath)) return "";
            var metaPath = pngPath + ".meta.txt";
            try { return File.Exists(metaPath) ? File.ReadAllText(metaPath, Encoding.UTF8) : ""; }
            catch { return ""; }
        }

        private static void AppendVisibleObjects(StringBuilder sb)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            var cam = sv.camera;
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int count = 0;
            sb.AppendLine("visible:");
            foreach (var t in transforms)
            {
                if (count >= 10) break;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                var path = ComponentSerializer.GetPath(t.gameObject);
                sb.AppendLine($"  {path} {RefManager.Assign(t.gameObject)}");
                count++;
            }
            if (count == 0) sb.AppendLine("  (none visible)");
        }
    }
}
