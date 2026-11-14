using System;

namespace UnityMCP.Editor
{
    // Consolidated action-dispatch handlers (animation/timeline/animator/particle/shader/
    // scene/ui/editor/menu/references) split from CommandRouter.cs for <200-line focus.
    public static partial class CommandRouter
    {
        // ARG adoption (Step 4, Batch 10e): the local ParseOptFloat/ParseOptBool/ParseOptInt
        // wrappers that used to live here were deleted once every call site in this file
        // migrated to the generated {Tool}Args accessor (which calls JsonHelper.ParseOpt*
        // directly) -- confirmed via a repo-wide grep that nothing outside JsonHelper.cs and
        // Generated/ still calls them unprefixed.

        private static string ExecGetAnimation(string args)
        {
            return AnimationSerializer.Serialize(
                AnimationArgs.Path(args),
                AnimationArgs.Clip(args),
                AnimationArgs.Time(args),
                AnimationArgs.Compact(args));
        }

        private static string ExecCreateAnimation(string args)
        {
            return AnimationHelper.CreateClip(
                AnimationArgs.Path(args),
                AnimationArgs.ClipName(args) ?? AnimationArgs.Clip(args),
                AnimationArgs.Property(args) ?? "localPosition",
                AnimationArgs.Keys(args) ?? "",
                AnimationArgs.ComponentType(args),
                AnimationArgs.BindingPath(args),
                AnimationArgs.Tangent(args));
        }

        private static string ExecEditAnimation(string args)
        {
            return AnimationHelper.EditClip(
                AnimationArgs.Path(args),
                AnimationArgs.Clip(args),
                AnimationArgs.Action(args),
                AnimationArgs.Property(args),
                AnimationArgs.Keys(args),
                AnimationArgs.ComponentType(args),
                AnimationArgs.BindingPath(args),
                AnimationArgs.Tangent(args));
        }

        private static string ExecPreviewAnimation(string args)
        {
            return AnimationHelper.Preview(
                AnimationArgs.Path(args),
                AnimationArgs.Clip(args),
                AnimationArgs.Action(args) ?? "sample",
                AnimationArgs.Time(args) ?? 0f);
        }

        private static string ExecGetTimeline(string args)
        {
            return TimelineSerializer.Serialize(
                TimelineArgs.Path(args),
                TimelineArgs.Track(args));
        }

        private static string ExecCreateTimeline(string args)
        {
            return TimelineHelper.CreateTimeline(
                TimelineArgs.AssetPath(args),
                TimelineArgs.DirectorPath(args) ?? TimelineArgs.Path(args),
                TimelineArgs.Tracks(args));
        }

        private static string ExecEditTimeline(string args)
        {
            return TimelineHelper.Edit(
                TimelineArgs.Path(args),
                TimelineArgs.Action(args),
                TimelineArgs.Track(args),
                TimelineArgs.TrackType(args),
                TimelineArgs.Clip(args),
                TimelineArgs.Binding(args),
                TimelineArgs.Start(args),
                TimelineArgs.Duration(args),
                TimelineArgs.BlendIn(args),
                TimelineArgs.BlendOut(args),
                TimelineArgs.Name(args),
                TimelineArgs.Index(args),
                TimelineArgs.Offset(args),
                TimelineArgs.Value(args));
        }

        private static string ExecSetClipIn(string args)
        {
            return TimelineHelper.SetClipIn(
                TimelineArgs.Path(args),
                TimelineArgs.Track(args),
                TimelineArgs.Clip(args),
                TimelineArgs.ClipIn(args) ?? 0f);
        }

        private static string ExecGetBindings(string args)
        {
            return TimelineHelper.GetBindings(TimelineArgs.Path(args));
        }

        private static string ExecPreviewTimeline(string args)
        {
            return TimelineHelper.Preview(
                TimelineArgs.Path(args),
                TimelineArgs.Action(args) ?? "sample",
                TimelineArgs.Time(args) ?? 0f);
        }

        // --- Consolidated command handlers ---

        // P-414: scene save must NOT create an undo group (prevents isDirty clearing in Unity 6): noUndo. scene is mutating as in
        // Python (step 7.2) and its read is the READ line `list`; the mutating actions (new/open/discard/close) record
        // mutation explicitly inside ExecScene
        internal static string ExecScene(string args)
        {
            var action = SceneArgs.Action(args);
            var result = action switch
            {
                "new" => SceneHelper.NewScene(),
                "open" => SceneHelper.OpenScene(SceneArgs.Path(args)),
                "save" => SceneHelper.SaveScene(SceneArgs.Path(args), SceneArgs.Scene(args)),
                "discard" => SceneHelper.DiscardChanges(SceneArgs.Scene(args)),
                "open_additive" => SceneHelper.OpenAdditive(SceneArgs.Path(args)),
                "close" => SceneHelper.CloseScene(SceneArgs.Path(args)),
                "set_active" => SceneHelper.SetActiveScene(SceneArgs.Path(args)),
                "list" => SceneHelper.ListScenes(),
                "save_copy" => SceneHelper.SaveCopy(
                    SceneArgs.Path(args),
                    SceneArgs.Scene(args)),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    ToolActions.Names("scene")))
            };
            // P-414: scene is mutating:false so undo group is never opened around saves.
            // Topology-changing sub-actions still need a mutation record for get_changes.
            if (action != "list" && action != "save" && action != "save_copy")
                ChangeWatcher.RecordMutation($"MCP_SCENE_{action.ToUpper()}");
            return result;
        }

        internal static string ExecAnimationConsolidated(string args)
        {
            var action = AnimationArgs.Action(args);
            var path = AnimationArgs.Path(args);
            var clip = AnimationArgs.Clip(args);
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
                    AnimationArgs.Time(args) ?? 0f,
                    AnimationArgs.FunctionName(args),
                    AnimationArgs.IntParam(args),
                    AnimationArgs.FloatParam(args),
                    AnimationArgs.StringParam(args)),
                "remove_event" => AnimationHelper.RemoveEvent(
                    path, clip, AnimationArgs.Time(args) ?? 0f),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    ToolActions.Names("animation")))
            };
        }

        internal static string ExecTimelineConsolidated(string args)
        {
            var action = TimelineArgs.Action(args);
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
                    ToolActions.Names("timeline")))
            };
        }

        // NOT ReferencesArgs.Action/.Path: "references" is not in ArgContract's enabled set, so
        // no required-arg gate protects this handler. Both accessors call JsonHelper.RequireString,
        // which would throw where this call site today lets a null action/path fall through to
        // the switch's own graceful ErrorHelper.InvalidAction message. Keep both raw.
        internal static string ExecReferencesConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => ReferenceHelper.GetReferences(
                    JsonHelper.ExtractString(args, "path"),
                    ReferencesArgs.Children(args),
                    ExtractInt(args, "depth", 1)),
                "find_to" => ReferenceHelper.FindReferencesTo(JsonHelper.ExtractString(args, "path")),
                "remap" => RemapReferencesHelper.RemapReferences(
                    ReferencesArgs.Source(args),
                    ReferencesArgs.Target(args),
                    ReferencesArgs.Mappings(args)),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    ToolActions.Names("references")))
            };
        }

        internal static string ExecCreateUI(string args)
        {
            return UIHelper.CreateUI(
                CreateUiArgs.Type(args),
                CreateUiArgs.Name(args),
                CreateUiArgs.Parent(args),
                CreateUiArgs.Anchor(args),
                CreateUiArgs.Pos(args),
                CreateUiArgs.Size(args),
                CreateUiArgs.Pivot(args),
                CreateUiArgs.Color(args),
                CreateUiArgs.Text(args),
                CreateUiArgs.FontSize(args),
                CreateUiArgs.RenderMode(args),
                CreateUiArgs.FontMin(args),   // G9: TMP autoSize min
                CreateUiArgs.FontMax(args));  // G9: TMP autoSize max
        }

        // G2: Lint uGUI — check EventSystem presence and GraphicRaycaster on Canvas.
        internal static string ExecLintUGUI(string args) =>
            UILinter.LintUGUI(LintUguiArgs.Root(args));

        // S4: Lint UITK — structural checks for .uxml/.uss files (A1–A6).
        internal static string ExecLintUITK(string args) =>
            UILinter.LintUITK(
                LintUitkArgs.Path(args),
                // NOT LintUitkArgs.Fix: that accessor returns bool? (JsonHelper.ParseOptBool,
                // which throws on a malformed "fix" value); this call site's `== "true"` never
                // throws and defaults to false on anything but the literal string "true". Keep raw.
                JsonHelper.ExtractString(args, "fix") == "true");

        internal static string ExecSetRect(string args)
        {
            return UIHelper.SetRect(
                SetRectArgs.Path(args),
                SetRectArgs.Anchor(args),
                SetRectArgs.Pos(args),
                SetRectArgs.Size(args),
                SetRectArgs.Pivot(args),
                SetRectArgs.OffsetMin(args),
                SetRectArgs.OffsetMax(args),
                SetRectArgs.Pos3(args));  // G8: anchoredPosition3D
        }

        internal static string ExecInspectUITK(string args) =>
            UIHelper.InspectUITK(
                InspectUitkArgs.Path(args),
                InspectUitkArgs.Depth(args) ?? 4,
                InspectUitkArgs.Selector(args),
                InspectUitkArgs.Filter(args),
                InspectUitkArgs.IncludeInternal(args) ?? false,
                InspectUitkArgs.ShowStyle(args) ?? false);

        // Phase 2: uitk_element — read/mutate a VisualElement
        internal static string ExecUitkElement(string args) =>
            UIHelper.UitkElement(
                UitkElementArgs.Action(args),
                UitkElementArgs.Path(args),
                UitkElementArgs.Ref(args),
                UitkElementArgs.Selector(args),
                UitkElementArgs.Name(args),
                UitkElementArgs.Value(args),
                UitkElementArgs.Property(args),
                UitkElementArgs.ClassName(args));

        // Session 9: attach UIDocument component to a GameObject
        internal static string ExecAttachUITK(string args)
        {
            // NOT AttachUitkArgs.SortOrder: that accessor returns int? via JsonHelper.ParseOptInt,
            // which throws on a malformed "sort_order" value; this call site's int.TryParse never
            // throws and silently falls back to 0. Keep raw.
            var sortingOrderStr = JsonHelper.ExtractString(args, "sort_order");
            int sortingOrder = sortingOrderStr != null && int.TryParse(sortingOrderStr, out int so) ? so : 0;
            return UIHelper.AttachUITK(
                AttachUitkArgs.Path(args),
                AttachUitkArgs.Uxml(args),
                AttachUitkArgs.PanelSettings(args),
                sortingOrder);
        }

        // Session 12: uitk_file — UXML/USS CRUD with two-gate validation
        internal static string ExecUitkFile(string args)
        {
            // NOT UitkFileArgs.Path: "uitk_file" is not in ArgContract's enabled set, so no
            // required-arg gate protects this handler -- RequireString would throw where this
            // call site today passes a possibly-null path through to UIFileHelper.*. Keep raw.
            var assetPath = JsonHelper.ExtractString(args, "path");
            var action    = UitkFileArgs.Action(args) ?? "read";

            if (action == "read")
                return UIFileHelper.ReadUIFile(assetPath);

            if (action == "revert")
                return UIFileHelper.RevertUIFile(assetPath);

            if (action == "write" || action == "create_uxml" || action == "create_uss")
                return UIFileHelper.WriteUIFile(assetPath, action,
                    UitkFileArgs.Content(args));

            return UIFileHelper.EditUIFile(assetPath, action,
                selector: UitkFileArgs.Selector(args),
                attr:     UitkFileArgs.Attr(args),
                value:    UitkFileArgs.Value(args),
                cssClass: UitkFileArgs.Class(args),
                parent:   UitkFileArgs.Parent(args),
                tag:      UitkFileArgs.Tag(args),
                attrs:    UitkFileArgs.Attrs(args),
                prop:     UitkFileArgs.Prop(args));
        }

        internal static string ExecEditor(string args)
        {
            var action = EditorArgs.Action(args) ?? "state";
            if (action == "state")
                return EditorStateHelper.GetState();
            if (action == "mutation_mode")
                return SourcePatchModePolicy.SetMutationIntent(EditorArgs.Enable(args));
            return EditorStateHelper.Control(action, EditorArgs.Path(args), args);
        }

        internal static string ExecAnimatorConsolidated(string args)
        {
            var action = AnimatorArgs.Action(args);
            var layerStr = AnimatorArgs.Layer(args);
            int layer = layerStr != null && int.TryParse(layerStr, out int li) ? li : 0;
            return action switch
            {
                "get" => AnimatorControllerSerializer.Serialize(
                    AnimatorArgs.Path(args), AnimatorArgs.State(args)),
                "add_param" => AnimatorControllerHelper.AddParameters(
                    AnimatorArgs.Path(args), AnimatorParamSpec(args)),
                "add_state" => AnimatorControllerHelper.AddStates(
                    AnimatorArgs.Path(args), AnimatorStatesSpec(args), layer),
                "add_transition" => ExecAddTransition(args, layer),
                "set_default" => AnimatorControllerHelper.SetDefault(
                    AnimatorArgs.Path(args), AnimatorArgs.State(args), layer),
                "remove" => AnimatorControllerHelper.Remove(
                    AnimatorArgs.Path(args), AnimatorArgs.Type(args),
                    AnimatorArgs.Name(args), AnimatorArgs.Source(args),
                    AnimatorArgs.Target(args)),
                "add_blend_tree" => AnimatorControllerHelper.AddBlendTree(
                    AnimatorArgs.Path(args), AnimatorArgs.State(args),
                    AnimatorArgs.BlendType(args),
                    AnimatorArgs.Param(args),
                    AnimatorArgs.ParamY(args),
                    AnimatorArgs.Children(args)),
                "edit_blend_tree" => AnimatorControllerHelper.EditBlendTree(
                    AnimatorArgs.Path(args), AnimatorArgs.State(args),
                    AnimatorArgs.EditAction(args),
                    AnimatorArgs.Children(args),
                    AnimatorArgs.Param(args),
                    AnimatorArgs.ParamY(args),
                    AnimatorArgs.BlendType(args)),
                "get_blend_tree" => AnimatorControllerHelper.GetBlendTreeDetail(
                    AnimatorArgs.Path(args), AnimatorArgs.State(args)),
                "add_layer" => AnimatorControllerHelper.AddLayer(
                    AnimatorArgs.Path(args),
                    AnimatorArgs.Name(args),
                    AnimatorArgs.Weight(args) ?? 0f,
                    AnimatorArgs.Blending(args) ?? "Override"),
                "remove_layer" => AnimatorControllerHelper.RemoveLayer(
                    AnimatorArgs.Path(args),
                    layerStr ?? "0"),
                "rename_layer" => AnimatorControllerHelper.RenameLayer(
                    AnimatorArgs.Path(args),
                    layerStr ?? "0",
                    AnimatorArgs.Name(args)),
                "set_layer_weight" => AnimatorControllerHelper.SetLayerWeight(
                    AnimatorArgs.Path(args),
                    layerStr ?? "0",
                    // Same field, different per-call-site default (0f for add_layer above,
                    // 1f here for set_layer_weight) -- AnimatorArgs.Weight has no baked default
                    // of its own (nullable), so each call site's own `?? X` is unaffected by the
                    // other's.
                    AnimatorArgs.Weight(args) ?? 1f),
                "set_layer_blending" => AnimatorControllerHelper.SetLayerBlending(
                    AnimatorArgs.Path(args),
                    layerStr ?? "0",
                    AnimatorArgs.Blending(args) ?? "Override"),
                "set_state_speed" => AnimatorControllerHelper.SetStateSpeed(
                    AnimatorArgs.Path(args),
                    AnimatorArgs.State(args),
                    AnimatorArgs.Value(args)),
                "update_transition" => AnimatorControllerHelper.UpdateTransition(
                    AnimatorArgs.Path(args),
                    AnimatorArgs.Source(args),
                    AnimatorArgs.Target(args),
                    AnimatorArgs.Duration(args),
                    AnimatorArgs.ExitTime(args),
                    AnimatorArgs.HasExitTime(args),
                    AnimatorArgs.Conditions(args),
                    layer),
                "set_avatar" => AnimatorControllerHelper.SetAvatar(
                    AnimatorArgs.Path(args),
                    AnimatorArgs.AvatarPath(args)),
                "rename_state" => AnimatorControllerHelper.RenameState(
                    AnimatorArgs.Path(args),
                    AnimatorArgs.State(args),
                    AnimatorArgs.Name(args)),
                "rename_param" => AnimatorControllerHelper.RenameParameter(
                    AnimatorArgs.Path(args),
                    AnimatorArgs.Param(args),
                    AnimatorArgs.Name(args)),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    ToolActions.Names("animator")))
            };
        }

        private static string AnimatorParamSpec(string args)
        {
            var spec = AnimatorArgs.Params(args);
            if (!string.IsNullOrEmpty(spec)) return spec;

            var name = AnimatorArgs.Name(args);
            if (string.IsNullOrEmpty(name)) return spec;

            var type = AnimatorArgs.Type(args) ?? "float";
            var value = AnimatorArgs.Value(args);
            return value == null ? $"{name}:{type}" : $"{name}:{type}:{value}";
        }

        private static string AnimatorStatesSpec(string args)
        {
            var spec = AnimatorArgs.States(args);
            if (!string.IsNullOrEmpty(spec)) return spec;
            return AnimatorArgs.State(args);
        }

        private static string ExecCreateParticle(string args)
        {
            var parentPath = ParticleArgs.Path(args);
            var name = ParticleArgs.Name(args);
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(parentPath)
                && ComponentSerializer.FindObject(parentPath) == null)
            {
                name = System.IO.Path.GetFileName(parentPath);
                parentPath = System.IO.Path.GetDirectoryName(parentPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parentPath) || parentPath == "/" || parentPath == "\\")
                    parentPath = null;
            }
            return ParticleHelper.Create(parentPath, name, ParticleArgs.Preset(args));
        }

        internal static string ExecParticleConsolidated(string args)
        {
            var action = ParticleArgs.Action(args);
            return action switch
            {
                "get" => ParticleSerializer.Serialize(
                    ParticleArgs.Path(args), ParticleArgs.Module(args)),
                "create" => ExecCreateParticle(args),
                "set" => ParticleHelper.SetProperty(
                    ParticleArgs.Path(args), ParticleArgs.Module(args),
                    ParticleArgs.Prop(args), ParticleArgs.Value(args)),
                "apply" => ParticleHelper.ApplyPreset(
                    ParticleArgs.Path(args), ParticleArgs.Preset(args)),
                "play" => ParticleHelper.Play(ParticleArgs.Path(args)),
                "stop" => ParticleHelper.Stop(ParticleArgs.Path(args)),
                "pause" => ParticleHelper.Pause(ParticleArgs.Path(args)),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    ToolActions.Names("particle")))
            };
        }

        // NOT ShaderArgs.Action/.Path throughout this handler and ExecShaderSet below: "shader"
        // is not in ArgContract's enabled set, so no required-arg gate protects this handler --
        // both accessors call JsonHelper.RequireString, which would throw where action/path are
        // today read as plain (possibly-null) strings. Keep both raw on every arm.
        internal static string ExecShaderConsolidated(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "get" => ShaderSerializer.Serialize(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.Target(args)),
                "create" => ShaderHelper.Create(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.Preset(args),
                    ShaderArgs.Code(args),
                    ShaderArgs.ShaderName(args)),
                "set" => ExecShaderSet(args),
                "graph_get" => ShaderGraphHelper.Get(JsonHelper.ExtractString(args, "path")),
                "graph_create" => ShaderGraphHelper.Create(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.Preset(args)),
                "graph_node" => ShaderGraphHelper.ManageNode(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.NodeType(args),
                    ShaderArgs.NodeId(args),
                    ShaderArgs.NodeAction(args) ?? "add"),
                "graph_edge" => ShaderGraphHelper.ManageEdge(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.OutputNode(args),
                    ExtractInt(args, "output_slot", 0),
                    ShaderArgs.InputNode(args),
                    ExtractInt(args, "input_slot", 0),
                    ShaderArgs.EdgeAction(args) ?? "add"),
                "graph_add_property" => ShaderGraphHelper.AddProperty(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.Name(args),
                    ShaderArgs.Type(args),
                    ShaderArgs.DefaultValue(args),
                    ShaderArgs.ReferenceName(args)),
                "graph_remove_property" => ShaderGraphHelper.RemoveProperty(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.Name(args)),
                "graph_rename_property" => ShaderGraphHelper.RenameProperty(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.Name(args),
                    ShaderArgs.NewName(args)),
                "graph_get_layout" => ShaderGraphHelper.GetLayout(
                    JsonHelper.ExtractString(args, "path")),
                "graph_set_layout" => ShaderGraphHelper.SetLayout(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.Layout(args)),
                "graph_auto_layout" => ShaderGraphHelper.AutoLayout(
                    JsonHelper.ExtractString(args, "path"),
                    ShaderArgs.HGap(args) ?? 80f,
                    ShaderArgs.VGap(args) ?? 50f),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    ToolActions.Names("shader")))
            };
        }

        private static string ExecShaderSet(string args)
        {
            var kw = ShaderArgs.Keyword(args);
            if (kw != null)
                return ShaderHelper.SetKeyword(
                    JsonHelper.ExtractString(args, "path"), kw,
                    ShaderArgs.Enabled(args) ?? "true");
            return ShaderHelper.SetProperty(
                JsonHelper.ExtractString(args, "path"),
                ShaderArgs.Prop(args),
                ShaderArgs.Value(args));
        }

        private static string ExecAddTransition(string args, int layer = 0)
        {
            return AnimatorControllerHelper.AddTransition(
                AnimatorArgs.Path(args),
                AnimatorArgs.Source(args),
                AnimatorArgs.Target(args),
                AnimatorArgs.Conditions(args),
                AnimatorArgs.Duration(args),
                AnimatorArgs.ExitTime(args),
                AnimatorArgs.HasExitTime(args), layer);
        }

        internal static string ExecMenu(string args)
        {
            var action = MenuArgs.Action(args);
            return action switch
            {
                "execute" => MenuHelper.Execute(MenuArgs.Path(args)),
                "list" => MenuHelper.List(MenuArgs.Path(args)),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    ToolActions.Names("menu")))
            };
        }
    }
}
