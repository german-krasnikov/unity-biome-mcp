using System;

namespace UnityMCP.Editor
{
    // Consolidated action-dispatch handlers (animation/timeline/animator/particle/shader/
    // scene/ui/editor/menu/references) split from CommandRouter.cs for <200-line focus.
    public static partial class CommandRouter
    {
        private static float? ParseOptFloat(string args, string key)
        {
            var s = JsonHelper.ExtractString(args, key);
            return s != null ? float.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : (float?)null;
        }

        private static string ExecGetAnimation(string args)
        {
            return AnimationSerializer.Serialize(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "clip"),
                ParseOptFloat(args, "time"));
        }

        private static string ExecCreateAnimation(string args)
        {
            return AnimationHelper.CreateClip(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "clip_name"),
                JsonHelper.ExtractString(args, "property") ?? "localPosition",
                JsonHelper.ExtractString(args, "keys") ?? "",
                JsonHelper.ExtractString(args, "component_type"));
        }

        private static string ExecEditAnimation(string args)
        {
            return AnimationHelper.EditClip(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "clip"),
                JsonHelper.ExtractString(args, "action"),
                JsonHelper.ExtractString(args, "property"),
                JsonHelper.ExtractString(args, "keys"),
                JsonHelper.ExtractString(args, "component_type"));
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
                JsonHelper.ExtractString(args, "director_path"),
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
                ParseOptFloat(args, "blend_out"));
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
            return action switch
            {
                "new" => SceneHelper.NewScene(),
                "open" => SceneHelper.OpenScene(JsonHelper.ExtractString(args, "path")),
                "save" => SceneHelper.SaveScene(JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "scene")),
                "discard" => SceneHelper.DiscardChanges(JsonHelper.ExtractString(args, "scene")),
                "open_additive" => SceneHelper.OpenAdditive(JsonHelper.ExtractString(args, "path")),
                "close" => SceneHelper.CloseScene(JsonHelper.ExtractString(args, "path")),
                "set_active" => SceneHelper.SetActiveScene(JsonHelper.ExtractString(args, "path")),
                "list" => SceneHelper.ListScenes(),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "new", "open", "save", "discard", "open_additive", "close", "set_active", "list" }))
            };
        }

        private static string ExecAnimationConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => ExecGetAnimation(args),
                "create" => ExecCreateAnimation(args),
                "edit" or "add_key" or "remove_key" or "remove_curve" or "set_keys" or "set_loop"
                    => ExecEditAnimation(args),
                "preview" => ExecPreviewAnimation(args),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "create", "edit", "add_key", "remove_key", "remove_curve", "set_keys", "set_loop", "preview" }))
            };
        }

        private static string ExecTimelineConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => ExecGetTimeline(args),
                "create" => ExecCreateTimeline(args),
                "edit" or "add_track" or "remove_track" or "add_clip" or "remove_clip"
                    or "set_binding" or "set_timing" or "mute" or "unmute"
                    or "lock" or "unlock"
                    => ExecEditTimeline(args),
                "preview" => ExecPreviewTimeline(args),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "create", "edit", "add_track", "remove_track", "add_clip", "remove_clip", "set_binding", "set_timing", "mute", "unmute", "lock", "unlock", "preview" }))
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
                JsonHelper.ExtractString(args, "fontSize"));
        }

        private static string ExecSetRect(string args)
        {
            return UIHelper.SetRect(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "anchor"),
                JsonHelper.ExtractString(args, "pos"),
                JsonHelper.ExtractString(args, "size"),
                JsonHelper.ExtractString(args, "pivot"),
                JsonHelper.ExtractString(args, "offsetMin"),
                JsonHelper.ExtractString(args, "offsetMax"));
        }

        private static string ExecEditor(string args)
        {
            var action = JsonHelper.ExtractString(args, "action") ?? "state";
            if (action == "state")
                return EditorStateHelper.GetState();
            return EditorStateHelper.Control(action, JsonHelper.ExtractString(args, "path"));
        }

        private static string ExecAnimatorConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => AnimatorControllerSerializer.Serialize(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "state")),
                "add_param" => AnimatorControllerHelper.AddParameters(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "params")),
                "add_state" => AnimatorControllerHelper.AddStates(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "states")),
                "add_transition" => ExecAddTransition(args),
                "set_default" => AnimatorControllerHelper.SetDefault(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "state")),
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
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "add_param", "add_state", "add_transition", "set_default", "remove",
                            "add_blend_tree", "edit_blend_tree", "get_blend_tree" }))
            };
        }

        private static string ExecParticleConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => ParticleSerializer.Serialize(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "module")),
                "create" => ParticleHelper.Create(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "name"),
                    JsonHelper.ExtractString(args, "preset")),
                "set" => ParticleHelper.SetProperty(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "module"),
                    JsonHelper.ExtractString(args, "prop"), JsonHelper.ExtractString(args, "value")),
                "apply" => ParticleHelper.ApplyPreset(
                    JsonHelper.ExtractString(args, "path"), JsonHelper.ExtractString(args, "preset")),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "create", "set", "apply" }))
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
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "create", "set", "graph_get", "graph_create", "graph_node", "graph_edge" }))
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

        private static string ExecAddTransition(string args)
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
                hasExitTime);
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
