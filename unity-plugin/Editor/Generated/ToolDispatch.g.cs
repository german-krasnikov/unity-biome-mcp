// Generated from dsl/tools.biome
// source-sha256=fdb456e124945afeb73e1ecd54f2c1f941271dd9b786628523f0e60d600d24dd
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
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
}
