using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class AnimationHelper
    {
        private static readonly string[] Vec3Suffixes = { ".x", ".y", ".z" };
        private static readonly string[] ColorSuffixes = { ".r", ".g", ".b", ".a" };

        private static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeof(Transform);
            // Check common Unity types first
            var unityType = Type.GetType($"UnityEngine.{typeName}, UnityEngine")
                ?? Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
            if (unityType != null) return unityType;
            // Full type name or Assembly-CSharp
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName, throwOnError: false);
                if (t != null) return t;
            }
            throw new ArgumentException($"Component type not found: {typeName}");
        }

        public static string CreateClip(string path, string clipName, string property, string keysStr, string componentType = null, string bindingPath = null, string tangent = null)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var clip = new AnimationClip();
            clip.name = clipName;
            clip.frameRate = 60;

            if (!string.IsNullOrEmpty(keysStr))
                SetCurvesFromKeys(clip, property, keysStr, ResolveComponentType(componentType), bindingPath ?? "", tangent);

            // Save as asset
            var assetPath = SaveClipAsset(clip, clipName);

            // Ensure Animator on object
            var animator = EnsureAnimator(go);

            // Create or get AnimatorController and add clip
            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (controller == null)
            {
                var ctrlPath = assetPath.Replace(".anim", ".controller");
                controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                animator.runtimeAnimatorController = controller;
            }
            controller.AddMotion(clip);

            var bindings = AnimationUtility.GetCurveBindings(clip);
            return $"created: {clipName} | {clip.length.ToString("F1", CultureInfo.InvariantCulture)}s | {bindings.Length} curves | saved: {assetPath}";
        }

        public static string EditClip(string path, string clipName, string action, string property, string keysStr, string componentType = null, string bindingPath = null, string tangent = null)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var clip = AnimationSerializer.FindClip(go, clipName);
            if (clip == null) throw new InvalidOperationException($"Clip not found: {clipName}");

            Undo.RecordObject(clip, "Edit Animation");

            var compType = ResolveComponentType(componentType);
            var bp = bindingPath ?? "";
            switch (action)
            {
                case "edit":
                case "add_key":
                    AddKeys(clip, property, keysStr, compType, bp, tangent);
                    break;
                case "remove_key":
                    RemoveKey(clip, property, keysStr, compType, bp);
                    break;
                case "remove_curve":
                    RemoveCurve(clip, property, compType, bp);
                    break;
                case "set_keys":
                    SetCurvesFromKeys(clip, property, keysStr, compType, bp, tangent);
                    break;
                case "set_loop":
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = keysStr != "false";
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    break;
                case "set_wrap":
                    var wrapSettings = AnimationUtility.GetAnimationClipSettings(clip);
                    var wrapValue = keysStr?.Trim().ToLowerInvariant() ?? "default";
                    wrapSettings.loopTime = wrapValue == "loop" || wrapValue == "pingpong";
                    clip.wrapMode = wrapValue switch
                    {
                        "once" => WrapMode.Once,
                        "loop" => WrapMode.Loop,
                        "pingpong" => WrapMode.PingPong,
                        "clamp" => WrapMode.ClampForever,
                        _ => WrapMode.Default
                    };
                    AnimationUtility.SetAnimationClipSettings(clip, wrapSettings);
                    break;
                case "set_framerate":
                    var rate = float.Parse(keysStr, CultureInfo.InvariantCulture);
                    if (rate <= 0) throw new ArgumentException($"frameRate must be > 0, got {rate}");
                    clip.frameRate = rate;
                    break;
                default:
                    throw new ArgumentException($"Unknown action: {action}");
            }

            EditorUtility.SetDirty(clip);
            return $"edited: {clipName} | {action} {property}";
        }

        public static string Preview(string path, string clipName, string action, float time)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var clip = AnimationSerializer.FindClip(go, clipName);
            if (clip == null) throw new InvalidOperationException($"Clip not found: {clipName}");

            switch (action)
            {
                case "start":
                    if (!AnimationMode.InAnimationMode())
                        AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, clip, 0f);
                    AnimationMode.EndSampling();
                    return "animation_mode: started";
                case "stop":
                    if (AnimationMode.InAnimationMode())
                        AnimationMode.StopAnimationMode();
                    return "animation_mode: stopped";
                case "sample":
                default:
                    bool wasActive = AnimationMode.InAnimationMode();
                    if (!wasActive)
                        AnimationMode.StartAnimationMode();
                    try
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(go, clip, time);
                        AnimationMode.EndSampling();

                        var sb = new StringBuilder();
                        sb.Append("preview: ").Append(clipName).Append(" @ ");
                        sb.Append(time.ToString("F2", CultureInfo.InvariantCulture)).AppendLine("s");
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        foreach (var b in bindings)
                        {
                            var curve = AnimationUtility.GetEditorCurve(clip, b);
                            sb.Append(b.propertyName).Append(": ");
                            sb.AppendLine(curve.Evaluate(time).ToString("G4", CultureInfo.InvariantCulture));
                        }
                        return sb.ToString().TrimEnd('\n');
                    }
                    finally { if (!wasActive) AnimationMode.StopAnimationMode(); }
            }
        }

        // Parse "t:0 v:(1,2,3); t:1 v:(4,5,6)" or "t:0 v:0; t:1 v:5"
        internal static bool IsVector3(string keysStr)
        {
            return keysStr.Contains("(");
        }

        internal static void SetCurvesFromKeys(AnimationClip clip, string property, string keysStr, Type componentType = null, string bindingPath = "", string tangent = null)
        {
            var type = componentType ?? typeof(Transform);
            var normalized = NormalizeProperty(property);
            if (keysStr.Contains("#"))
            {
                SetColorCurves(clip, property, keysStr, type, bindingPath, tangent);
                return;
            }
            if (IsVector3(keysStr))
            {
                var parsed = ParseVector3Keys(keysStr);
                for (int axis = 0; axis < 3; axis++)
                {
                    var binding = EditorCurveBinding.FloatCurve(bindingPath, type, $"{normalized}{Vec3Suffixes[axis]}");
                    var curve = new AnimationCurve(parsed[axis]);
                    ApplyTangents(curve, tangent);
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
            }
            else
            {
                var keys = ParseFloatKeys(keysStr);
                var binding = EditorCurveBinding.FloatCurve(bindingPath, type, normalized);
                var curve = new AnimationCurve(keys);
                ApplyTangents(curve, tangent);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        internal static Keyframe[][] ParseVector3Keys(string keysStr)
        {
            // Returns [x_keys, y_keys, z_keys]
            var result = new Keyframe[3][];
            var xList = new List<Keyframe>();
            var yList = new List<Keyframe>();
            var zList = new List<Keyframe>();

            var parts = keysStr.Split(';');
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                float time = ParseTimeFromPart(p);
                var vecStr = ExtractValue(p);
                var vec = ValueParser.ParseVector3(vecStr);
                xList.Add(new Keyframe(time, vec.x));
                yList.Add(new Keyframe(time, vec.y));
                zList.Add(new Keyframe(time, vec.z));
            }
            result[0] = xList.ToArray();
            result[1] = yList.ToArray();
            result[2] = zList.ToArray();
            return result;
        }

        internal static Keyframe[] ParseFloatKeys(string keysStr)
        {
            var list = new List<Keyframe>();
            var parts = keysStr.Split(';');
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;
                float time = ParseTimeFromPart(p);
                float value = float.Parse(ExtractValue(p), CultureInfo.InvariantCulture);
                list.Add(new Keyframe(time, value));
            }
            return list.ToArray();
        }

        private static float ParseTimeFromPart(string part)
        {
            // "t:0.5 v:..." → 0.5
            var tIdx = part.IndexOf("t:");
            if (tIdx == -1) return 0f;
            var end = part.IndexOf(' ', tIdx + 2);
            if (end == -1) end = part.Length;
            return float.Parse(part.Substring(tIdx + 2, end - tIdx - 2), CultureInfo.InvariantCulture);
        }

        private static string ExtractValue(string part)
        {
            // "t:0 v:(1,2,3)" → "(1,2,3)" or "t:0 v:5" → "5"
            var vIdx = part.IndexOf("v:");
            if (vIdx == -1) return "0";
            return part.Substring(vIdx + 2).Trim();
        }

        private static void AddKeys(AnimationClip clip, string property, string keysStr, Type componentType = null, string bindingPath = "", string tangent = null)
        {
            var type = componentType ?? typeof(Transform);
            var normalized = NormalizeProperty(property);
            if (IsVector3(keysStr))
            {
                var parsed = ParseVector3Keys(keysStr);
                for (int axis = 0; axis < 3; axis++)
                {
                    var binding = EditorCurveBinding.FloatCurve(bindingPath, type, $"{normalized}{Vec3Suffixes[axis]}");
                    var existing = AnimationUtility.GetEditorCurve(clip, binding) ?? new AnimationCurve();
                    foreach (var k in parsed[axis])
                        existing.AddKey(k);
                    ApplyTangents(existing, tangent);
                    AnimationUtility.SetEditorCurve(clip, binding, existing);
                }
            }
            else
            {
                var newKeys = ParseFloatKeys(keysStr);
                var binding = EditorCurveBinding.FloatCurve(bindingPath, type, normalized);
                var existing = AnimationUtility.GetEditorCurve(clip, binding);
                if (existing == null) existing = new AnimationCurve();
                foreach (var k in newKeys)
                    existing.AddKey(k);
                ApplyTangents(existing, tangent);
                AnimationUtility.SetEditorCurve(clip, binding, existing);
            }
        }

        private static void RemoveKey(AnimationClip clip, string property, string keysStr, Type componentType = null, string bindingPath = "")
        {
            var type = componentType ?? typeof(Transform);
            float time = ParseTimeFromPart(keysStr ?? "t:0");
            var normalized = NormalizeProperty(property);

            // For Vector3 properties, remove from all axis curves
            bool isVec3 = normalized == "m_LocalPosition" || normalized == "m_LocalScale" || normalized == "localEulerAnglesRaw";
            if (isVec3)
            {
                foreach (var suffix in Vec3Suffixes)
                {
                    var b = EditorCurveBinding.FloatCurve(bindingPath, type, normalized + suffix);
                    var c = AnimationUtility.GetEditorCurve(clip, b);
                    if (c == null) continue;
                    for (int i = 0; i < c.keys.Length; i++)
                    {
                        if (Mathf.Approximately(c.keys[i].time, time)) { c.RemoveKey(i); break; }
                    }
                    AnimationUtility.SetEditorCurve(clip, b, c);
                }
                return;
            }

            var binding = EditorCurveBinding.FloatCurve(bindingPath, type, normalized);
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null) return;
            for (int i = 0; i < curve.keys.Length; i++)
            {
                if (Mathf.Approximately(curve.keys[i].time, time)) { curve.RemoveKey(i); break; }
            }
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static void RemoveCurve(AnimationClip clip, string property, Type componentType = null, string bindingPath = "")
        {
            var type = componentType ?? typeof(Transform);
            var normalized = NormalizeProperty(property);

            // Try removing single property
            var binding = EditorCurveBinding.FloatCurve(bindingPath, type, normalized);
            AnimationUtility.SetEditorCurve(clip, binding, null);

            // Also try removing .x, .y, .z variants
            foreach (var suffix in Vec3Suffixes)
            {
                var b = EditorCurveBinding.FloatCurve(bindingPath, type, normalized + suffix);
                AnimationUtility.SetEditorCurve(clip, b, null);
            }
        }

        private static string NormalizeProperty(string property)
        {
            string suffix = "";
            int dotIdx = property.LastIndexOf('.');
            if (dotIdx >= 0)
            {
                suffix = property.Substring(dotIdx);
                property = property.Substring(0, dotIdx);
            }
            var normalized = property switch
            {
                "localPosition" => "m_LocalPosition",
                "localScale" => "m_LocalScale",
                "localRotation" or "localEulerAngles" => "localEulerAnglesRaw",
                _ => property
            };
            return normalized + suffix;
        }

        private static void ApplyTangents(AnimationCurve curve, string tangent)
        {
            var mode = (tangent ?? "auto") switch
            {
                "linear"   => AnimationUtility.TangentMode.Linear,
                "constant" => AnimationUtility.TangentMode.Constant,
                "free"     => AnimationUtility.TangentMode.Auto,
                "smooth"   => AnimationUtility.TangentMode.Auto,
                _          => AnimationUtility.TangentMode.ClampedAuto
            };
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
                AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
            }
        }

        private static Animator EnsureAnimator(GameObject go)
        {
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(go);
            return animator;
        }

        private static string SaveClipAsset(AnimationClip clip, string name)
        {
            if (name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0)
                throw new ArgumentException($"clipName must not contain path separators: {name}");
            var assetPath = $"Assets/Animations/{name}.anim";
            AssetHelper.EnsureDirectory(assetPath);
            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssets();
            return assetPath;
        }

        public static string GetEvents(string path, string clipName)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));
            var clip = AnimationSerializer.FindClip(go, clipName);
            if (clip == null) throw new InvalidOperationException($"Clip not found: {clipName}");

            var events = AnimationUtility.GetAnimationEvents(clip);
            if (events.Length == 0) return "events: 0";

            var sb = new StringBuilder();
            sb.Append("events: ").AppendLine(events.Length.ToString());
            foreach (var e in events)
            {
                sb.Append("t:").Append(e.time.ToString("F4", CultureInfo.InvariantCulture))
                  .Append(" fn:").Append(e.functionName)
                  .Append(" i:").Append(e.intParameter)
                  .Append(" f:").Append(e.floatParameter.ToString("G4", CultureInfo.InvariantCulture))
                  .Append(" s:").AppendLine(e.stringParameter);
            }
            return sb.ToString().TrimEnd('\n', '\r');
        }

        public static string AddEvent(string path, string clipName, float time, string functionName,
            int? intParam, float? floatParam, string stringParam)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));
            var clip = AnimationSerializer.FindClip(go, clipName);
            if (clip == null) throw new InvalidOperationException($"Clip not found: {clipName}");
            if (!AssetDatabase.IsMainAsset(clip) && !AssetDatabase.IsSubAsset(clip))
                throw new InvalidOperationException($"Clip is not an asset (read-only): {clipName}");

            Undo.RecordObject(clip, "Add Animation Event");
            var e = new AnimationEvent
            {
                time = time,
                functionName = functionName ?? "",
                intParameter = intParam ?? 0,
                floatParameter = floatParam ?? 0f,
                stringParameter = stringParam ?? ""
            };
            var list = AnimationUtility.GetAnimationEvents(clip).ToList();
            list.Add(e);
            list.Sort((a, b) => a.time.CompareTo(b.time));
            AnimationUtility.SetAnimationEvents(clip, list.ToArray());
            EditorUtility.SetDirty(clip);
            return $"event added: {e.functionName} @ {time.ToString("F2", CultureInfo.InvariantCulture)}s";
        }

        public static string RemoveEvent(string path, string clipName, float time)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));
            var clip = AnimationSerializer.FindClip(go, clipName);
            if (clip == null) throw new InvalidOperationException($"Clip not found: {clipName}");

            Undo.RecordObject(clip, "Remove Animation Event");
            var list = AnimationUtility.GetAnimationEvents(clip).ToList();
            int removed = list.RemoveAll(e => Mathf.Approximately(e.time, time));
            if (removed == 0)
                throw new InvalidOperationException($"No events found at time {time.ToString("F4", CultureInfo.InvariantCulture)}");
            AnimationUtility.SetAnimationEvents(clip, list.ToArray());
            EditorUtility.SetDirty(clip);
            return $"event removed: {removed} at {time.ToString("F2", CultureInfo.InvariantCulture)}s";
        }

        internal static void SetColorCurves(AnimationClip clip, string property, string keysStr, Type componentType, string bindingPath, string tangent)
        {
            var type = componentType ?? typeof(UnityEngine.Light);
            var normalized = NormalizeProperty(property);
            var rKeys = new List<Keyframe>();
            var gKeys = new List<Keyframe>();
            var bKeys = new List<Keyframe>();
            var aKeys = new List<Keyframe>();

            foreach (var part in keysStr.Split(';'))
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;
                float time2 = ParseTimeFromPart(p);
                var hex = ExtractValue(p).Trim();
                var color = ParseHexColor(hex);
                rKeys.Add(new Keyframe(time2, color.r));
                gKeys.Add(new Keyframe(time2, color.g));
                bKeys.Add(new Keyframe(time2, color.b));
                aKeys.Add(new Keyframe(time2, color.a));
            }

            var keyArrays = new[] { rKeys, gKeys, bKeys, aKeys };
            for (int i = 0; i < 4; i++)
            {
                var binding = EditorCurveBinding.FloatCurve(bindingPath, type, $"{normalized}{ColorSuffixes[i]}");
                var curve = new AnimationCurve(keyArrays[i].ToArray());
                ApplyTangents(curve, tangent);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        private static Color ParseHexColor(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color c))
                throw new ArgumentException($"Invalid color hex: '{hex}'");
            return c;
        }

        public static string GetClipPath(string path, string clipName)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));
            var clip = AnimationSerializer.FindClip(go, clipName);
            if (clip == null) throw new InvalidOperationException($"Clip not found: {clipName}");
            var assetPath = AssetDatabase.GetAssetPath(clip);
            return string.IsNullOrEmpty(assetPath) ? "no asset path" : assetPath;
        }
    }
}
