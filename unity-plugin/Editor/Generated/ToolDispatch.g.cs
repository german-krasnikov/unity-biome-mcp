// Generated from dsl/tools.biome
// source-sha256=385b6968907572e67385e5e7dc2e105b35a7e35bfd730786d74a3fea06784905
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
internal static partial class AssetDatabaseHelper
{
    internal static string Execute(string action, string argsJson)
    {
        switch (action)
        {
            case "find": return Find(argsJson);
            case "get_info": return GetInfo(argsJson);
            case "create": return Create(argsJson);
            case "move": return Move(argsJson);
            case "validate_move": return ValidateMove(argsJson);
            case "duplicate": return Duplicate(argsJson);
            case "delete": return Delete(argsJson);
            case "get_dependencies": return GetDependencies(argsJson);
            case "find_dependents": return FindDependents(argsJson);
            case "import_settings": return ImportSettings(argsJson);
            case "export_package": return ExportPackage(argsJson);
            case "import_package": return ImportPackage(argsJson);
            case "read_text": return ReadText(argsJson);
            case "write_text": return WriteText(argsJson);
            case "reimport": return Reimport(argsJson);
            default:
                throw new System.Exception(ErrorHelper.InvalidAction(action, ToolActions.Names("asset")));
        }
    }
}
internal static partial class MaterialHelper
{
    internal static string Execute(string action, string argsJson)
    {
        switch (action)
        {
            case "create": return Create(argsJson);
            case "get": return Get(argsJson);
            case "set": return Set(argsJson);
            case "copy": return Copy(argsJson);
            case "list_properties": return ListProperties(argsJson);
            case "list_slots": return ListSlots(argsJson);
            case "get_errors": return GetShaderErrors(argsJson);
            case "list_shaders": return ListShaders(argsJson);
            case "set_fields": return SetFields(argsJson);
            default:
                throw new System.ArgumentException(ErrorHelper.InvalidAction(action, ToolActions.Names("material")));
        }
    }
}
internal static partial class PrefabHelper
{
    internal static string Execute(string action, string argsJson)
    {
        switch (action)
        {
            case "save": return Save(argsJson);
            case "create_variant": return CreateVariant(argsJson);
            case "instantiate": return Instantiate(argsJson);
            case "apply": return Apply(argsJson);
            case "revert": return Revert(argsJson);
            case "get_overrides": return GetOverrides(argsJson);
            case "unpack": return Unpack(argsJson);
            case "edit": return Edit(argsJson);
            default:
                throw new System.ArgumentException(ErrorHelper.InvalidAction(action, ToolActions.Names("prefab")));
        }
    }
}
internal static partial class ScriptableObjectHelper
{
    internal static string Execute(string action, string argsJson)
    {
        switch (action)
        {
            case "create": return Create(argsJson);
            case "get": return Get(argsJson);
            case "set": return Set(argsJson);
            case "list_types": return ListTypes(argsJson);
            case "find": return Find(argsJson);
            default:
                throw new System.ArgumentException(ErrorHelper.InvalidAction(action, ToolActions.Names("scriptable_object")));
        }
    }
}
}
