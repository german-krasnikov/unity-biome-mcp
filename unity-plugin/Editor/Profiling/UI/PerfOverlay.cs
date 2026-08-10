using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Profiling
{
    [Overlay(typeof(SceneView), id: "🧬MCP Profiler", displayName: "🧬MCP Profiler", defaultDisplay: false)]
    internal sealed class PerfOverlay : Overlay
    {
        PerfGraphElement _sparkline;
        Label _fpsLabel, _cpuLabel, _gpuLabel;
        Label _dcLabel, _batchLabel, _triLabel, _spLabel;
        string _fpsBand = "";

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.AddToClassList("perf-overlay");

            var ss = MCPEditorUtils.LoadStyleSheet("Profiling/UI/PerfOverlay.uss");
            if (ss != null) root.styleSheets.Add(ss);

            root.Add(BuildRow1());
            root.Add(BuildRow2());

            root.schedule.Execute(Refresh).Every(200);
            return root;
        }

        VisualElement BuildRow1()
        {
            var row = new VisualElement();
            row.AddToClassList("perf-row");

            row.Add(MakeLabel("FPS"));
            _fpsLabel = MakeValue("--");
            row.Add(_fpsLabel);

            _sparkline = new PerfGraphElement(32) { MinValue = 0f, MaxValue = 120f };
            _sparkline.style.width = 100;
            _sparkline.style.height = 30;
            _sparkline.style.marginRight = 8;
            row.Add(_sparkline);

            row.Add(MakeLabel("CPU"));
            _cpuLabel = MakeValue("--");
            row.Add(_cpuLabel);

            row.Add(MakeLabel("GPU"));
            _gpuLabel = MakeValue("--");
            row.Add(_gpuLabel);

            return row;
        }

        VisualElement BuildRow2()
        {
            var row = new VisualElement();
            row.AddToClassList("perf-row");

            row.Add(MakeLabel("DC"));
            _dcLabel = MakeValue("--");
            row.Add(_dcLabel);

            row.Add(MakeLabel("B"));
            _batchLabel = MakeValue("--");
            row.Add(_batchLabel);

            row.Add(MakeLabel("Tri"));
            _triLabel = MakeValue("--");
            row.Add(_triLabel);

            row.Add(MakeLabel("SP"));
            _spLabel = MakeValue("--");
            row.Add(_spLabel);

            return row;
        }

        void Refresh()
        {
            var f = ProfilerBridge.CollectFrame();
            float fps = f.DeltaTime > 0f ? 1f / f.DeltaTime : 0f;

            string fpsText = fps > 0f ? Mathf.RoundToInt(fps).ToString() : "N/A";
            if (_fpsLabel.text != fpsText) _fpsLabel.text = fpsText;

            string band = PerfThresholds.FpsBand(fps);
            if (band != _fpsBand)
            {
                if (_fpsBand.Length > 0) _fpsLabel.RemoveFromClassList("perf-value--" + _fpsBand);
                _fpsLabel.AddToClassList("perf-value--" + band);
                _fpsBand = band;
            }

            _sparkline.PushValue(fps);

            SetText(_cpuLabel, f.CpuMs > 0f ? f.CpuMs.ToString("F1") + "ms" : "N/A");
            SetText(_gpuLabel, f.GpuMs >= 0f ? f.GpuMs.ToString("F1") + "ms" : "N/A");
            SetText(_dcLabel, f.DrawCalls.ToString());
            SetText(_batchLabel, f.Batches.ToString());
            SetText(_triLabel, FormatTri(f.Triangles));
            SetText(_spLabel, f.SetPassCalls.ToString());
        }

        static void SetText(Label l, string v) { if (l.text != v) l.text = v; }

        static string FormatTri(long t) =>
            t >= 1_000_000 ? $"{t / 1_000_000f:F1}M" :
            t >= 1_000 ? $"{t / 1000}K" :
            t.ToString();

        static Label MakeLabel(string t) { var l = new Label(t); l.AddToClassList("perf-label"); return l; }
        static Label MakeValue(string t) { var l = new Label(t); l.AddToClassList("perf-value"); return l; }
    }
}
