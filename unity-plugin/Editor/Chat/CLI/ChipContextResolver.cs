// Resolves chip paths to context text at three depths.
// PathOnly: path as-is. Summary: SelectionSummary-style. Full: ComponentSerializer dump.
// Asset paths (not starting with '/') are always PathOnly.
// Full > FullBudget chars falls back to Summary.
// H6: ChipKind enum removed — string kindKey used throughout.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    // Internal enum kept for resolution logic — NOT part of public surface (CRITIC NOTE).
    internal enum ChipDepth { PathOnly, Summary, Full }

    internal static class ChipContextResolver
    {
        internal const int FullBudget = 2000;

#if UNITY_INCLUDE_TESTS
        // Seam: inject in tests instead of calling ComponentSerializer.FindObject
        internal static Func<string, GameObject> FindObjectOverride;
#endif

        /// <summary>
        /// Serialize a chip ref to the AI-facing bracket format using the string kindKey.
        /// Hierarchy objects include a transient EntityId: [hierarchy:/Player#12345].
        /// </summary>
        internal static string FormatChipRef(string kindKey, string path, string objectId)
        {
            if (kindKey == ChipKindKeys.Hierarchy && !string.IsNullOrEmpty(objectId) && objectId != "0")
                return $"[{kindKey}:{path}#{objectId}]";
            return $"[{kindKey}:{path}]";
        }

        /// <summary>
        /// Full pipeline: Object → bracket ref string. Returns null if object not recognized.
        /// Reuses ChipKindRegistry.Resolve → provider.Create → FormatChipRef (same as chat LLM path).
        /// </summary>
        internal static string FormatAsRef(UnityEngine.Object obj)
        {
            if (obj == null) return null;
            var assetPath = AssetDatabase.GetAssetPath(obj);
            var provider = ChipKindRegistry.Resolve(obj, assetPath);
            if (provider == null) return null;
            var chip = provider.Create(obj, assetPath);
            return FormatChipRef(chip.KindKey, chip.Path, chip.ObjectId);
        }

        /// <summary>
        /// Emit the AI-facing string for one chip using the registry pipeline.
        /// depth "none"    → "" (skip).
        /// depth "path"    → bracket format only.
        /// depth "summary" → bracket + newline + resolved summary.
        /// depth "full"    → bracket + newline + full resolved text.
        /// Any other depth → treated as "path" (safe fallback).
        /// </summary>
        internal static string EmitTyped(string kindKey, string path, string objectId, string depth,
            Func<string, ChipDepth, string> resolveFn)
        {
            if (depth == "none") return "";
            var bracket = FormatChipRef(kindKey, path, objectId);
            if (depth != "summary" && depth != "full") return bracket;
            var chipDepth = depth == "full" ? ChipDepth.Full : ChipDepth.Summary;
            var resolved  = resolveFn(path, chipDepth);
            return bracket + "\n" + resolved;
        }

        /// <summary>
        /// Full registry pipeline: resolves each chip via its provider's FormatPayload.
        /// Falls back to EmitTyped if provider not found.
        /// </summary>
        internal static string ResolveAllTyped(List<ChipData> chips, ChipConfig cfg)
        {
            if (chips == null || chips.Count == 0) return "";
            cfg = cfg ?? new ChipConfig();
            var sb = new StringBuilder();
            foreach (var chip in chips)
            {
                var depth    = cfg.DepthFor(chip.KindKey);
                var emitted  = EmitViaProvider(chip, depth);
                if (string.IsNullOrEmpty(emitted)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(emitted);
            }
            return sb.ToString();
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>1 chip → Full depth; 2+ chips → Summary for each. Test-only — no production callers.</summary>
        internal static string ResolveAll(List<string> chipPaths)
        {
            if (chipPaths == null || chipPaths.Count == 0) return "";
            var depth = chipPaths.Count == 1 ? ChipDepth.Full : ChipDepth.Summary;
            var sb = new StringBuilder();
            foreach (var path in chipPaths)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(ResolveOne(path, depth));
            }
            return sb.ToString();
        }
#endif

        internal static string ResolveOne(string chipPath, ChipDepth depth, int budgetOverride = -1)
        {
            if (IsAssetPath(chipPath)) return chipPath;
            if (depth == ChipDepth.PathOnly)
            {
                var goForId = FindGo(chipPath);
                if (goForId != null && goForId)
                    return chipPath + TransientObjectId.GetHexRef(goForId);
                return chipPath;
            }

            var go = FindGo(chipPath);
            if (go == null || !go) return chipPath;

            if (depth == ChipDepth.Summary) return SelectionSummary.Summarize(go, "Context");

            var budget = budgetOverride >= 0 ? budgetOverride : FullBudget;
            var full = ComponentSerializer.SerializeAll(TransientObjectId.GetWireValue(go));
            if (full != null && full.Length <= budget) return full;
            return SelectionSummary.Summarize(go, "Context");
        }

        // ── private helpers ───────────────────────────────────────────────────

        /// <summary>Emit via provider.FormatPayload if provider found; else fallback EmitTyped.</summary>
        private static string EmitViaProvider(ChipData chip, string depth)
        {
            if (depth == "none") return "";
            var provider = ChipKindRegistry.ForKey(chip.KindKey);
            if (provider == null)
                return EmitTyped(chip.KindKey, chip.Path, chip.ObjectId, depth,
                    (p, d) => ResolveOne(p, d));

            // Resolve summary for context
            string resolved = "";
            if (depth == "summary" || depth == "full")
            {
                var chipDepth = depth == "full" ? ChipDepth.Full : ChipDepth.Summary;
                resolved = ResolveOne(chip.Path, chipDepth);
            }
            var ctx = new ChipPayloadContext(depth, resolved);
            return provider.FormatPayload(chip, ctx);
        }

        private static bool IsAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.StartsWith("/")) return false;                                          // unqualified scene path
            if (path.IndexOf(":/", System.StringComparison.Ordinal) > 0) return false;     // scene-qualified "SceneName:/..."
            return true;
        }

        private static GameObject FindGo(string path)
        {
#if UNITY_INCLUDE_TESTS
            if (FindObjectOverride != null) return FindObjectOverride(path);
#endif
            try { return ComponentSerializer.FindObject(path); }
            catch { return null; }
        }
    }
}
