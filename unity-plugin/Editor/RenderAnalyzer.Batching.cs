// Batching analysis: SRP Batcher, static, dynamic, GPU instancing.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor
{
    internal static partial class RenderAnalyzer
    {
        // Reflection cache — initialized once via static field initializer
        private static readonly MethodInfo s_GetSRPCode =
            typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",
                BindingFlags.Static | BindingFlags.NonPublic);

        private static string Batching(string path, string detail)
        {
            var renderers = GetRenderers(path);
            var sb = new StringBuilder();
            sb.AppendLine("BATCHING:");

            AppendSRPBatcher(sb, renderers, detail);
            AppendStaticBatching(sb, renderers, detail);
            AppendGPUInstancing(sb, renderers, detail);

            return sb.ToString().TrimEnd();
        }

        private static void AppendSRPBatcher(StringBuilder sb, Renderer[] renderers, string detail)
        {
            bool srpEnabled = IsSRPEnabled();
            sb.AppendLine($"  SRP BATCHER: {(srpEnabled ? "enabled" : "disabled (built-in pipeline)")}");
            if (!srpEnabled) return;

            var shaderStats = new Dictionary<string, (int compat, int incompat)>();
            foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials)
            {
                if (mat?.shader == null) continue;
                var sn = mat.shader.name;
                int code = SRPBatcherCode(mat.shader);
                shaderStats.TryGetValue(sn, out var s);
                if (code == 0)
                    shaderStats[sn] = (s.compat + 1, s.incompat);
                else
                    shaderStats[sn] = (s.compat, s.incompat + 1);
            }

            int totalIncompat = 0;
            foreach (var kv in shaderStats)
            {
                if (kv.Value.incompat > 0)
                {
                    totalIncompat += kv.Value.incompat;
                    if (detail == "full")
                        sb.AppendLine($"    NOT compat: {kv.Key} ({kv.Value.incompat} refs)");
                }
            }
            if (totalIncompat > 0)
                sb.AppendLine($"  WARN: {totalIncompat} renderers use SRP-incompatible shaders");
        }

        private static void AppendStaticBatching(StringBuilder sb, Renderer[] renderers, string detail)
        {
            int flagged = 0, candidates = 0;
            var candidatePaths = new List<string>();
            foreach (var r in renderers)
            {
                if (GameObjectUtility.GetStaticEditorFlags(r.gameObject)
                    .HasFlag(StaticEditorFlags.BatchingStatic))
                {
                    flagged++;
                }
                else if (IsStaticCandidate(r))
                {
                    candidates++;
                    if (detail == "full" && candidatePaths.Count < 10)
                        candidatePaths.Add(ComponentSerializer.GetPath(r.gameObject));
                }
            }
            sb.AppendLine($"  STATIC BATCH: {flagged} flagged, {candidates} candidates");
            if (detail == "full")
                foreach (var p in candidatePaths)
                    sb.AppendLine($"    candidate: {p}");
        }

        private static void AppendGPUInstancing(StringBuilder sb, Renderer[] renderers, string detail)
        {
            int enabled = 0, couldEnable = 0;
            var matGroups = new Dictionary<Material, int>();
            foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                matGroups.TryGetValue(mat, out var count);
                matGroups[mat] = count + 1;
            }

            foreach (var kv in matGroups)
            {
                var mat = kv.Key;
                var count = kv.Value;
                if (mat.enableInstancing) enabled++;
                else if (count > 1) couldEnable++;
            }
            sb.AppendLine($"  GPU INSTANCING: {enabled} enabled, {couldEnable} could enable");
        }

        // Heuristic: stable (no physics/anim/nav) MeshRenderer on non-static object
        private static bool IsStaticCandidate(Renderer r)
        {
            var go = r.gameObject;
            if (!(r is MeshRenderer)) return false;
            if (r.GetComponent<MeshFilter>() == null) return false;
            if (go.GetComponent<Rigidbody>() != null) return false;
            if (go.GetComponent<Rigidbody2D>() != null) return false;
            if (go.GetComponent<Animator>() != null) return false;
            if (go.GetComponentInParent<Animator>() != null) return false;
#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
            if (go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null) return false;
#endif
            return true;
        }

        private static bool IsSRPEnabled() =>
            GraphicsSettings.currentRenderPipeline != null;

        private static int SRPBatcherCode(Shader s) =>
            s_GetSRPCode?.Invoke(null, new object[] { s, 0 }) is int i ? i : -1;

        internal static string GetBatchKey(Renderer r, bool isSRP)
        {
            var mat = r.sharedMaterial;
            if (mat == null) return "no-material";
            return isSRP
                ? $"srp:{mat?.shader?.name ?? "none"}:{mat.renderQueue}"
                : $"mat:{TransientObjectId.GetWireValue(mat)}:{mat.renderQueue}";
        }
    }
}
