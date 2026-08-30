using System;

namespace UnityMCP.Editor.SourcePatch
{
    /// <summary>
    /// The one in-process registration slot for the one optional provider.
    /// No discovery, no provider collection (§3.1: "One registration slot,
    /// one provider, one coordinator"). This is ordinary in-memory static
    /// state scoped to one AppDomain/session — not a filesystem singleton.
    /// </summary>
    public static class SourcePatchProviderSlot
    {
        private static string _registeredProviderId;
        private static ISourcePatchProvider _registeredProvider;

        public static SourcePatchRegistrationResult Register(string providerId, ISourcePatchProvider provider)
        {
            if (string.IsNullOrEmpty(providerId))
            {
                throw new ArgumentException("providerId must be non-empty", nameof(providerId));
            }
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (_registeredProvider == null)
            {
                _registeredProviderId = providerId;
                _registeredProvider = provider;
                return SourcePatchRegistrationResult.Registered;
            }

            if (_registeredProviderId == providerId)
            {
                return SourcePatchRegistrationResult.AlreadyRegistered;
            }

            return SourcePatchRegistrationResult.Conflict;
        }

        public static bool TryGet(out ISourcePatchProvider provider)
        {
            provider = _registeredProvider;
            return provider != null;
        }

        internal static void ResetForTests()
        {
            _registeredProviderId = null;
            _registeredProvider = null;
        }
    }

    public enum SourcePatchRegistrationResult
    {
        Registered,
        AlreadyRegistered,
        Conflict,
    }
}
