using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    internal static class PlaytestMonitorRegistry
    {
        static Dictionary<string, Type> _types;
        static readonly List<IPlaytestMonitor> _active = new();

        static void EnsureLoaded()
        {
            if (_types != null) return;
            _types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            var iface = typeof(IPlaytestMonitor);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.IsAbstract || t.IsInterface || !iface.IsAssignableFrom(t)) continue;
                    try
                    {
                        var inst = (IPlaytestMonitor)Activator.CreateInstance(t);
                        _types[inst.Name] = t;
                    }
                    catch { /* skip */ }
                }
            }
        }

        /// <summary>Start a named monitor. Returns error string if not found.</summary>
        public static string Start(string name)
        {
            EnsureLoaded();
            if (!_types.TryGetValue(name, out var type))
                return $"Monitor not found: '{name}'. Available: {string.Join(", ", _types.Keys)}";
            var monitor = (IPlaytestMonitor)Activator.CreateInstance(type);
            monitor.Start();
            _active.Add(monitor);
            return $"MONITOR {name} started";
        }

        public static void StopAll()
        {
            foreach (var m in _active) try { m.Stop(); } catch { /* ignore */ }
            _active.Clear();
        }

        public static string BuildReport()
        {
            if (_active.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            foreach (var m in _active) sb.AppendLine(m.Report());
            return sb.ToString().TrimEnd();
        }

        // ── Test seams ────────────────────────────────────────────────────────

        /// <summary>Number of currently active monitors. For unit tests only.</summary>
        internal static int ActiveCount => _active.Count;

        /// <summary>Directly add a monitor without registry lookup. For unit tests only.</summary>
        internal static void InjectForTest(IPlaytestMonitor m) => _active.Add(m);

        // For testing: reset cache
        internal static void Reset() { _types = null; _active.Clear(); }
    }
}
