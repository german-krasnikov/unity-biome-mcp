using System;

namespace UnityMCP.Editor
{
    // Consolidated action-dispatch handlers (animation/timeline/animator/particle/shader/
    // scene/ui/editor/menu/references) split from CommandRouter.cs for <200-line focus.
    public static partial class CommandRouter
    {
        // Shared UIElementSerializer instance — holds VERefTable across Exec* calls.
        // ResetRefTable() is called at the start of every Serialize() invocation.
        private static readonly UIElementSerializer _serializer = new UIElementSerializer();

        private static float? ParseOptFloat(string args, string key)
        {
            var s = JsonHelper.ExtractString(args, key);
            return s != null ? float.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : (float?)null;
        }

        private static bool? ParseOptBool(string args, string key)
        {
            var s = JsonHelper.ExtractString(args, key);
            return s != null ? s == "true" : (bool?)null;
        }

        private static int? ParseOptInt(string args, string key)
        {
            var s = JsonHelper.ExtractString(args, key);
            return s != null ? int.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : (int?)null;
        }

        private static string ExecGetAnimation(string args)
        {
            return AnimationSerializer.Serialize(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "clip"),
                ParseOptFloat(args, "time"),
                JsonHelper.ExtractString(args, "compact") == "true");
        }

        private static string ExecCreateAnimation(string args)
        {
            return AnimationHelper.CreateClip(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "clip_name") ?? JsonHelper.ExtractString(args, "clip"),
                JsonHelper.ExtractString(args, "property") ?? "localPosition",
                JsonHelper.ExtractString(args, "keys") ?? "",
                JsonHelper.ExtractString(args, "component_type"),
                JsonHelper.ExtractString(args, "binding_path"),
                JsonHelper.ExtractString(args, "tangent"));
        }

        private static string ExecEditAnimation(string args)
        {
            return AnimationHelper.EditClip(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "clip"),
                JsonHelper.ExtractString(args, "action"),
                JsonHelper.ExtractString(args, "property"),
                JsonHelper.ExtractString(args, "keys"),
                JsonHelper.ExtractString(args, "component_type"),
                JsonHelper.ExtractString(args, "binding_path"),
                JsonHelper.ExtractString(args, "tangent"));
        }

        private static string ExecPreviewAnimation(string args)
        {
            return AnimationHelper.Preview(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "clip"),
                JsonHelper.ExtractString(args, "action") ?? "sample",
                ParseOptFloat(args, "time") ?? 0f);
        }

        private static string ExecGetTimeline(string args)
        {
            return TimelineSerializer.Serialize(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "track"));
        }

        private static string ExecCreateTimeline(string args)
        {
            return TimelineHelper.CreateTimeline(
                JsonHelper.ExtractString(args, "asset_path"),
                JsonHelper.ExtractString(args, "director_path") ?? JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "tracks"));
        }

        private static string ExecEditTimeline(string args)
        {
            return TimelineHelper.Edit(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "action"),
                JsonHelper.ExtractString(args, "track"),
                JsonHelper.ExtractString(args, "track_type"),
                JsonHelper.ExtractString(args, "clip"),
                JsonHelper.ExtractString(args, "binding"),
                ParseOptFloat(args, "start"),
                ParseOptFloat(args, "duration"),
                ParseOptFloat(args, "blend_in"),
                ParseOptFloat(args, "blend_out"),
                JsonHelper.ExtractString(args, "name"),
                ParseOptInt(args, "index"),
                ParseOptFloat(args, "offset"),
                JsonHelper.ExtractString(args, "value"));
        }

        private static string ExecSetClipIn(string args)
        {
            return TimelineHelper.SetClipIn(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "track"),
                JsonHelper.ExtractString(args, "clip"),
                ParseOptFloat(args, "clip_in") ?? 0f);
        }

        private static string ExecGetBindings(string args)
        {
            return TimelineHelper.GetBindings(JsonHelper.ExtractString(args, "path"));
        }

        private static string ExecPreviewTimeline(string args)
        {
            return TimelineHelper.Preview(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "action") ?? "sample",
                ParseOptFloat(args, "time") ?? 0f);
        }

        // --- Consolidated command handlers ---

        private static string ExecScene(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            var result = action switch
            {
                "new" => SceneHelper.NewScene(),
                "open" => SceneHelper.OpenScene(JsonHelper.ExtractString(args, "path")),
                "save" => SceneHelper.SaveScene(JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "scene")),
                "discard" => SceneHelper.DiscardChanges(JsonHelper.ExtractString(args, "scene")),
                "open_additive" => SceneHelper.OpenAdditive(JsonHelper.ExtractString(args, "path")),
                "close" => SceneHelper.CloseScene(JsonHelper.ExtractString(args, "path")),
                "set_active" => SceneHelper.SetActiveScene(JsonHelper.ExtractString(args, "path")),
                "list" => SceneHelper.ListScenes(),
                "save_copy" => SceneHelper.SaveCopy(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "scene")),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "new", "open", "save", "discard", "open_additive", "close", "set_active", "list", "save_copy" }))
            };
            // P-414: scene is mutating:false so undo group is never opened around saves.
            // Topology-changing sub-actions still need a mutation record for get_changes.
            if (action != "list" && action != "save" && action != "save_copy")
                ChangeWatcher.RecordMutation($"MCP_SCENE_{action.ToUpper()}");
            return result;
        }

        private static string ExecAnimationConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            var path = JsonHelper.ExtractString(args, "path");
            var clip = JsonHelper.ExtractString(args, "clip");
            return action switch
            {
                "get" => ExecGetAnimation(args),
                "create" => ExecCreateAnimation(args),
                "edit" or "add_key" or "remove_key" or "remove_curve" or "set_keys" or "set_loop" or "set_wrap" or "set_framerate"
                    => ExecEditAnimation(args),
                "preview" => ExecPreviewAnimation(args),
                "get_clip_path" => AnimationHelper.GetClipPath(path, clip),
                "get_events" => AnimationHelper.GetEvents(path, clip),
                "add_event" => AnimationHelper.AddEvent(
                    path, clip,
                    ParseOptFloat(args, "time") ?? 0f,
                    JsonHelper.ExtractString(args, "function_name"),
                    ParseOptInt(args, "int_param"),
                    ParseOptFloat(args, "float_param"),
                    JsonHelper.ExtractString(args, "string_param")),
                "remove_event" => AnimationHelper.RemoveEvent(
                    path, clip, ParseOptFloat(args, "time") ?? 0f),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "create", "edit", "add_key", "remove_key", "remove_curve",
                            "set_keys", "set_loop", "set_wrap", "set_framerate", "preview",
                            "get_clip_path", "get_events", "add_event", "remove_event" }))
            };
        }

        private static string ExecTimelineConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            // Read-only actions allowed in Play Mode; mutating actions are not.
            if (action != "get" && action != "get_bindings" && action != "preview"
                && UnityEditor.EditorApplication.isPlaying)
                return "err: timeline write actions not allowed in Play Mode";
            return action switch
            {
                "get" => ExecGetTimeline(args),
                "create" => ExecCreateTimeline(args),
                "edit" or "add_track" or "remove_track" or "add_clip" or "remove_clip"
                    or "set_binding" or "set_timing" or "mute" or "unmute"
                    or "lock" or "unlock" or "rename_track"
                    or "reorder_track" or "duplicate_clip" or "add_marker" or "remove_marker"
                    or "set_track_offset" or "set_duration" or "add_sub_track"
                    => ExecEditTimeline(args),
                "set_clip_in" => ExecSetClipIn(args),
                "get_bindings" => ExecGetBindings(args),
                "preview" => ExecPreviewTimeline(args),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "create", "edit", "add_track", "remove_track", "add_clip", "remove_clip",
                            "set_binding", "set_timing", "mute", "unmute", "lock", "unlock", "rename_track",
                            "reorder_track", "duplicate_clip", "add_marker", "remove_marker",
                            "set_track_offset", "set_duration", "add_sub_track",
                            "set_clip_in", "get_bindings", "preview" }))
            };
        }

        private static string ExecReferencesConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => ReferenceHelper.GetReferences(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "children") == "true",
                    ExtractInt(args, "depth", 1)),
                "find_to" => ReferenceHelper.FindReferencesTo(JsonHelper.ExtractString(args, "path")),
                "remap" => RemapReferencesHelper.RemapReferences(
                    JsonHelper.ExtractString(args, "source"),
                    JsonHelper.ExtractString(args, "target"),
                    JsonHelper.ExtractString(args, "mappings")),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action, new[] { "get", "find_to", "remap" }))
            };
        }

        private static string ExecCreateUI(string args)
        {
            return UIHelper.CreateUI(
                JsonHelper.ExtractString(args, "type"),
                JsonHelper.ExtractString(args, "name"),
                JsonHelper.ExtractString(args, "parent"),
                JsonHelper.ExtractString(args, "anchor"),
                JsonHelper.ExtractString(args, "pos"),
                JsonHelper.ExtractString(args, "size"),
                JsonHelper.ExtractString(args, "pivot"),
                JsonHelper.ExtractString(args, "color"),
                JsonHelper.ExtractString(args, "text"),
                JsonHelper.ExtractString(args, "font_size"),
                JsonHelper.ExtractString(args, "render_mode"));
        }

        // G2: Lint uGUI — check EventSystem presence and GraphicRaycaster on Canvas.
        private static string ExecLintUGUI(string args) =>
            UILinter.LintUGUI(JsonHelper.ExtractString(args, "root"));

        // S4: Lint UITK — structural checks for .uxml/.uss files (A1–A6).
        private static string ExecLintUITK(string args) =>
            UILinter.LintUITK(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "fix") == "true");

        private static string ExecSetRect(string args)
        {
            return UIHelper.SetRect(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "anchor"),
                JsonHelper.ExtractString(args, "pos"),
                JsonHelper.ExtractString(args, "size"),
                JsonHelper.ExtractString(args, "pivot"),
                JsonHelper.ExtractString(args, "offset_min"),
                JsonHelper.ExtractString(args, "offset_max"));
        }

        private static string ExecInspectUITK(string args) =>
            UIHelper.InspectUITK(
                JsonHelper.ExtractString(args, "path"),
                ParseOptInt(args, "depth") ?? 4,
                JsonHelper.ExtractString(args, "selector"),
                JsonHelper.ExtractString(args, "filter"),
                ParseOptBool(args, "include_internal") ?? false,
                ParseOptBool(args, "show_style") ?? false);

        private static string ExecEditor(string args)
        {
            var action = JsonHelper.ExtractString(args, "action") ?? "state";
            if (action == "state")
                return EditorStateHelper.GetState();
            return EditorStateHelper.Control(action, JsonHelper.ExtractString(args, "path"), args);
        }

        private static string ExecAnimatorConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            var layerStr = JsonHelper.ExtractString(args, "layer");
            int layer = layerStr != null && int.TryParse(layerStr, out int li) ? li : 0;
            return action switch
            {
                "get" => AnimatorControllerSerializer.Serialize(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "state")),
                "add_param" => AnimatorControllerHelper.AddParameters(
                    JsonHelper.ExtractString(args, "path"), AnimatorParamSpec(args)),
                "add_state" => AnimatorControllerHelper.AddStates(
                    JsonHelper.ExtractString(args, "path"), AnimatorStatesSpec(args), layer),
                "add_transition" => ExecAddTransition(args, layer),
                "set_default" => AnimatorControllerHelper.SetDefault(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "state"), layer),
                "remove" => AnimatorControllerHelper.Remove(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "type"),
                    JsonHelper.ExtractString(args, "name"), JsonHelper.ExtractString(args, "source"),
                    JsonHelper.ExtractString(args, "target")),
                "add_blend_tree" => AnimatorControllerHelper.AddBlendTree(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "state"),
                    JsonHelper.ExtractString(args, "blend_type"),
                    JsonHelper.ExtractString(args, "param"),
                    JsonHelper.ExtractString(args, "param_y"),
                    JsonHelper.ExtractString(args, "children")),
                "edit_blend_tree" => AnimatorControllerHelper.EditBlendTree(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "state"),
                    JsonHelper.ExtractString(args, "edit_action"),
                    JsonHelper.ExtractString(args, "children"),
                    JsonHelper.ExtractString(args, "param"),
                    JsonHelper.ExtractString(args, "param_y"),
                    JsonHelper.ExtractString(args, "blend_type")),
                "get_blend_tree" => AnimatorControllerHelper.GetBlendTreeDetail(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "state")),
                "add_layer" => AnimatorControllerHelper.AddLayer(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "name"),
                    ParseOptFloat(args, "weight") ?? 0f,
                    JsonHelper.ExtractString(args, "blending") ?? "Override"),
                "remove_layer" => AnimatorControllerHelper.RemoveLayer(
                    JsonHelper.ExtractString(args, "path"),
                    layerStr ?? "0"),
                "rename_layer" => AnimatorControllerHelper.RenameLayer(
                    JsonHelper.ExtractString(args, "path"),
                    layerStr ?? "0",
                    JsonHelper.ExtractString(args, "name")),
                "set_layer_weight" => AnimatorControllerHelper.SetLayerWeight(
                    JsonHelper.ExtractString(args, "path"),
                    layerStr ?? "0",
                    ParseOptFloat(args, "weight") ?? 1f),
                "set_layer_blending" => AnimatorControllerHelper.SetLayerBlending(
                    JsonHelper.ExtractString(args, "path"),
                    layerStr ?? "0",
                    JsonHelper.ExtractString(args, "blending") ?? "Override"),
                "set_state_speed" => AnimatorControllerHelper.SetStateSpeed(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "state"),
                    JsonHelper.ExtractString(args, "value")),
                "update_transition" => AnimatorControllerHelper.UpdateTransition(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "source"),
                    JsonHelper.ExtractString(args, "target"),
                    ParseOptFloat(args, "duration"),
                    ParseOptFloat(args, "exit_time"),
                    ParseOptBool(args, "has_exit_time"),
                    JsonHelper.ExtractString(args, "conditions"),
                    layer),
                "set_avatar" => AnimatorControllerHelper.SetAvatar(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "avatar_path")),
                "rename_state" => AnimatorControllerHelper.RenameState(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "state"),
                    JsonHelper.ExtractString(args, "name")),
                "rename_param" => AnimatorControllerHelper.RenameParameter(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "param"),
                    JsonHelper.ExtractString(args, "name")),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "add_param", "add_state", "add_transition", "set_default", "remove",
                            "add_blend_tree", "edit_blend_tree", "get_blend_tree",
                            "add_layer", "remove_layer", "rename_layer", "set_layer_weight", "set_layer_blending",
                            "set_state_speed", "update_transition", "set_avatar", "rename_state", "rename_param" }))
            };
        }

        private static string AnimatorParamSpec(string args)
        {
            var spec = JsonHelper.ExtractString(args, "params");
            if (!string.IsNullOrEmpty(spec)) return spec;

            var name = JsonHelper.ExtractString(args, "name");
            if (string.IsNullOrEmpty(name)) return spec;

            var type = JsonHelper.ExtractString(args, "type") ?? "float";
            var value = JsonHelper.ExtractString(args, "value");
            return value == null ? $"{name}:{type}" : $"{name}:{type}:{value}";
        }

        private static string AnimatorStatesSpec(string args)
        {
            var spec = JsonHelper.ExtractString(args, "states");
            if (!string.IsNullOrEmpty(spec)) return spec;
            return JsonHelper.ExtractString(args, "state");
        }

        private static string ExecCreateParticle(string args)
        {
            var parentPath = JsonHelper.ExtractString(args, "path");
            var name = JsonHelper.ExtractString(args, "name");
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(parentPath)
                && ComponentSerializer.FindObject(parentPath) == null)
            {
                name = System.IO.Path.GetFileName(parentPath);
                parentPath = System.IO.Path.GetDirectoryName(parentPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parentPath) || parentPath == "/" || parentPath == "\\")
                    parentPath = null;
            }
            return ParticleHelper.Create(parentPath, name, JsonHelper.ExtractString(args, "preset"));
        }

        private static string ExecParticleConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => ParticleSerializer.Serialize(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "module")),
                "create" => ExecCreateParticle(args),
                "set" => ParticleHelper.SetProperty(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "module"),
                    JsonHelper.ExtractString(args, "prop"), JsonHelper.ExtractString(args, "value")),
                "apply" => ParticleHelper.ApplyPreset(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "preset")),
                "play" => ParticleHelper.Play(JsonHelper.ExtractString(args, "path")),
                "stop" => ParticleHelper.Stop(JsonHelper.ExtractString(args, "path")),
                "pause" => ParticleHelper.Pause(JsonHelper.ExtractString(args, "path")),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "create", "set", "apply", "play", "stop", "pause" }))
            };
        }

        private static string ExecShaderConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => ShaderSerializer.Serialize(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "target")),
                "create" => ShaderHelper.Create(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "preset"),
                    JsonHelper.ExtractString(args, "code"),
                    JsonHelper.ExtractString(args, "shader_name")),
                "set" => ExecShaderSet(args),
                "graph_get" => ShaderGraphHelper.Get(JsonHelper.ExtractString(args, "path")),
                "graph_create" => ShaderGraphHelper.Create(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "preset")),
                "graph_node" => ShaderGraphHelper.ManageNode(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "node_type"),
                    JsonHelper.ExtractString(args, "node_id"),
                    JsonHelper.ExtractString(args, "node_action") ?? "add"),
                "graph_edge" => ShaderGraphHelper.ManageEdge(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "output_node"),
                    ExtractInt(args, "output_slot", 0),
                    JsonHelper.ExtractString(args, "input_node"),
                    ExtractInt(args, "input_slot", 0),
                    JsonHelper.ExtractString(args, "edge_action") ?? "add"),
                "graph_add_property" => ShaderGraphHelper.AddProperty(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "name"),
                    JsonHelper.ExtractString(args, "type"),
                    JsonHelper.ExtractString(args, "default_value"),
                    JsonHelper.ExtractString(args, "reference_name")),
                "graph_remove_property" => ShaderGraphHelper.RemoveProperty(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "name")),
                "graph_rename_property" => ShaderGraphHelper.RenameProperty(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "name"),
                    JsonHelper.ExtractString(args, "new_name")),
                "graph_get_layout" => ShaderGraphHelper.GetLayout(
                    JsonHelper.ExtractString(args, "path")),
                "graph_set_layout" => ShaderGraphHelper.SetLayout(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "layout")),
                "graph_auto_layout" => ShaderGraphHelper.AutoLayout(
                    JsonHelper.ExtractString(args, "path"),
                    ParseOptFloat(args, "h_gap") ?? 80f,
                    ParseOptFloat(args, "v_gap") ?? 50f),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "create", "set", "graph_get", "graph_create", "graph_node", "graph_edge",
                            "graph_add_property", "graph_remove_property", "graph_rename_property",
                            "graph_get_layout", "graph_set_layout", "graph_auto_layout" }))
            };
        }

        private static string ExecShaderSet(string args)
        {
            var kw = JsonHelper.ExtractString(args, "keyword");
            if (kw != null)
                return ShaderHelper.SetKeyword(
                    JsonHelper.ExtractString(args, "path"), kw,
                    JsonHelper.ExtractString(args, "enabled") ?? "true");
            return ShaderHelper.SetProperty(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "prop"),
                JsonHelper.ExtractString(args, "value"));
        }

        private static string ExecAddTransition(string args, int layer = 0)
        {
            var hasExitTimeStr = JsonHelper.ExtractString(args, "has_exit_time");
            bool? hasExitTime = hasExitTimeStr != null ? hasExitTimeStr == "true" : (bool?)null;

            return AnimatorControllerHelper.AddTransition(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "source"),
                JsonHelper.ExtractString(args, "target"),
                JsonHelper.ExtractString(args, "conditions"),
                ParseOptFloat(args, "duration"),
                ParseOptFloat(args, "exit_time"),
                hasExitTime, layer);
        }

        private static string ExecMenu(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "execute" => MenuHelper.Execute(JsonHelper.ExtractString(args, "path")),
                "list" => MenuHelper.List(JsonHelper.ExtractString(args, "path")),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action, new[] { "execute", "list" }))
            };
        }
    }
}
