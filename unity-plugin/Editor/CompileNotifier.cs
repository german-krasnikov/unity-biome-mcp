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

        private static TestIsolationScope _activeTestIsolation;

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

        /// <summary>
        /// Preserves the complete mutable notifier state for a test, including whether each
        /// SessionState key existed. Scopes may nest but must unwind in reverse order.
        /// </summary>
        internal static IDisposable BeginTestIsolation()
        {
            var scope = new TestIsolationScope(_activeTestIsolation);
            _activeTestIsolation = scope;
            return scope;
        }

        private sealed class TestIsolationScope : IDisposable
        {
            private readonly TestIsolationScope _previous;
            private readonly Func<float> _clock;
            private readonly FloatSessionValue _start;
            private readonly FloatSessionValue _duration;
            private readonly BoolSessionValue _failed;
            private bool _disposed;

            internal TestIsolationScope(TestIsolationScope previous)
            {
                _previous = previous;
                _clock = NowSecondsFloat;
                _start = FloatSessionValue.Capture(StartKey);
                _duration = FloatSessionValue.Capture(DurationKey);
                _failed = BoolSessionValue.Capture(FailedKey);
            }

            public void Dispose()
            {
                if (_disposed) return;
                if (!ReferenceEquals(_activeTestIsolation, this))
                    throw new InvalidOperationException(
                        "CompileNotifier test-isolation scopes must be disposed in LIFO order.");

                var errors = new System.Collections.Generic.List<Exception>();
                Restore(_failed.Restore, errors);
                Restore(_duration.Restore, errors);
                Restore(_start.Restore, errors);
                Restore(() => NowSecondsFloat = _clock, errors);
                _activeTestIsolation = _previous;
                _disposed = true;

                if (errors.Count > 0)
                    throw new AggregateException(
                        "CompileNotifier test-isolation restoration failed.", errors);
            }

            private static void Restore(
                Action restore,
                System.Collections.Generic.ICollection<Exception> errors)
            {
                try { restore(); }
                catch (Exception error) { errors.Add(error); }
            }
        }

        private readonly struct FloatSessionValue
        {
            private readonly string _key;
            private readonly bool _existed;
            private readonly float _value;

            private FloatSessionValue(string key, bool existed, float value)
            {
                _key = key;
                _existed = existed;
                _value = value;
            }

            internal static FloatSessionValue Capture(string key)
            {
                var first = SessionState.GetFloat(key, -1234567.25f);
                var second = SessionState.GetFloat(key, 7654321.5f);
                return new FloatSessionValue(key, first.Equals(second), first);
            }

            internal void Restore()
            {
                if (_existed) SessionState.SetFloat(_key, _value);
                else SessionState.EraseFloat(_key);
            }
        }

        private readonly struct BoolSessionValue
        {
            private readonly string _key;
            private readonly bool _existed;
            private readonly bool _value;

            private BoolSessionValue(string key, bool existed, bool value)
            {
                _key = key;
                _existed = existed;
                _value = value;
            }

            internal static BoolSessionValue Capture(string key)
            {
                var first = SessionState.GetBool(key, false);
                var second = SessionState.GetBool(key, true);
                return new BoolSessionValue(key, first == second, first);
            }

            internal void Restore()
            {
                if (_existed) SessionState.SetBool(_key, _value);
                else SessionState.EraseBool(_key);
            }
        }
    }
}
