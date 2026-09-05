namespace UnityMCP.Playtest.Core
{
    // D06 blocker fix (pulled forward from D09's design, ahead of its originally-scheduled
    // slot): ResolveQuery's `PlaytestConfig` parameter is an Editor-only ScriptableObject
    // dependency that would stop the Core relocation (D06) compiling standalone. Decoupled via
    // this interface instead — see PlaytestConfig.cs (implicit implementation) and
    // PlaytestParser.Subroutines.cs's ResolveQuery. Namespace stays UnityMCP.Editor for now,
    // matching Float3/NumericParsing's "temporary, D07 renames every Core type" convention.

    /// <summary>A single resolved alias: physical path + component + field. Engine-free bare
    /// data (no UnityEngine/UnityEditor dependency) so Core (noEngineReferences) can hold it.</summary>
    public readonly struct AliasMatch
    {
        public readonly string Path;
        public readonly string Component;
        public readonly string Field;

        public AliasMatch(string path, string component, string field)
        {
            Path = path;
            Component = component;
            Field = field;
        }
    }

    /// <summary>Anything that can resolve a DSL alias name to a path/component/field triple.
    /// PlaytestConfig (a ScriptableObject) implements this implicitly — the only production
    /// implementor and the only call site (PlaytestParser.ResolveQuery), per D09's own
    /// verified 1-caller/1-implementor SRP trade.</summary>
    public interface IAliasSource
    {
        AliasMatch? FindAlias(string name);
    }
}
