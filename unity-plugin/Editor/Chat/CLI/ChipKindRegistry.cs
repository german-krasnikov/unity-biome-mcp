// Extensible registry of IChipKindProvider instances.
// PUBLIC static class — public members must be reachable from external assemblies.
// [InitializeOnLoad] seeds built-ins once per domain-reload.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    public static class ChipKindRegistry
    {
        private static readonly List<IChipKindProvider>            _providers = new List<IChipKindProvider>();
        private static readonly Dictionary<string, IChipKindProvider> _byKey   = new Dictionary<string, IChipKindProvider>();
        private static bool _builtInsRegistered;
        private static int  _version;

        static ChipKindRegistry() => EnsureBuiltIns();

        // ── Public API ────────────────────────────────────────────────────────

        public static int Version => _version;

        public static IReadOnlyList<string> AllKeys
        {
            get
            {
                var keys = new List<string>(_providers.Count);
                foreach (var p in _providers) keys.Add(p.Key);
                return keys;
            }
        }

        /// <summary>
        /// Register a provider. Duplicate key = keep-first + LogWarning (no throw).
        /// Key must match ^[a-z0-9_]+$.
        /// </summary>
        public static bool Register(IChipKindProvider p)
        {
            if (p == null) return false;
            if (!Regex.IsMatch(p.Key, @"^[a-z0-9_]+$"))
            {
                Debug.LogWarning($"[ChipKindRegistry] Invalid key '{p.Key}'. Must match ^[a-z0-9_]+$");
                return false;
            }
            if (_byKey.ContainsKey(p.Key))
            {
                Debug.LogWarning($"[ChipKindRegistry] Duplicate key '{p.Key}' — keeping first registration.");
                return false;
            }
            // Insert sorted by Priority ascending
            int idx = _providers.Count;
            for (int i = 0; i < _providers.Count; i++)
            {
                if (p.Priority < _providers[i].Priority) { idx = i; break; }
            }
            _providers.Insert(idx, p);
            _byKey[p.Key] = p;
            _version++;
            return true;
        }

        /// <summary>Remove a provider by key. Returns true if found and removed.</summary>
        public static bool Unregister(string key)
        {
            if (!_byKey.TryGetValue(key, out var p)) return false;
            _providers.Remove(p);
            _byKey.Remove(key);
            _version++;
            return true;
        }

        /// <summary>Find the first provider (lowest Priority) that CanHandle the object.</summary>
        public static IChipKindProvider Resolve(Object obj, string assetPath)
        {
            EnsureBuiltIns();
            foreach (var p in _providers)
                if (p.CanHandle(obj, assetPath)) return p;
            return null;
        }

        /// <summary>Look up a provider by exact key. Returns null if not found.</summary>
        public static IChipKindProvider ForKey(string key)
        {
            if (key == null) return null;
            _byKey.TryGetValue(key, out var p);
            return p;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Preserve every registration and the exact version/built-in guard values.
        /// Intended for the common test isolation layer; unlike ResetToBuiltIns it
        /// cannot discard providers installed by another package.
        /// </summary>
        internal static System.IDisposable PreserveStateForTests()
        {
            var builtInsRegistered = _builtInsRegistered;
            var registryScope = ProviderRegistry<IChipKindProvider>.PreserveState(
                _providers, _byKey, () => _version, value => _version = value);
            return new RestoreScope(registryScope, builtInsRegistered);
        }

        /// <summary>TEST-ONLY: clear all providers and re-register built-ins.</summary>
        internal static void ResetForTests()
        {
            _providers.Clear();
            _byKey.Clear();
            _builtInsRegistered = false;
            _version++;
            EnsureBuiltIns();
        }

        private sealed class RestoreScope : System.IDisposable
        {
            private System.IDisposable _registryScope;
            private readonly bool _builtInsRegistered;

            internal RestoreScope(System.IDisposable registryScope, bool builtInsRegistered)
            {
                _registryScope = registryScope;
                _builtInsRegistered = builtInsRegistered;
            }

            public void Dispose()
            {
                var registryScope = _registryScope;
                if (registryScope == null) return;
                _registryScope = null;
                try { registryScope.Dispose(); }
                finally { ChipKindRegistry._builtInsRegistered = _builtInsRegistered; }
            }
        }
#endif

        // ── Internal bootstrap ────────────────────────────────────────────────

        /// <summary>
        /// Register the 8 built-in providers once. NEVER calls Clear() —
        /// third-party registrations made before this call survive.
        /// </summary>
        internal static void EnsureBuiltIns()
        {
            if (_builtInsRegistered) return;
            _builtInsRegistered = true;
            Register(new AgentChipProvider());    // priority 5  — sub-agent @-mentions (programmatic only)
            Register(new ImageChipProvider());    // priority 50 — external images (obj==null)
            Register(new HierarchyChipProvider());
            Register(new ComponentChipProvider()); // priority 125 — programmatic only
            Register(new FieldChipProvider());     // priority 130 — programmatic only
            Register(new FolderChipProvider());
            Register(new SceneChipProvider());
            Register(new ScriptChipProvider());
            Register(new PrefabChipProvider());
            Register(new ModelChipProvider());
            Register(new MaterialChipProvider());
            Register(new AudioChipProvider());
            Register(new TextureChipProvider());
            Register(new SOChipProvider());
            Register(new AssetChipProvider());
        }
    }
}
