// Single source of truth for the per-project MCP config file targets — avoids repeating
// paths/root-keys across the orchestrator. See Plans/Install/11-phase1a-design.md.
// The rows (and the ProjectConfigTarget type itself) are generated from
// dsl/data/projectconfig.data-contract (T1/Q10) — see Editor/Generated/ProjectConfigTargets.g.cs.
// Junie's root key is assumed "mcpServers" (no local grounding in this repo's existing config
// code) — flagged in 11-phase1a-design.md Risks; if wrong, only the contract's row changes.
namespace UnityMCP.Editor.Wizard
{
    internal static class ProjectConfigTargets
    {
        internal static readonly ProjectConfigTarget[] All = ProjectConfigTargetRows.All;

        // Single source of truth for BackendDescriptor.Key → per-project relative path —
        // used by ConfigureScreen so the UI module doesn't keep its own duplicate map.
        internal static string RelativePathFor(string key)
        {
            foreach (var t in All)
                if (t.key == key) return t.relativePath;
            return null;
        }
    }
}
