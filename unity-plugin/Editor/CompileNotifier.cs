using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class CompileNotifier
    {
        private const string StartKey    = "MCP_CompileStart";
        private const string DurationKey = "MCP_LastDuration";
        private const string FailedKey   = "MCP_CompileFailed";

        // G14: staleness ceiling — if IsCompiling stays latched past this threshold with
        // no compilationFinished event, GetStatus() emits "idle-stale" to surface the wedge.
        // 300s (5 min) is conservative: real compiles never take that long.
        public const float StaleCeilingSeconds = 300f;

        // Injectable clock seam for unit tests (avoids dependency on EditorApplication.timeSinceStartup).
        internal static Func<float> NowSecondsFloat = () => (float)EditorApplication.timeSinceStartup;

        static CompileNotifier()
        {
            CompilationPipeline.compilationStarted += _ =>
            {
                SessionState.SetFloat(StartKey, NowSecondsFloat());
                SessionState.SetBool(FailedKey, false);
            };

            CompilationPipeline.compilationFinished += _ =>
            {
                var start = SessionState.GetFloat(StartKey, 0f);
                if (start > 0f)
                    SessionState.SetFloat(DurationKey, NowSecondsFloat() - start);
                SessionState.SetFloat(StartKey, 0f);
                // Discriminate failed vs success (ref §9: compilationFinished fires on FAIL too)
                if (EditorUtility.scriptCompilationFailed)
                    SessionState.SetBool(FailedKey, true);
            };
        }

        public static bool IsCompiling => SessionState.GetFloat(StartKey, 0f) > 0f;

        public static float ElapsedSeconds => IsCompiling
            ? NowSecondsFloat() - SessionState.GetFloat(StartKey, 0f)
            : 0f;

        public static float LastDurationSeconds => SessionState.GetFloat(DurationKey, 0f);

        /// <summary>Clear the stale FailedKey flag (self-heal after Bee cache-hit).</summary>
        public static void ClearFailed() => SessionState.SetBool(FailedKey, false);

        public static string GetStatus()
        {
            if (IsCompiling)
            {
                var elapsed = ElapsedSeconds;
                // G14: latched-isCompiling ceiling — after StaleCeilingSeconds with no
                // compilationFinished event, override with "idle-stale" so the wedge surfaces.
                if (elapsed > StaleCeilingSeconds)
                    return "idle-stale|" + elapsed.ToString("F1", CultureInfo.InvariantCulture);
                return "compiling|" + elapsed.ToString("F1", CultureInfo.InvariantCulture);
            }
            var last = LastDurationSeconds;
            var durStr = last > 0f ? last.ToString("F1", CultureInfo.InvariantCulture) : "0";
            // Add fail discriminator so callers can distinguish failed-idle from success-idle
            if (SessionState.GetBool(FailedKey, false))
                return "idle-failed|" + durStr;
            // C6: distinguish never-compiled from clean-idle.
            // last==0 AND StartKey==0 AND FailedKey==false → compilation has never run this session.
            // Python callers must treat "idle-never" as non-clean (Track P P4).
            if (last <= 0f && SessionState.GetFloat(StartKey, 0f) <= 0f)
                return "idle-never|0";
            return "idle|" + durStr;
        }
    }
}
