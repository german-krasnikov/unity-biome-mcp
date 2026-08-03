using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Orchestrates profiling session lifecycle. Subscribes to EditorApplication.update.
    /// Supports: burst (auto-stop), manual (explicit stop), triggered (on spike).
    /// LRU eviction at 10 sessions.
    /// </summary>
    internal static class ProfileRecorder
    {
        // Injected in tests to replace real Unity API
        internal static Func<FrameSample> _frameProvider = ProfilerBridge.CollectFrame;
        internal static Func<float> _realtime = () => Time.realtimeSinceStartup;

        private static readonly FrameRingBuffer _buffer = new(600);
        private static RecordState _state = RecordState.Idle;
        private static RecordMode _mode;
        private static float _startTime, _duration;
        private static string _activeSessionId;
        private static int _sessionCounter;
        private static readonly Dictionary<string, ProfileSession> _sessions = new();

        [InitializeOnLoadMethod]
        static void Init()
        {
            // Only register cleanup hook — Tick is subscribed only during active recording
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
        }

        internal static string Dispatch(string action, string args) => action switch
        {
            "start" => Start(args),
            "stop" => Stop(),
            "status" => Status(),
            "analyze" => Analyze(args),
            "compare" => Compare(args),
            "list_sessions" => ListSessions(),
            _ => throw new ArgumentException($"Unknown profile action: {action}"),
        };

        // Called by tests via SimulateTick() to advance the recording without Unity's update loop
        internal static void SimulateTick() => Tick();

        // Resets all static state for test isolation
        internal static void Reset()
        {
            EditorApplication.update -= Tick;
            ProfilerBridge.Shutdown();
            _buffer.Clear();
            _state = RecordState.Idle;
            _mode = default;
            _activeSessionId = null;
            _sessionCounter = 0;
            _sessions.Clear();
            _startTime = 0f;
            _duration = 0f;
        }

        private static void Tick()
        {
            if (_state != RecordState.Recording) return;
            _buffer.Add(_frameProvider());
            if (_mode == RecordMode.Burst && _realtime() - _startTime >= _duration)
                FinalizeSession();
        }

        // NOTE: All session data is lost on domain reload by design —
        // ProfilerRecorder handles can't survive an assembly reload.
        private static void OnBeforeReload()
        {
            if (_state == RecordState.Recording)
                FinalizeSession(); // FinalizeSession calls ProfilerBridge.Shutdown()
            else
                ProfilerBridge.Shutdown(); // Dispose recorder even if not recording
            Debug.LogWarning("[MCP] Profile sessions cleared (domain reload)");
        }

        private static string Start(string args)
        {
            if (_state == RecordState.Recording)
                return $"error: already recording session {_activeSessionId}";

            var modeStr = JsonHelper.ExtractString(args, "mode") ?? "burst";
            _mode = modeStr.ToLower() switch
            {
                "manual" => RecordMode.Manual,
                "triggered" => RecordMode.Triggered,
                _ => RecordMode.Burst,
            };

            if (_mode == RecordMode.Triggered)
                return "err:triggered mode not yet implemented";

            var durStr = JsonHelper.ExtractString(args, "duration");
            _duration = 5f;
            if (durStr != null && !float.TryParse(durStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _duration))
                return $"error: invalid duration '{durStr}'";

            _buffer.Clear();
            _startTime = _realtime();
            _state = RecordState.Recording;
            _activeSessionId = "p" + ++_sessionCounter;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            return $"started session:{_activeSessionId} duration={_duration}s mode={_mode.ToString().ToLower()}";
        }

        private static string Stop()
        {
            if (_state != RecordState.Recording)
                return "error: not recording";
            string sid = _activeSessionId;
            FinalizeSession();
            return ProfileFormatter.FormatSummary(sid, _sessions[sid].Stats);
        }

        private static string Status()
        {
            if (_state == RecordState.Idle)
                return $"idle sessions={_sessions.Count}";
            float elapsed = _realtime() - _startTime;
            return ProfileFormatter.FormatStatus(_activeSessionId, _buffer.Count, elapsed,
                _mode == RecordMode.Burst ? _duration : 0f);
        }

        private static string Analyze(string args)
        {
            string sid = JsonHelper.ExtractString(args, "session");
            if (sid == null || !_sessions.TryGetValue(sid, out var session))
                return $"error: session not found: {sid}";

            string focus = JsonHelper.ExtractString(args, "focus");
            string full = ProfileFormatter.FormatSummary(sid, session.Stats);
            if (focus == null) return full;

            // Filter output lines by focus keyword; memory maps to "mem"+"gc"
            var lines = full.Split('\n');
            var sb = new StringBuilder(lines[0].TrimEnd());
            string fl = focus.ToLower();
            for (int i = 1; i < lines.Length; i++)
            {
                string lc = lines[i].ToLower();
                bool match = fl == "memory"
                    ? lc.StartsWith("mem") || lc.StartsWith("gc")
                    : lc.Contains(fl);
                if (match) sb.Append('\n').Append(lines[i].TrimEnd());
            }
            return sb.ToString();
        }

        private static string Compare(string args)
        {
            string sidA = JsonHelper.ExtractString(args, "compare_with");
            string sidB = JsonHelper.ExtractString(args, "session");

            if (sidA == null || !_sessions.TryGetValue(sidA, out var sessA))
                return $"error: session not found: {sidA}";
            if (sidB == null || !_sessions.TryGetValue(sidB, out var sessB))
                return $"error: session not found: {sidB}";

            var delta = ProfileAnalyzer.Compare(sessA.Stats, sessB.Stats);
            return ProfileFormatter.FormatCompare(sidA, sidB, sessA.Stats, sessB.Stats, delta);
        }

        private static string ListSessions()
        {
            if (_sessions.Count == 0) return "no sessions";
            return ProfileFormatter.FormatList(_sessions.Values.OrderBy(s => s.Timestamp));
        }

        private static void FinalizeSession()
        {
            EditorApplication.update -= Tick;
            ProfilerBridge.Shutdown();
            var stats = ProfileAnalyzer.Compute(_buffer.ToArray());
            if (_sessions.Count >= 10) EvictOldest();
            _sessions[_activeSessionId] = new ProfileSession
            {
                Id = _activeSessionId,
                Mode = _mode,
                Timestamp = DateTime.Now,
                Stats = stats,
            };
            _state = RecordState.Idle;
        }

        private static void EvictOldest()
        {
            string oldestKey = null;
            DateTime oldest = DateTime.MaxValue;
            foreach (var kv in _sessions)
                if (kv.Value.Timestamp < oldest) { oldest = kv.Value.Timestamp; oldestKey = kv.Key; }
            if (oldestKey != null) _sessions.Remove(oldestKey);
        }
    }
}
