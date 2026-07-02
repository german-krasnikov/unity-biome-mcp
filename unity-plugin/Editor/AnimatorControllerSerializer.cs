using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class AnimatorControllerSerializer
    {
        public static string Serialize(string path, string stateName)
        {
            var ctrl = AnimatorControllerHelper.GetController(path);
            if (ctrl == null) throw new System.InvalidOperationException($"No AnimatorController on '{path}'");

            if (!string.IsNullOrEmpty(stateName))
                return SerializeStateDetail(ctrl, stateName);

            return SerializeOverview(ctrl);
        }

        private static string SerializeOverview(AnimatorController ctrl)
        {
            var sb = new StringBuilder();
            var paramCount = ctrl.parameters.Length;
            var totalStates = 0;
            foreach (var l in ctrl.layers) totalStates += l.stateMachine.states.Length;

            sb.Append("AnimatorController: ").Append(ctrl.name);
            sb.Append(" | ").Append(ctrl.layers.Length).Append(" layer");
            sb.Append(" | ").Append(paramCount).Append(" params");
            sb.Append(" | ").Append(totalStates).Append(" states");
            sb.AppendLine();

            // Parameters
            if (paramCount > 0)
            {
                sb.AppendLine("---");
                sb.AppendLine("params:");
                foreach (var p in ctrl.parameters)
                {
                    sb.Append("  ").Append(p.name).Append(" : ");
                    switch (p.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            sb.Append("float = ").Append(p.defaultFloat.ToString("G4", CultureInfo.InvariantCulture));
                            break;
                        case AnimatorControllerParameterType.Int:
                            sb.Append("int = ").Append(p.defaultInt);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            sb.Append("bool = ").Append(p.defaultBool ? "true" : "false");
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            sb.Append("trigger");
                            break;
                    }
                    sb.AppendLine();
                }
            }

            // States — all layers
            foreach (var layer in ctrl.layers)
            {
                var sm = layer.stateMachine;
                if (sm.states.Length == 0) continue;
                sb.AppendLine("---");
                sb.Append("states [").Append(layer.name);
                sb.Append(" w:").Append(layer.defaultWeight.ToString("G4", CultureInfo.InvariantCulture));
                sb.Append(" blend:").Append(layer.blendingMode.ToString());
                sb.AppendLine("]:");
                var defaultState = sm.defaultState;
                foreach (var cs in sm.states)
                {
                    var st = cs.state;
                    sb.Append("  ");
                    if (defaultState == st) sb.Append("* ");
                    sb.Append(st.name);
                    if (st.motion is BlendTree motionBt)
                        sb.Append(" | [BT:").Append(motionBt.blendType).Append(' ').Append(motionBt.children.Length).Append("ch]");
                    else if (st.motion != null)
                        sb.Append(" | ").Append(st.motion.name).Append(st.motion.name.EndsWith(".anim") ? "" : ".anim");
                    sb.Append(" | ").Append(st.speed.ToString("G4", CultureInfo.InvariantCulture)).Append("x");
                    if (!string.IsNullOrEmpty(st.tag))
                        sb.Append(" | tag:").Append(st.tag);
                    sb.AppendLine();
                }
                AppendTransitions(sb, sm);
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static string SerializeStateDetail(AnimatorController ctrl, string stateName)
        {
            AnimatorState state = null;
            foreach (var layer in ctrl.layers)
            {
                state = AnimatorControllerHelper.FindState(layer.stateMachine, stateName);
                if (state != null) break;
            }
            if (state == null) throw new System.InvalidOperationException($"State not found: {stateName}");

            var sb = new StringBuilder();
            sb.Append("state: ").Append(state.name);
            if (state.motion is BlendTree detailBt)
                sb.Append(" | [BT:").Append(detailBt.blendType).Append(']');
            else if (state.motion != null)
                sb.Append(" | ").Append(state.motion.name);
            sb.Append(" | speed:").Append(state.speed.ToString("G4", CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(state.tag)) sb.Append(" | tag:").Append(state.tag);
            sb.AppendLine();

            // BlendTree detail
            if (state.motion is BlendTree stateBt)
            {
                sb.AppendLine("---");
                sb.AppendLine(SerializeBlendTree(stateBt));
            }

            // Outgoing transitions
            if (state.transitions.Length > 0)
            {
                sb.AppendLine("---");
                sb.AppendLine("transitions:");
                foreach (var t in state.transitions)
                {
                    sb.Append("  → ").Append(t.destinationState?.name ?? "Exit");
                    AppendConditions(sb, t.conditions);
                    if (t.hasExitTime)
                        sb.Append(" | exit:").Append(t.exitTime.ToString("G4", CultureInfo.InvariantCulture));
                    sb.Append(" | ").Append(t.duration.ToString("G4", CultureInfo.InvariantCulture)).Append("s");
                    sb.AppendLine();
                }
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static void AppendTransitions(StringBuilder sb, AnimatorStateMachine sm)
        {
            bool hasTransitions = false;

            // State transitions
            foreach (var cs in sm.states)
            {
                foreach (var t in cs.state.transitions)
                {
                    if (!hasTransitions) { sb.AppendLine("---"); sb.AppendLine("transitions:"); hasTransitions = true; }
                    sb.Append("  ").Append(cs.state.name).Append(" → ").Append(t.destinationState?.name ?? "Exit");
                    AppendConditions(sb, t.conditions);
                    if (t.hasExitTime)
                        sb.Append(" | exit:").Append(t.exitTime.ToString("G4", CultureInfo.InvariantCulture));
                    sb.Append(" | ").Append(t.duration.ToString("G4", CultureInfo.InvariantCulture)).Append("s");
                    sb.AppendLine();
                }
            }

            // AnyState transitions
            foreach (var t in sm.anyStateTransitions)
            {
                if (!hasTransitions) { sb.AppendLine("---"); sb.AppendLine("transitions:"); hasTransitions = true; }
                sb.Append("  [Any] → ").Append(t.destinationState?.name ?? "Exit");
                AppendConditions(sb, t.conditions);
                if (t.hasExitTime)
                    sb.Append(" | exit:").Append(t.exitTime.ToString("G4", CultureInfo.InvariantCulture));
                sb.Append(" | ").Append(t.duration.ToString("G4", CultureInfo.InvariantCulture)).Append("s");
                sb.AppendLine();
            }
        }

        public static string SerializeBlendTree(BlendTree bt)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"type:{bt.blendType}");
            sb.AppendLine($"param:{bt.blendParameter}");
            if (bt.blendType != BlendTreeType.Simple1D)
                sb.AppendLine($"paramY:{bt.blendParameterY}");
            sb.AppendLine($"children:{bt.children.Length}");
            for (int i = 0; i < bt.children.Length; i++)
            {
                var c = bt.children[i];
                var motionName = c.motion != null ? c.motion.name : "null";
                if (bt.blendType == BlendTreeType.Simple1D)
                    sb.AppendLine($"  [{i}] {motionName} threshold:{c.threshold.ToString("G4", CultureInfo.InvariantCulture)}");
                else
                    sb.AppendLine($"  [{i}] {motionName} pos:({c.position.x.ToString("G4", CultureInfo.InvariantCulture)},{c.position.y.ToString("G4", CultureInfo.InvariantCulture)})");
            }
            return sb.ToString().TrimEnd();
        }

        private static void AppendConditions(StringBuilder sb, AnimatorCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return;
            sb.Append(" | ");
            for (int i = 0; i < conditions.Length; i++)
            {
                if (i > 0) sb.Append(" & ");
                var c = conditions[i];
                switch (c.mode)
                {
                    case AnimatorConditionMode.Greater:
                        sb.Append(c.parameter).Append(">").Append(c.threshold.ToString("G4", CultureInfo.InvariantCulture));
                        break;
                    case AnimatorConditionMode.Less:
                        sb.Append(c.parameter).Append("<").Append(c.threshold.ToString("G4", CultureInfo.InvariantCulture));
                        break;
                    case AnimatorConditionMode.Equals:
                        sb.Append(c.parameter).Append("=").Append(c.threshold.ToString("G4", CultureInfo.InvariantCulture));
                        break;
                    case AnimatorConditionMode.NotEqual:
                        sb.Append(c.parameter).Append("!=").Append(c.threshold.ToString("G4", CultureInfo.InvariantCulture));
                        break;
                    case AnimatorConditionMode.If:
                        sb.Append(c.parameter);
                        break;
                    case AnimatorConditionMode.IfNot:
                        sb.Append('!').Append(c.parameter);
                        break;
                }
            }
        }
    }
}
