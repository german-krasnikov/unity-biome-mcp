// BakeHelper: lighting + occlusion bake sub-commands.
// fire-and-forget async lighting bake; synchronous occlusion bake.
using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class BakeHelper
    {
        internal static string Execute(string action, string argsJson)
        {
            var target = JsonHelper.ExtractString(argsJson, "target");
            // action from JSON (if sent), fallback to caller-supplied default
            var act = JsonHelper.ExtractString(argsJson, "action") ?? action ?? "start";

            return target switch
            {
                "lighting" => DispatchLighting(act),
                "occlusion" => DispatchOcclusion(act),
                _ => throw new ArgumentException($"Unknown target '{target}'. Valid: lighting, occlusion"),
            };
        }

        // ── lighting ──────────────────────────────────────────────────────────

        static string DispatchLighting(string action) => action switch
        {
            "start" or "" => StartLightingBake(),
            "status"      => GetLightingStatus(),
            "cancel"      => CancelLightingBake(),
            "clear"       => ClearLightingBake(),
            "settings"    => GetLightingSettings(),
            _             => throw new ArgumentException($"Unknown lighting action '{action}'"),
        };

        static string StartLightingBake()
        {
            _ = Lightmapping.BakeAsync();
            return "status:started";
        }

        static string GetLightingStatus()
        {
            if (Lightmapping.isRunning)
            {
                var progress = Lightmapping.buildProgress;
                return $"status:baking\nprogress:{progress:F2}";
            }
            return "status:idle";
        }

        static string CancelLightingBake()
        {
            Lightmapping.Cancel();
            return "ok:cancelled";
        }

        static string ClearLightingBake()
        {
            Lightmapping.Clear();
            return "ok:cleared";
        }

        static string GetLightingSettings()
        {
            if (!Lightmapping.TryGetLightingSettings(out var ls))
                return "err:no_lighting_settings";
            return $"bakeMode:{ls.mixedBakeMode}\n" +
                   $"lightmapResolution:{ls.lightmapResolution}\n" +
                   $"maxAtlasSize:{ls.lightmapMaxSize}\n" +
                   $"bounces:{ls.bounces}\n" +
                   $"filteringMode:{ls.filteringMode}";
        }

        // ── occlusion ─────────────────────────────────────────────────────────

        static string DispatchOcclusion(string action) => action switch
        {
            "start" or "" => StartOcclusionBake(),
            "status"      => GetOcclusionStatus(),
            "clear"       => ClearOcclusionBake(),
            _             => throw new ArgumentException($"Unknown occlusion action '{action}'"),
        };

        static string StartOcclusionBake()
        {
            StaticOcclusionCulling.Compute();
            return "status:started";
        }

        static string GetOcclusionStatus()
        {
            var baked = StaticOcclusionCulling.umbraDataSize > 0;
            return $"status:{(StaticOcclusionCulling.isRunning ? "running" : "idle")}\n" +
                   $"baked:{baked}\n" +
                   $"bytes:{StaticOcclusionCulling.umbraDataSize}";
        }

        static string ClearOcclusionBake()
        {
            StaticOcclusionCulling.Clear();
            return "ok:cleared";
        }
    }
}
