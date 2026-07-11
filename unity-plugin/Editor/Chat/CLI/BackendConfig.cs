// Per-backend CLI config — data types only. Persistence lives in BackendConfigStore.cs.
// JsonUtility requires [Serializable] + public fields.
// H15: HierarchyDepth default changed from "summary" to "path" (BREAKING).
// P4: Generic override layer via parallel arrays (JsonUtility-friendly).
// Model preset types live in ModelPresets.cs.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    [Serializable]
    internal sealed class ClaudeBackendConfig
    {
        public string PermissionMode = "plan";   // "plan" | "acceptEdits"
        public string Model          = "";        // empty = default
        public string ExtraArgs      = "";        // whitespace-split appended to argv

        internal ClaudeBackendConfig WithModel(string m) =>
            Model == m ? this : new ClaudeBackendConfig { PermissionMode = PermissionMode, Model = m, ExtraArgs = ExtraArgs };
    }

    [Serializable]
    internal sealed class CodexBackendConfig
    {
        public string Model             = "";
        public string PermissionMode    = "danger-full-access";
        public int    StartupTimeoutSec = 30;
        public string ExtraArgs         = "";

        internal CodexBackendConfig WithModel(string m) =>
            Model == m ? this : new CodexBackendConfig { Model = m, PermissionMode = PermissionMode, StartupTimeoutSec = StartupTimeoutSec, ExtraArgs = ExtraArgs };
    }

    [Serializable]
    internal sealed class AntigravityBackendConfig
    {
        public string Model        = ""; // empty = agy default
        public string ApprovalMode = ""; // "" | "yolo" (mapped to --dangerously-skip-permissions)
        public bool   Sandbox      = false;
        public string ExtraArgs    = "";

        internal AntigravityBackendConfig WithModel(string m) =>
            Model == m ? this : new AntigravityBackendConfig { Model = m, ApprovalMode = ApprovalMode, Sandbox = Sandbox, ExtraArgs = ExtraArgs };
    }

    [Serializable]
    internal sealed class KimiBackendConfig
    {
        public string Model        = ""; // empty = use kimi's config.toml default
        public string ApprovalMode = "";           // "" | "yolo" | "plan"
        public string ExtraArgs    = "";

        internal KimiBackendConfig WithModel(string m) =>
            Model == m ? this : new KimiBackendConfig { Model = m, ApprovalMode = ApprovalMode, ExtraArgs = ExtraArgs };
    }

    [Serializable]
    internal sealed class OpenCodeBackendConfig
    {
        public string Model            = "";    // "anthropic/claude-sonnet-4" or empty = opencode default
        public bool   SkipPermissions  = true;  // --dangerously-skip-permissions
        public string ExtraArgs        = "";

        internal OpenCodeBackendConfig WithModel(string m) =>
            Model == m ? this : new OpenCodeBackendConfig { Model = m, SkipPermissions = SkipPermissions, ExtraArgs = ExtraArgs };
    }

    /// <summary>Per-kind user overrides. Null fields = use provider default.</summary>
    internal readonly struct ChipDisplayOverride
    {
        public readonly string Depth;    // null = use provider default
        public readonly string HexColor; // null = use provider default

        public ChipDisplayOverride(string depth, string hexColor)
        {
            Depth    = string.IsNullOrEmpty(depth)    ? null : depth;
            HexColor = string.IsNullOrEmpty(hexColor) ? null : hexColor;
        }
    }

    /// <summary>Per-kind detail depth for chip context sent to AI.</summary>
    [Serializable]
    internal sealed class ChipConfig
    {
        // Legacy explicit fields — kept for backward-compat with existing JSON files.
        // "path" = bracket format only. "summary" = SelectionSummary.
        // "full" = ComponentSerializer dump (hierarchy only). "none" = omit.
        // BREAKING: HierarchyDepth default changed from "summary" to "path" (H15).
        public string HierarchyDepth = "path";
        public string ScriptDepth    = "path";
        public string SceneDepth     = "path";
        public string PrefabDepth    = "path";
        public string AssetDepth     = "path";

        // P4: Generic per-kind overrides — parallel arrays (JsonUtility-friendly).
        // Keyed by kindKey. Overrides the explicit fields above when present.
        public string[] OverrideKeys   = new string[0];
        public string[] OverrideDepths = new string[0]; // "" = no override
        public string[] OverrideColors = new string[0]; // "" = no override

        [NonSerialized] private Dictionary<string, ChipDisplayOverride> _cache;

        /// <summary>Get override entry for a kind (fields may be null meaning "not set").</summary>
        internal ChipDisplayOverride GetOverride(string kindKey)
        {
            EnsureCache();
            _cache.TryGetValue(kindKey, out var ov);
            return ov;
        }

        /// <summary>Set depth override. null = clear.</summary>
        internal void SetDepthOverride(string kindKey, string depth)
        {
            EnsureCache();
            _cache.TryGetValue(kindKey, out var existing);
            var updated = new ChipDisplayOverride(depth, existing.HexColor);
            if (string.IsNullOrEmpty(updated.Depth) && string.IsNullOrEmpty(updated.HexColor)) _cache.Remove(kindKey);
            else _cache[kindKey] = updated;
        }

        /// <summary>Set color override. null = clear.</summary>
        internal void SetColorOverride(string kindKey, string hexColor)
        {
            EnsureCache();
            _cache.TryGetValue(kindKey, out var existing);
            var updated = new ChipDisplayOverride(existing.Depth, hexColor);
            if (string.IsNullOrEmpty(updated.Depth) && string.IsNullOrEmpty(updated.HexColor)) _cache.Remove(kindKey);
            else _cache[kindKey] = updated;
        }

        /// <summary>Resolve effective hex color: override > provider > gray fallback.</summary>
        internal string ResolveColor(string kindKey)
        {
            EnsureCache();
            if (_cache.TryGetValue(kindKey, out var ov) && ov.HexColor != null)
                return ov.HexColor;
            return ChipKindRegistry.ForKey(kindKey)?.HexColor ?? "#94a3b8";
        }

        /// <summary>
        /// Return the configured depth string for a given kind key.
        /// Resolution: override cache -> legacy switch -> provider default -> "path".
        /// </summary>
        internal string DepthFor(string kindKey)
        {
            EnsureCache();
            // 1. Override cache wins
            if (_cache.TryGetValue(kindKey, out var ov) && ov.Depth != null)
                return ov.Depth;
            // 2. Legacy explicit fields
            switch (kindKey)
            {
                case ChipKindKeys.Hierarchy: return HierarchyDepth;
                case ChipKindKeys.Script:    return ScriptDepth;
                case ChipKindKeys.Scene:     return SceneDepth;
                case ChipKindKeys.Prefab:    return PrefabDepth;
                case ChipKindKeys.Material:
                case ChipKindKeys.Texture:
                case ChipKindKeys.ScriptableObject:
                case ChipKindKeys.Asset:     return AssetDepth;
                // 3. Registry provider default or "path"
                default: return ChipKindRegistry.ForKey(kindKey)?.DefaultDepth ?? "path";
            }
        }

        /// <summary>Flush cache back to parallel arrays (call before Save).</summary>
        internal void FlushToArrays()
        {
            if (_cache == null) return;
            var keys   = new List<string>(_cache.Count);
            var depths = new List<string>(_cache.Count);
            var colors = new List<string>(_cache.Count);
            foreach (var kv in _cache)
            {
                keys.Add(kv.Key);
                depths.Add(kv.Value.Depth   ?? "");
                colors.Add(kv.Value.HexColor ?? "");
            }
            OverrideKeys   = keys.ToArray();
            OverrideDepths = depths.ToArray();
            OverrideColors = colors.ToArray();
        }

        private void EnsureCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, ChipDisplayOverride>();

            // One-time legacy→override migration: no override keys yet → seed from non-default legacy fields.
            if (OverrideKeys == null || OverrideKeys.Length == 0)
            {
                TrySeedLegacy(ChipKindKeys.Hierarchy, HierarchyDepth, "path");
                TrySeedLegacy(ChipKindKeys.Script,    ScriptDepth,    "path");
                TrySeedLegacy(ChipKindKeys.Scene,     SceneDepth,     "path");
                TrySeedLegacy(ChipKindKeys.Prefab,    PrefabDepth,    "path");
                TrySeedLegacy(ChipKindKeys.Asset,     AssetDepth,     "path");
                return;
            }

            // Rebuild from parallel arrays (length-safe for jagged data).
            int len = OverrideKeys.Length;
            for (int i = 0; i < len; i++)
            {
                var key   = OverrideKeys[i];
                if (string.IsNullOrEmpty(key)) continue;
                var depth = i < (OverrideDepths?.Length ?? 0) ? OverrideDepths[i] : "";
                var color = i < (OverrideColors?.Length ?? 0) ? OverrideColors[i] : "";
                if (!string.IsNullOrEmpty(depth) || !string.IsNullOrEmpty(color))
                    _cache[key] = new ChipDisplayOverride(depth, color);
            }
        }

        private void TrySeedLegacy(string key, string value, string def)
        {
            if (!string.IsNullOrEmpty(value) && value != def)
                _cache[key] = new ChipDisplayOverride(value, null);
        }
    }

}
