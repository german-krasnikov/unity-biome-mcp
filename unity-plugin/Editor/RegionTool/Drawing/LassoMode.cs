using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Freehand lasso drawing mode. Captures mouse drag points and RDP-simplifies on finalize.
    /// Extracted from SceneRegionTool._rawPoints + FinalizeDrawing logic.
    /// </summary>
    internal sealed class LassoMode : IDrawingMode
    {
        const float MinDistSq = 0.04f; // 0.2m min distance between points

        readonly List<Vector2> _points = new(2048);

        public DrawingModeId Id => DrawingModeId.Lasso;
        public IReadOnlyList<Vector2> PreviewVertices => _points;
        public bool IsComplete { get; private set; }
        public bool IsActive { get; private set; }

        public void Begin(Vector2 startXZ, bool gridSnap)
        {
            _points.Clear();
            _points.Add(startXZ);
            IsActive = true;
            IsComplete = false;
        }

        public bool OnEvent(Event e, Vector2 currentXZ)
        {
            if (e.type == EventType.MouseDrag)
            {
                AppendIfFar(currentXZ);
                return true;
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                AppendIfFar(currentXZ);
                IsComplete = true;
                return true;
            }
            return false;
        }

        public Polygon2D? Build()
        {
            if (_points.Count < 3) return null;
            return new Polygon2D(_points.ToArray());
        }

        public void Reset()
        {
            _points.Clear();
            IsActive = false;
            IsComplete = false;
        }

        public bool CanConfirm => false;
        public void ConfirmPending() { }

        void AppendIfFar(Vector2 xz)
        {
            if (_points.Count > 0)
            {
                var last = _points[_points.Count - 1];
                if ((xz - last).sqrMagnitude < MinDistSq) return;
            }
            if (_points.Count < 2048)
                _points.Add(xz);
        }
    }
}
