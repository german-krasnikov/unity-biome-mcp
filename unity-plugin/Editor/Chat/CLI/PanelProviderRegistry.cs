// Registry for IPanelProvider. Uses ProviderRegistry<T> base to eliminate boilerplate.
// Sort key: MenuPriority (normalized to same role as Order in other registries).
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public static class PanelProviderRegistry
    {
        private static readonly List<IPanelProvider>               _providers = new List<IPanelProvider>();
        private static readonly Dictionary<string, IPanelProvider> _byKey     = new Dictionary<string, IPanelProvider>();
        private static int _version;

        public static int Version => _version;
        public static IReadOnlyList<IPanelProvider> All => _providers;

        public static bool Register(IPanelProvider p)
            => ProviderRegistry<IPanelProvider>.Register(
                "PanelProviderRegistry", p,
                x => x.Key, x => x.MenuPriority,
                _providers, _byKey, ref _version);

        public static bool Unregister(string key)
            => ProviderRegistry<IPanelProvider>.Unregister(
                key, x => x.Key, _providers, _byKey, ref _version);

        /// <summary>Call Show() on the provider with the given key. No-op if key not found.</summary>
        public static void ShowPanel(string key)
        {
            if (key == null || !_byKey.TryGetValue(key, out var p)) return;
            try { p.Show(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

#if UNITY_INCLUDE_TESTS
        internal static System.IDisposable PreserveStateForTests()
            => ProviderRegistry<IPanelProvider>.PreserveState(
                _providers, _byKey, () => _version, value => _version = value);

        public static void ResetForTests()
            => ProviderRegistry<IPanelProvider>.Reset(_providers, _byKey, ref _version);
#endif
    }
}
