using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Multi-mode region selection tool for Scene View.
    /// Shift+R to activate. Q/W/E/R to switch modes. G=grid snap.
    /// Enter to commit. Escape to cancel.
    /// </summary>
    [EditorTool("🧬MCP Region Select")]
    internal sealed class SceneRegionTool : EditorTool
    {
        // Local to this file — EditorPrefs keys for the tool's own mode/snap state.
        private const string RegionModeKey = "MCP_RegionMode";
        private const string RegionSnapKey = "MCP_RegionSnap";

        enum State { Idle, Drawing, Preview }

        State          _state   = State.Idle;
        IDrawingMode   _activeMode;
        DrawingModeId  _modeId;
        bool           _gridSnap;

        Polygon2D? _rawPolygon;   // full-fidelity after Finalize()
        Polygon2D? _polygon;      // simplified for display + commit
        GameObject[] _matchedObjects;
        Vector2      _cursorXZ;

        // ── Public seams (Overlay + Chat) ────────────────────────────────────

        public static Action<string, string> OnRegionCommitted;

        internal static PreviewStateSnapshot PreviewState { get; private set; }
        internal static Action  CommitAction;
        internal static Action<DrawingModeId> SetModeAction;
        internal static Action  RequestResimplify;
        internal static Action<bool> SetGridSnapAction;
        internal static Action       ConfirmPointAction;
        internal static Func<bool>   CanConfirmQuery;
        internal static Func<bool>   CanCommitQuery;
        internal static Action       CancelAction;
        public   static DrawingModeId CurrentModeId   { get; private set; }
        public   static bool          GridSnap         { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void OnActivated()
        {
            _modeId   = Enum.TryParse<DrawingModeId>(
                EditorPrefs.GetString(RegionModeKey, "Lasso"), out var m) ? m : DrawingModeId.Lasso;
            _gridSnap = EditorPrefs.GetBool(RegionSnapKey, false);
            _activeMode = DrawingModeFactory.Create(_modeId);
            CurrentModeId = _modeId;
            GridSnap      = _gridSnap;

            _state = State.Idle;
            CommitAction      = CommitRegion;
            SetModeAction     = SwitchMode;
            RequestResimplify = Resimplify;
            SetGridSnapAction = v => { _gridSnap = v; GridSnap = v; EditorPrefs.SetBool(RegionSnapKey, v); };
            ConfirmPointAction = () => { if (_activeMode?.CanConfirm == true) _activeMode.ConfirmPending(); };
            CanConfirmQuery    = () => _state == State.Drawing && (_activeMode?.CanConfirm ?? false);
            CanCommitQuery     = () => _state == State.Preview || (_state == State.Drawing && (_activeMode?.IsComplete ?? false));
            CancelAction       = CancelToIdle;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public override void OnWillBeDeactivated()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            CommitAction       = null;
            SetModeAction      = null;
            RequestResimplify  = null;
            SetGridSnapAction  = null;
            ConfirmPointAction = null;
            CanConfirmQuery    = null;
            CanCommitQuery     = null;
            CancelAction       = null;
            PreviewState       = null;
            CancelToIdle();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (_state != State.Idle) CancelToIdle();
                return;
            }
            if (MouseToXZ(Event.current.mousePosition, out var xz)) _cursorXZ = xz;
            HandleInput(Event.current);
        }

        // ── Input ─────────────────────────────────────────────────────────────

        void HandleInput(Event e)
        {
            switch (_state)
            {
                case State.Idle:
                    HandleModeKeys(e);
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        if (!MouseToXZ(e.mousePosition, out var xz)) break;
                        _activeMode.Begin(xz, _gridSnap);
                        _state = State.Drawing;
                        e.Use();
                    }
                    break;

                case State.Drawing:
                    HandleModeKeys(e);
                    if (MouseToXZ(e.mousePosition, out var cur) && _activeMode.OnEvent(e, cur))
                        e.Use();
                    if (_activeMode.IsComplete || !_activeMode.IsActive)
                        FinalizeDrawing();
                    else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                    { CancelToIdle(); e.Use(); }
                    break;

                case State.Preview:
                    if (e.type == EventType.KeyDown)
                    {
                        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                        { CommitRegion(); e.Use(); }
                        else if (e.keyCode == KeyCode.Escape)
                        { CancelToIdle(); e.Use(); }
                    }
                    break;
            }
        }

        void HandleModeKeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            DrawingModeId? next = e.keyCode switch
            {
                KeyCode.Q => DrawingModeId.Lasso,
                KeyCode.W => DrawingModeId.Rectangle,
                KeyCode.E => DrawingModeId.Circle,
                KeyCode.R => DrawingModeId.PointByPoint,
                _         => (DrawingModeId?)null
            };
            if (next.HasValue) { SwitchMode(next.Value); e.Use(); return; }
            if (e.keyCode == KeyCode.G)
            {
                _gridSnap = !_gridSnap;
                GridSnap  = _gridSnap;
                EditorPrefs.SetBool(RegionSnapKey, _gridSnap);
                e.Use();
            }
        }

        // ── Core state transitions ────────────────────────────────────────────

        void FinalizeDrawing()
        {
            var raw = _activeMode.Finalize();
            if (raw == null || raw.Value.Vertices.Length < 3 || raw.Value.Area() < 0.01f)
            { CancelToIdle(); return; }

            _rawPolygon = raw;
            var level = PolygonDetailSettings.Default;
            var eps   = PolygonDetailConfig.Epsilon(level);
            _polygon  = eps > 0f ? raw.Value.Simplify(eps) : raw;

            if (_polygon.Value.Vertices.Length < 3) { CancelToIdle(); return; }

            _matchedObjects = SceneRegionQuery.FindInside(_polygon.Value, cap: 50);
            var verts = _polygon.Value.Vertices.Length;
            PreviewState = new PreviewStateSnapshot
            {
                Area           = _polygon.Value.Area(),
                ObjectCount    = _matchedObjects.Length,
                VertexCount    = verts,
                RawVertexCount = _activeMode.PreviewVertices.Count,
                DetailLevel    = level,
            };
            _state = State.Preview;
            SceneView.RepaintAll();
        }

        void CommitRegion()
        {
            if (!_polygon.HasValue) { CancelToIdle(); return; }
            var snap = RegionSnapshot.Create(
                id:        GenerateId(),
                polygon:   _polygon.Value,
                objects:   _matchedObjects ?? Array.Empty<GameObject>(),
                sceneName: SceneManager.GetActiveScene().name);
            SceneRegionState.SetRegion(snap);
            SessionState.SetString(PrefKeys.ActiveRegionId, snap.Id);
            OnRegionCommitted?.Invoke(snap.Id, snap.ShortLabel);
            CancelToIdle();
        }

        void CancelToIdle()
        {
            _state = State.Idle;
            _rawPolygon     = null;
            _polygon        = null;
            _matchedObjects = null;
            PreviewState    = null;
            _activeMode?.Reset();
            SceneView.RepaintAll();
        }

        void SwitchMode(DrawingModeId id)
        {
            _modeId     = id;
            CurrentModeId = id;
            _activeMode = DrawingModeFactory.Create(id);
            EditorPrefs.SetString(RegionModeKey, id.ToString());
            if (_state == State.Drawing) CancelToIdle();
        }

        void Resimplify()
        {
            if (_rawPolygon == null) return;
            var level = PolygonDetailSettings.Default;
            var eps   = PolygonDetailConfig.Epsilon(level);
            _polygon  = eps > 0f ? _rawPolygon.Value.Simplify(eps) : _rawPolygon;
            if (PreviewState != null)
            {
                PreviewState.VertexCount = _polygon.Value.Vertices.Length;
                PreviewState.DetailLevel = level;
            }
            SceneView.RepaintAll();
        }

        // ── Rendering ─────────────────────────────────────────────────────────

        void OnSceneGUI(SceneView sv)
        {
            if (_state == State.Idle) return;

            var verts  = _state == State.Preview && _polygon.HasValue
                ? (IReadOnlyList<Vector2>)_polygon.Value.Vertices
                : _activeMode.PreviewVertices;

            var snap = PreviewState;
            RegionRenderer.Draw(new RenderState
            {
                IsDrawing      = _state == State.Drawing,
                IsPreview      = _state == State.Preview,
                Mode           = _modeId,
                Vertices       = verts,
                MatchedObjects = _matchedObjects,
                GridSnap       = _gridSnap,
                CursorXZ       = _cursorXZ,
                VertexCount    = snap?.VertexCount    ?? 0,
                RawVertexCount = snap?.RawVertexCount ?? _activeMode.PreviewVertices.Count,
                Area           = snap?.Area           ?? 0f,
                ObjectCount    = snap?.ObjectCount    ?? 0,
                TokenEstimate  = snap != null
                    ? snap.VertexCount * PolygonDetailConfig.TokensPerVertex : 0,
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static bool MouseToXZ(Vector2 guiPos, out Vector2 xz)
        {
            var ray   = HandleUtility.GUIPointToWorldRay(guiPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                var hit = ray.GetPoint(enter);
                xz = new Vector2(hit.x, hit.z);
                return true;
            }
            xz = default;
            return false;
        }

        static string GenerateId() => Guid.NewGuid().ToString("N").Substring(0, 8);

        // ── PreviewStateSnapshot ──────────────────────────────────────────────

        internal sealed class PreviewStateSnapshot
        {
            public float             Area;
            public int               ObjectCount;
            public int               VertexCount;
            public int               RawVertexCount;
            public PolygonDetailLevel DetailLevel;
        }
    }

    // ── Shortcut ──────────────────────────────────────────────────────────────

    internal static class SceneRegionShortcut
    {
        [Shortcut("🧬MCP/Region Select", typeof(SceneView), KeyCode.R, ShortcutModifiers.Shift)]
        static void Activate() => ToolManager.SetActiveTool<SceneRegionTool>();
    }
}
