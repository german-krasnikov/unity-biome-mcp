// Auto-discovers IBackendProvider implementations via TypeCache.
// Test seam: set _override before calling All/Get.
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityMCP.Editor.Chat
{
    internal static class BackendProviderRegistry
    {
        private static List<IBackendProvider> _cache;

#if UNITY_INCLUDE_TESTS
        // Inject known providers in unit tests (TypeCache is Unity-only).
        internal static List<IBackendProvider> Override;
        internal static void ResetForTests() { _cache = null; Override = null; }
#endif

        /// <summary>All discovered providers, sorted by SortOrder.</summary>
        internal static IReadOnlyList<IBackendProvider> All
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (Override != null) return Override;
#endif
                return _cache ?? (_cache = Discover());
            }
        }

        /// <summary>Find provider by ProviderId. Returns null if not found.</summary>
        internal static IBackendProvider Get(string providerId)
        {
            foreach (var p in All)
                if (string.Equals(p.ProviderId, providerId, StringComparison.Ordinal))
                    return p;
            return null;
        }

        /// <summary>Map BackendKind enum to ProviderId string.</summary>
        internal static string KindToId(BackendKind kind)
        {
            switch (kind)
            {
                case BackendKind.Codex:    return "codex";
                case BackendKind.Antigravity: return "antigravity";
                case BackendKind.Kimi:     return "kimi";
                case BackendKind.OpenCode: return "opencode";
                default:                   return "claude";
            }
        }

        private static List<IBackendProvider> Discover()
        {
            var result = new List<IBackendProvider>();
#if UNITY_EDITOR
            foreach (var type in TypeCache.GetTypesDerivedFrom<IBackendProvider>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (TryInstantiate(type, out var p)) result.Add(p);
            }
#endif
            result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return result;
        }

        // Returns false and logs a warning when instantiation fails.
        // MissingMethodException (no parameterless ctor) is skipped silently — those are
        // test stubs or abstract-like types, not broken providers.
        private static bool TryInstantiate(Type type, out IBackendProvider provider)
        {
            provider = null;
            try
            {
                provider = (IBackendProvider)Activator.CreateInstance(type);
                return true;
            }
            catch (MissingMethodException) { return false; } // no parameterless ctor — skip
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BackendProviderRegistry] Failed to instantiate {type.Name}: {ex.Message}");
                return false;
            }
        }

#if UNITY_INCLUDE_TESTS
        // Exposes TryInstantiate for unit tests — same production logic, bypasses TypeCache.
        internal static bool TryInstantiate_ForTest(Type type, out IBackendProvider p)
            => TryInstantiate(type, out p);
#endif
    }
}
