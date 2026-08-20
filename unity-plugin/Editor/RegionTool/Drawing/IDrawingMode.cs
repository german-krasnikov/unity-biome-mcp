using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    internal interface IDrawingMode
    {
        DrawingModeId Id { get; }

        // Called on MouseDown to begin interaction
        void Begin(Vector2 startXZ, bool gridSnap);

        // Called on each relevant event. Returns true if consumed.
        bool OnEvent(Event e, Vector2 currentXZ);

        // Live preview vertices for rendering
        IReadOnlyList<Vector2> PreviewVertices { get; }

        // True when mode has enough data to finalize (e.g. MouseUp for lasso/rect/circle)
        bool IsComplete { get; }

        // False means auto-transition to Preview (PbP sets false when closed or 0 verts)
        bool IsActive { get; }

        // Produce final polygon. Returns null if < 3 valid vertices.
        Polygon2D? Build();

        void Reset();

        // True when clicking "Confirm Point" makes sense (click-by-click modes only)
        bool CanConfirm { get; }

        // Place next vertex at current cursor XZ — no-op if !CanConfirm or IsComplete
        void ConfirmPending();
    }

    internal enum DrawingModeId { Lasso, Rectangle, Circle, PointByPoint }

    internal static class DrawingModeFactory
    {
        public static IDrawingMode Create(DrawingModeId id) => id switch
        {
            DrawingModeId.Lasso        => new LassoMode(),
            DrawingModeId.Rectangle    => new RectangleMode(),
            DrawingModeId.Circle       => new CircleMode(
                PolygonDetailConfig.CircleVertices(PolygonDetailSettings.Default)),
            DrawingModeId.PointByPoint => new PointByPointMode(),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
        };
    }

    // ── Annotation modes (Point / Polyline / Measurement) ──────────────────

    internal enum AnnotationModeId { Point, Polyline, Measurement }

    /// <summary>
    /// Lightweight drawing mode for annotation primitives (Point, Polyline, Measurement).
    /// Unlike IDrawingMode, Build() is not required — use FinalizedPoints instead.
    /// </summary>
    internal interface IAnnotationMode
    {
        AnnotationModeId Id { get; }
        void Begin(Vector2 startXZ, bool gridSnap);
        bool OnEvent(Event e, Vector2 currentXZ);
        IReadOnlyList<Vector2> PreviewVertices { get; }
        bool IsComplete { get; }
        bool IsActive { get; }
        Vector2[] FinalizedPoints { get; }
        void Reset();

        // True when clicking "Confirm Point" makes sense (click-by-click modes only)
        bool CanConfirm { get; }

        // Place next vertex at current cursor — no-op if !CanConfirm or IsComplete
        void ConfirmPending();
    }

    internal static class AnnotationModeFactory
    {
        public static IAnnotationMode Create(AnnotationModeId id) => id switch
        {
            AnnotationModeId.Point       => new PointMode(),
            AnnotationModeId.Polyline    => new PolylineMode(),
            AnnotationModeId.Measurement => new MeasurementMode(),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
        };
    }
}
