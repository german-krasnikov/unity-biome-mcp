// Generated from dsl/tools.biome
// source-sha256=fb0ae2e48afd8305810f08991a9eb3150492943f27c500c18e93ef755b12c6ea
// Edit the source, then run biome generate.

// identifier = PascalCase of the wire key of dsl/tools.biome; LOCAL args have no accessor.
namespace UnityMCP.Editor
{
internal static class AliasStatusArgs
{
}
internal static class AnalyzeLodCullingArgs
{
    internal static string Focus(string args) => JsonHelper.ExtractString(args, "focus");
}
internal static class AnimationArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Clip(string args) => JsonHelper.ExtractString(args, "clip");
    internal static string ClipName(string args) => JsonHelper.ExtractString(args, "clip_name");
    internal static string Property(string args) => JsonHelper.ExtractString(args, "property");
    internal static string Keys(string args) => JsonHelper.ExtractString(args, "keys");
    internal static float? Time(string args) => JsonHelper.ParseOptFloat(args, "time");
    internal static string ComponentType(string args) => JsonHelper.ExtractString(args, "component_type");
    internal static string BindingPath(string args) => JsonHelper.ExtractString(args, "binding_path");
    internal static string Tangent(string args) => JsonHelper.ExtractString(args, "tangent");
    internal static string FunctionName(string args) => JsonHelper.ExtractString(args, "function_name");
    internal static int? IntParam(string args) => JsonHelper.ParseOptInt(args, "int_param");
    internal static float? FloatParam(string args) => JsonHelper.ParseOptFloat(args, "float_param");
    internal static string StringParam(string args) => JsonHelper.ExtractString(args, "string_param");
    internal static bool Compact(string args) => JsonHelper.ExtractString(args, "compact") == "true";
}
internal static class AnimatorArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string State(string args) => JsonHelper.ExtractString(args, "state");
    internal static string States(string args) => JsonHelper.ExtractString(args, "states");
    internal static string Params(string args) => JsonHelper.ExtractString(args, "params");
    internal static string Source(string args) => JsonHelper.ExtractString(args, "source");
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Conditions(string args) => JsonHelper.ExtractString(args, "conditions");
    internal static float? Duration(string args) => JsonHelper.ParseOptFloat(args, "duration");
    internal static float? ExitTime(string args) => JsonHelper.ParseOptFloat(args, "exit_time");
    internal static bool? HasExitTime(string args) => JsonHelper.ParseOptBool(args, "has_exit_time");
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string BlendType(string args) => JsonHelper.ExtractString(args, "blend_type");
    internal static string Param(string args) => JsonHelper.ExtractString(args, "param");
    internal static string ParamY(string args) => JsonHelper.ExtractString(args, "param_y");
    internal static string Children(string args) => JsonHelper.ExtractString(args, "children");
    internal static string EditAction(string args) => JsonHelper.ExtractString(args, "edit_action");
    internal static string Layer(string args) => JsonHelper.ExtractString(args, "layer");
    internal static float? Weight(string args) => JsonHelper.ParseOptFloat(args, "weight");
    internal static string Blending(string args) => JsonHelper.ExtractString(args, "blending");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string AvatarPath(string args) => JsonHelper.ExtractString(args, "avatar_path");
}
internal static class AnimatorIntentArgs
{
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class ApplySceneChangeArgs
{
    internal static string PlanId(string args) => JsonHelper.ExtractString(args, "plan_id");
    internal static string Commands(string args) => JsonHelper.ExtractString(args, "commands");
    internal static bool Verify(string args) => JsonHelper.ExtractString(args, "verify") == "true";
    internal static bool Save(string args) => JsonHelper.ExtractString(args, "save") == "true";
}
internal static class ApplyTemplateArgs
{
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Params(string args) => JsonHelper.ExtractString(args, "params");
}
internal static class AskArgs
{
    internal static string Question(string args) => JsonHelper.ExtractString(args, "question");
}
internal static class AskUserArgs
{
    internal static string Questions(string args) => JsonHelper.ExtractString(args, "questions");
}
internal static class AssetArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Folder(string args) => JsonHelper.ExtractString(args, "folder");
    internal static string Source(string args) => JsonHelper.ExtractString(args, "source");
    internal static string Dest(string args) => JsonHelper.ExtractString(args, "dest");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static bool Recursive(string args) => JsonHelper.ExtractString(args, "recursive") == "true";
    internal static string Labels(string args) => JsonHelper.ExtractString(args, "labels");
    internal static string Output(string args) => JsonHelper.ExtractString(args, "output");
    internal static bool IncludeDeps(string args) => JsonHelper.ExtractString(args, "include_deps") == "true";
    internal static string Content(string args) => JsonHelper.ExtractString(args, "content");
    internal static string Class(string args) => JsonHelper.ExtractString(args, "class");
    internal static bool PathOnly(string args) => JsonHelper.ExtractString(args, "path_only") == "true";
}
internal static class AttachUitkArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Uxml(string args) => JsonHelper.ExtractString(args, "uxml");
    internal static string PanelSettings(string args) => JsonHelper.ExtractString(args, "panel_settings");
    internal static int? SortOrder(string args) => JsonHelper.ParseOptInt(args, "sort_order");
}
internal static class AutoFixArgs
{
    internal static string Ctx(string args) => JsonHelper.ExtractString(args, "ctx");
}
internal static class AutoWireArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class AutofitColliderArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
}
internal static class AwaitCompileArgs
{
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static int? ExpectedGeneration(string args) => JsonHelper.ParseOptInt(args, "expected_generation");
}
internal static class BakeArgs
{
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
}
internal static class BatchArgs
{
    internal static string Commands(string args) => JsonHelper.ExtractString(args, "commands");
    internal static string OnError(string args) => JsonHelper.ExtractString(args, "on_error");
    internal static bool Atomic(string args) => JsonHelper.ExtractString(args, "atomic") == "true";
    internal static bool ValidateAliases(string args) => JsonHelper.ExtractString(args, "validate_aliases") == "true";
}
internal static class BriefBuildArgs
{
    internal static string Kinds(string args) => JsonHelper.ExtractString(args, "kinds");
    internal static int Budget(string args) => JsonHelper.ExtractInt(args, "budget");
}
internal static class BudgetStatusArgs
{
}
internal static class BuildArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Scenes(string args) => JsonHelper.ExtractString(args, "scenes");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool Dev(string args) => JsonHelper.ExtractString(args, "dev") == "true";
}
internal static class CancelTestRunArgs
{
    internal static string RunId(string args) => JsonHelper.ExtractString(args, "run_id");
}
internal static class CheckCollidersArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class CheckpointArgs
{
    internal static string Label(string args) => JsonHelper.ExtractString(args, "label");
}
internal static class CheckpointCreateArgs
{
    internal static string Paths(string args) => JsonHelper.ExtractString(args, "paths");
}
internal static class CheckpointRestoreArgs
{
    internal static string CheckpointId(string args) => JsonHelper.ExtractString(args, "checkpoint_id");
    internal static bool Force(string args) => JsonHelper.ExtractString(args, "force") == "true";
}
internal static class CompilePreflightArgs
{
    internal static string FilePath(string args) => JsonHelper.ExtractString(args, "file_path");
    internal static string NewContent(string args) => JsonHelper.ExtractString(args, "new_content");
}
internal static class CompileStatusArgs
{
}
internal static class ConfigureObjectsArgs
{
    internal static string Config(string args) => JsonHelper.ExtractString(args, "config");
}
internal static class ConsoleMarkArgs
{
    internal static string Label(string args) => JsonHelper.ExtractString(args, "label");
}
internal static class CreateObjectArgs
{
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string Components(string args) => JsonHelper.ExtractString(args, "components");
    internal static string Primitive(string args) => JsonHelper.ExtractString(args, "primitive");
    internal static string PrefabPath(string args) => JsonHelper.ExtractString(args, "prefab_path");
    internal static string Scene(string args) => JsonHelper.ExtractString(args, "scene");
}
internal static class CreateUiArgs
{
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string Anchor(string args) => JsonHelper.ExtractString(args, "anchor");
    internal static string Pos(string args) => JsonHelper.ExtractString(args, "pos");
    internal static string Size(string args) => JsonHelper.ExtractString(args, "size");
    internal static string Pivot(string args) => JsonHelper.ExtractString(args, "pivot");
    internal static string Color(string args) => JsonHelper.ExtractString(args, "color");
    internal static string Text(string args) => JsonHelper.ExtractString(args, "text");
    internal static string FontSize(string args) => JsonHelper.ExtractString(args, "font_size");
    internal static string RenderMode(string args) => JsonHelper.ExtractString(args, "render_mode");
    internal static string FontMin(string args) => JsonHelper.ExtractString(args, "font_min");
    internal static string FontMax(string args) => JsonHelper.ExtractString(args, "font_max");
}
internal static class DebugArgs
{
    internal static string Symptom(string args) => JsonHelper.ExtractString(args, "symptom");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Gather(string args) => JsonHelper.ExtractString(args, "gather");
}
internal static class DebugAnimatorArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class DebugPhysicsArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static float Radius(string args) => JsonHelper.ExtractFloat(args, "radius");
}
internal static class DeleteObjectArgs
{
    internal static int? Id(string args) => JsonHelper.ParseOptInt(args, "id");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool Force(string args) => JsonHelper.ExtractString(args, "force") == "true";
}
internal static class DiagnoseArgs
{
}
internal static class DiscoverToolsArgs
{
    internal static string Category(string args) => JsonHelper.ExtractString(args, "category");
    internal static bool Enable(string args) => JsonHelper.ExtractString(args, "enable") == "true";
    internal static bool IncludeLegacy(string args) => JsonHelper.ExtractString(args, "include_legacy") == "true";
    internal static bool Structured(string args) => JsonHelper.ExtractString(args, "structured") == "true";
    internal static string Ctx(string args) => JsonHelper.ExtractString(args, "ctx");
}
internal static class DoArgs
{
    internal static string Intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class DoctorArgs
{
    internal static bool Fix(string args) => JsonHelper.ExtractString(args, "fix") == "true";
}
internal static class EditorArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Paths(string args) => JsonHelper.ExtractString(args, "paths");
    internal static bool? Enable(string args) => JsonHelper.ParseOptBool(args, "enable");
}
internal static class ExecuteCodeArgs
{
    internal static string Code(string args) => JsonHelper.ExtractString(args, "code");
    internal static string UndoLabel(string args) => JsonHelper.ExtractString(args, "undo_label");
}
internal static class ExportPackageArgs
{
}
internal static class ExportPlaytestAliasesToDefsArgs
{
    internal static string Asset(string args) => JsonHelper.ExtractString(args, "asset");
    internal static string Defs(string args) => JsonHelper.ExtractString(args, "defs");
}
internal static class FindObjectsArgs
{
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Tag(string args) => JsonHelper.ExtractString(args, "tag");
    internal static string Layer(string args) => JsonHelper.ExtractString(args, "layer");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
}
internal static class FingerprintArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static int Depth(string args) => JsonHelper.ExtractInt(args, "depth");
}
internal static class GetAliasesArgs
{
}
internal static class GetCapabilitiesArgs
{
}
internal static class GetChangesArgs
{
    internal static bool Clear(string args) => JsonHelper.ExtractString(args, "clear") == "true";
}
internal static class GetChangesetArgs
{
}
internal static class GetCompileErrorsArgs
{
}
internal static class GetComponentArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string Fields(string args) => JsonHelper.ExtractString(args, "fields");
    internal static bool Compress(string args) => JsonHelper.ExtractString(args, "compress") == "true";
}
internal static class GetComponentsListArgs
{
    internal static int Id(string args) => JsonHelper.ExtractInt(args, "id");
}
internal static class GetConsoleArgs
{
    internal static int Count(string args) => JsonHelper.ExtractInt(args, "count");
    internal static string Level(string args) => JsonHelper.ExtractString(args, "level");
    internal static int First(string args) => JsonHelper.ExtractInt(args, "first");
    internal static string Keyword(string args) => JsonHelper.ExtractString(args, "keyword");
    internal static bool CountOnly(string args) => JsonHelper.ExtractString(args, "count_only") == "true";
    internal static float? Since(string args) => JsonHelper.ParseOptFloat(args, "since");
}
internal static class GetConsoleSinceArgs
{
    internal static string MarkId(string args) => JsonHelper.ExtractString(args, "mark_id");
    internal static string Level(string args) => JsonHelper.ExtractString(args, "level");
    internal static int Count(string args) => JsonHelper.ExtractInt(args, "count");
    internal static string Keyword(string args) => JsonHelper.ExtractString(args, "keyword");
    internal static bool CountOnly(string args) => JsonHelper.ExtractString(args, "count_only") == "true";
}
internal static class GetEnabledToolsArgs
{
}
internal static class GetFrameStatsArgs
{
    internal static string Include(string args) => JsonHelper.ExtractString(args, "include");
}
internal static class GetHierarchyArgs
{
    internal static int Depth(string args) => JsonHelper.ExtractInt(args, "depth");
    internal static string Root(string args) => JsonHelper.ExtractString(args, "root");
    internal static string Filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static bool Components(string args) => JsonHelper.ExtractString(args, "components") == "true";
    internal static bool Compress(string args) => JsonHelper.ExtractString(args, "compress") == "true";
    internal static bool Summary(string args) => JsonHelper.ExtractString(args, "summary") == "true";
    internal static bool Incremental(string args) => JsonHelper.ExtractString(args, "incremental") == "true";
    internal static string Scene(string args) => JsonHelper.ExtractString(args, "scene");
}
internal static class GetMemoryArgs
{
    internal static string Include(string args) => JsonHelper.ExtractString(args, "include");
}
internal static class GetMetricsArgs
{
    internal static string Format(string args) => JsonHelper.ExtractString(args, "format");
    internal static bool Reset(string args) => JsonHelper.ExtractString(args, "reset") == "true";
}
internal static class GetObjectDetailArgs
{
    internal static int Id(string args) => JsonHelper.ExtractInt(args, "id");
}
internal static class GetPlaytestRunArgs
{
    internal static string RunId(string args) => JsonHelper.ExtractString(args, "run_id");
}
internal static class GetSchemaArgs
{
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
}
internal static class GetSelectionArgs
{
}
internal static class GetSpatialContextArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static float Radius(string args) => JsonHelper.ExtractFloat(args, "radius");
}
internal static class GetStatusArgs
{
}
internal static class GetTestCountArgs
{
}
internal static class GetTestProgressArgs
{
    internal static string RunId(string args) => JsonHelper.ExtractString(args, "run_id");
}
internal static class GetTestResultsArgs
{
    internal static string RunId(string args) => JsonHelper.ExtractString(args, "run_id");
}
internal static class GetTestRunArgs
{
    internal static string RunId(string args) => JsonHelper.ExtractString(args, "run_id");
    internal static bool Compact(string args) => JsonHelper.ExtractString(args, "compact") == "true";
}
internal static class GetUnityEventsArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class GetVersionArgs
{
}
internal static class GetWatchesArgs
{
}
internal static class ImportPackageArgs
{
}
internal static class InspectArgs
{
    internal static string Paths(string args) => JsonHelper.ExtractString(args, "paths");
    internal static string Components(string args) => JsonHelper.ExtractString(args, "components");
    internal static string Fields(string args) => JsonHelper.ExtractString(args, "fields");
    internal static bool Compress(string args) => JsonHelper.ExtractString(args, "compress") == "true";
    internal static string FindType(string args) => JsonHelper.ExtractString(args, "find_type");
}
internal static class InspectUitkArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static int? Depth(string args) => JsonHelper.ParseOptInt(args, "depth");
    internal static string Selector(string args) => JsonHelper.ExtractString(args, "selector");
    internal static string Filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static bool? IncludeInternal(string args) => JsonHelper.ParseOptBool(args, "include_internal");
    internal static bool? ShowStyle(string args) => JsonHelper.ParseOptBool(args, "show_style");
}
internal static class InvokeMethodArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Method(string args) => JsonHelper.ExtractString(args, "method");
    internal static string Args(string args) => JsonHelper.ExtractString(args, "args");
}
internal static class LintPlaytestArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Script(string args) => JsonHelper.ExtractString(args, "script");
}
internal static class LintPlaytestSuiteArgs
{
    internal static string Pattern(string args) => JsonHelper.ExtractString(args, "pattern");
    internal static string SuitePath(string args) => JsonHelper.ExtractString(args, "suite_path");
}
internal static class LintSceneRefsArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Snippet(string args) => JsonHelper.ExtractString(args, "snippet");
}
internal static class LintUguiArgs
{
    internal static string Root(string args) => JsonHelper.ExtractString(args, "root");
}
internal static class LintUitkArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool? Fix(string args) => JsonHelper.ParseOptBool(args, "fix");
}
internal static class ListConnectionsArgs
{
}
internal static class ListEventsArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Event(string args) => JsonHelper.ExtractString(args, "event");
}
internal static class ListPlaytestFilesArgs
{
    internal static string Pattern(string args) => JsonHelper.ExtractString(args, "pattern");
}
internal static class ListSkillsArgs
{
}
internal static class ListTemplatesArgs
{
}
internal static class ListTestRunsArgs
{
    internal static int Limit(string args) => JsonHelper.ExtractInt(args, "limit");
}
internal static class LoadSessionArgs
{
}
internal static class ManageComponentArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
}
internal static class MaterialArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string ObjectPath(string args) => JsonHelper.ExtractString(args, "object_path");
    internal static string Shader(string args) => JsonHelper.ExtractString(args, "shader");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string Source(string args) => JsonHelper.ExtractString(args, "source");
    internal static string Targets(string args) => JsonHelper.ExtractString(args, "targets");
    internal static int? Slot(string args) => JsonHelper.ParseOptInt(args, "slot");
    internal static string Filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
}
internal static class MaterialAuditArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Platform(string args) => JsonHelper.ExtractString(args, "platform");
}
internal static class McpStatusArgs
{
}
internal static class MenuArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class MoveToArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Position(string args) => JsonHelper.ExtractString(args, "position");
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
}
internal static class NavmeshQueryArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Center(string args) => JsonHelper.ExtractString(args, "center");
    internal static string FromPos(string args) => JsonHelper.ExtractString(args, "from_pos");
    internal static string To(string args) => JsonHelper.ExtractString(args, "to");
    internal static float MaxDistance(string args) => JsonHelper.ExtractFloat(args, "max_distance");
    internal static int AreaMask(string args) => JsonHelper.ExtractInt(args, "area_mask");
    internal static float? AgentRadius(string args) => JsonHelper.ParseOptFloat(args, "agentRadius");
    internal static float? AgentHeight(string args) => JsonHelper.ParseOptFloat(args, "agentHeight");
    internal static float? AgentClimb(string args) => JsonHelper.ParseOptFloat(args, "agentClimb");
    internal static float? AgentSlope(string args) => JsonHelper.ParseOptFloat(args, "agentSlope");
}
internal static class ObjectDiffArgs
{
    internal static string PathA(string args) => JsonHelper.ExtractString(args, "path_a");
    internal static string PathB(string args) => JsonHelper.ExtractString(args, "path_b");
}
internal static class PackageArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Version(string args) => JsonHelper.ExtractString(args, "version");
    internal static string Query(string args) => JsonHelper.ExtractString(args, "query");
}
internal static class ParticleArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Module(string args) => JsonHelper.ExtractString(args, "module");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string Preset(string args) => JsonHelper.ExtractString(args, "preset");
}
internal static class PermissionPromptArgs
{
    internal static string ToolName(string args) => JsonHelper.ExtractString(args, "tool_name");
    internal static string Input(string args) => JsonHelper.ExtractString(args, "input");
    internal static string ToolUseId(string args) => JsonHelper.ExtractString(args, "tool_use_id");
}
internal static class PingArgs
{
}
internal static class PingObjectArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
}
internal static class PrefabArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string AssetPath(string args) => JsonHelper.ExtractString(args, "asset_path");
    internal static string BasePath(string args) => JsonHelper.ExtractString(args, "base_path");
    internal static string VariantPath(string args) => JsonHelper.ExtractString(args, "variant_path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string AddComponent(string args) => JsonHelper.ExtractString(args, "add_component");
    internal static string RemoveComponent(string args) => JsonHelper.ExtractString(args, "remove_component");
    internal static bool Recursive(string args) => JsonHelper.ExtractString(args, "recursive") == "true";
    internal static string Mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static string Scope(string args) => JsonHelper.ExtractString(args, "scope");
    internal static string Format(string args) => JsonHelper.ExtractString(args, "format");
    internal static string Parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string ChildPath(string args) => JsonHelper.ExtractString(args, "child_path");
}
internal static class ProfileArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static float Duration(string args) => JsonHelper.ExtractFloat(args, "duration");
    internal static string Session(string args) => JsonHelper.ExtractString(args, "session");
    internal static string CompareWith(string args) => JsonHelper.ExtractString(args, "compare_with");
    internal static string Focus(string args) => JsonHelper.ExtractString(args, "focus");
    internal static string Mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static float ThresholdMs(string args) => JsonHelper.ExtractFloat(args, "threshold_ms");
}
internal static class ProjectSettingsArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static int? Index(string args) => JsonHelper.ParseOptInt(args, "index");
    internal static string BuildTarget(string args) => JsonHelper.ExtractString(args, "build_target");
}
internal static class QueryStateArgs
{
    internal static string Queries(string args) => JsonHelper.ExtractString(args, "queries");
}
internal static class RecompileArgs
{
}
internal static class ReconnectUnityArgs
{
    internal static int Port(string args) => JsonHelper.ExtractInt(args, "port");
    internal static string Ctx(string args) => JsonHelper.ExtractString(args, "ctx");
}
internal static class ReferencesArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool Children(string args) => JsonHelper.ExtractString(args, "children") == "true";
    internal static int Depth(string args) => JsonHelper.ExtractInt(args, "depth");
    internal static string Source(string args) => JsonHelper.ExtractString(args, "source");
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Mappings(string args) => JsonHelper.ExtractString(args, "mappings");
}
internal static class RegionClearArgs
{
    internal static string Vertices(string args) => JsonHelper.ExtractString(args, "vertices");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
    internal static string Filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static int Cap(string args) => JsonHelper.ExtractInt(args, "cap");
}
internal static class ReleaseSmokeArgs
{
}
internal static class RenameObjectArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
}
internal static class RenderAnalyzeArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Detail(string args) => JsonHelper.ExtractString(args, "detail");
    internal static string BaselineId(string args) => JsonHelper.ExtractString(args, "baseline_id");
    internal static int? MaxEvents(string args) => JsonHelper.ParseOptInt(args, "max_events");
}
internal static class ResolveSceneRefsArgs
{
    internal static string Refs(string args) => JsonHelper.ExtractString(args, "refs");
    internal static string Fields(string args) => JsonHelper.ExtractString(args, "fields");
}
internal static class ResolveTestRequestArgs
{
    internal static string RequestId(string args) => JsonHelper.ExtractString(args, "request_id");
}
internal static class ResolveToolSchemaArgs
{
    internal static string Tools(string args) => JsonHelper.ExtractString(args, "tools");
}
internal static class RunPlaytestArgs
{
    internal static string Script(string args) => JsonHelper.ExtractString(args, "script");
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static bool AbortOnFail(string args) => JsonHelper.ExtractString(args, "abort_on_fail") == "true";
    internal static string Defs(string args) => JsonHelper.ExtractString(args, "defs");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool SnapshotOnFailure(string args) => JsonHelper.ExtractString(args, "snapshot_on_failure") == "true";
    internal static bool Fresh(string args) => JsonHelper.ExtractString(args, "fresh") == "true";
    internal static string BeforeHook(string args) => JsonHelper.ExtractString(args, "before_hook");
    internal static string AfterHook(string args) => JsonHelper.ExtractString(args, "after_hook");
    internal static string Format(string args) => JsonHelper.ExtractString(args, "format");
}
internal static class RunPlaytestSuiteArgs
{
    internal static string Pattern(string args) => JsonHelper.ExtractString(args, "pattern");
    internal static string SuitePath(string args) => JsonHelper.ExtractString(args, "suite_path");
    internal static float TimeoutPerTest(string args) => JsonHelper.ExtractFloat(args, "timeout_per_test");
    internal static bool StopOnFail(string args) => JsonHelper.ExtractString(args, "stop_on_fail") == "true";
    internal static bool StopAfter(string args) => JsonHelper.ExtractString(args, "stop_after") == "true";
    internal static bool AutoPlay(string args) => JsonHelper.ExtractString(args, "auto_play") == "true";
    internal static bool RestartBetween(string args) => JsonHelper.ExtractString(args, "restart_between") == "true";
    internal static float SuiteTimeout(string args) => JsonHelper.ExtractFloat(args, "suite_timeout");
    internal static string Tag(string args) => JsonHelper.ExtractString(args, "tag");
}
internal static class RunTestsArgs
{
    internal static string Mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static string Filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static string RequestId(string args) => JsonHelper.ExtractString(args, "request_id");
    internal static string Assemblies(string args) => JsonHelper.ExtractString(args, "assemblies");
    internal static string Categories(string args) => JsonHelper.ExtractString(args, "categories");
    internal static string Group(string args) => JsonHelper.ExtractString(args, "group");
    internal static string Tests(string args) => JsonHelper.ExtractString(args, "tests");
}
internal static class RunTestsWaitArgs
{
    internal static string Mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static string Filter(string args) => JsonHelper.ExtractString(args, "filter");
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static float PollInterval(string args) => JsonHelper.ExtractFloat(args, "poll_interval");
    internal static string RequestId(string args) => JsonHelper.ExtractString(args, "request_id");
}
internal static class RuntimeSnapshotArgs
{
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static bool Compress(string args) => JsonHelper.ExtractString(args, "compress") == "true";
}
internal static class SaveSessionArgs
{
}
internal static class SaveSkillArgs
{
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Description(string args) => JsonHelper.ExtractString(args, "description");
    internal static string Code(string args) => JsonHelper.ExtractString(args, "code");
}
internal static class SaveTemplateArgs
{
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Code(string args) => JsonHelper.ExtractString(args, "code");
}
internal static class ScanSceneArgs
{
}
internal static class SceneArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Scene(string args) => JsonHelper.ExtractString(args, "scene");
    internal static bool IncludeUnsaved(string args) => JsonHelper.ExtractString(args, "include_unsaved") == "true";
}
internal static class SceneChangePlanArgs
{
    internal static string Goal(string args) => JsonHelper.ExtractString(args, "goal");
    internal static string Targets(string args) => JsonHelper.ExtractString(args, "targets");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class SceneDiffArgs
{
}
internal static class SceneEnvironmentArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
}
internal static class SceneHealthArgs
{
    internal static string Focus(string args) => JsonHelper.ExtractString(args, "focus");
}
internal static class ScreenshotArgs
{
    internal static int Width(string args) => JsonHelper.ExtractInt(args, "width");
    internal static int Height(string args) => JsonHelper.ExtractInt(args, "height");
    internal static string Camera(string args) => JsonHelper.ExtractString(args, "camera");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string OutputPath(string args) => JsonHelper.ExtractString(args, "output_path");
    internal static float? Zoom(string args) => JsonHelper.ParseOptFloat(args, "zoom");
    internal static string Angles(string args) => JsonHelper.ExtractString(args, "angles");
    internal static int? Supersample(string args) => JsonHelper.ParseOptInt(args, "supersample");
    internal static string Offset(string args) => JsonHelper.ExtractString(args, "offset");
    internal static float? FixedSize(string args) => JsonHelper.ParseOptFloat(args, "fixed_size");
    internal static string Highlight(string args) => JsonHelper.ExtractString(args, "highlight");
    internal static bool? ShowColliders(string args) => JsonHelper.ParseOptBool(args, "show_colliders");
    internal static string Angle(string args) => JsonHelper.ExtractString(args, "angle");
    internal static string AnnotationId(string args) => JsonHelper.ExtractString(args, "annotation_id");
}
internal static class ScreenshotBaselineArgs
{
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static int Width(string args) => JsonHelper.ExtractInt(args, "width");
    internal static int Height(string args) => JsonHelper.ExtractInt(args, "height");
    internal static string Camera(string args) => JsonHelper.ExtractString(args, "camera");
}
internal static class ScreenshotCompareArgs
{
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static int Width(string args) => JsonHelper.ExtractInt(args, "width");
    internal static int Height(string args) => JsonHelper.ExtractInt(args, "height");
    internal static string Camera(string args) => JsonHelper.ExtractString(args, "camera");
    internal static string Mode(string args) => JsonHelper.ExtractString(args, "mode");
    internal static string Question(string args) => JsonHelper.ExtractString(args, "question");
}
internal static class ScriptableObjectArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string Fields(string args) => JsonHelper.ExtractString(args, "fields");
    internal static string Filter(string args) => JsonHelper.ExtractString(args, "filter");
}
internal static class SearchSceneArgs
{
    internal static string Query(string args) => JsonHelper.ExtractString(args, "query");
    internal static string Root(string args) => JsonHelper.ExtractString(args, "root");
    internal static int Limit(string args) => JsonHelper.ExtractInt(args, "limit");
    internal static string Scene(string args) => JsonHelper.ExtractString(args, "scene");
}
internal static class SerializedFieldRenameAuditArgs
{
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string OldField(string args) => JsonHelper.ExtractString(args, "old_field");
    internal static string NewField(string args) => JsonHelper.ExtractString(args, "new_field");
    internal static string Include(string args) => JsonHelper.ExtractString(args, "include");
}
internal static class SetActiveArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool Active(string args) => JsonHelper.ExtractString(args, "active") == "true";
}
internal static class SetLlmConfigArgs
{
    internal static string Config(string args) => JsonHelper.ExtractString(args, "config");
}
internal static class SetMaterialArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Color(string args) => JsonHelper.ExtractString(args, "color");
    internal static string Shader(string args) => JsonHelper.ExtractString(args, "shader");
}
internal static class SetParentArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static bool WorldPositionStays(string args) => JsonHelper.ExtractString(args, "world_position_stays") == "true";
}
internal static class SetPropertiesArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Props(string args) => JsonHelper.ExtractString(args, "props");
}
internal static class SetPropertyArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
    internal static string FindType(string args) => JsonHelper.ExtractString(args, "find_type");
}
internal static class SetPropertyDeltaArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Delta(string args) => JsonHelper.ExtractString(args, "delta");
}
internal static class SetRectArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Anchor(string args) => JsonHelper.ExtractString(args, "anchor");
    internal static string Pos(string args) => JsonHelper.ExtractString(args, "pos");
    internal static string Size(string args) => JsonHelper.ExtractString(args, "size");
    internal static string Pivot(string args) => JsonHelper.ExtractString(args, "pivot");
    internal static string OffsetMin(string args) => JsonHelper.ExtractString(args, "offset_min");
    internal static string OffsetMax(string args) => JsonHelper.ExtractString(args, "offset_max");
    internal static string Pos3(string args) => JsonHelper.ExtractString(args, "pos3");
}
internal static class SetSiblingIndexArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static int Index(string args) => JsonHelper.ExtractInt(args, "index");
}
internal static class SetupObjectsArgs
{
    internal static string Specs(string args) => JsonHelper.ExtractString(args, "specs");
}
internal static class ShaderArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Preset(string args) => JsonHelper.ExtractString(args, "preset");
    internal static string Code(string args) => JsonHelper.ExtractString(args, "code");
    internal static string ShaderName(string args) => JsonHelper.ExtractString(args, "shader_name");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string Keyword(string args) => JsonHelper.ExtractString(args, "keyword");
    internal static string Enabled(string args) => JsonHelper.ExtractString(args, "enabled");
    internal static string NodeType(string args) => JsonHelper.ExtractString(args, "node_type");
    internal static string NodeId(string args) => JsonHelper.ExtractString(args, "node_id");
    internal static string NodeAction(string args) => JsonHelper.ExtractString(args, "node_action");
    internal static string OutputNode(string args) => JsonHelper.ExtractString(args, "output_node");
    internal static int? OutputSlot(string args) => JsonHelper.ParseOptInt(args, "output_slot");
    internal static string InputNode(string args) => JsonHelper.ExtractString(args, "input_node");
    internal static int? InputSlot(string args) => JsonHelper.ParseOptInt(args, "input_slot");
    internal static string EdgeAction(string args) => JsonHelper.ExtractString(args, "edge_action");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Type(string args) => JsonHelper.ExtractString(args, "type");
    internal static string DefaultValue(string args) => JsonHelper.ExtractString(args, "default_value");
    internal static string ReferenceName(string args) => JsonHelper.ExtractString(args, "reference_name");
    internal static string NewName(string args) => JsonHelper.ExtractString(args, "new_name");
    internal static string Layout(string args) => JsonHelper.ExtractString(args, "layout");
    internal static float? HGap(string args) => JsonHelper.ParseOptFloat(args, "h_gap");
    internal static float? VGap(string args) => JsonHelper.ParseOptFloat(args, "v_gap");
}
internal static class SmartBuildArgs
{
    internal static string Description(string args) => JsonHelper.ExtractString(args, "description");
    internal static string Ctx(string args) => JsonHelper.ExtractString(args, "ctx");
}
internal static class SnapshotArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Label(string args) => JsonHelper.ExtractString(args, "label");
    internal static string Compare(string args) => JsonHelper.ExtractString(args, "compare");
}
internal static class SourcePatchWriteArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Content(string args) => JsonHelper.ExtractString(args, "content");
}
internal static class SpatialQueryArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static float? Distance(string args) => JsonHelper.ParseOptFloat(args, "distance");
    internal static float? Radius(string args) => JsonHelper.ParseOptFloat(args, "radius");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static float? CellSize(string args) => JsonHelper.ParseOptFloat(args, "cell_size");
    internal static string LayerMask(string args) => JsonHelper.ExtractString(args, "layer_mask");
    internal static string Center(string args) => JsonHelper.ExtractString(args, "center");
    internal static string Vertices(string args) => JsonHelper.ExtractString(args, "vertices");
    internal static string RegionId(string args) => JsonHelper.ExtractString(args, "region_id");
    internal static int? Cap(string args) => JsonHelper.ParseOptInt(args, "cap");
}
internal static class StartPlaytestArgs
{
    internal static string Script(string args) => JsonHelper.ExtractString(args, "script");
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static bool AbortOnFail(string args) => JsonHelper.ExtractString(args, "abort_on_fail") == "true";
    internal static string Defs(string args) => JsonHelper.ExtractString(args, "defs");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static bool SnapshotOnFailure(string args) => JsonHelper.ExtractString(args, "snapshot_on_failure") == "true";
    internal static bool Fresh(string args) => JsonHelper.ExtractString(args, "fresh") == "true";
    internal static string BeforeHook(string args) => JsonHelper.ExtractString(args, "before_hook");
    internal static string AfterHook(string args) => JsonHelper.ExtractString(args, "after_hook");
    internal static string Format(string args) => JsonHelper.ExtractString(args, "format");
}
internal static class SyncPlaytestAliasesFromDefsArgs
{
    internal static string Defs(string args) => JsonHelper.ExtractString(args, "defs");
    internal static string Asset(string args) => JsonHelper.ExtractString(args, "asset");
}
internal static class SyncUnityArgs
{
    internal static bool Resolve(string args) => JsonHelper.ExtractString(args, "resolve") == "true";
    internal static bool Bump(string args) => JsonHelper.ExtractString(args, "bump") == "true";
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
}
internal static class TestStepArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Position(string args) => JsonHelper.ExtractString(args, "position");
    internal static string ChecksBefore(string args) => JsonHelper.ExtractString(args, "checks_before");
    internal static string ChecksAfter(string args) => JsonHelper.ExtractString(args, "checks_after");
    internal static float WaitAfter(string args) => JsonHelper.ExtractFloat(args, "wait_after");
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
}
internal static class TimelineArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Track(string args) => JsonHelper.ExtractString(args, "track");
    internal static string TrackType(string args) => JsonHelper.ExtractString(args, "track_type");
    internal static string Clip(string args) => JsonHelper.ExtractString(args, "clip");
    internal static string Binding(string args) => JsonHelper.ExtractString(args, "binding");
    internal static float? Start(string args) => JsonHelper.ParseOptFloat(args, "start");
    internal static float? Duration(string args) => JsonHelper.ParseOptFloat(args, "duration");
    internal static float? BlendIn(string args) => JsonHelper.ParseOptFloat(args, "blend_in");
    internal static float? BlendOut(string args) => JsonHelper.ParseOptFloat(args, "blend_out");
    internal static string AssetPath(string args) => JsonHelper.ExtractString(args, "asset_path");
    internal static string DirectorPath(string args) => JsonHelper.ExtractString(args, "director_path");
    internal static string Tracks(string args) => JsonHelper.ExtractString(args, "tracks");
    internal static float? Time(string args) => JsonHelper.ParseOptFloat(args, "time");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static float? ClipIn(string args) => JsonHelper.ParseOptFloat(args, "clip_in");
    internal static int? Index(string args) => JsonHelper.ParseOptInt(args, "index");
    internal static float? Offset(string args) => JsonHelper.ParseOptFloat(args, "offset");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
}
internal static class TransferObjectArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string TargetScene(string args) => JsonHelper.ExtractString(args, "target_scene");
    internal static string Parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static bool WorldPositionStays(string args) => JsonHelper.ExtractString(args, "world_position_stays") == "true";
}
internal static class UiIntentArgs
{
    internal static string Intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static string Parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string Template(string args) => JsonHelper.ExtractString(args, "template");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class UitkElementArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Ref(string args) => JsonHelper.ExtractString(args, "ref");
    internal static string Selector(string args) => JsonHelper.ExtractString(args, "selector");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string Property(string args) => JsonHelper.ExtractString(args, "property");
    internal static string ClassName(string args) => JsonHelper.ExtractString(args, "class_name");
}
internal static class UitkFileArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string Content(string args) => JsonHelper.ExtractString(args, "content");
    internal static string Selector(string args) => JsonHelper.ExtractString(args, "selector");
    internal static string Attr(string args) => JsonHelper.ExtractString(args, "attr");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static string Class(string args) => JsonHelper.ExtractString(args, "class");
    internal static string Parent(string args) => JsonHelper.ExtractString(args, "parent");
    internal static string Tag(string args) => JsonHelper.ExtractString(args, "tag");
    internal static string Attrs(string args) => JsonHelper.ExtractString(args, "attrs");
    internal static string Prop(string args) => JsonHelper.ExtractString(args, "prop");
}
internal static class UitkIntentArgs
{
    internal static string Intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string AttachTo(string args) => JsonHelper.ExtractString(args, "attach_to");
    internal static string Template(string args) => JsonHelper.ExtractString(args, "template");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class UndoLastArgs
{
    internal static int Turns(string args) => JsonHelper.ExtractInt(args, "turns");
}
internal static class UnwireEventArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Event(string args) => JsonHelper.ExtractString(args, "event");
    internal static int? Index(string args) => JsonHelper.ParseOptInt(args, "index");
}
internal static class UseSkillArgs
{
    internal static string Name(string args) => JsonHelper.ExtractString(args, "name");
    internal static string Params(string args) => JsonHelper.ExtractString(args, "params");
}
internal static class ValidatePlaytestAliasesArgs
{
    internal static string Defs(string args) => JsonHelper.ExtractString(args, "defs");
    internal static string Asset(string args) => JsonHelper.ExtractString(args, "asset");
}
internal static class ValidateReferencesArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static int Depth(string args) => JsonHelper.ExtractInt(args, "depth");
    internal static bool Verbose(string args) => JsonHelper.ExtractString(args, "verbose") == "true";
    internal static bool IgnoreOptional(string args) => JsonHelper.ExtractString(args, "ignore_optional") == "true";
}
internal static class ValidateTriggersArgs
{
    internal static string Root(string args) => JsonHelper.ExtractString(args, "root");
    internal static float MinDistance(string args) => JsonHelper.ExtractFloat(args, "min_distance");
}
internal static class VerifyAfterChangeArgs
{
    internal static string ChangedFiles(string args) => JsonHelper.ExtractString(args, "changed_files");
    internal static string TestFilter(string args) => JsonHelper.ExtractString(args, "test_filter");
    internal static string RunTestsMode(string args) => JsonHelper.ExtractString(args, "run_tests_mode");
    internal static string Playtests(string args) => JsonHelper.ExtractString(args, "playtests");
    internal static string MarkId(string args) => JsonHelper.ExtractString(args, "mark_id");
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static bool AutoPlay(string args) => JsonHelper.ExtractString(args, "auto_play") == "true";
    internal static bool RestartBetween(string args) => JsonHelper.ExtractString(args, "restart_between") == "true";
    internal static float SuiteTimeout(string args) => JsonHelper.ExtractFloat(args, "suite_timeout");
}
internal static class VfxIntentArgs
{
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Intent(string args) => JsonHelper.ExtractString(args, "intent");
    internal static string Kind(string args) => JsonHelper.ExtractString(args, "kind");
    internal static bool DryRun(string args) => JsonHelper.ExtractString(args, "dry_run") == "true";
}
internal static class WaitUntilArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Field(string args) => JsonHelper.ExtractString(args, "field");
    internal static string Value(string args) => JsonHelper.ExtractString(args, "value");
    internal static float Timeout(string args) => JsonHelper.ExtractFloat(args, "timeout");
    internal static bool Negate(string args) => JsonHelper.ExtractString(args, "negate") == "true";
    internal static bool AbortOnFail(string args) => JsonHelper.ExtractString(args, "abort_on_fail") == "true";
}
internal static class WatchArgs
{
    internal static string Action(string args) => JsonHelper.ExtractString(args, "action");
    internal static string WatchId(string args) => JsonHelper.ExtractString(args, "watch_id");
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Field(string args) => JsonHelper.ExtractString(args, "field");
    internal static string Condition(string args) => JsonHelper.ExtractString(args, "condition");
    internal static string TriggerAction(string args) => JsonHelper.ExtractString(args, "trigger_action");
    internal static int IntervalMs(string args) => JsonHelper.ExtractInt(args, "interval_ms");
}
internal static class WireEventArgs
{
    internal static string Path(string args) => JsonHelper.ExtractString(args, "path");
    internal static string Component(string args) => JsonHelper.ExtractString(args, "component");
    internal static string Event(string args) => JsonHelper.ExtractString(args, "event");
    internal static string Target(string args) => JsonHelper.ExtractString(args, "target");
    internal static string Method(string args) => JsonHelper.ExtractString(args, "method");
    internal static string ArgType(string args) => JsonHelper.ExtractString(args, "arg_type");
    internal static string ArgValue(string args) => JsonHelper.ExtractString(args, "arg_value");
    internal static string TargetComponentType(string args) => JsonHelper.ExtractString(args, "target_component_type");
    internal static string ParameterTypes(string args) => JsonHelper.ExtractString(args, "parameter_types");
}
}
