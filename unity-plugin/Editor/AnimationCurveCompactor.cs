using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Groups .x/.y/.z and .r/.g/.b/.a float curves into compact vector/color summary lines.
    // Each line shows first-keyframe-values → last-keyframe-values.
    // Groups where all components have start == end are omitted entirely.
    internal static class AnimationCurveCompactor
    {
        static readonly string[] Vec3Suffixes  = { ".x", ".y", ".z" };
        static readonly string[] ColorSuffixes = { ".r", ".g", ".b", ".a" };

        // Returns compact text for all float bindings (one line per property / group).
        internal static string Format(
            AnimationClip clip,
            EditorCurveBinding[] sortedBindings,
            Dictionary<string, string> pathAliasMap)
        {
            var sb      = new StringBuilder();
            var handled = new HashSet<int>();

            for (int i = 0; i < sortedBindings.Length; i++)
            {
                if (handled.Contains(i)) continue;

                int[] group;
                if (TryFindGroup(sortedBindings, i, Vec3Suffixes, out group))
                {
                    AppendGroup(sb, clip, sortedBindings, group, Vec3Suffixes, pathAliasMap);
                    foreach (int idx in group) handled.Add(idx);
                    continue;
                }
                if (TryFindGroup(sortedBindings, i, ColorSuffixes, out group))
                {
                    AppendGroup(sb, clip, sortedBindings, group, ColorSuffixes, pathAliasMap);
                    foreach (int idx in group) handled.Add(idx);
                    continue;
                }

                AppendStandalone(sb, clip, sortedBindings[i], pathAliasMap);
            }

            return sb.ToString();
        }

        // Returns true when binding[startIdx] is a member of a complete suffix group
        // AND startIdx is the minimum index in that group (so each group is handled once).
        // Searches the entire bindings array (both directions) to handle alphabetic sort
        // edge cases where the "first" logical suffix (e.g. ".r") may appear after others.
        private static bool TryFindGroup(EditorCurveBinding[] bindings, int startIdx,
            string[] suffixes, out int[] indices)
        {
            var prop = bindings[startIdx].propertyName;

            // Identify which suffix this binding matches
            string matchedSuffix = null;
            foreach (var s in suffixes)
                if (prop.EndsWith(s)) { matchedSuffix = s; break; }
            if (matchedSuffix == null) { indices = null; return false; }

            var baseName = prop.Substring(0, prop.Length - matchedSuffix.Length);
            var result   = new int[suffixes.Length];

            for (int s = 0; s < suffixes.Length; s++)
            {
                if (suffixes[s] == matchedSuffix) { result[s] = startIdx; continue; }

                var target = baseName + suffixes[s];
                bool found = false;
                for (int j = 0; j < bindings.Length; j++)
                {
                    if (j == startIdx) continue;
                    if (bindings[j].path == bindings[startIdx].path &&
                        bindings[j].propertyName == target)
                    {
                        result[s] = j;
                        found = true;
                        break;
                    }
                }
                if (!found) { indices = null; return false; }
            }

            // Only process the group when startIdx is the minimum member index,
            // so the group is emitted exactly once as the compactor iterates forward.
            foreach (int idx in result)
                if (idx < startIdx) { indices = null; return false; }

            indices = result;
            return true;
        }

        // Appends one grouped line, or nothing when all components are unchanged.
        private static void AppendGroup(StringBuilder sb, AnimationClip clip,
            EditorCurveBinding[] bindings, int[] groupIndices, string[] suffixes,
            Dictionary<string, string> pathAliasMap)
        {
            var firstVals = new float[groupIndices.Length];
            var lastVals  = new float[groupIndices.Length];

            for (int i = 0; i < groupIndices.Length; i++)
            {
                var keys = AnimationUtility.GetEditorCurve(clip, bindings[groupIndices[i]]).keys;
                firstVals[i] = keys.Length > 0 ? keys[0].value : 0f;
                lastVals[i]  = keys.Length > 0 ? keys[keys.Length - 1].value : 0f;
            }

            // Skip when every component is unchanged
            bool unchanged = true;
            for (int i = 0; i < groupIndices.Length; i++)
                if (!Mathf.Approximately(firstVals[i], lastVals[i])) { unchanged = false; break; }
            if (unchanged) return;

            var baseProp = bindings[groupIndices[0]].propertyName;
            baseProp = baseProp.Substring(0, baseProp.Length - suffixes[0].Length);

            sb.Append(AnimationSerializer.ApplyPropertyAlias(baseProp));
            AnimationSerializer.AppendPathSuffix(sb, bindings[groupIndices[0]].path, pathAliasMap);
            sb.Append(' ');
            AppendTuple(sb, firstVals);
            sb.Append('→');
            AppendTuple(sb, lastVals);
            sb.AppendLine();
        }

        // Appends one line for an ungrouped scalar curve, or nothing when unchanged.
        private static void AppendStandalone(StringBuilder sb, AnimationClip clip,
            EditorCurveBinding binding, Dictionary<string, string> pathAliasMap)
        {
            var keys = AnimationUtility.GetEditorCurve(clip, binding).keys;
            if (keys.Length == 0) return;
            float first = keys[0].value;
            float last  = keys[keys.Length - 1].value;
            if (Mathf.Approximately(first, last)) return;

            sb.Append(AnimationSerializer.ApplyPropertyAlias(binding.propertyName));
            AnimationSerializer.AppendPathSuffix(sb, binding.path, pathAliasMap);
            sb.Append(' ');
            sb.Append(first.ToString("G4", CultureInfo.InvariantCulture));
            sb.Append('→');
            sb.AppendLine(last.ToString("G4", CultureInfo.InvariantCulture));
        }

        private static void AppendTuple(StringBuilder sb, float[] vals)
        {
            sb.Append('(');
            for (int i = 0; i < vals.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(vals[i].ToString("G4", CultureInfo.InvariantCulture));
            }
            sb.Append(')');
        }
    }
}
