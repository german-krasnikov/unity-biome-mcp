using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class AnimatorControllerHelper
    {
        public static string AddParameters(string path, string paramsStr)
        {
            var ctrl = GetOrCreateController(path);
            var added = new List<string>();

            if (string.IsNullOrEmpty(paramsStr))
                throw new ArgumentException("'params' is required for add_param");

            foreach (var part in paramsStr.Split(';'))
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                var tokens = p.Split(':');
                var name = tokens[0].Trim();
                var typeStr = tokens.Length > 1 ? tokens[1].Trim().ToLower() : "float";
                var defaultStr = tokens.Length > 2 ? tokens[2].Trim() : null;

                // Skip if already exists
                if (HasParameter(ctrl, name)) { added.Add($"{name}(exists)"); continue; }

                Undo.RecordObject(ctrl, "Add Parameter");
                var pType = typeStr switch
                {
                    "float" => AnimatorControllerParameterType.Float,
                    "int" => AnimatorControllerParameterType.Int,
                    "bool" => AnimatorControllerParameterType.Bool,
                    "trigger" => AnimatorControllerParameterType.Trigger,
                    _ => throw new ArgumentException($"Unknown param type: {typeStr}. Use float|int|bool|trigger")
                };
                ctrl.AddParameter(name, pType);
                if (defaultStr != null && typeStr != "trigger")
                    SetParamDefault(ctrl, name, typeStr, defaultStr);
                added.Add($"{name}({typeStr})");
            }

            SaveController(ctrl);
            return $"added: {string.Join(", ", added)}";
        }

        public static string AddStates(string path, string statesStr, int layer = 0)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl, layer);
            var added = new List<string>();

            if (string.IsNullOrEmpty(statesStr))
                throw new ArgumentException("'states' is required for add_state");
            int index = sm.states.Length;
            foreach (var part in statesStr.Split(';'))
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                var tokens = p.Split(':');
                var stateName = tokens[0].Trim();
                var clipPath = tokens.Length > 1 ? tokens[1].Trim() : null;

                // Skip if already exists
                if (FindState(sm, stateName) != null) { added.Add($"{stateName}(exists)"); continue; }

                Undo.RecordObject(ctrl, "Add State");
                var state = sm.AddState(stateName, new Vector3(300, index * 80, 0));

                if (!string.IsNullOrEmpty(clipPath))
                {
                    var clip = FindClipAsset(clipPath);
                    if (clip != null) state.motion = clip;
                }

                added.Add(stateName);
                index++;
            }

            SaveController(ctrl);
            return $"added: {string.Join(", ", added)}";
        }

        public static string AddTransition(string path, string source, string target,
            string conditions, float? duration, float? exitTime, bool? hasExitTime, int layer = 0)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl, layer);

            var targetState = FindState(sm, target);
            if (targetState == null) throw new InvalidOperationException($"Target state not found: {target}");

            Undo.RecordObject(ctrl, "Add Transition");
            AnimatorStateTransition transition;

            if (source == "*")
            {
                transition = sm.AddAnyStateTransition(targetState);
                transition.canTransitionToSelf = false;
            }
            else
            {
                var sourceState = FindState(sm, source);
                if (sourceState == null) throw new InvalidOperationException($"Source state not found: {source}");
                transition = sourceState.AddTransition(targetState);
            }

            // Settings
            transition.hasExitTime = hasExitTime ?? (exitTime.HasValue);
            if (exitTime.HasValue) transition.exitTime = exitTime.Value;
            if (duration.HasValue) transition.duration = duration.Value;

            // Parse conditions
            if (!string.IsNullOrEmpty(conditions))
            {
                foreach (var condStr in conditions.Split(';'))
                {
                    var c = condStr.Trim();
                    if (string.IsNullOrEmpty(c)) continue;
                    var cond = ParseCondition(c, ctrl);
                    transition.AddCondition(cond.mode, cond.threshold, cond.parameter);
                }
                // If we have conditions and no explicit hasExitTime, disable it
                if (!hasExitTime.HasValue && !exitTime.HasValue)
                    transition.hasExitTime = false;
            }

            SaveController(ctrl);
            var label = source == "*" ? "[Any]" : source;
            return $"transition: {label} → {target}";
        }

        public static string SetDefault(string path, string stateName, int layer = 0)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl, layer);
            var state = FindState(sm, stateName);
            if (state == null) throw new InvalidOperationException($"State not found: {stateName}");

            Undo.RecordObject(ctrl, "Set Default State");
            sm.defaultState = state;
            SaveController(ctrl);
            return $"default: {stateName}";
        }

        public static string Remove(string path, string type, string name, string source, string target)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl);
            Undo.RecordObject(ctrl, "Remove " + type);

            switch (type)
            {
                case "param":
                    RemoveParameter(ctrl, name);
                    break;
                case "state":
                    RemoveState(sm, name);
                    break;
                case "transition":
                    RemoveTransition(sm, source, target);
                    break;
                default:
                    throw new ArgumentException($"Unknown remove type: {type}. Use param|state|transition");
            }

            SaveController(ctrl);
            return $"removed: {type} {name}{(source != null ? $" ({source} → {target})" : "")}";
        }

        // --- Blend Tree (path-based overloads for CommandRouter) ---

        public static string AddBlendTree(string path, string stateName,
            string blendType, string param, string paramY, string children)
        {
            return AddBlendTree(GetOrCreateController(path), stateName, blendType, param, paramY, children);
        }

        public static string EditBlendTree(string path, string stateName,
            string editAction, string children, string param, string paramY, string blendType)
        {
            var ctrl = GetController(path);
            if (ctrl == null) throw new ArgumentException($"no AnimatorController on '{path}'");
            return EditBlendTree(ctrl, stateName, editAction, children, param, paramY, blendType);
        }

        public static string GetBlendTreeDetail(string path, string stateName)
        {
            var ctrl = GetController(path);
            if (ctrl == null) throw new ArgumentException($"no AnimatorController on '{path}'");
            var state = FindStateAcrossLayers(ctrl, stateName);
            if (state == null) throw new InvalidOperationException($"state '{stateName}' not found");
            var bt = state.motion as BlendTree;
            if (bt == null) throw new InvalidOperationException($"state '{stateName}' is not a BlendTree");
            return AnimatorControllerSerializer.SerializeBlendTree(bt);
        }

        private static AnimatorState FindStateAcrossLayers(AnimatorController ctrl, string name)
        {
            foreach (var layer in ctrl.layers)
            {
                var s = FindStateRecursive(layer.stateMachine, name);
                if (s != null) return s;
            }
            return null;
        }

        private static AnimatorState FindStateRecursive(
            AnimatorStateMachine sm, string name,
            HashSet<AnimatorStateMachine> visited = null)
        {
            visited ??= new HashSet<AnimatorStateMachine>();
            if (!visited.Add(sm)) return null;
            foreach (var cs in sm.states)
                if (cs.state.name == name) return cs.state;
            foreach (var child in sm.stateMachines)
            {
                var r = FindStateRecursive(child.stateMachine, name, visited);
                if (r != null) return r;
            }
            return null;
        }

        // --- Blend Tree (core implementation) ---

        public static string AddBlendTree(AnimatorController ctrl, string stateName,
            string blendType, string param, string paramY, string children)
        {
            var sm = GetStateMachine(ctrl);
            if (FindState(sm, stateName) != null)
                throw new ArgumentException($"state '{stateName}' already exists");

            var btType = ParseBlendType(blendType);

            // Validate BEFORE any mutation: resolve all child clips/thresholds up front so a bad
            // entry (unknown clip, unparseable number) never leaves an orphaned state/sub-asset.
            var parsedChildren = string.IsNullOrEmpty(children)
                ? new List<(AnimationClip clip, float x, float y)>()
                : ParseChildren(children, btType);

            EnsureParameter(ctrl, param, AnimatorControllerParameterType.Float);
            if (!string.IsNullOrEmpty(paramY))
                EnsureParameter(ctrl, paramY, AnimatorControllerParameterType.Float);

            var bt = new BlendTree { name = stateName, blendParameter = param, blendType = btType };
            if (!string.IsNullOrEmpty(paramY))
                bt.blendParameterY = paramY;

            AssetDatabase.AddObjectToAsset(bt, ctrl);

            Undo.RecordObject(ctrl, "Add BlendTree");
            int index = sm.states.Length;
            var state = sm.AddState(stateName, new Vector3(300, index * 80, 0));
            state.motion = bt;

            AddParsedChildren(bt, parsedChildren, btType);

            SaveController(ctrl);
            return $"blend_tree:{stateName} type:{btType} param:{param} children:{bt.children.Length}";
        }

        public static string EditBlendTree(AnimatorController ctrl, string stateName,
            string editAction, string children, string param, string paramY, string blendType)
        {
            var sm = GetStateMachine(ctrl);
            var state = FindState(sm, stateName);
            if (state == null)
                throw new InvalidOperationException($"state '{stateName}' not found");

            var bt = state.motion as BlendTree;
            if (bt == null)
                throw new InvalidOperationException($"state '{stateName}' is not a BlendTree");

            Undo.RecordObject(bt, "MCP Edit BlendTree");

            switch (editAction)
            {
                case "add_child":
                    // Validate before mutating: resolve all children first, then apply.
                    var parsedChildren = ParseChildren(children, bt.blendType);
                    AddParsedChildren(bt, parsedChildren, bt.blendType);
                    break;
                case "remove_child":
                    RemoveBlendChild(bt, children);
                    break;
                case "set_thresholds":
                    SetThresholds(bt, children);
                    break;
                case "set_param":
                    bt.blendParameter = param;
                    EnsureParameter(ctrl, param, AnimatorControllerParameterType.Float);
                    if (!string.IsNullOrEmpty(paramY))
                    {
                        bt.blendParameterY = paramY;
                        EnsureParameter(ctrl, paramY, AnimatorControllerParameterType.Float);
                    }
                    break;
                case "set_type":
                    bt.blendType = ParseBlendType(blendType);
                    break;
                default:
                    throw new ArgumentException($"unknown edit_action '{editAction}'");
            }

            SaveController(ctrl);
            return $"edited:{stateName} action:{editAction}";
        }

        private static BlendTreeType ParseBlendType(string blendType)
        {
            return (blendType ?? "1d").ToLowerInvariant() switch
            {
                "1d" or "simple1d" => BlendTreeType.Simple1D,
                "2d_simple" or "simpledirectional2d" => BlendTreeType.SimpleDirectional2D,
                "2d_freeform" or "freeformdirectional2d" => BlendTreeType.FreeformDirectional2D,
                "2d_cartesian" or "freeformcartesian2d" => BlendTreeType.FreeformCartesian2D,
                "direct" => BlendTreeType.Direct,
                _ => throw new ArgumentException($"Unknown blend_type: {blendType}. Use 1d|2d_simple|2d_freeform|2d_cartesian|direct")
            };
        }

        private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (HasParameter(ctrl, name)) return;
            ctrl.AddParameter(name, type);
        }

        // Pure parse — resolves clip names + thresholds/coordinates with ZERO side effects.
        // Throws on the first bad entry (missing clip, unparseable number) BEFORE any BlendTree
        // is mutated, so a bad "children" string never leaves a half-built state behind.
        private static List<(AnimationClip clip, float x, float y)> ParseChildren(string childrenStr, BlendTreeType btType)
        {
            var result = new List<(AnimationClip clip, float x, float y)>();
            if (string.IsNullOrEmpty(childrenStr)) return result;

            foreach (var part in childrenStr.Split(';'))
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var colonIdx = trimmed.IndexOf(':');
                var clipName = colonIdx > 0 ? trimmed.Substring(0, colonIdx).Trim() : trimmed;
                var clip = FindClipByName(clipName);
                if (clip == null)
                    throw new ArgumentException($"Clip not found: '{clipName}'");

                if (btType == BlendTreeType.Simple1D)
                {
                    float threshold = colonIdx > 0
                        ? ParseFloatOrThrow(trimmed.Substring(colonIdx + 1).Trim())
                        : 0f;
                    result.Add((clip, threshold, 0f));
                }
                else
                {
                    if (colonIdx < 0) { result.Add((clip, 0f, 0f)); continue; }
                    var coords = trimmed.Substring(colonIdx + 1).Split(',');
                    float x = ParseFloatOrThrow(coords[0].Trim());
                    float y = coords.Length > 1 ? ParseFloatOrThrow(coords[1].Trim()) : 0f;
                    result.Add((clip, x, y));
                }
            }
            return result;
        }

        private static float ParseFloatOrThrow(string s)
        {
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new ArgumentException($"Invalid threshold/coordinate value: '{s}'");
            return v;
        }

        // Pure mutation — input already validated by ParseChildren, cannot throw.
        private static void AddParsedChildren(BlendTree bt, List<(AnimationClip clip, float x, float y)> parsed, BlendTreeType btType)
        {
            foreach (var (clip, x, y) in parsed)
            {
                if (btType == BlendTreeType.Simple1D)
                    bt.AddChild(clip, x);
                else
                    bt.AddChild(clip, new Vector2(x, y));
            }
        }

        private static AnimationClip FindClipByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var guids = AssetDatabase.FindAssets($"t:AnimationClip {name}");
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                if (c != null && c.name == name) return c;
            }
            return null;
        }

        private static void RemoveBlendChild(BlendTree bt, string indexStr)
        {
            if (!int.TryParse(indexStr?.Trim(), out int idx)) return;
            var list = new List<ChildMotion>(bt.children);
            if (idx < 0 || idx >= list.Count) return;
            list.RemoveAt(idx);
            bt.children = list.ToArray();
        }

        private static void SetThresholds(BlendTree bt, string thresholdStr)
        {
            // Format: "index:value; index:value" e.g. "0:0; 1:0.3; 2:0.8"
            bt.useAutomaticThresholds = false;
            var arr = bt.children;
            foreach (var part in thresholdStr.Split(';'))
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var tokens = trimmed.Split(':');
                if (tokens.Length < 2) continue;
                if (!int.TryParse(tokens[0].Trim(), out int idx)) continue;
                if (idx < 0 || idx >= arr.Length) continue;
                arr[idx].threshold = float.Parse(tokens[1].Trim(), CultureInfo.InvariantCulture);
            }
            bt.children = arr;
        }

        // --- Internal helpers ---

        internal static AnimatorController GetController(string path)
        {
            if (path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var animator = go.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return null;

            return animator.runtimeAnimatorController as AnimatorController;
        }

        private static AnimatorController GetOrCreateController(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(go);

            var ctrl = animator.runtimeAnimatorController as AnimatorController;
            if (ctrl == null)
            {
                var dir = AnimationHelper.AssetDirectory;
                var ctrlPath = $"{dir}/{go.name}.controller";
                AssetHelper.EnsureDirectory(ctrlPath);
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                animator.runtimeAnimatorController = ctrl;
            }
            return ctrl;
        }

        internal static AnimatorStateMachine GetStateMachine(AnimatorController ctrl, int layer = 0)
        {
            return ctrl.layers[layer].stateMachine;
        }

        internal static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var cs in sm.states)
                if (cs.state.name == name) return cs.state;
            return null;
        }

        private static AnimationClip FindClipAsset(string clipPath)
        {
            // Try exact path first
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip != null) return clip;

            // Try Assets/Animations/{name}
            if (!clipPath.Contains("/"))
            {
                var withDir = $"{AnimationHelper.AssetDirectory}/{clipPath}";
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(withDir);
                if (clip != null) return clip;
            }

            // Search by name
            var guids = AssetDatabase.FindAssets($"t:AnimationClip {System.IO.Path.GetFileNameWithoutExtension(clipPath)}");
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                if (c != null && (c.name == System.IO.Path.GetFileNameWithoutExtension(clipPath) || p.EndsWith(clipPath)))
                    return c;
            }
            return null;
        }

        private static bool HasParameter(AnimatorController ctrl, string name)
        {
            foreach (var p in ctrl.parameters)
                if (p.name == name) return true;
            return false;
        }

        private static void SetParamDefault(AnimatorController ctrl, string name, string typeStr, string defaultStr)
        {
            var parms = ctrl.parameters;
            for (int i = 0; i < parms.Length; i++)
            {
                if (parms[i].name != name) continue;
                switch (typeStr)
                {
                    case "float": parms[i].defaultFloat = float.Parse(defaultStr, CultureInfo.InvariantCulture); break;
                    case "int": parms[i].defaultInt = int.Parse(defaultStr); break;
                    case "bool": parms[i].defaultBool = defaultStr == "true"; break;
                }
                ctrl.parameters = parms;
                return;
            }
        }

        internal struct ParsedCondition
        {
            public AnimatorConditionMode mode;
            public float threshold;
            public string parameter;
        }

        internal static ParsedCondition ParseCondition(string condStr, AnimatorController ctrl)
        {
            var c = condStr.Trim();
            var result = new ParsedCondition();

            // "!IsGrounded" → IfNot
            if (c.StartsWith("!"))
            {
                result.parameter = c.Substring(1);
                result.mode = AnimatorConditionMode.IfNot;
                result.threshold = 0;
                return result;
            }

            // "Speed>0.1" "Speed<0.1" "Type=2" "State!=0"
            int opIdx = -1;
            string op = null;
            // Check != before = to avoid false match
            var neqIdx = c.IndexOf("!=");
            if (neqIdx > 0) { opIdx = neqIdx; op = "!="; }
            else
            {
                for (int i = 1; i < c.Length; i++)
                {
                    if (i + 1 < c.Length && c[i] == '=' && c[i+1] == '=')
                    { opIdx = i; op = "=="; break; }
                    if (c[i] == '>' || c[i] == '<' || c[i] == '=')
                    { opIdx = i; op = c[i].ToString(); break; }
                }
            }

            if (op != null && opIdx > 0)
            {
                result.parameter = c.Substring(0, opIdx).Trim();
                var valueStr = c.Substring(opIdx + op.Length).Trim().ToLower();
                // Bool shorthand: Param==true → If, Param==false → IfNot
                if (valueStr == "true")  { result.mode = AnimatorConditionMode.If;    result.threshold = 0; return result; }
                if (valueStr == "false") { result.mode = AnimatorConditionMode.IfNot; result.threshold = 0; return result; }
                result.threshold = float.Parse(valueStr, CultureInfo.InvariantCulture);
                result.mode = op switch
                {
                    ">" => AnimatorConditionMode.Greater,
                    "<" => AnimatorConditionMode.Less,
                    "=" => AnimatorConditionMode.Equals,
                    "==" => AnimatorConditionMode.Equals,
                    "!=" => AnimatorConditionMode.NotEqual,
                    _ => AnimatorConditionMode.If
                };
                return result;
            }

            // "IsGrounded" or "Jump" → If (bool/trigger = true)
            result.parameter = c;
            result.mode = AnimatorConditionMode.If;
            result.threshold = 0;
            return result;
        }

        private static void RemoveParameter(AnimatorController ctrl, string name)
        {
            var parms = ctrl.parameters;
            for (int i = 0; i < parms.Length; i++)
            {
                if (parms[i].name == name)
                {
                    ctrl.RemoveParameter(i);
                    return;
                }
            }
            throw new InvalidOperationException($"Parameter not found: {name}");
        }

        private static void RemoveState(AnimatorStateMachine sm, string name)
        {
            var state = FindState(sm, name);
            if (state == null) throw new InvalidOperationException($"State not found: {name}");
            sm.RemoveState(state);
        }

        private static void RemoveTransition(AnimatorStateMachine sm, string source, string target)
        {
            if (source == "*")
            {
                var anyTransitions = sm.anyStateTransitions;
                for (int i = 0; i < anyTransitions.Length; i++)
                {
                    if (anyTransitions[i].destinationState?.name == target)
                    {
                        sm.RemoveAnyStateTransition(anyTransitions[i]);
                        return;
                    }
                }
                throw new InvalidOperationException($"AnyState transition to '{target}' not found");
            }

            var sourceState = FindState(sm, source);
            if (sourceState == null) throw new InvalidOperationException($"Source state not found: {source}");

            foreach (var t in sourceState.transitions)
            {
                if (t.destinationState?.name == target)
                {
                    sourceState.RemoveTransition(t);
                    return;
                }
            }
            throw new InvalidOperationException($"Transition '{source} → {target}' not found");
        }

        // --- Layer CRUD ---

        public static string AddLayer(string path, string name, float weight, string blending)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Layer name cannot be empty");
            var ctrl = GetOrCreateController(path);
            foreach (var l in ctrl.layers)
                if (l.name == name) throw new ArgumentException($"Layer already exists: {name}");

            Undo.RecordObject(ctrl, "Add Layer");
            ctrl.AddLayer(name);
            var layers = ctrl.layers;
            int idx = layers.Length - 1;
            layers[idx].defaultWeight = Mathf.Clamp01(weight);
            layers[idx].blendingMode = ParseBlendingMode(blending);
            ctrl.layers = layers;
            SaveController(ctrl);
            return $"layer added: {name} (idx:{idx}) w:{weight} blend:{blending}";
        }

        public static string RemoveLayer(string path, string layer)
        {
            var ctrl = GetOrCreateController(path);
            int idx = ResolveLayerIndex(ctrl, layer);
            if (idx == 0) throw new ArgumentException("Cannot remove layer 0 (Base)");
            string name = ctrl.layers[idx].name;
            Undo.RecordObject(ctrl, "Remove Layer");
            ctrl.RemoveLayer(idx);
            SaveController(ctrl);
            return $"layer removed: {name} (idx:{idx})";
        }

        public static string RenameLayer(string path, string oldName, string newName)
        {
            var ctrl = GetOrCreateController(path);
            int idx = ResolveLayerIndex(ctrl, oldName);
            Undo.RecordObject(ctrl, "Rename Layer");
            var layers = ctrl.layers;
            layers[idx].name = newName;
            ctrl.layers = layers;
            SaveController(ctrl);
            return $"layer renamed: {oldName} → {newName} (idx:{idx})";
        }

        public static string SetLayerWeight(string path, string layer, float weight)
        {
            var ctrl = GetOrCreateController(path);
            int idx = ResolveLayerIndex(ctrl, layer);
            string name = ctrl.layers[idx].name;
            Undo.RecordObject(ctrl, "Set Layer Weight");
            var layers = ctrl.layers;
            layers[idx].defaultWeight = Mathf.Clamp01(weight);
            ctrl.layers = layers;
            SaveController(ctrl);
            return $"layer weight: {name} = {layers[idx].defaultWeight}";
        }

        public static string SetLayerBlending(string path, string layer, string mode)
        {
            var ctrl = GetOrCreateController(path);
            int idx = ResolveLayerIndex(ctrl, layer);
            string name = ctrl.layers[idx].name;
            var parsed = ParseBlendingMode(mode);
            Undo.RecordObject(ctrl, "Set Layer Blending");
            var layers = ctrl.layers;
            layers[idx].blendingMode = parsed;
            ctrl.layers = layers;
            SaveController(ctrl);
            return $"layer blending: {name} = {parsed}";
        }

        private static int ResolveLayerIndex(AnimatorController ctrl, string layer)
        {
            if (int.TryParse(layer, out int idx))
            {
                if (idx < 0 || idx >= ctrl.layers.Length)
                    throw new ArgumentOutOfRangeException(nameof(layer),
                        $"Layer index {idx} out of range (0–{ctrl.layers.Length - 1})");
                return idx;
            }
            for (int i = 0; i < ctrl.layers.Length; i++)
                if (ctrl.layers[i].name == layer) return i;
            throw new ArgumentException($"Layer not found: {layer}");
        }

        private static AnimatorLayerBlendingMode ParseBlendingMode(string mode)
        {
            return (mode ?? "Override").ToLowerInvariant() switch
            {
                "additive" => AnimatorLayerBlendingMode.Additive,
                _ => AnimatorLayerBlendingMode.Override
            };
        }

        // --- M7: Set State Speed ---

        public static string SetStateSpeed(string path, string stateName, string value)
        {
            var ctrl = GetController(path);
            if (ctrl == null) throw new ArgumentException($"no AnimatorController on '{path}'");
            var state = FindStateAcrossLayers(ctrl, stateName);
            if (state == null) throw new InvalidOperationException($"State not found: {stateName}");
            Undo.RecordObject(state, "Set State Speed");
            state.speed = float.Parse(value, CultureInfo.InvariantCulture);
            SaveController(ctrl);
            return $"state speed: {stateName} = {value}";
        }

        // --- M8: Update Transition ---

        public static string UpdateTransition(string path, string source, string target,
            float? duration, float? exitTime, bool? hasExitTime, string conditions, int layer = 0)
        {
            var ctrl = GetOrCreateController(path);
            var sm = GetStateMachine(ctrl, layer);
            var t = FindTransition(sm, source, target);
            Undo.RecordObject(t, "Update Transition");
            if (duration.HasValue) t.duration = duration.Value;
            if (exitTime.HasValue) t.exitTime = exitTime.Value;
            if (hasExitTime.HasValue) t.hasExitTime = hasExitTime.Value;
            if (!string.IsNullOrEmpty(conditions))
            {
                t.conditions = Array.Empty<AnimatorCondition>();
                foreach (var condStr in conditions.Split(';'))
                {
                    var c = condStr.Trim();
                    if (string.IsNullOrEmpty(c)) continue;
                    var cond = ParseCondition(c, ctrl);
                    t.AddCondition(cond.mode, cond.threshold, cond.parameter);
                }
            }
            SaveController(ctrl);
            var label = source == "*" ? "[Any]" : source;
            return $"transition updated: {label} → {target}";
        }

        private static AnimatorStateTransition FindTransition(AnimatorStateMachine sm, string source, string target)
        {
            if (source == "*")
            {
                foreach (var t in sm.anyStateTransitions)
                    if (t.destinationState?.name == target) return t;
                throw new InvalidOperationException($"AnyState transition to '{target}' not found");
            }
            var sourceState = FindState(sm, source);
            if (sourceState == null) throw new InvalidOperationException($"Source state not found: {source}");
            foreach (var t in sourceState.transitions)
                if (t.destinationState?.name == target) return t;
            throw new InvalidOperationException($"Transition '{source} → {target}' not found");
        }

        // --- M9: Set Avatar ---

        public static string SetAvatar(string path, string avatarPath)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));
            var anim = go.GetComponent<Animator>();
            if (anim == null) throw new InvalidOperationException($"No Animator component on '{path}'");
            Avatar avatar = null;
            if (!string.IsNullOrEmpty(avatarPath))
            {
                avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
                if (avatar == null) throw new ArgumentException($"Avatar not found: {avatarPath}");
            }
            Undo.RecordObject(anim, "Set Avatar");
            anim.avatar = avatar;
            EditorUtility.SetDirty(anim);
            return $"avatar set: {(avatar != null ? avatar.name : "none")}";
        }

        // --- M10: Rename State / Rename Parameter ---

        public static string RenameState(string path, string oldName, string newName)
        {
            var ctrl = GetController(path);
            if (ctrl == null) throw new ArgumentException($"no AnimatorController on '{path}'");
            var state = FindStateAcrossLayers(ctrl, oldName);
            if (state == null) throw new InvalidOperationException($"State not found: {oldName}");
            Undo.RecordObject(state, "Rename State");
            state.name = newName;
            SaveController(ctrl);
            return $"state renamed: {oldName} → {newName}";
        }

        public static string RenameParameter(string path, string oldName, string newName)
        {
            var ctrl = GetController(path);
            if (ctrl == null) throw new ArgumentException($"no AnimatorController on '{path}'");
            var parms = ctrl.parameters;
            for (int i = 0; i < parms.Length; i++)
            {
                if (parms[i].name == oldName)
                {
                    Undo.RecordObject(ctrl, "Rename Parameter");
                    parms[i].name = newName;
                    ctrl.parameters = parms;
                    SaveController(ctrl);
                    return $"param renamed: {oldName} → {newName}";
                }
            }
            throw new InvalidOperationException($"Parameter not found: {oldName}");
        }

        private static void SaveController(AnimatorController ctrl)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }
    }
}
