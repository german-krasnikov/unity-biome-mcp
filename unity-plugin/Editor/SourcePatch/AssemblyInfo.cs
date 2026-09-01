using System.Runtime.CompilerServices;

// P0-40: only the current real consumer gets friend access. UnityMCP.Editor
// (the future SourcePatchHost, P0-50) is not added until it actually
// references this assembly — see AI note in
// Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §3.1.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]

// P0-50: SourcePatchHost (the main-assembly integration seam) now references
// this assembly's internal SourcePatchState — see §3.1/§6 P0-50 in the same
// handoff doc. This is the exact, pre-announced minimal expansion the P0-40
// comment above was waiting for.
[assembly: InternalsVisibleTo("UnityMCP.Editor")]
