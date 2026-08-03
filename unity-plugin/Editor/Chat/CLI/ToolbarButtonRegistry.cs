// Registry for IToolbarButtonProvider. Uses ProviderRegistry<T> base to eliminate boilerplate.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class ToolbarButtonRegistry
    {
        private static readonly List<IToolbarButtonProvider>               _providers = new List<IToolbarButtonProvider>();
        private static readonly Dictionary<string, IToolbarButtonProvider> _byKey     = new Dictionary<string, IToolbarButtonProvider>();
        private static int _version;

        public static int Version => _version;
        public static IReadOnlyList<IToolbarButtonProvider> All => _providers;

        public static bool Register(IToolbarButtonProvider p)
            => ProviderRegistry<IToolbarButtonProvider>.Register(
                "ToolbarButtonRegistry", p,
                x => x.Key, x => x.Order,
                _providers, _byKey, ref _version);

        public static bool Unregister(string key)
            => ProviderRegistry<IToolbarButtonProvider>.Unregister(
                key, x => x.Key, _providers, _byKey, ref _version);

#if UNITY_INCLUDE_TESTS
        internal static System.IDisposable PreserveStateForTests()
            => ProviderRegistry<IToolbarButtonProvider>.PreserveState(
                _providers, _byKey, () => _version, value => _version = value);

        public static void ResetForTests()
            => ProviderRegistry<IToolbarButtonProvider>.Reset(_providers, _byKey, ref _version);
#endif
    }
}
