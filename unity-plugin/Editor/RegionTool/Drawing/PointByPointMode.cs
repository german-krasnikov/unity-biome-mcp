using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Click-to-place polygon drawing mode. Double-click or click near start to close.
    /// Escape removes the last placed vertex; with 0 vertices sets IsActive=false.
    /// PreviewVertices = placed vertices + live cursor position as last point.
    /// </summary>
    internal sealed class PointByPointMode : IDrawingMode
    {
        const float CloseThreshold = 0.4f;

        readonly List<Vector2> _vertices = new();
        Vector2 _cursor;
        bool _gridSnap;

        // Combined preview list rebuilt on demand
        readonly List<Vector2> _preview = new();

        public DrawingModeId Id => DrawingModeId.PointByPoint;
        public IReadOnlyList<Vector2> PreviewVertices => _preview;
        public bool IsComplete { get; private set; }
        public bool IsActive { get; private set; }

        public void Begin(Vector2 startXZ, bool gridSnap)
        {
            _vertices.Clear();
            _gridSnap = gridSnap;
            _cursor = startXZ;
            _vertices.Add(gridSnap ? Snap(startXZ) : startXZ);
            IsActive = true;
            IsComplete = false;
            RebuildPreview();
        }

        public bool OnEvent(Event e, Vector2 currentXZ)
        {
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                _cursor = _gridSnap ? Snap(currentXZ) : currentXZ;
                RebuildPreview();
                return true;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var xz = _gridSnap ? Snap(currentXZ) : currentXZ;

                bool nearStart = _vertices.Count >= 3 &&
                                 Vector2.Distance(xz, _vertices[0]) < CloseThreshold;
                bool dblClick = e.clickCount >= 2;

                if (nearStart || dblClick)
                {
                    IsComplete = true;
                    RebuildPreview();
                    return true;
                }

                _vertices.Add(xz);
                _cursor = xz;
                RebuildPreview();
                return true;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (_vertices.Count <= 1)
                {
                    IsActive = false;
                    _vertices.Clear();
                    _preview.Clear();
                }
                else
                {
                    _vertices.RemoveAt(_vertices.Count - 1);
                    RebuildPreview();
                }
                return true;
            }

            return false;
        }

        public Polygon2D? Build()
        {
            if (_vertices.Count < 3) return null;
            return new Polygon2D(_vertices.ToArray());
        }

        public void Reset()
        {
            _vertices.Clear();
            _preview.Clear();
            IsActive = false;
            IsComplete = false;
        }

        public bool CanConfirm => IsActive && !IsComplete;

        public void ConfirmPending()
        {
            if (!CanConfirm) return;
            _vertices.Add(_cursor);
            RebuildPreview();
        }

        void RebuildPreview()
        {
            _preview.Clear();
            _preview.AddRange(_vertices);
            // Append cursor as live preview point (only while still drawing)
            if (IsActive && !IsComplete)
                _preview.Add(_cursor);
        }

        static Vector2 Snap(Vector2 v) => DrawingUtils.Snap(v);
    }
}
