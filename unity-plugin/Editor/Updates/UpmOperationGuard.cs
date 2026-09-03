using System;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// SessionState-backed mutex serializing UPM <c>Client.Add</c> calls across
    /// LevelUpPanel, rollback, and align — all three funnel through
    /// <c>UpmPluginUpdater.Update()</c> (ARC-10 T3). Zero in-memory fields: every
    /// read/write goes straight to SessionState, so a domain reload (which wipes
    /// static fields but not SessionState) never loses the in-flight claim —
    /// survival is definitional, not something this class has to implement.
    /// </summary>
    internal static class UpmOperationGuard
    {
        private const string InFlightKey = "UnityMCP.UpmOperationGuard.InFlight";
        private const string VersionKey  = "UnityMCP.UpmOperationGuard.Version";
        private const string StartedKey  = "UnityMCP.UpmOperationGuard.StartedAt";

        /// <summary>
        /// A holder that dies without calling <see cref="Complete"/> (e.g. a domain
        /// reload that ate the poll loop mid-<c>Client.Add</c>) must not deadlock
        /// every future update forever. Single-sourced from
        /// <see cref="CompileNotifier.StaleCeilingSeconds"/> (300s) rather than an
        /// independent value: <c>UpmPluginUpdater.Update()</c> chains two sequential
        /// <c>Client.Add</c> calls at 120s each — a 240s legitimate worst case — and a
        /// shorter ceiling would self-heal past a real, still-running update.
        /// </summary>
        public const float StaleCeilingSeconds = CompileNotifier.StaleCeilingSeconds;

        /// <summary>Injectable clock seam for unit tests (mirrors CompileNotifier).</summary>
        internal static Func<float> NowSecondsFloat = () => (float)EditorApplication.timeSinceStartup;

        public static bool IsInFlight => SessionState.GetBool(InFlightKey, false);

        public static string InFlightVersion => SessionState.GetString(VersionKey, "");

        public static float ElapsedSeconds => IsInFlight
            ? NowSecondsFloat() - SessionState.GetFloat(StartedKey, 0f)
            : 0f;

        /// <summary>
        /// True when a claim exists and has not exceeded <see cref="StaleCeilingSeconds"/>.
        /// Call this instead of reading <see cref="IsInFlight"/> from any passive UI
        /// (LevelUpPanel, VersionPickerPage) so a claim orphaned by a reload OTHER than
        /// the update's own version bump (an unrelated script save, entering Play Mode, a
        /// sync_unity recompile) still self-heals on the next rebuild instead of only
        /// clearing on <c>CheckVersionChange</c>'s narrower version-match path (C1 r2 #5).
        /// Past the ceiling this actively clears the claim via <see cref="Complete"/> and
        /// returns false — it does not merely report staleness. A method, not a property,
        /// because reading it can mutate SessionState (CA1024).
        /// </summary>
        public static bool IsActiveOrHeal()
        {
            if (!IsInFlight) return false;
            if (ElapsedSeconds <= StaleCeilingSeconds) return true;
            Complete();
            return false;
        }

        /// <summary>
        /// Claims the guard for <paramref name="version"/>. Returns false when another
        /// operation is already in flight and has not exceeded <see cref="StaleCeilingSeconds"/>;
        /// self-heals past the ceiling so a silently-dead holder doesn't block forever.
        /// </summary>
        public static bool TryBegin(string version)
        {
            if (IsActiveOrHeal())
                return false;

            SessionState.SetBool(InFlightKey, true);
            SessionState.SetString(VersionKey, version);
            SessionState.SetFloat(StartedKey, NowSecondsFloat());
            return true;
        }

        /// <summary>Releases the guard. Safe to call even when nothing is in flight.</summary>
        public static void Complete()
        {
            SessionState.EraseBool(InFlightKey);
            SessionState.EraseString(VersionKey);
            SessionState.EraseFloat(StartedKey);
        }
    }
}
