using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Rectangle drawing mode. Begin stores corner A; drag updates corner B;
    /// MouseUp finalizes. Produces 4-vertex CCW polygon.
    /// </summary>
    internal sealed class RectangleMode : IDrawingMode
    {
        const float MinArea = 0.01f;

        Vector2 _start;
        Vector2 _current;
        bool _gridSnap;
        readonly Vector2[] _preview = new Vector2[4];

        public DrawingModeId Id => DrawingModeId.Rectangle;
        public IReadOnlyList<Vector2> PreviewVertices => _preview;
        public bool IsComplete { get; private set; }
        public bool IsActive { get; private set; }

        public void Begin(Vector2 startXZ, bool gridSnap)
        {
            _gridSnap = gridSnap;
            _start = gridSnap ? Snap(startXZ) : startXZ;
            _current = _start;
            IsActive = true;
            IsComplete = false;
            UpdatePreview();
        }

        public bool OnEvent(Event e, Vector2 currentXZ)
        {
            if (e.type == EventType.MouseDrag)
            {
                _current = _gridSnap ? Snap(currentXZ) : currentXZ;
                UpdatePreview();
                return true;
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _current = _gridSnap ? Snap(currentXZ) : currentXZ;
                UpdatePreview();
                IsComplete = true;
                return true;
            }
            return false;
        }

        public Polygon2D? Build()
        {
            var a = _start;
            var b = _current;
            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minZ = Mathf.Min(a.y, b.y), maxZ = Mathf.Max(a.y, b.y);
            float area = (maxX - minX) * (maxZ - minZ);
            if (area < MinArea) return null;
            // CCW: BL → BR → TR → TL
            return new Polygon2D(new[]
            {
                new Vector2(minX, minZ),
                new Vector2(maxX, minZ),
                new Vector2(maxX, maxZ),
                new Vector2(minX, maxZ),
            });
        }

        public void Reset()
        {
            IsActive = false;
            IsComplete = false;
        }

        public bool CanConfirm => false;
        public void ConfirmPending() { }

        void UpdatePreview()
        {
            float minX = Mathf.Min(_start.x, _current.x), maxX = Mathf.Max(_start.x, _current.x);
            float minZ = Mathf.Min(_start.y, _current.y), maxZ = Mathf.Max(_start.y, _current.y);
            _preview[0] = new Vector2(minX, minZ);
            _preview[1] = new Vector2(maxX, minZ);
            _preview[2] = new Vector2(maxX, maxZ);
            _preview[3] = new Vector2(minX, maxZ);
        }

        static Vector2 Snap(Vector2 v) => DrawingUtils.Snap(v);
    }
}
