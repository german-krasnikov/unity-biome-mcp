using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Circle drawing mode. Begin stores center; drag updates radius;
    /// MouseUp finalizes. Segments clamped 12..64.
    /// </summary>
    internal sealed class CircleMode : IDrawingMode
    {
        const int MinSegments = 12;
        const int MaxSegments = 64;

        readonly int _segments;
        Vector2 _center;
        float _radius;
        bool _gridSnap;
        Vector2[] _previewBuf;

        public DrawingModeId Id => DrawingModeId.Circle;
        public IReadOnlyList<Vector2> PreviewVertices => _previewBuf;
        public bool IsComplete { get; private set; }
        public bool IsActive { get; private set; }

        public CircleMode(int segments = 16)
        {
            _segments = Mathf.Clamp(segments, MinSegments, MaxSegments);
            _previewBuf = new Vector2[_segments];
        }

        public void Begin(Vector2 startXZ, bool gridSnap)
        {
            _center = startXZ;
            _radius = 0f;
            _gridSnap = gridSnap;
            IsActive = true;
            IsComplete = false;
            UpdatePreview();
        }

        public bool OnEvent(Event e, Vector2 currentXZ)
        {
            if (e.type == EventType.MouseDrag)
            {
                UpdateRadius(currentXZ);
                UpdatePreview();
                return true;
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                UpdateRadius(currentXZ);
                UpdatePreview();
                IsComplete = true;
                return true;
            }
            return false;
        }

        public Polygon2D? Build()
        {
            if (_radius < 0.01f) return null;
            return new Polygon2D(BuildCircle(_center, _radius, _segments));
        }

        public void Reset()
        {
            _radius = 0f;
            IsActive = false;
            IsComplete = false;
        }

        public bool CanConfirm => false;
        public void ConfirmPending() { }

        void UpdateRadius(Vector2 xz)
        {
            float r = Vector2.Distance(_center, xz);
            _radius = _gridSnap ? DrawingUtils.SnapRadius(r) : r;
        }

        void UpdatePreview() => BuildCircle(_center, _radius, _segments, _previewBuf);

        static Vector2[] BuildCircle(Vector2 center, float radius, int segments)
        {
            var v = new Vector2[segments];
            BuildCircle(center, radius, segments, v);
            return v;
        }

        static void BuildCircle(Vector2 center, float radius, int segments, Vector2[] buf)
        {
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
                buf[i] = center + new Vector2(Mathf.Cos(i * step) * radius,
                                              Mathf.Sin(i * step) * radius);
        }
    }
}
