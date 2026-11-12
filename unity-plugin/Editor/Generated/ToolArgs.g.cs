// Generated from dsl/tools.biome
// source-sha256=ae7ae5192ef26bc47e023f639ac9ca2c0ed65b883653a301f194ebed26f4de43
// Edit the source, then run biome generate.

// identifier = wire key of dsl/tools.biome; LOCAL args have no accessor.
namespace UnityMCP.Editor
{
internal static class @alias_statusArgs
{
}
internal static class @analyze_lod_cullingArgs
{
    internal static string @focus(string args) => JsonHelper.ExtractString(args, "focus");
}
internal static class @animationArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @clip(string args) => JsonHelper.ExtractString(args, "clip");
    internal static string @clip_name(string args) => JsonHelper.ExtractString(args, "clip_name");
    internal static string @property(string args) => JsonHelper.ExtractString(args, "property");
    internal static string @keys(string args) => JsonHelper.ExtractString(args, "keys");
    internal static float? @time(string args) => JsonHelper.ParseOptFloat(args, "time");
    internal static string @component_type(string args) => JsonHelper.ExtractString(args, "component_type");
    internal static string @binding_path(string args) => JsonHelper.ExtractString(args, "binding_path");
    internal static string @tangent(string args) => JsonHelper.ExtractString(args, "tangent");
    internal static string @function_name(string args) => JsonHelper.ExtractString(args, "function_name");
    internal static int? @int_param(string args) => JsonHelper.ParseOptInt(args, "int_param");
    internal static float? @float_param(string args) => JsonHelper.ParseOptFloat(args, "float_param");
    internal static string @string_param(string args) => JsonHelper.ExtractString(args, "string_param");
    internal static bool @compact(string args) => JsonHelper.ExtractString(args, "compact") == "true";
}
internal static class @animatorArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @state(string args) => JsonHelper.ExtractString(args, "state");
    internal static string @states(string args) => JsonHelper.ExtractString(args, "states");
    internal static string @params(string args) => JsonHelper.ExtractString(args, "params");
    internal static string @source(string args) => JsonHelper.ExtractString(args, "source");
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @conditions(string args) => JsonHelper.ExtractString(args, "conditions");
    internal static float? @duration(string args) => JsonHelper.ParseOptFloat(args, "duration");
    internal static float? @exit_time(string args) => JsonHelper.ParseOptFloat(args, "exit_time");
    internal static bool? @has_exit_time(string args) => JsonHelper.ParseOptBool(args, "has_exit_time");
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @blend_type(string args) => JsonHelper.ExtractString(args, "blend_type");
    internal static string @param(string args) => JsonHelper.ExtractString(args, "param");
    internal static string @param_y(string args) => JsonHelper.ExtractString(args, "param_y");
    internal static string @children(string args) => JsonHelper.ExtractString(args, "children");
    internal static string @edit_action(string args) => JsonHelper.ExtractString(args, "edit_action");
    internal static string @layer(string args) => JsonHelper.ExtractString(args, "layer");
    internal static float? @weight(string args) => JsonHelper.ParseOptFloat(args, "weight");
    internal static string @blending(string args) => JsonHelper.ExtractString(args, "blending");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string @avatar_path(string args) => JsonHelper.ExtractString(args, "avatar_path");
}
internal static class @animator_intentArgs
{
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class @apply_scene_changeArgs
{
    internal static string @plan_id(string args) => JsonHelper.ExtractString(args, "plan_id");
    internal static string @commands(string args) => JsonHelper.ExtractString(args, "commands");
    internal static bool @verify(string args) => JsonHelper.ExtractString(args, "verify") == "true";
    internal static bool @save(string args) => JsonHelper.ExtractString(args, "save") == "true";
}
internal static class @apply_templateArgs
{
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @params(string args) => JsonHelper.ExtractString(args, "params");
}
internal static class @askArgs
{
    internal static string @question(string args) => JsonHelper.ExtractString(args, "question");
}
internal static class @ask_userArgs
{
    internal static string @questions(string args) => JsonHelper.ExtractString(args, "questions");
}
internal static class @assetArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @folder(string args) => JsonHelper.ExtractString(args, "folder");
    internal static string @source(string args) => JsonHelper.ExtractString(args, "source");
    internal static string @dest(string args) => JsonHelper.ExtractString(args, "dest");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static bool @recursive(string args) => JsonHelper.ExtractString(args, "recursive") == "true";
    internal static string @labels(string args) => JsonHelper.ExtractString(args, "labels");
    internal static string @output(string args) => JsonHelper.ExtractString(args, "output");
    internal static bool @include_deps(string args) => JsonHelper.ExtractString(args, "include_deps") == "true";
    internal static string @content(string args) => JsonHelper.ExtractString(args, "content");
    internal static string @class(string args) => JsonHelper.ExtractString(args, "class");
    internal static bool @path_only(string args) => JsonHelper.ExtractString(args, "path_only") == "true";
}
internal static class @attach_uitkArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @uxml(string args) => JsonHelper.ExtractString(args, "uxml");
    internal static string @panel_settings(string args) => JsonHelper.ExtractString(args, "panel_settings");
    internal static int? @sort_order(string args) => JsonHelper.ParseOptInt(args, "sort_order");
}
internal static class @auto_fixArgs
{
    internal static string @ctx(string args) => JsonHelper.ExtractString(args, "ctx");
}
internal static class @auto_wireArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class @autofit_colliderArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
}
internal static class @await_compileArgs
{
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static int? @expected_generation(string args) => JsonHelper.ParseOptInt(args, "expected_generation");
}
internal static class @bakeArgs
{
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
}
internal static class @batchArgs
{
    internal static string @commands(string args) => JsonHelper.ExtractString(args, "commands");
    internal static string @on_error(string args) => JsonHelper.ExtractString(args, "on_error");
    internal static bool @atomic(string args) => JsonHelper.ExtractString(args, "atomic") == "true";
    internal static bool @validate_aliases(string args) => JsonHelper.ExtractString(args, "validate_aliases") == "true";
}
internal static class @brief_buildArgs
{
    internal static string @kinds(string args) => JsonHelper.ExtractString(args, "kinds");
    internal static int @budget(string args) => JsonHelper.ExtractInt(args, "budget");
}
internal static class @budget_statusArgs
{
}
internal static class @buildArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @scenes(string args) => JsonHelper.ExtractString(args, "scenes");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool @dev(string args) => JsonHelper.ExtractString(args, "dev") == "true";
}
internal static class @cancel_test_runArgs
{
    internal static string @run_id(string args) => JsonHelper.ExtractString(args, "run_id");
}
internal static class @check_collidersArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class @checkpointArgs
{
    internal static string @label(string args) => JsonHelper.ExtractString(args, "label");
}
internal static class @checkpoint_createArgs
{
    internal static string @paths(string args) => JsonHelper.ExtractString(args, "paths");
}
internal static class @checkpoint_restoreArgs
{
    internal static string @checkpoint_id(string args) => JsonHelper.ExtractString(args, "checkpoint_id");
    internal static bool @force(string args) => JsonHelper.ExtractString(args, "force") == "true";
}
internal static class @compile_preflightArgs
{
    internal static string @file_path(string args) => JsonHelper.ExtractString(args, "file_path");
    internal static string @new_content(string args) => JsonHelper.ExtractString(args, "new_content");
}
internal static class @compile_statusArgs
{
}
internal static class @configure_objectsArgs
{
    internal static string @config(string args) => JsonHelper.ExtractString(args, "config");
}
internal static class @console_markArgs
{
    internal static string @label(string args) => JsonHelper.ExtractString(args, "label");
}
internal static class @create_objectArgs
{
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string @components(string args) => JsonHelper.ExtractString(args, "components");
    internal static string @primitive(string args) => JsonHelper.ExtractString(args, "primitive");
    internal static string @prefab_path(string args) => JsonHelper.ExtractString(args, "prefab_path");
    internal static string @scene(string args) => JsonHelper.ExtractString(args, "scene");
}
internal static class @create_uiArgs
{
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string @anchor(string args) => JsonHelper.ExtractString(args, "anchor");
    internal static string @pos(string args) => JsonHelper.ExtractString(args, "pos");
    internal static string @size(string args) => JsonHelper.ExtractString(args, "size");
    internal static string @pivot(string args) => JsonHelper.ExtractString(args, "pivot");
    internal static string @color(string args) => JsonHelper.ExtractString(args, "color");
    internal static string @text(string args) => JsonHelper.ExtractString(args, "text");
    internal static string @font_size(string args) => JsonHelper.ExtractString(args, "font_size");
    internal static string @render_mode(string args) => JsonHelper.ExtractString(args, "render_mode");
    internal static string @font_min(string args) => JsonHelper.ExtractString(args, "font_min");
    internal static string @font_max(string args) => JsonHelper.ExtractString(args, "font_max");
}
internal static class @debugArgs
{
    internal static string @symptom(string args) => JsonHelper.ExtractString(args, "symptom");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @gather(string args) => JsonHelper.ExtractString(args, "gather");
}
internal static class @debug_animatorArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class @debug_physicsArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static float @radius(string args) => JsonHelper.ExtractFloat(args, "radius");
}
internal static class @delete_objectArgs
{
    internal static int? @id(string args) => JsonHelper.ParseOptInt(args, "id");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool @force(string args) => JsonHelper.ExtractString(args, "force") == "true";
}
internal static class @diagnoseArgs
{
}
internal static class @discover_toolsArgs
{
    internal static string @category(string args) => JsonHelper.ExtractString(args, "category");
    internal static bool @enable(string args) => JsonHelper.ExtractString(args, "enable") == "true";
    internal static bool @include_legacy(string args) => JsonHelper.ExtractString(args, "include_legacy") == "true";
    internal static bool @structured(string args) => JsonHelper.ExtractString(args, "structured") == "true";
    internal static string @ctx(string args) => JsonHelper.ExtractString(args, "ctx");
}
internal static class @doArgs
{
    internal static string @intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class @doctorArgs
{
    internal static bool @fix(string args) => JsonHelper.ExtractString(args, "fix") == "true";
}
internal static class @editorArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @paths(string args) => JsonHelper.ExtractString(args, "paths");
    internal static bool? @enable(string args) => JsonHelper.ParseOptBool(args, "enable");
}
internal static class @execute_codeArgs
{
    internal static string @code(string args) => JsonHelper.ExtractString(args, "code");
    internal static string @undo_label(string args) => JsonHelper.ExtractString(args, "undo_label");
}
internal static class @export_packageArgs
{
}
internal static class @export_playtest_aliases_to_defsArgs
{
    internal static string @asset(string args) => JsonHelper.ExtractString(args, "asset");
    internal static string @defs(string args) => JsonHelper.ExtractString(args, "defs");
}
internal static class @find_objectsArgs
{
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @tag(string args) => JsonHelper.ExtractString(args, "tag");
    internal static string @layer(string args) => JsonHelper.ExtractString(args, "layer");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
}
internal static class @fingerprintArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static int @depth(string args) => JsonHelper.ExtractInt(args, "depth");
}
internal static class @get_aliasesArgs
{
}
internal static class @get_capabilitiesArgs
{
}
internal static class @get_changesArgs
{
    internal static bool @clear(string args) => JsonHelper.ExtractString(args, "clear") == "true";
}
internal static class @get_changesetArgs
{
}
internal static class @get_compile_errorsArgs
{
}
internal static class @get_componentArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @fields(string args) => JsonHelper.ExtractString(args, "fields");
    internal static bool @compress(string args) => JsonHelper.ExtractString(args, "compress") == "true";
}
internal static class @get_components_listArgs
{
    internal static int @id(string args) => JsonHelper.ExtractInt(args, "id");
}
internal static class @get_consoleArgs
{
    internal static int @count(string args) => JsonHelper.ExtractInt(args, "count");
    internal static string @level(string args) => JsonHelper.ExtractString(args, "level");
    internal static int @first(string args) => JsonHelper.ExtractInt(args, "first");
    internal static string @keyword(string args) => JsonHelper.ExtractString(args, "keyword");
    internal static bool @count_only(string args) => JsonHelper.ExtractString(args, "count_only") == "true";
    internal static float? @since(string args) => JsonHelper.ParseOptFloat(args, "since");
}
internal static class @get_console_sinceArgs
{
    internal static string @mark_id(string args) => JsonHelper.ExtractString(args, "mark_id");
    internal static string @level(string args) => JsonHelper.ExtractString(args, "level");
    internal static int @count(string args) => JsonHelper.ExtractInt(args, "count");
    internal static string @keyword(string args) => JsonHelper.ExtractString(args, "keyword");
    internal static bool @count_only(string args) => JsonHelper.ExtractString(args, "count_only") == "true";
}
internal static class @get_enabled_toolsArgs
{
}
internal static class @get_frame_statsArgs
{
    internal static string @include(string args) => JsonHelper.ExtractString(args, "include");
}
internal static class @get_hierarchyArgs
{
    internal static int @depth(string args) => JsonHelper.ExtractInt(args, "depth");
    internal static string @root(string args) => JsonHelper.ExtractString(args, "root");
    internal static string @filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static bool @components(string args) => JsonHelper.ExtractString(args, "components") == "true";
    internal static bool @compress(string args) => JsonHelper.ExtractString(args, "compress") == "true";
    internal static bool @summary(string args) => JsonHelper.ExtractString(args, "summary") == "true";
    internal static bool @incremental(string args) => JsonHelper.ExtractString(args, "incremental") == "true";
    internal static string @scene(string args) => JsonHelper.ExtractString(args, "scene");
}
internal static class @get_memoryArgs
{
    internal static string @include(string args) => JsonHelper.ExtractString(args, "include");
}
internal static class @get_metricsArgs
{
    internal static string @format(string args) => JsonHelper.ExtractString(args, "format");
    internal static bool @reset(string args) => JsonHelper.ExtractString(args, "reset") == "true";
}
internal static class @get_object_detailArgs
{
    internal static int @id(string args) => JsonHelper.ExtractInt(args, "id");
}
internal static class @get_playtest_runArgs
{
    internal static string @run_id(string args) => JsonHelper.ExtractString(args, "run_id");
}
internal static class @get_schemaArgs
{
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
}
internal static class @get_selectionArgs
{
}
internal static class @get_spatial_contextArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static float @radius(string args) => JsonHelper.ExtractFloat(args, "radius");
}
internal static class @get_statusArgs
{
}
internal static class @get_test_countArgs
{
}
internal static class @get_test_progressArgs
{
    internal static string @run_id(string args) => JsonHelper.ExtractString(args, "run_id");
}
internal static class @get_test_resultsArgs
{
    internal static string @run_id(string args) => JsonHelper.ExtractString(args, "run_id");
}
internal static class @get_test_runArgs
{
    internal static string @run_id(string args) => JsonHelper.ExtractString(args, "run_id");
    internal static bool @compact(string args) => JsonHelper.ExtractString(args, "compact") == "true";
}
internal static class @get_unity_eventsArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class @get_versionArgs
{
}
internal static class @get_watchesArgs
{
}
internal static class @import_packageArgs
{
}
internal static class @inspectArgs
{
    internal static string @paths(string args) => JsonHelper.ExtractString(args, "paths");
    internal static string @components(string args) => JsonHelper.ExtractString(args, "components");
    internal static string @fields(string args) => JsonHelper.ExtractString(args, "fields");
    internal static bool @compress(string args) => JsonHelper.ExtractString(args, "compress") == "true";
    internal static string @find_type(string args) => JsonHelper.ExtractString(args, "find_type");
}
internal static class @inspect_uitkArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static int? @depth(string args) => JsonHelper.ParseOptInt(args, "depth");
    internal static string @selector(string args) => JsonHelper.ExtractString(args, "selector");
    internal static string @filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static bool? @include_internal(string args) => JsonHelper.ParseOptBool(args, "include_internal");
    internal static bool? @show_style(string args) => JsonHelper.ParseOptBool(args, "show_style");
}
internal static class @invoke_methodArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @method(string args) => JsonHelper.ExtractString(args, "method");
    internal static string @args(string args) => JsonHelper.ExtractString(args, "args");
}
internal static class @lint_playtestArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @script(string args) => JsonHelper.ExtractString(args, "script");
}
internal static class @lint_playtest_suiteArgs
{
    internal static string @pattern(string args) => JsonHelper.ExtractString(args, "pattern");
    internal static string @suite_path(string args) => JsonHelper.ExtractString(args, "suite_path");
}
internal static class @lint_scene_refsArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @snippet(string args) => JsonHelper.ExtractString(args, "snippet");
}
internal static class @lint_uguiArgs
{
    internal static string @root(string args) => JsonHelper.ExtractString(args, "root");
}
internal static class @lint_uitkArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool? @fix(string args) => JsonHelper.ParseOptBool(args, "fix");
}
internal static class @list_connectionsArgs
{
}
internal static class @list_eventsArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @event(string args) => JsonHelper.ExtractString(args, "event");
}
internal static class @list_playtest_filesArgs
{
    internal static string @pattern(string args) => JsonHelper.ExtractString(args, "pattern");
}
internal static class @list_skillsArgs
{
}
internal static class @list_templatesArgs
{
}
internal static class @list_test_runsArgs
{
    internal static int @limit(string args) => JsonHelper.ExtractInt(args, "limit");
}
internal static class @load_sessionArgs
{
}
internal static class @manage_componentArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
}
internal static class @materialArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @object_path(string args) => JsonHelper.ExtractString(args, "object_path");
    internal static string @shader(string args) => JsonHelper.ExtractString(args, "shader");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string @source(string args) => JsonHelper.ExtractString(args, "source");
    internal static string @targets(string args) => JsonHelper.ExtractString(args, "targets");
    internal static int? @slot(string args) => JsonHelper.ParseOptInt(args, "slot");
    internal static string @filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
}
internal static class @material_auditArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @platform(string args) => JsonHelper.ExtractString(args, "platform");
}
internal static class @mcp_statusArgs
{
}
internal static class @menuArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class @move_toArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @position(string args) => JsonHelper.ExtractString(args, "position");
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
}
internal static class @navmesh_queryArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @center(string args) => JsonHelper.ExtractString(args, "center");
    internal static string @from_pos(string args) => JsonHelper.ExtractString(args, "from_pos");
    internal static string @to(string args) => JsonHelper.ExtractString(args, "to");
    internal static float @max_distance(string args) => JsonHelper.ExtractFloat(args, "max_distance");
    internal static int @area_mask(string args) => JsonHelper.ExtractInt(args, "area_mask");
    internal static float? @agentRadius(string args) => JsonHelper.ParseOptFloat(args, "agentRadius");
    internal static float? @agentHeight(string args) => JsonHelper.ParseOptFloat(args, "agentHeight");
    internal static float? @agentClimb(string args) => JsonHelper.ParseOptFloat(args, "agentClimb");
    internal static float? @agentSlope(string args) => JsonHelper.ParseOptFloat(args, "agentSlope");
}
internal static class @object_diffArgs
{
    internal static string @path_a(string args) => JsonHelper.ExtractString(args, "path_a");
    internal static string @path_b(string args) => JsonHelper.ExtractString(args, "path_b");
}
internal static class @packageArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @version(string args) => JsonHelper.ExtractString(args, "version");
    internal static string @query(string args) => JsonHelper.ExtractString(args, "query");
}
internal static class @particleArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @module(string args) => JsonHelper.ExtractString(args, "module");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string @preset(string args) => JsonHelper.ExtractString(args, "preset");
}
internal static class @permission_promptArgs
{
    internal static string @tool_name(string args) => JsonHelper.ExtractString(args, "tool_name");
    internal static string @input(string args) => JsonHelper.ExtractString(args, "input");
    internal static string @tool_use_id(string args) => JsonHelper.ExtractString(args, "tool_use_id");
}
internal static class @pingArgs
{
}
internal static class @ping_objectArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class @prefabArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @asset_path(string args) => JsonHelper.ExtractString(args, "asset_path");
    internal static string @base_path(string args) => JsonHelper.ExtractString(args, "base_path");
    internal static string @variant_path(string args) => JsonHelper.ExtractString(args, "variant_path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string @add_component(string args) => JsonHelper.ExtractString(args, "add_component");
    internal static string @remove_component(string args) => JsonHelper.ExtractString(args, "remove_component");
    internal static bool @recursive(string args) => JsonHelper.ExtractString(args, "recursive") == "true";
    internal static string @mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static string @scope(string args) => JsonHelper.ExtractString(args, "scope");
    internal static string @format(string args) => JsonHelper.ExtractString(args, "format");
    internal static string @parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string @child_path(string args) => JsonHelper.ExtractString(args, "child_path");
}
internal static class @profileArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static float @duration(string args) => JsonHelper.ExtractFloat(args, "duration");
    internal static string @session(string args) => JsonHelper.ExtractString(args, "session");
    internal static string @compare_with(string args) => JsonHelper.ExtractString(args, "compare_with");
    internal static string @focus(string args) => JsonHelper.ExtractString(args, "focus");
    internal static string @mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static float @threshold_ms(string args) => JsonHelper.ExtractFloat(args, "threshold_ms");
}
internal static class @project_settingsArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static int? @index(string args) => JsonHelper.ParseOptInt(args, "index");
    internal static string @build_target(string args) => JsonHelper.ExtractString(args, "build_target");
}
internal static class @query_stateArgs
{
    internal static string @queries(string args) => JsonHelper.ExtractString(args, "queries");
}
internal static class @recompileArgs
{
}
internal static class @reconnect_unityArgs
{
    internal static int @port(string args) => JsonHelper.ExtractInt(args, "port");
    internal static string @ctx(string args) => JsonHelper.ExtractString(args, "ctx");
}
internal static class @referencesArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool @children(string args) => JsonHelper.ExtractString(args, "children") == "true";
    internal static int @depth(string args) => JsonHelper.ExtractInt(args, "depth");
    internal static string @source(string args) => JsonHelper.ExtractString(args, "source");
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @mappings(string args) => JsonHelper.ExtractString(args, "mappings");
}
internal static class @region_clearArgs
{
    internal static string @vertices(string args) => JsonHelper.ExtractString(args, "vertices");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
    internal static string @filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static int @cap(string args) => JsonHelper.ExtractInt(args, "cap");
}
internal static class @release_smokeArgs
{
}
internal static class @rename_objectArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
}
internal static class @render_analyzeArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @detail(string args) => JsonHelper.ExtractString(args, "detail");
    internal static string @baseline_id(string args) => JsonHelper.ExtractString(args, "baseline_id");
    internal static int? @max_events(string args) => JsonHelper.ParseOptInt(args, "max_events");
}
internal static class @resolve_scene_refsArgs
{
    internal static string @refs(string args) => JsonHelper.ExtractString(args, "refs");
    internal static string @fields(string args) => JsonHelper.ExtractString(args, "fields");
}
internal static class @resolve_test_requestArgs
{
    internal static string @request_id(string args) => JsonHelper.ExtractString(args, "request_id");
}
internal static class @resolve_tool_schemaArgs
{
    internal static string @tools(string args) => JsonHelper.ExtractString(args, "tools");
}
internal static class @run_playtestArgs
{
    internal static string @script(string args) => JsonHelper.ExtractString(args, "script");
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static bool @abort_on_fail(string args) => JsonHelper.ExtractString(args, "abort_on_fail") == "true";
    internal static string @defs(string args) => JsonHelper.ExtractString(args, "defs");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool @snapshot_on_failure(string args) => JsonHelper.ExtractString(args, "snapshot_on_failure") == "true";
    internal static bool @fresh(string args) => JsonHelper.ExtractString(args, "fresh") == "true";
    internal static string @before_hook(string args) => JsonHelper.ExtractString(args, "before_hook");
    internal static string @after_hook(string args) => JsonHelper.ExtractString(args, "after_hook");
    internal static string @format(string args) => JsonHelper.ExtractString(args, "format");
}
internal static class @run_playtest_suiteArgs
{
    internal static string @pattern(string args) => JsonHelper.ExtractString(args, "pattern");
    internal static string @suite_path(string args) => JsonHelper.ExtractString(args, "suite_path");
    internal static float @timeout_per_test(string args) => JsonHelper.ExtractFloat(args, "timeout_per_test");
    internal static bool @stop_on_fail(string args) => JsonHelper.ExtractString(args, "stop_on_fail") == "true";
    internal static bool @stop_after(string args) => JsonHelper.ExtractString(args, "stop_after") == "true";
    internal static bool @auto_play(string args) => JsonHelper.ExtractString(args, "auto_play") == "true";
    internal static bool @restart_between(string args) => JsonHelper.ExtractString(args, "restart_between") == "true";
    internal static float @suite_timeout(string args) => JsonHelper.ExtractFloat(args, "suite_timeout");
    internal static string @tag(string args) => JsonHelper.ExtractString(args, "tag");
}
internal static class @run_testsArgs
{
    internal static string @mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static string @filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static string @request_id(string args) => JsonHelper.ExtractString(args, "request_id");
    internal static string @assemblies(string args) => JsonHelper.ExtractString(args, "assemblies");
    internal static string @categories(string args) => JsonHelper.ExtractString(args, "categories");
    internal static string @group(string args) => JsonHelper.ExtractString(args, "group");
    internal static string @tests(string args) => JsonHelper.ExtractString(args, "tests");
}
internal static class @run_tests_waitArgs
{
    internal static string @mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static string @filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static float @poll_interval(string args) => JsonHelper.ExtractFloat(args, "poll_interval");
    internal static string @request_id(string args) => JsonHelper.ExtractString(args, "request_id");
}
internal static class @runtime_snapshotArgs
{
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static bool @compress(string args) => JsonHelper.ExtractString(args, "compress") == "true";
}
internal static class @save_sessionArgs
{
}
internal static class @save_skillArgs
{
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @description(string args) => JsonHelper.ExtractString(args, "description");
    internal static string @code(string args) => JsonHelper.ExtractString(args, "code");
}
internal static class @save_templateArgs
{
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @code(string args) => JsonHelper.ExtractString(args, "code");
}
internal static class @scan_sceneArgs
{
}
internal static class @sceneArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @scene(string args) => JsonHelper.ExtractString(args, "scene");
    internal static bool @include_unsaved(string args) => JsonHelper.ExtractString(args, "include_unsaved") == "true";
}
internal static class @scene_change_planArgs
{
    internal static string @goal(string args) => JsonHelper.ExtractString(args, "goal");
    internal static string @targets(string args) => JsonHelper.ExtractString(args, "targets");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class @scene_diffArgs
{
}
internal static class @scene_environmentArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
}
internal static class @scene_healthArgs
{
    internal static string @focus(string args) => JsonHelper.ExtractString(args, "focus");
}
internal static class @screenshotArgs
{
    internal static int @width(string args) => JsonHelper.ExtractInt(args, "width");
    internal static int @height(string args) => JsonHelper.ExtractInt(args, "height");
    internal static string @camera(string args) => JsonHelper.ExtractString(args, "camera");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @output_path(string args) => JsonHelper.ExtractString(args, "output_path");
    internal static float? @zoom(string args) => JsonHelper.ParseOptFloat(args, "zoom");
    internal static string @angles(string args) => JsonHelper.ExtractString(args, "angles");
    internal static int? @supersample(string args) => JsonHelper.ParseOptInt(args, "supersample");
    internal static string @offset(string args) => JsonHelper.ExtractString(args, "offset");
    internal static float? @fixed_size(string args) => JsonHelper.ParseOptFloat(args, "fixed_size");
    internal static string @highlight(string args) => JsonHelper.ExtractString(args, "highlight");
    internal static bool? @show_colliders(string args) => JsonHelper.ParseOptBool(args, "show_colliders");
    internal static string @angle(string args) => JsonHelper.ExtractString(args, "angle");
    internal static string @annotation_id(string args) => JsonHelper.ExtractString(args, "annotation_id");
}
internal static class @screenshot_baselineArgs
{
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static int @width(string args) => JsonHelper.ExtractInt(args, "width");
    internal static int @height(string args) => JsonHelper.ExtractInt(args, "height");
    internal static string @camera(string args) => JsonHelper.ExtractString(args, "camera");
}
internal static class @screenshot_compareArgs
{
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static int @width(string args) => JsonHelper.ExtractInt(args, "width");
    internal static int @height(string args) => JsonHelper.ExtractInt(args, "height");
    internal static string @camera(string args) => JsonHelper.ExtractString(args, "camera");
    internal static string @mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static string @question(string args) => JsonHelper.ExtractString(args, "question");
}
internal static class @scriptable_objectArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string @fields(string args) => JsonHelper.ExtractString(args, "fields");
    internal static string @filter(string args) => JsonHelper.ExtractString(args, "filter");
}
internal static class @search_sceneArgs
{
    internal static string @query(string args) => JsonHelper.ExtractString(args, "query");
    internal static string @root(string args) => JsonHelper.ExtractString(args, "root");
    internal static int @limit(string args) => JsonHelper.ExtractInt(args, "limit");
    internal static string @scene(string args) => JsonHelper.ExtractString(args, "scene");
}
internal static class @serialized_field_rename_auditArgs
{
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @old_field(string args) => JsonHelper.ExtractString(args, "old_field");
    internal static string @new_field(string args) => JsonHelper.ExtractString(args, "new_field");
    internal static string @include(string args) => JsonHelper.ExtractString(args, "include");
}
internal static class @set_activeArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool @active(string args) => JsonHelper.ExtractString(args, "active") == "true";
}
internal static class @set_llm_configArgs
{
    internal static string @config(string args) => JsonHelper.ExtractString(args, "config");
}
internal static class @set_materialArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @color(string args) => JsonHelper.ExtractString(args, "color");
    internal static string @shader(string args) => JsonHelper.ExtractString(args, "shader");
}
internal static class @set_parentArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static bool @world_position_stays(string args) => JsonHelper.ExtractString(args, "world_position_stays") == "true";
}
internal static class @set_propertiesArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @props(string args) => JsonHelper.ExtractString(args, "props");
}
internal static class @set_propertyArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
    internal static string @find_type(string args) => JsonHelper.ExtractString(args, "find_type");
}
internal static class @set_property_deltaArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @delta(string args) => JsonHelper.ExtractString(args, "delta");
}
internal static class @set_rectArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @anchor(string args) => JsonHelper.ExtractString(args, "anchor");
    internal static string @pos(string args) => JsonHelper.ExtractString(args, "pos");
    internal static string @size(string args) => JsonHelper.ExtractString(args, "size");
    internal static string @pivot(string args) => JsonHelper.ExtractString(args, "pivot");
    internal static string @offset_min(string args) => JsonHelper.ExtractString(args, "offset_min");
    internal static string @offset_max(string args) => JsonHelper.ExtractString(args, "offset_max");
    internal static string @pos3(string args) => JsonHelper.ExtractString(args, "pos3");
}
internal static class @set_sibling_indexArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static int @index(string args) => JsonHelper.ExtractInt(args, "index");
}
internal static class @setup_objectsArgs
{
    internal static string @specs(string args) => JsonHelper.ExtractString(args, "specs");
}
internal static class @shaderArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @preset(string args) => JsonHelper.ExtractString(args, "preset");
    internal static string @code(string args) => JsonHelper.ExtractString(args, "code");
    internal static string @shader_name(string args) => JsonHelper.ExtractString(args, "shader_name");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string @keyword(string args) => JsonHelper.ExtractString(args, "keyword");
    internal static string @enabled(string args) => JsonHelper.ExtractString(args, "enabled");
    internal static string @node_type(string args) => JsonHelper.ExtractString(args, "node_type");
    internal static string @node_id(string args) => JsonHelper.ExtractString(args, "node_id");
    internal static string @node_action(string args) => JsonHelper.ExtractString(args, "node_action");
    internal static string @output_node(string args) => JsonHelper.ExtractString(args, "output_node");
    internal static int? @output_slot(string args) => JsonHelper.ParseOptInt(args, "output_slot");
    internal static string @input_node(string args) => JsonHelper.ExtractString(args, "input_node");
    internal static int? @input_slot(string args) => JsonHelper.ParseOptInt(args, "input_slot");
    internal static string @edge_action(string args) => JsonHelper.ExtractString(args, "edge_action");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string @default_value(string args) => JsonHelper.ExtractString(args, "default_value");
    internal static string @reference_name(string args) => JsonHelper.ExtractString(args, "reference_name");
    internal static string @new_name(string args) => JsonHelper.ExtractString(args, "new_name");
    internal static string @layout(string args) => JsonHelper.ExtractString(args, "layout");
    internal static float? @h_gap(string args) => JsonHelper.ParseOptFloat(args, "h_gap");
    internal static float? @v_gap(string args) => JsonHelper.ParseOptFloat(args, "v_gap");
}
internal static class @smart_buildArgs
{
    internal static string @description(string args) => JsonHelper.ExtractString(args, "description");
    internal static string @ctx(string args) => JsonHelper.ExtractString(args, "ctx");
}
internal static class @snapshotArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @label(string args) => JsonHelper.ExtractString(args, "label");
    internal static string @compare(string args) => JsonHelper.ExtractString(args, "compare");
}
internal static class @source_patch_writeArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @content(string args) => JsonHelper.ExtractString(args, "content");
}
internal static class @spatial_queryArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static float? @distance(string args) => JsonHelper.ParseOptFloat(args, "distance");
    internal static float? @radius(string args) => JsonHelper.ParseOptFloat(args, "radius");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static float? @cell_size(string args) => JsonHelper.ParseOptFloat(args, "cell_size");
    internal static string @layer_mask(string args) => JsonHelper.ExtractString(args, "layer_mask");
    internal static string @center(string args) => JsonHelper.ExtractString(args, "center");
    internal static string @vertices(string args) => JsonHelper.ExtractString(args, "vertices");
    internal static string @region_id(string args) => JsonHelper.ExtractString(args, "region_id");
    internal static int? @cap(string args) => JsonHelper.ParseOptInt(args, "cap");
}
internal static class @start_playtestArgs
{
    internal static string @script(string args) => JsonHelper.ExtractString(args, "script");
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static bool @abort_on_fail(string args) => JsonHelper.ExtractString(args, "abort_on_fail") == "true";
    internal static string @defs(string args) => JsonHelper.ExtractString(args, "defs");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool @snapshot_on_failure(string args) => JsonHelper.ExtractString(args, "snapshot_on_failure") == "true";
    internal static bool @fresh(string args) => JsonHelper.ExtractString(args, "fresh") == "true";
    internal static string @before_hook(string args) => JsonHelper.ExtractString(args, "before_hook");
    internal static string @after_hook(string args) => JsonHelper.ExtractString(args, "after_hook");
    internal static string @format(string args) => JsonHelper.ExtractString(args, "format");
}
internal static class @sync_playtest_aliases_from_defsArgs
{
    internal static string @defs(string args) => JsonHelper.ExtractString(args, "defs");
    internal static string @asset(string args) => JsonHelper.ExtractString(args, "asset");
}
internal static class @sync_unityArgs
{
    internal static bool @resolve(string args) => JsonHelper.ExtractString(args, "resolve") == "true";
    internal static bool @bump(string args) => JsonHelper.ExtractString(args, "bump") == "true";
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
}
internal static class @test_stepArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @position(string args) => JsonHelper.ExtractString(args, "position");
    internal static string @checks_before(string args) => JsonHelper.ExtractString(args, "checks_before");
    internal static string @checks_after(string args) => JsonHelper.ExtractString(args, "checks_after");
    internal static float @wait_after(string args) => JsonHelper.ExtractFloat(args, "wait_after");
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
}
internal static class @timelineArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @track(string args) => JsonHelper.ExtractString(args, "track");
    internal static string @track_type(string args) => JsonHelper.ExtractString(args, "track_type");
    internal static string @clip(string args) => JsonHelper.ExtractString(args, "clip");
    internal static string @binding(string args) => JsonHelper.ExtractString(args, "binding");
    internal static float? @start(string args) => JsonHelper.ParseOptFloat(args, "start");
    internal static float? @duration(string args) => JsonHelper.ParseOptFloat(args, "duration");
    internal static float? @blend_in(string args) => JsonHelper.ParseOptFloat(args, "blend_in");
    internal static float? @blend_out(string args) => JsonHelper.ParseOptFloat(args, "blend_out");
    internal static string @asset_path(string args) => JsonHelper.ExtractString(args, "asset_path");
    internal static string @director_path(string args) => JsonHelper.ExtractString(args, "director_path");
    internal static string @tracks(string args) => JsonHelper.ExtractString(args, "tracks");
    internal static float? @time(string args) => JsonHelper.ParseOptFloat(args, "time");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static float? @clip_in(string args) => JsonHelper.ParseOptFloat(args, "clip_in");
    internal static int? @index(string args) => JsonHelper.ParseOptInt(args, "index");
    internal static float? @offset(string args) => JsonHelper.ParseOptFloat(args, "offset");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
}
internal static class @transfer_objectArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @target_scene(string args) => JsonHelper.ExtractString(args, "target_scene");
    internal static string @parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static bool @world_position_stays(string args) => JsonHelper.ExtractString(args, "world_position_stays") == "true";
}
internal static class @ui_intentArgs
{
    internal static string @intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static string @parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string @template(string args) => JsonHelper.ExtractString(args, "template");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class @uitk_elementArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @ref(string args) => JsonHelper.ExtractString(args, "ref");
    internal static string @selector(string args) => JsonHelper.ExtractString(args, "selector");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string @property(string args) => JsonHelper.ExtractString(args, "property");
    internal static string @class_name(string args) => JsonHelper.ExtractString(args, "class_name");
}
internal static class @uitk_fileArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @content(string args) => JsonHelper.ExtractString(args, "content");
    internal static string @selector(string args) => JsonHelper.ExtractString(args, "selector");
    internal static string @attr(string args) => JsonHelper.ExtractString(args, "attr");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string @class(string args) => JsonHelper.ExtractString(args, "class");
    internal static string @parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string @tag(string args) => JsonHelper.ExtractString(args, "tag");
    internal static string @attrs(string args) => JsonHelper.ExtractString(args, "attrs");
    internal static string @prop(string args) => JsonHelper.ExtractString(args, "prop");
}
internal static class @uitk_intentArgs
{
    internal static string @intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @attach_to(string args) => JsonHelper.ExtractString(args, "attach_to");
    internal static string @template(string args) => JsonHelper.ExtractString(args, "template");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class @undo_lastArgs
{
    internal static int @turns(string args) => JsonHelper.ExtractInt(args, "turns");
}
internal static class @unwire_eventArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @event(string args) => JsonHelper.ExtractString(args, "event");
    internal static int? @index(string args) => JsonHelper.ParseOptInt(args, "index");
}
internal static class @use_skillArgs
{
    internal static string @name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string @params(string args) => JsonHelper.ExtractString(args, "params");
}
internal static class @validate_playtest_aliasesArgs
{
    internal static string @defs(string args) => JsonHelper.ExtractString(args, "defs");
    internal static string @asset(string args) => JsonHelper.ExtractString(args, "asset");
}
internal static class @validate_referencesArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static int @depth(string args) => JsonHelper.ExtractInt(args, "depth");
    internal static bool @verbose(string args) => JsonHelper.ExtractString(args, "verbose") == "true";
    internal static bool @ignore_optional(string args) => JsonHelper.ExtractString(args, "ignore_optional") == "true";
}
internal static class @validate_triggersArgs
{
    internal static string @root(string args) => JsonHelper.ExtractString(args, "root");
    internal static float @min_distance(string args) => JsonHelper.ExtractFloat(args, "min_distance");
}
internal static class @verify_after_changeArgs
{
    internal static string @changed_files(string args) => JsonHelper.ExtractString(args, "changed_files");
    internal static string @test_filter(string args) => JsonHelper.ExtractString(args, "test_filter");
    internal static string @run_tests_mode(string args) => JsonHelper.ExtractString(args, "run_tests_mode");
    internal static string @playtests(string args) => JsonHelper.ExtractString(args, "playtests");
    internal static string @mark_id(string args) => JsonHelper.ExtractString(args, "mark_id");
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static bool @auto_play(string args) => JsonHelper.ExtractString(args, "auto_play") == "true";
    internal static bool @restart_between(string args) => JsonHelper.ExtractString(args, "restart_between") == "true";
    internal static float @suite_timeout(string args) => JsonHelper.ExtractFloat(args, "suite_timeout");
}
internal static class @vfx_intentArgs
{
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static string @kind(string args) => JsonHelper.ExtractString(args, "kind");
    internal static bool @dry_run(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class @wait_untilArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @field(string args) => JsonHelper.ExtractString(args, "field");
    internal static string @value(string args) => JsonHelper.ExtractString(args, "value");
    internal static float @timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static bool @negate(string args) => JsonHelper.ExtractString(args, "negate") == "true";
    internal static bool @abort_on_fail(string args) => JsonHelper.ExtractString(args, "abort_on_fail") == "true";
}
internal static class @watchArgs
{
    internal static string @action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string @watch_id(string args) => JsonHelper.ExtractString(args, "watch_id");
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @field(string args) => JsonHelper.ExtractString(args, "field");
    internal static string @condition(string args) => JsonHelper.ExtractString(args, "condition");
    internal static string @trigger_action(string args) => JsonHelper.ExtractString(args, "trigger_action");
    internal static int @interval_ms(string args) => JsonHelper.ExtractInt(args, "interval_ms");
}
internal static class @wire_eventArgs
{
    internal static string @path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string @component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string @event(string args) => JsonHelper.ExtractString(args, "event");
    internal static string @target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string @method(string args) => JsonHelper.ExtractString(args, "method");
    internal static string @arg_type(string args) => JsonHelper.ExtractString(args, "arg_type");
    internal static string @arg_value(string args) => JsonHelper.ExtractString(args, "arg_value");
    internal static string @target_component_type(string args) => JsonHelper.ExtractString(args, "target_component_type");
    internal static string @parameter_types(string args) => JsonHelper.ExtractString(args, "parameter_types");
}
}
