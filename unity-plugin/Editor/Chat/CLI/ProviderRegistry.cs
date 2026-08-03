// Generic base for extensible plugin registries (settings, toolbar, panels).
// Handles dual-storage (list + dict), key validation, insert-sort, version bump.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ProviderRegistry
    {
        internal static readonly Regex KeyRegex = new Regex(@"^[a-z0-9_]+$");
    }

    internal static class ProviderRegistry<T>
    {

        /// <summary>
        /// Register p into providers/byKey using sortOrder to determine insertion point.
        /// Duplicate key = keep-first + LogWarning. Invalid key = reject + LogWarning.
        /// </summary>
        internal static bool Register(
            string registryName,
            T p,
            Func<T, string> getKey,
            Func<T, int>    getOrder,
            List<T>                    providers,
            Dictionary<string, T>      byKey,
            ref int                    version)
        {
            if (p == null) return false;
            var key = getKey(p);
            if (!ProviderRegistry.KeyRegex.IsMatch(key ?? ""))
            {
                Debug.LogWarning($"[{registryName}] Invalid key '{key}'. Must match ^[a-z0-9_]+$");
                return false;
            }
            if (byKey.ContainsKey(key))
            {
                Debug.LogWarning($"[{registryName}] Duplicate key '{key}' — keeping first registration.");
                return false;
            }
            // Insert sorted by order ascending
            int idx = providers.Count;
            var order = getOrder(p);
            for (int i = 0; i < providers.Count; i++)
            {
                if (order < getOrder(providers[i])) { idx = i; break; }
            }
            providers.Insert(idx, p);
            byKey[key] = p;
            version++;
            return true;
        }

        /// <summary>Remove by key. Returns true if found and removed.</summary>
        internal static bool Unregister(
            string key,
            Func<T, string>       getKey,
            List<T>               providers,
            Dictionary<string, T> byKey,
            ref int               version)
        {
            if (!byKey.TryGetValue(key, out var p)) return false;
            providers.Remove(p);
            byKey.Remove(key);
            version++;
            return true;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Preserve the registry exactly for a test scope. Restore bypasses the
        /// registration API so provider identity, order and version all return to
        /// their original values without dropping third-party registrations.
        /// </summary>
        internal static IDisposable PreserveState(
            List<T> providers,
            Dictionary<string, T> byKey,
            Func<int> getVersion,
            Action<int> setVersion)
        {
            var providerSnapshot = new List<T>(providers);
            var keySnapshot = new List<KeyValuePair<string, T>>(byKey);
            var versionSnapshot = getVersion();

            return new RestoreScope(() =>
            {
                providers.Clear();
                providers.AddRange(providerSnapshot);

                byKey.Clear();
                foreach (var pair in keySnapshot)
                    byKey.Add(pair.Key, pair.Value);

                setVersion(versionSnapshot);
            });
        }
#endif

        /// <summary>Clear all entries (test use only).</summary>
        internal static void Reset(
            List<T>               providers,
            Dictionary<string, T> byKey,
            ref int               version)
        {
            providers.Clear();
            byKey.Clear();
            version++;
        }

#if UNITY_INCLUDE_TESTS
        private sealed class RestoreScope : IDisposable
        {
            private Action _restore;

            internal RestoreScope(Action restore) => _restore = restore;

            public void Dispose()
            {
                var restore = _restore;
                _restore = null;
                restore?.Invoke();
            }
        }
#endif
    }
}
