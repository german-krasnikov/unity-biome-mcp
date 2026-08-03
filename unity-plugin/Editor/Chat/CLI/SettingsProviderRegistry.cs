// Registry for ISettingsProvider. Uses ProviderRegistry<T> base to eliminate boilerplate.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class SettingsProviderRegistry
    {
        private static readonly List<ISettingsProvider>               _providers = new List<ISettingsProvider>();
        private static readonly Dictionary<string, ISettingsProvider> _byKey     = new Dictionary<string, ISettingsProvider>();
        private static int _version;

        public static int Version => _version;
        public static IReadOnlyList<ISettingsProvider> All => _providers;

        public static bool Register(ISettingsProvider p)
            => ProviderRegistry<ISettingsProvider>.Register(
                "SettingsProviderRegistry", p,
                x => x.Key, x => x.Order,
                _providers, _byKey, ref _version);

        public static bool Unregister(string key)
            => ProviderRegistry<ISettingsProvider>.Unregister(
                key, x => x.Key, _providers, _byKey, ref _version);

#if UNITY_INCLUDE_TESTS
        internal static System.IDisposable PreserveStateForTests()
            => ProviderRegistry<ISettingsProvider>.PreserveState(
                _providers, _byKey, () => _version, value => _version = value);

        public static void ResetForTests()
            => ProviderRegistry<ISettingsProvider>.Reset(_providers, _byKey, ref _version);
#endif
    }
}
