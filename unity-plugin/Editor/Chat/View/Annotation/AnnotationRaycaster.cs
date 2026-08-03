using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Annotation
{
    /// <summary>Camera state frozen at screenshot capture time.</summary>
    internal readonly struct CameraSnapshot
    {
        internal readonly Matrix4x4 WorldToCamera;
        internal readonly Matrix4x4 Projection;
        internal readonly Vector3 Position;
        internal readonly bool IsValid;

        internal CameraSnapshot(Camera cam)
        {
            if (cam == null) { this = default; return; }
            WorldToCamera = cam.worldToCameraMatrix;
            Projection    = cam.projectionMatrix;
            Position      = cam.transform.position;
            IsValid       = true;
        }

        /// <summary>Reconstruct a world-space ray from viewport coords (bottom-left=0,0).</summary>
        internal Ray ViewportToRay(Vector2 vp)
        {
            // NDC: [-1,1]
            var ndc = new Vector4(vp.x * 2f - 1f, vp.y * 2f - 1f, -1f, 1f);
            var eye = Projection.inverse * ndc;
            eye /= eye.w;
            eye.w = 0f;
            var dir = (WorldToCamera.inverse * eye).normalized;
            return new Ray(Position, new Vector3(dir.x, dir.y, dir.z));
        }
    }

    /// <summary>Result of raycasting one annotation key point.</summary>
    internal readonly struct AnnotationHit
    {
        internal readonly Vector3 WorldPos;
        internal readonly string  ObjectPath;
        internal readonly string  ObjectId;
        internal readonly bool    DidHit;

        internal AnnotationHit(Vector3 worldPos, string objectPath, string objectId)
        {
            WorldPos   = worldPos;
            ObjectPath = objectPath;
            ObjectId   = objectId;
            DidHit     = true;
        }

        internal AnnotationHit(Vector3 worldPos, string objectPath, int legacyId)
            : this(worldPos, objectPath,
                legacyId == 0 ? "" : legacyId.ToString(CultureInfo.InvariantCulture))
        {
        }

        internal static readonly AnnotationHit Miss = default;
    }

    internal static class AnnotationRaycaster
    {
        /// <summary>Test seam: override Physics.Raycast + plane-fallback.</summary>
        internal static Func<Ray, AnnotationHit> RaycastFunc;

        internal static List<(AnnotationTool tool, string text, Vector2 keyPoint, AnnotationHit hit)>
            RaycastAll(CameraSnapshot snapshot, IReadOnlyList<IAnnotationCommand> commands)
        {
            var results = new List<(AnnotationTool, string, Vector2, AnnotationHit)>();
            if (!snapshot.IsValid) return results;

            foreach (var cmd in commands)
            {
                if (cmd.Tool == AnnotationTool.Erase) continue;
                var keyPt = GetKeyPoint(cmd);
                // Annotation: top-left=(0,0). Unity viewport: bottom-left=(0,0). Flip Y.
                var vp  = new Vector2(keyPt.x, 1f - keyPt.y);
                var ray = snapshot.ViewportToRay(vp);
                results.Add((cmd.Tool, cmd.Text, keyPt, DoRaycast(ray)));
            }
            return results;
        }

        internal static Vector2 GetKeyPoint(IAnnotationCommand cmd) => cmd.Tool switch
        {
            AnnotationTool.Arrow   => cmd.Points[cmd.Points.Count - 1],
            AnnotationTool.Line    => cmd.Points[cmd.Points.Count - 1],
            AnnotationTool.Rect    => (cmd.Points[0] + cmd.Points[1]) * 0.5f,
            AnnotationTool.Ellipse => (cmd.Points[0] + cmd.Points[1]) * 0.5f,
            AnnotationTool.Pen     => cmd.Points[cmd.Points.Count - 1],
            _                      => cmd.Points[0], // Text + fallback
        };

        /// <summary>Format raycast results as plain text for AnnotationMetaWriter.</summary>
        internal static string FormatAnnotations(
            List<(AnnotationTool tool, string text, Vector2 keyPoint, AnnotationHit hit)> annotations)
        {
            if (annotations == null || annotations.Count == 0) return null;
            var sb = new StringBuilder();
            sb.AppendLine("annotations:");
            foreach (var (tool, text, keyPt, hit) in annotations)
            {
                var name = tool.ToString().ToLowerInvariant();
                if (!hit.DidHit)
                {
                    sb.AppendLine($"  {name} at=({keyPt.x:F2},{keyPt.y:F2}) → (sky)");
                    continue;
                }
                var p = hit.WorldPos;
                sb.Append($"  {name} → {hit.ObjectPath} ({p.x:F1},{p.y:F1},{p.z:F1})");
                if (!string.IsNullOrEmpty(hit.ObjectId) && hit.ObjectId != "0") sb.Append($" #{hit.ObjectId}");
                if (tool == AnnotationTool.Text && !string.IsNullOrEmpty(text))
                    sb.Append($" \"{text}\"");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static AnnotationHit DoRaycast(Ray ray)
        {
            if (RaycastFunc != null) return RaycastFunc(ray);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                var path = ComponentSerializer.GetPath(hit.transform.gameObject);
                return new AnnotationHit(hit.point, path, TransientObjectId.GetWireValue(hit.transform.gameObject));
            }

            // Fallback: y=0 plane
            if (Mathf.Abs(ray.direction.y) > 0.001f)
            {
                float t = -ray.origin.y / ray.direction.y;
                if (t > 0f) return new AnnotationHit(ray.GetPoint(t), "(ground)", "");
            }

            return AnnotationHit.Miss;
        }
    }
}
