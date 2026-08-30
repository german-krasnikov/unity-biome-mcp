using System;

namespace UnityMCP.Editor
{
    /// <summary>
    /// The one main-assembly integration seam for `.cs` source writes (§3.1/§6
    /// P0-50 in Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md).
    ///
    /// <see cref="GuardLegacyCsWrite"/> is called from
    /// <c>AssetDatabaseHelper.WriteText</c>, which both raw direct "asset"
    /// dispatch and batch dispatch reach through the identical
    /// <c>CommandRouter.ExecuteCommand</c> call — so this one call site
    /// authoritatively covers both surfaces; there is nothing batch-side left
    /// to duplicate.
    ///
    /// <see cref="CurrentState"/> is a settable seam, not a live coordinator:
    /// no code path exists yet that can move it away from
    /// <see cref="SourcePatch.SourcePatchState.Unavailable"/> (no provider
    /// registration/P0-60, no mutation_mode command/P0-70), so production
    /// behavior today is always OFF-equivalent. Tests set it directly to
    /// exercise every state. P0-70 replaces this field with delegation to a
    /// real <c>SourcePatchCoordinator</c> without touching either call site
    /// below.
    /// </summary>
    internal static class SourcePatchHost
    {
        internal static SourcePatch.SourcePatchState CurrentState { get; set; } =
            SourcePatch.SourcePatchState.Unavailable;

        internal static void ResetForTests() =>
            CurrentState = SourcePatch.SourcePatchState.Unavailable;

        private static bool AllowsLegacyRoute =>
            CurrentState == SourcePatch.SourcePatchState.Unavailable ||
            CurrentState == SourcePatch.SourcePatchState.Off;

        private static bool IsCsPath(string assetPath) =>
            assetPath != null && assetPath.EndsWith(".cs", StringComparison.Ordinal);

        /// <summary>
        /// Called by AssetDatabaseHelper.WriteText before any file/import
        /// effect. Off/Unavailable: no-op, legacy owns `.cs` like any other
        /// asset. Anything else: reject — raw/batch `.cs` writes must go
        /// through source_patch_write instead.
        /// </summary>
        internal static void GuardLegacyCsWrite(string assetPath)
        {
            if (!IsCsPath(assetPath) || AllowsLegacyRoute) return;
            throw new InvalidOperationException(
                $"state={CurrentState}: source patch active — legacy .cs write rejected pre-effect");
        }

        /// <summary>
        /// source_patch_write command body (P0-50 scope only). Off/Unavailable:
        /// delegate exactly once to the legacy writer — byte-identical to the
        /// pre-existing "asset" write_text route. Anything else: typed
        /// rejection; P0-60/P0-70 land the real apply path, never a silent
        /// legacy fallback (§3.2: never probes by writing first).
        /// </summary>
        internal static string WriteText(string argsJson)
        {
            if (AllowsLegacyRoute) return AssetDatabaseHelper.Execute("write_text", argsJson);
            throw new InvalidOperationException(
                $"state={CurrentState}: source patch active — legacy .cs write rejected pre-effect");
        }
    }
}
