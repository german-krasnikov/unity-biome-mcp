using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditorInternal;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Frame Debugger capture via reflection on UnityEditorInternal.FrameDebuggerUtility.
    /// Gracefully degrades when reflection fails (returns ERR message, never throws).
    /// CRITICAL: always restores previous enabled state in finally block.
    /// Lazy: reflection is performed only on first Capture() call, not on domain reload.
    /// </summary>
    internal static class FrameDebugHelper
    {
        private static bool _initialized;
        private static bool _reflectionFailed;
        private static Type         _utilType;
        private static MethodInfo   _setEnabled;
        private static MethodInfo   _getEventData;
        private static PropertyInfo _count;
        private static PropertyInfo _limit;
        private static PropertyInfo _isLocalEnabled;

        // Lookup table for batchBreakCause integer (empirical — may vary per Unity version)
        private static readonly string[] _breakReasons =
        {
            "none",                      // 0
            "DifferentMaterial",         // 1
            "DifferentShader",           // 2
            "DifferentTexture",          // 3
            "DifferentReflectionProbe",  // 4
            "DifferentLight",            // 5
            "DifferentLightmap",         // 6
            "DifferentShadowSettings",   // 7
            "DifferentStaticBatchFlags", // 8
            "TooManyVerts",              // 9
            "TooManyIndices",            // 10
            "ShaderDisablesBatching",    // 11
            "Multipass",                 // 12
            "MaterialPropertyBlock",     // 13
            "NegativeScale",             // 14
            "NonInstanceableProperty",   // 15
            "DifferentGeometry",         // 16
            "DifferentCustomProps",      // 17
        };

        private static void EnsureReflection()
        {
            // Guard: already initialized OR already failed (or externally set by tests)
            if (_initialized || _reflectionFailed) return;
            _initialized = true;
            try
            {
                _utilType = typeof(UnityEditor.Editor).Assembly
                    .GetType("UnityEditorInternal.FrameDebuggerUtility");
                if (_utilType == null) { _reflectionFailed = true; return; }

                var bf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                _setEnabled     = _utilType.GetMethod("SetEnabled", bf);
                _count          = _utilType.GetProperty("count", bf);
                _limit          = _utilType.GetProperty("limit", bf);
                _isLocalEnabled = _utilType.GetProperty("IsLocalEnabled", bf);
                _getEventData   = _utilType.GetMethod("GetFrameEventData", bf);

                if (_setEnabled == null || _count == null || _getEventData == null)
                    _reflectionFailed = true;
            }
            catch
            {
                _reflectionFailed = true;
            }
        }

        public static string Capture(string args)
        {
            EnsureReflection();
            if (_reflectionFailed)
                return "ERR:FrameDebugger reflection unavailable in this Unity version — use action=stats for aggregate data";

            var maxEventsStr = JsonHelper.ExtractString(args, "max_events");
            int maxEvents = 200;
            if (maxEventsStr != null) int.TryParse(maxEventsStr, out maxEvents);
            if (maxEvents <= 0) maxEvents = 200;

            bool wasEnabled = false;
            if (_isLocalEnabled != null)
                try { wasEnabled = (bool)_isLocalEnabled.GetValue(null); } catch { wasEnabled = false; }

            try
            {
                if (!wasEnabled)
                    _setEnabled?.Invoke(null, new object[] { true, 0 });

                InternalEditorUtility.RepaintAllViews();

                if (_count == null) return "ERR:count property not found via reflection";
                int total = (int)_count.GetValue(null);
                if (total == 0)
                    return "FRAME EVENTS: 0 — open Game or Scene View and render a frame first";

                if (_limit != null)
                {
                    try { _limit.SetValue(null, total); } catch { /* non-critical */ }
                }

                var sb   = new StringBuilder();
                var breaks = new Dictionary<string, int>();

                sb.AppendLine($"FRAME EVENTS total={total}");
                int end = Math.Min(total, maxEvents);

                for (int i = 0; i < end; i++)
                {
                    if (_getEventData == null) continue;
                    var p = new object[] { i, null };
                    bool ok = false;
                    try { ok = (bool)_getEventData.Invoke(null, p); } catch { continue; }
                    if (!ok || p[1] == null) continue;

                    var data    = p[1];
                    var dtype   = data.GetType();
                    var cause   = GetField<int>(dtype, data, "batchBreakCause");
                    var verts   = GetField<int>(dtype, data, "vertexCount");
                    var shader  = GetField<string>(dtype, data, "shaderName") ?? "?";
                    var reason  = BreakReason(cause);

                    if (cause > 0) sb.AppendLine($"  [{i}] break={reason} shader={shader} verts={RenderAnalyzer.FormatNum(verts)}");
                    else           sb.AppendLine($"  [{i}] batched shader={shader} verts={RenderAnalyzer.FormatNum(verts)}");

                    breaks[reason] = breaks.TryGetValue(reason, out int cnt) ? cnt + 1 : 1;
                }

                if (total > maxEvents)
                    sb.AppendLine($"... truncated ({total - maxEvents} more) — pass max_events={total} for full capture");

                if (breaks.Count > 0)
                {
                    sb.AppendLine("BREAK SUMMARY");
                    foreach (var (reason, cnt) in breaks)
                        sb.AppendLine($"  {reason}: {cnt}");
                }

                return sb.ToString().TrimEnd();
            }
            finally
            {
                // Always restore previous state — Frame Debugger pauses rendering
                if (!wasEnabled)
                {
                    try { _setEnabled?.Invoke(null, new object[] { false, 0 }); }
                    catch (Exception ex) { Debug.LogWarning($"{BiomeLabel.Tag} FrameDebugHelper: failed to disable: {ex.Message}"); }
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static string BreakReason(int cause) =>
            cause >= 0 && cause < _breakReasons.Length
                ? _breakReasons[cause]
                : $"unknown({cause})";

        /// <summary>
        /// Access a field by NAME on an object instance. Never by offset — guards against struct layout changes.
        /// Returns default(T) on missing field or cast failure.
        /// </summary>
        private static T GetField<T>(Type type, object obj, string name)
        {
            if (type == null || name == null) return default;
            var fi = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return default;
            try { return (T)fi.GetValue(obj); } catch { return default; }
        }

    }
}
