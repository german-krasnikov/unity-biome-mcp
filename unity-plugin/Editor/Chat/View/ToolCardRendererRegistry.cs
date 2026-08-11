// Extensible registry of IToolCardRenderer instances.
// Dispatch is O(1) via StringComparer.Ordinal dictionary.
// External packages register from [InitializeOnLoad]; fallback = ToolDetailBuilder.
using System;
using System.Collections.Generic;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    public static class ToolCardRendererRegistry
    {
        private static readonly Dictionary<string, IToolCardRenderer> _registry =
            new Dictionary<string, IToolCardRenderer>(StringComparer.Ordinal);
        private static int _version;

        static ToolCardRendererRegistry() { } // domain-reload hook for [InitializeOnLoad]

        public static int Version => _version;

        /// <summary>Register a renderer for toolName. Keep-first on duplicates. Returns false on invalid args.</summary>
        public static bool Register(string name, IToolCardRenderer renderer)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (renderer == null) return false;
            if (_registry.ContainsKey(name)) return false;
            _registry[name] = renderer;
            _version++;
            return true;
        }

        /// <summary>Remove a renderer by tool name. Returns false if not found.</summary>
        public static bool Unregister(string name)
        {
            if (name == null || !_registry.Remove(name)) return false;
            _version++;
            return true;
        }

        /// <summary>Look up a renderer by exact tool name. Returns null if not registered.</summary>
        public static IToolCardRenderer Resolve(string name)
        {
            if (name == null) return null;
            _registry.TryGetValue(name, out var r);
            return r;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>Snapshot current state; dispose to restore. Used by UnityMcpTestBase.</summary>
        internal static System.IDisposable PreserveStateForTests()
        {
            var snapshot = new Dictionary<string, IToolCardRenderer>(_registry, StringComparer.Ordinal);
            var versionSnapshot = _version;
            return new RestoreScope(snapshot, versionSnapshot);
        }

        /// <summary>Clear registry and increment version. For per-test setup in test fixtures.</summary>
        internal static void ResetForTests()
        {
            _registry.Clear();
            _version++;
        }

        private sealed class RestoreScope : System.IDisposable
        {
            private Dictionary<string, IToolCardRenderer> _snapshot;
            private readonly int _savedVersion;

            internal RestoreScope(Dictionary<string, IToolCardRenderer> snapshot, int savedVersion)
            {
                _snapshot    = snapshot;
                _savedVersion = savedVersion;
            }

            public void Dispose()
            {
                if (_snapshot == null) return;
                _registry.Clear();
                foreach (var kv in _snapshot) _registry[kv.Key] = kv.Value;
                _version  = _savedVersion;
                _snapshot = null;
            }
        }
#endif
    }
}
