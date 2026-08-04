// Rendering analysis: draw calls, overdraw, materials, shaders, audit, compare.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMCP.Editor.Profiling;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor
{
    internal static partial class RenderAnalyzer
    {
        // Baseline for compare action
        private static Dictionary<string, long> _baseline;
        private static string _baselineTime;

        public static string Execute(string args)
        {
            var action     = JsonHelper.ExtractString(args, "action") ?? "stats";
            var path       = JsonHelper.ExtractString(args, "path");
            var detail     = JsonHelper.ExtractString(args, "detail") ?? "brief";
            var baselineId = JsonHelper.ExtractString(args, "baseline_id");

            return action switch
            {
                "stats"          => Stats(detail),
                "materials"      => Materials(path, detail),
                "shaders"        => Shaders(path, detail),
                "overdraw"       => Overdraw(path, detail),
                "audit"          => Audit(path),
                "compare"        => Compare(baselineId),
                "batching"       => Batching(path, detail),
                "lights"         => Lights(path, detail),
                "shadow_audit"   => ShadowAudit(path, detail),
                "probe_audit"    => ProbeAudit(path, detail),
                "light_optimize" => LightOptimize(path),
                "frame_debug"    => FrameDebugHelper.Capture(args),
                _ => throw new InvalidOperationException(
                    $"Unknown action '{action}'. Valid: stats|materials|shaders|lights|batching|overdraw|audit|compare|frame_debug|shadow_audit|probe_audit|light_optimize")
            };
        }

        // Shared scene traversal
        internal static Renderer[] GetRenderers(string path)
        {
            if (path != null)
            {
                var root = ComponentSerializer.FindObject(path);
                return root?.GetComponentsInChildren<Renderer>() ?? Array.Empty<Renderer>();
            }
            return Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        }

        private static string Stats(string detail)
        {
            var renderers = GetRenderers(null);
            long totalTris = 0, totalVerts = 0;
            int skinned = 0;
            foreach (var r in renderers)
            {
                try
                {
                    Mesh m = null;
                    if (r is SkinnedMeshRenderer smr) { m = smr.sharedMesh; skinned++; }
                    else if (r is MeshRenderer) m = r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (m == null) continue;
                    for (int s = 0; s < m.subMeshCount; s++)
                        totalTris += (long)m.GetIndexCount(s) / 3;
                    totalVerts += m.vertexCount;
                }
                catch (System.Exception) { }
            }

            int draws   = UnityStats.drawCalls;
            var batches = ProfilerBridge.CollectBatchCount();
            int setPass = UnityStats.setPassCalls;
            int shadows = UnityStats.shadowCasters;
            bool live   = draws > 0;

            var sb = new StringBuilder();
            sb.AppendLine($"RENDER STATS{(live ? "" : " [WARN:open SceneView for live counters]")}");
            sb.Append($"draw={draws} batches={FormatBatchCount(batches)} tris={FormatNum(totalTris)} verts={FormatNum(totalVerts)} setpass={setPass} shadows={shadows} skinned={skinned}");
            if (detail == "full")
                sb.Append($"\nstatic={UnityStats.staticBatchedDrawCalls} dynamic={UnityStats.dynamicBatchedDrawCalls} instanced={UnityStats.instancedBatchedDrawCalls}");
            SaveBaseline(draws, batches.IsAvailable ? batches.Value : -1, setPass, shadows, totalTris, totalVerts);
            return sb.ToString().TrimEnd();
        }

        private static string Overdraw(string path, string detail)
        {
            var renderers = GetRenderers(path);
            int opaque = 0, transparent = 0, particles = 0, uiOverlay = 0;
            long opTris = 0, trTris = 0;

            foreach (var r in renderers)
            {
                bool isTr = false;
                foreach (var mat in r.sharedMaterials)
                    if (mat != null && (mat.renderQueue >= (int)RenderQueue.Transparent ||
                        mat.GetTag("RenderType", false) == "Transparent"))
                    { isTr = true; break; }
                if (r is ParticleSystemRenderer) particles++;
                else if (isTr) { transparent++; trTris += GetRendererTris(r); }
                else { opaque++; opTris += GetRendererTris(r); }
            }

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) uiOverlay++;

            var sb = new StringBuilder();
            sb.Append($"OVERDRAW opaque={opaque}({FormatNum(opTris)}) transparent={transparent}({FormatNum(trTris)}) particles={particles} ui={uiOverlay}");
            if (uiOverlay > 1) sb.Append(" WARN:multi-ui-overdraw");
            if (transparent > opaque * 0.15f) sb.Append("\nWARN: transparent > 15% of renderers — overdraw risk");
            return sb.ToString();
        }

        private static string Audit(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Stats("brief"));
            sb.AppendLine();
            sb.AppendLine(Overdraw(path, "brief"));
            return sb.ToString().TrimEnd();
        }

        private static string Compare(string baselineId)
        {
            if (_baseline == null) return "err:no baseline — call render_analyze(action=stats) first";

            int draws   = UnityStats.drawCalls;
            var batches = ProfilerBridge.CollectBatchCount();
            int setPass = UnityStats.setPassCalls;

            var sb = new StringBuilder();
            sb.AppendLine($"COMPARE (vs {_baselineTime ?? "last stats"})");
            AppendDelta(sb, "draw calls", BGet("draws"), draws);
            AppendBatchDelta(sb, BGet("batches"), batches);
            AppendDelta(sb, "set-pass",   BGet("setPass"), setPass);
            return sb.ToString().TrimEnd();
        }

        private static long BGet(string key) =>
            _baseline != null && _baseline.TryGetValue(key, out var v) ? v : 0L;

        private static void AppendDelta(StringBuilder sb, string label, long old, long now)
        {
            long delta = now - old;
            string sign = delta >= 0 ? "+" : "";
            string pct  = old > 0 ? $" ({sign}{delta * 100 / old}%)" : "";
            string tag  = delta < 0 ? " GOOD" : delta > 0 ? " WARN" : "";
            sb.AppendLine($"  {label}: {now} ({sign}{delta}{pct}){tag}");
        }

        private static void AppendBatchDelta(
            StringBuilder sb,
            long old,
            ProfilerBridge.BatchCountSample current)
        {
            if (old < 0)
            {
                sb.AppendLine("  batches: unavailable(baseline-unavailable)");
                return;
            }

            if (!current.IsAvailable)
            {
                sb.AppendLine($"  batches: {FormatBatchCount(current)}");
                return;
            }

            AppendDelta(sb, "batches", old, current.Value);
        }

        private static void SaveBaseline(int draws, int batches, int setPass, int shadows, long tris, long verts)
        {
            _baseline = new Dictionary<string, long>
            {
                ["draws"]   = draws,
                ["batches"] = batches,
                ["setPass"] = setPass,
                ["shadows"] = shadows,
                ["tris"]    = tris,
                ["verts"]   = verts,
            };
            _baselineTime = System.DateTime.Now.ToString("HH:mm:ss");
        }

        private static long GetRendererTris(Renderer r)
        {
            Mesh m = r is SkinnedMeshRenderer smr ? smr.sharedMesh
                : r is MeshRenderer ? r.GetComponent<MeshFilter>()?.sharedMesh
                : null;
            if (m == null) return 0;
            long t = 0;
            try
            {
                for (int s = 0; s < m.subMeshCount; s++)
                    t += (long)m.GetIndexCount(s) / 3;
            }
            catch (System.Exception) { /* non-readable mesh or missing component — skip */ }
            return t;
        }

        // Exposed for tests
        internal static string FormatNum(long n) =>
            n >= 1_000_000 ? $"{n / 1_000_000.0:F1}M" :
            n >= 1_000     ? $"{n / 1_000.0:F1}K" : n.ToString();

        internal static string FormatBatchCount(ProfilerBridge.BatchCountSample sample) =>
            sample.IsAvailable ? sample.Value.ToString() : $"unavailable({sample.UnavailableReason})";

        internal static void ClearBaselineForTest() { _baseline = null; _baselineTime = null; }
    }
}
