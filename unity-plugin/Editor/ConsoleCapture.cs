using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    using LogEntry = UnityMCP.Editor.ConsoleRingBuffer.LogEntry;

    // Issue 27 (M14): orchestrates the in-memory ring buffer (ConsoleRingBuffer) and the
    // reload-surviving problem log (ConsoleProblemPersistence) behind one public query API.
    [UnityEditor.InitializeOnLoad]
    public static class ConsoleCapture
    {
        private const int MAX_STACKTRACE_LENGTH = 500;

        // Issue 27 (C1): logs worth surfacing as "a problem happened" — not just LogType.Error.
        // Unhandled C# exceptions arrive as LogType.Exception; failed asserts as LogType.Assert.
        // Mirrors PROBLEM_LEVELS in server/src/unity_mcp/console_levels.py (ROI reliability
        // sprint item m2). Keep both lists in sync manually; no automated cross-language
        // contract test exists yet.
        private static readonly LogType[] ProblemTypes = { LogType.Error, LogType.Exception, LogType.Assert };

        // Issue 27 (C3/Step 4): count of problem-type entries evicted — either by ring overflow
        // (ConsoleRingBuffer.Write) or by ConsoleProblemPersistence's own FIFO cap (M9) —
        // surfaced as an explicit marker instead of silently losing them.
        private static int _droppedProblemCount = 0;

        private static readonly object _lock = new object();

        static ConsoleCapture()
        {
            Application.logMessageReceived += OnLogReceived;
            // G10: hook Unity's built-in Console.Clear so our buffer stays in sync.
            HookUnityConsoleClear();
        }

        // G10: subscribe to Unity's internal console-cleared event via reflection
        // (no public API — event name varies between Editor builds).
        private static void HookUnityConsoleClear()
        {
            try
            {
                var logEntries = typeof(UnityEditor.EditorApplication).Assembly
                    .GetType("UnityEditor.LogEntries");
                if (logEntries == null) return;
                var evt = logEntries.GetEvent("onClearDevelopmentConsole",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (evt == null) return;
                evt.AddEventHandler(null, new Action(OnUnityConsoleClear));
            }
            catch { /* Graceful fallback — event not available in this Editor build */ }
        }

        private static void OnUnityConsoleClear()
        {
            lock (_lock)
            {
                ConsoleRingBuffer.Reset();
                ConsoleProblemPersistence.Clear();
                _droppedProblemCount = 0;
            }
        }

        private static void OnLogReceived(string message, string stackTrace, LogType type) =>
            RecordEntry(message, stackTrace, type, DateTime.Now);

        // Issue B23-Win (Windows CI race): split out so test seams can pin an exact
        // Timestamp instead of racing two independent DateTime.Now calls (see
        // GetErrorsSince_ReturnsError_WhenTimestampExactlyEqualsSince).
        private static void RecordEntry(string message, string stackTrace, LogType type, DateTime timestamp)
        {
            lock (_lock)
            {
                var entry = new LogEntry
                {
                    Message = message,
                    StackTrace = stackTrace != null && stackTrace.Length > MAX_STACKTRACE_LENGTH
                        ? stackTrace.Substring(0, MAX_STACKTRACE_LENGTH)
                        : stackTrace,
                    Type = type,
                    Timestamp = timestamp
                };

                // M9: ConsoleProblemPersistence's own FIFO cap (20) can evict independently of
                // the ring buffer below — track that eviction too, not just the ring's.
                if (Array.IndexOf(ProblemTypes, type) >= 0)
                    if (ConsoleProblemPersistence.Append(entry.Type, entry.Message, entry.Timestamp))
                        _droppedProblemCount++;

                // Issue 27 (Step 4): ring-buffer eviction of a problem-type entry counts too.
                if (ConsoleRingBuffer.Write(entry, out var evicted) &&
                    Array.IndexOf(ProblemTypes, evicted.Type) >= 0)
                    _droppedProblemCount++;
            }
        }

        /// <summary>
        /// Get logs. count=-1 means all.
        /// first>0: return first N entries from init buffer + last (count-first) from ring.
        /// first=0: return last count from combined (init + ring in order).
        /// </summary>
        public static string GetLogs(int count = -1, string level = null, int first = 0,
                                     string keyword = null, bool countOnly = false,
                                     float sinceSeconds = 0)
        {
            lock (_lock)
            {
                // Issue 27 (C2 fix): fallback entries now flow through the SAME level/keyword/
                // count filtering below as live entries — no more bypassing filters on reload.
                var rawCombined = BuildCombinedWithFallback(DateTime.MinValue);

                // S5: filter by time window before any other filtering
                if (sinceSeconds > 0)
                {
                    var cutoff = DateTime.Now.AddSeconds(-sinceSeconds);
                    rawCombined = rawCombined.FindAll(e => e.Timestamp >= cutoff);
                }

                var levelFilter = ConsoleRingBuffer.ParseLevels(level);
                var combined = ConsoleRingBuffer.FilterByTypes(rawCombined, levelFilter);

                List<LogEntry> selected;
                if (first > 0 && count > 0)
                {
                    // first N from init, last (count-first) from ring — filter each independently
                    var initEntries = ConsoleRingBuffer.FilterByTypes(ConsoleRingBuffer.GetInitEntries(first), levelFilter);
                    var ringEntries = ConsoleRingBuffer.FilterByTypes(ConsoleRingBuffer.GetRingEntries(count - first), levelFilter);
                    selected = new List<LogEntry>(initEntries.Count + ringEntries.Count);
                    selected.AddRange(initEntries);
                    selected.AddRange(ringEntries);
                }
                else if (count > 0)
                {
                    int skip = combined.Count > count ? combined.Count - count : 0;
                    selected = combined.GetRange(skip, combined.Count - skip);
                }
                else
                {
                    selected = combined;
                }

                if (!string.IsNullOrEmpty(keyword))
                    selected = ConsoleRingBuffer.FilterByKeyword(selected, keyword);

                if (countOnly)
                    return AppendDroppedSuffix(selected.Count.ToString());

                var sb = new StringBuilder();
                // Single-pass: output each unique run once with a repeat count suffix
                for (int di = 0; di < selected.Count; )
                {
                    var e = selected[di];
                    int run = 1;
                    while (di + run < selected.Count &&
                           selected[di + run].Message == e.Message &&
                           selected[di + run].Type == e.Type) run++;
                    var fileLoc = ConsoleStackParser.ExtractFileLocation(e.StackTrace);
                    string suffix = run > 1 ? $" (x{run})" : "";
                    if (fileLoc != null)
                        sb.AppendFormat("[{0}] {1:HH:mm:ss.fff} {2}{3} @ {4}\n", e.Type, e.Timestamp, e.Message, suffix, fileLoc);
                    else
                        sb.AppendFormat("[{0}] {1:HH:mm:ss.fff} {2}{3}\n", e.Type, e.Timestamp, e.Message, suffix);
                    di += run;
                }
                if (sinceSeconds > 0)
                {
                    var text = sb.ToString().TrimEnd('\n');
                    return _droppedProblemCount > 0
                        ? text + $"\n#MCP_INTERNAL overflow:{_droppedProblemCount}"
                        : text;
                }
                return AppendDroppedSuffix(sb.ToString().TrimEnd('\n'));
            }
        }

        public static string GetErrorsSince(DateTime since, int maxCount = 5)
        {
            lock (_lock)
            {
                // Issue 27 (C1 fix): fallback entries are filtered by `since` too — a reload no
                // longer resurrects every persisted problem regardless of when it happened.
                var combined = BuildCombinedWithFallback(since);
                var sb = new StringBuilder();
                int found = 0;
                foreach (var e in combined)
                {
                    if (found >= maxCount) break;
                    // Issue B23-Win: >= not > — stepStart and a step's own error can share the
                    // exact same DateTime.Now tick on Windows' coarser clock resolution; excluding
                    // ties silently dropped that step's genuine console error (CI-reproducible).
                    if (e.Timestamp >= since && Array.IndexOf(ProblemTypes, e.Type) >= 0)
                    {
                        sb.AppendLine(e.Message);
                        found++;
                    }
                }
                // Issue B23 (MCP-CONSOLE-032 class): GetErrorsSince is a delta/watermark query
                // like GetLogs(sinceSeconds>0) -- it must never manufacture a phantom result
                // from the lifetime-global _droppedProblemCount when this specific since-window
                // matched zero problem entries. Otherwise console pollution from earlier,
                // unrelated tests (overflowing the 20-entry persisted-problem FIFO) makes every
                // later step in a full-suite run report a false CONSOLE_ERR.
                if (found == 0) return null;
                string result = AppendDroppedSuffix(sb.ToString().TrimEnd());
                return string.IsNullOrEmpty(result) ? null : result;
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                ConsoleRingBuffer.Reset();
                ConsoleProblemPersistence.Clear();
                _droppedProblemCount = 0;
            }
        }

        public static void ClearDroppedCount()
        {
            lock (_lock) { _droppedProblemCount = 0; }
        }

        // --- helpers ---

        // Issue 27 (Step 4): explicit marker instead of silently dropping evicted problem entries.
        private static string AppendDroppedSuffix(string text) =>
            _droppedProblemCount > 0 ? text + $"\n[+{_droppedProblemCount} older problem entries dropped]" : text;

        // Issue 27 (C1/C2 fix): when a domain reload wiped the in-memory ring buffer, reconstruct
        // entries from ConsoleProblemPersistence instead of returning raw unfiltered text —
        // callers get the SAME `since`/level/keyword filtering as the live path.
        private static List<LogEntry> BuildCombinedWithFallback(DateTime since)
        {
            var combined = ConsoleRingBuffer.BuildCombined();
            if (combined.Count > 0) return combined;

            var persisted = ConsoleProblemPersistence.GetSince(since);
            var list = new List<LogEntry>(persisted.Count);
            foreach (var p in persisted)
                list.Add(new LogEntry { Message = p.Message, StackTrace = null, Type = p.Type, Timestamp = p.Timestamp });
            return list;
        }

#if UNITY_INCLUDE_TESTS
        internal static void InjectForTest(string message, LogType type, string stackTrace = null)
        {
            OnLogReceived(message, stackTrace, type);
        }

        // Test seam: pins an exact Timestamp instead of DateTime.Now, so boundary races
        // (e.g. Timestamp == since) can be reproduced deterministically.
        internal static void InjectForTestAt(string message, LogType type, DateTime timestamp, string stackTrace = null)
        {
            RecordEntry(message, stackTrace, type, timestamp);
        }

        /// <summary>Test seam: simulate domain reload — wipes in-memory state, leaves
        /// SessionState (already written) untouched. Mirrors CompileErrorCapture.SimulateDomainReload().</summary>
        internal static void SimulateDomainReloadForTest()
        {
            lock (_lock)
            {
                ConsoleRingBuffer.Reset();
                ConsoleProblemPersistence.SimulateDomainReloadForTest();
            }
        }

        /// <summary>Test seam: simulate Unity's built-in Console.Clear (Editor menu / API)
        /// without going through MCP's clear_console command. G10 fix.</summary>
        internal static void SimulateUnityConsoleClearForTest() => OnUnityConsoleClear();
#endif
    }
}
