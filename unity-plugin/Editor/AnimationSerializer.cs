using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class AnimationSerializer
    {
        private static readonly Dictionary<string, string> PropertyAliasRegistry = new()
        {
            ["m_LocalPosition"]     = "pos",
            ["m_LocalRotation"]     = "rot",
            ["m_LocalScale"]        = "scale",
            ["localEulerAnglesRaw"] = "euler",
            ["blendShape"]          = "blend",
            ["m_Color"]             = "color",
            ["m_Materials"]         = "mat",
            ["m_IsActive"]          = "active",
        };

        public static string Serialize(string path, string clipName, float? sampleTime, bool compact = false)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new System.InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            if (string.IsNullOrEmpty(clipName))
                return SerializeClipList(go);

            var clip = FindClip(go, clipName);
            if (clip == null) throw new System.InvalidOperationException($"Clip not found: {clipName}");

            if (sampleTime.HasValue)
                return SerializeClipAtTime(go, clip, sampleTime.Value);

            return SerializeClipDetail(clip, compact);
        }

        private static string SerializeClipList(GameObject go)
        {
            var clips = GetAllClips(go);
            if (clips == null || clips.Length == 0)
                return "No animation clips";

            var sb = new StringBuilder();
            var animator = go.GetComponent<Animator>();
            sb.Append(animator != null ? "Animator: " : "Animation: ");
            sb.AppendLine(string.Join(", ", System.Array.ConvertAll(clips, c => c.name)));
            sb.AppendLine("---");

            foreach (var clip in clips)
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                sb.Append(clip.name);
                sb.Append(" | ").Append(clip.length.ToString("F1", CultureInfo.InvariantCulture)).Append("s");
                sb.Append(" | ").Append(bindings.Length).Append(" curves");
                if (settings.loopTime) sb.Append(" | loop");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd('\n');
        }

        private static string SerializeClipDetail(AnimationClip clip, bool compact = false)
        {
            var sb       = new StringBuilder();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            var floatB   = AnimationUtility.GetCurveBindings(clip);
            var ptrB     = AnimationUtility.GetObjectReferenceCurveBindings(clip);

            System.Array.Sort(floatB, BindingComparer);
            System.Array.Sort(ptrB,   BindingComparer);

            // Header
            sb.Append("clip: ").Append(clip.name)
              .Append(" | ").Append(clip.length.ToString("F1", CultureInfo.InvariantCulture)).Append("s");
            if (settings.loopTime) sb.Append(" | loop");
            sb.Append(" | ").Append(floatB.Length).Append(" curves");
            if (ptrB.Length > 0) sb.Append(' ').Append(ptrB.Length).Append(" refs");
            sb.AppendLine();

            // Path alias table
            var pathCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var b in floatB) IncrementPath(pathCounts, b.path);
            foreach (var b in ptrB)   IncrementPath(pathCounts, b.path);

            var sortedPaths  = new List<string>(pathCounts.Keys);
            sortedPaths.Sort();
            var pathAliasMap = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var takenNames   = new HashSet<string>();
            foreach (var path in sortedPaths)
            {
                if (pathCounts[path] < 2) continue;
                var segments = path.Split('/');
                var cand     = ToAliasSegment(segments[segments.Length - 1]);
                if (!string.IsNullOrEmpty(cand) && takenNames.Add(cand))
                {
                    pathAliasMap[path] = cand;
                }
                else if (segments.Length >= 2)
                {
                    var parent = ToAliasSegment(segments[segments.Length - 2]);
                    var cand2  = string.IsNullOrEmpty(parent) ? cand + "2" : parent + "_" + cand;
                    if (!string.IsNullOrEmpty(cand2) && takenNames.Add(cand2))
                        pathAliasMap[path] = cand2;
                }
            }

            // Collect property prefixes actually used in float bindings
            var usedPrefixes = new HashSet<string>();
            foreach (var b in floatB)
            {
                var dot    = b.propertyName.IndexOf('.');
                var prefix = dot >= 0 ? b.propertyName.Substring(0, dot) : b.propertyName;
                if (PropertyAliasRegistry.ContainsKey(prefix)) usedPrefixes.Add(prefix);
            }

            // VAL block
            bool hasAliases = usedPrefixes.Count > 0 || pathAliasMap.Count > 0;
            if (hasAliases)
            {
                foreach (var kv in PropertyAliasRegistry)
                    if (usedPrefixes.Contains(kv.Key))
                        sb.Append("VAL $").Append(kv.Value).Append(' ').AppendLine(kv.Key);

                var pathAliasList = new List<KeyValuePair<string, string>>(pathAliasMap);
                pathAliasList.Sort((a, b) => string.Compare(a.Value, b.Value, System.StringComparison.Ordinal));
                foreach (var kv in pathAliasList)
                    sb.Append("VAL $").Append(kv.Value).Append(' ').AppendLine(kv.Key);
            }
            sb.AppendLine("---");

            // Float curves — compact groups x/y/z and r/g/b/a, omits unchanged
            if (compact)
            {
                sb.Append(AnimationCurveCompactor.Format(clip, floatB, pathAliasMap));
            }
            else
            {
                foreach (var binding in floatB)
                {
                    var keys     = AnimationUtility.GetEditorCurve(clip, binding).keys;
                    var propName = ApplyPropertyAlias(binding.propertyName);
                    sb.Append("curve: ").Append(propName);
                    AppendPathSuffix(sb, binding.path, pathAliasMap);
                    sb.AppendLine();
                    AppendKeyframes(sb, keys);
                }
            }

            // PPtrCurve curves
            foreach (var binding in ptrB)
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                sb.Append("ref: ").Append(binding.propertyName);
                AppendPathSuffix(sb, binding.path, pathAliasMap);
                sb.AppendLine();

                int limit = System.Math.Min(keys.Length, 50);
                for (int i = 0; i < limit; i++)
                {
                    sb.Append("  ")
                      .Append(keys[i].time.ToString("F3", CultureInfo.InvariantCulture))
                      .Append(": ")
                      .AppendLine(keys[i].value != null ? keys[i].value.name : "null");
                }
                if (keys.Length > 50) sb.AppendLine($"  ...+{keys.Length - 50} more");
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static string SerializeClipAtTime(GameObject go, AnimationClip clip, float time)
        {
            var sb = new StringBuilder();
            sb.Append("sample: ").Append(clip.name).Append(" @ ");
            sb.Append(time.ToString("F2", CultureInfo.InvariantCulture)).AppendLine("s");
            sb.AppendLine("---");

            bool wasActive = AnimationMode.InAnimationMode();
            if (!wasActive) AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, clip, time);
                AnimationMode.EndSampling();

                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    float val = curve.Evaluate(time);
                    sb.Append(binding.propertyName).Append(": ");
                    sb.AppendLine(val.ToString("G4", CultureInfo.InvariantCulture));
                }
            }
            finally { if (!wasActive) AnimationMode.StopAnimationMode(); }
            return sb.ToString().TrimEnd('\n');
        }

        internal static AnimationClip FindClip(GameObject go, string clipName)
        {
            var clips = GetAllClips(go);
            if (clips == null) return null;
            foreach (var c in clips)
                if (c.name == clipName) return c;
            return null;
        }

        internal static AnimationClip[] GetAllClips(GameObject go)
        {
            var animator = go.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                return animator.runtimeAnimatorController.animationClips;

            var animation = go.GetComponent<Animation>();
            if (animation != null)
            {
                var list = new List<AnimationClip>();
                foreach (AnimationState state in animation)
                    list.Add(state.clip);
                return list.ToArray();
            }
            return null;
        }

        private static int BindingComparer(EditorCurveBinding a, EditorCurveBinding b)
        {
            int c = string.Compare(a.path, b.path, System.StringComparison.Ordinal);
            return c != 0 ? c : string.Compare(a.propertyName, b.propertyName, System.StringComparison.Ordinal);
        }

        private static string ToAliasSegment(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == '_' || c == ' ') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        private static void IncrementPath(Dictionary<string, int> dict, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            dict[path] = dict.TryGetValue(path, out var c) ? c + 1 : 1;
        }

        internal static string ApplyPropertyAlias(string propertyName)
        {
            var dot = propertyName.IndexOf('.');
            if (dot < 0)
                return PropertyAliasRegistry.TryGetValue(propertyName, out var exact)
                    ? "$" + exact
                    : propertyName;
            var prefix = propertyName.Substring(0, dot);
            return PropertyAliasRegistry.TryGetValue(prefix, out var alias)
                ? "$" + alias + propertyName.Substring(dot)
                : propertyName;
        }

        internal static void AppendPathSuffix(StringBuilder sb, string path,
                                              Dictionary<string, string> pathAliasMap)
        {
            if (string.IsNullOrEmpty(path)) return;
            sb.Append(" (");
            sb.Append(pathAliasMap.TryGetValue(path, out var alias) ? "$" + alias : path);
            sb.Append(')');
        }

        private static void AppendKeyframes(StringBuilder sb, UnityEngine.Keyframe[] keys)
        {
            int limit = System.Math.Min(keys.Length, 50);
            for (int i = 0; i < limit; i++)
            {
                sb.Append("  ")
                  .Append(keys[i].time.ToString("F3", CultureInfo.InvariantCulture))
                  .Append(": ")
                  .AppendLine(keys[i].value.ToString("G4", CultureInfo.InvariantCulture));
            }
            if (keys.Length > 50) sb.AppendLine($"  ...+{keys.Length - 50} more");
        }
    }
}
