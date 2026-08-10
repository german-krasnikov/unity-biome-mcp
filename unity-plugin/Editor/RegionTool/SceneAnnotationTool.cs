using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Scene View tool for placing Point, Polyline, and Measurement annotations.
    /// Shift+A to activate. 1/2/3 to switch type. Enter=Commit, Esc=Cancel, Backspace=Undo vertex.
    /// </summary>
    [EditorTool("🧬MCP Annotation")]
    internal sealed class SceneAnnotationTool : EditorTool
    {
        enum State { Idle, Drawing }

        State           _state = State.Idle;
        IAnnotationMode _mode;
        AnnotationModeId _modeId = AnnotationModeId.Point;
        bool            _gridSnap;
        Vector2         _cursorXZ;
        string          _pendingLabel = "";

        // ── Public seams ──────────────────────────────────────────────────

        public static Action<string, string> OnAnnotationCommitted; // (id, shortLabel)
        internal static Action                        CommitAction;
        internal static Action<AnnotationModeId>      SetModeAction;
        internal static Action       ConfirmPointAction;
        internal static Func<bool>   CanConfirmQuery;
        internal static Func<bool>   CanCommitQuery;
        internal static Action       CancelAction;
        public static AnnotationModeId  CurrentModeId  { get; private set; }
        public static bool              GridSnap        { get; private set; }
        public static string            PendingLabel
        {
            get => _instance?._pendingLabel ?? "";
            set { if (_instance != null) _instance._pendingLabel = value ?? ""; }
        }

        static SceneAnnotationTool _instance;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void OnActivated()
        {
            _instance = this;
            _modeId   = (AnnotationModeId)EditorPrefs.GetInt(PrefKeys.DefaultAnnotationMode, (int)AnnotationModeId.Point);
            _gridSnap = EditorPrefs.GetBool(PrefKeys.AnnotationGridSnap, false);
            _mode     = AnnotationModeFactory.Create(_modeId);
            CurrentModeId = _modeId;
            GridSnap      = _gridSnap;
            _state        = State.Idle;
            _pendingLabel = "";

            CommitAction       = CommitAnnotation;
            SetModeAction      = SwitchMode;
            ConfirmPointAction = () => { if (_mode?.CanConfirm == true) _mode.ConfirmPending(); };
            CanConfirmQuery    = () => _state == State.Drawing && (_mode?.CanConfirm ?? false);
            CanCommitQuery     = () => _state == State.Drawing && (_mode?.IsComplete ?? false);
            CancelAction       = CancelToIdle;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public override void OnWillBeDeactivated()
        {
            _instance = null;
            SceneView.duringSceneGui -= OnSceneGUI;
            CommitAction       = null;
            SetModeAction      = null;
            ConfirmPointAction = null;
            CanConfirmQuery    = null;
            CanCommitQuery     = null;
            CancelAction       = null;
            CancelToIdle();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { if (_state != State.Idle) CancelToIdle(); return; }

            if (SceneAnnotationUtils.MouseToXZ(Event.current.mousePosition, out var xz)) _cursorXZ = xz;
            HandleInput(Event.current);
        }

        void HandleInput(Event e)
        {
            HandleModeKeys(e);

            if (_state == State.Idle)
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (!SceneAnnotationUtils.MouseToXZ(e.mousePosition, out var xz)) return;
                    _mode.Begin(xz, _gridSnap);
                    _state = State.Drawing;
                    e.Use();
                }
                return;
            }

            // Drawing state
            if (SceneAnnotationUtils.MouseToXZ(e.mousePosition, out var cur) && _mode.OnEvent(e, cur))
                e.Use();

            if (_mode.IsComplete || !_mode.IsActive)
            { FinalizeDrawing(); return; }

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape)        { CancelToIdle(); e.Use(); }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                { CommitAnnotation(); e.Use(); }
            }
        }

        void HandleModeKeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            AnnotationModeId? next = e.keyCode switch
            {
                KeyCode.Alpha1 => AnnotationModeId.Point,
                KeyCode.Alpha2 => AnnotationModeId.Polyline,
                KeyCode.Alpha3 => AnnotationModeId.Measurement,
                _              => (AnnotationModeId?)null
            };
            if (next.HasValue) { SwitchMode(next.Value); e.Use(); return; }
            if (e.keyCode == KeyCode.G)
            {
                _gridSnap = !_gridSnap;
                GridSnap  = _gridSnap;
                EditorPrefs.SetBool(PrefKeys.AnnotationGridSnap, _gridSnap);
                e.Use();
            }
        }

        void FinalizeDrawing()
        {
            var pts = _mode.FinalizedPoints;
            int minVerts = _modeId == AnnotationModeId.Point ? 1 : 2;
            if (pts == null || pts.Length < minVerts) { CancelToIdle(); return; }
            CommitAnnotation();
        }

        void CommitAnnotation()
        {
            var pts = _mode.FinalizedPoints;
            if (pts == null || pts.Length == 0) return;

            int minVerts = _modeId == AnnotationModeId.Point ? 1 : 2;
            if (pts.Length < minVerts) return;

            var sceneName = SceneManager.GetActiveScene().name;
            var id = SceneAnnotationUtils.GenerateId();
            var label = _pendingLabel;

            var polyNearPaths = SceneAnnotationUtils.PolyNearPaths(_modeId, pts);

            RegionSnapshot snap = _modeId switch
            {
                AnnotationModeId.Point =>
                    RegionSnapshot.CreatePoint(id, pts[0], Array.Empty<string>(), sceneName, label),
                AnnotationModeId.Polyline =>
                    RegionSnapshot.CreatePolyline(id, pts, polyNearPaths, sceneName, label),
                AnnotationModeId.Measurement =>
                    RegionSnapshot.CreateMeasurement(id, pts[0], pts[1], sceneName, label),
                _ => null
            };

            if (snap == null) return;
            SceneRegionState.SetRegion(snap);
            SessionState.SetString(PrefKeys.ActiveRegionId, snap.Id);
            OnAnnotationCommitted?.Invoke(snap.Id, snap.ShortLabel);
            CancelToIdle();
        }

        void CancelToIdle()
        {
            _state = State.Idle;
            _mode?.Reset();
            SceneView.RepaintAll();
        }

        void SwitchMode(AnnotationModeId id)
        {
            _modeId       = id;
            CurrentModeId = id;
            _mode         = AnnotationModeFactory.Create(id);
            if (_state == State.Drawing) CancelToIdle();
        }

        void OnSceneGUI(SceneView sv)
        {
            if (_state == State.Idle) return;
            var verts = _mode.PreviewVertices;
            if (verts == null || verts.Count == 0) return;

            RegionRenderer.DrawAnnotation(new RenderState
            {
                AnnotationType = _modeId.ToString().ToLowerInvariant(),
                Vertices       = verts,
                Label          = _pendingLabel,
                Length         = SceneAnnotationUtils.ComputeLength(verts),
                GridSnap       = _gridSnap,
                CursorXZ       = _cursorXZ,
            });
        }
    }
}
