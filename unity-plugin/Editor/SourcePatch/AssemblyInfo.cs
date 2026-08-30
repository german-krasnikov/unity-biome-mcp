using System.Runtime.CompilerServices;

// P0-40: only the current real consumer gets friend access. UnityMCP.Editor
// (the future SourcePatchHost, P0-50) is not added until it actually
// references this assembly — see AI note in
// Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §3.1.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
