// Minimal compile-contract stubs of the ImmersiveVRTools surface the
// adapter binds against (adapter-cache/BiomeFsrSourcePatchProvider.cs,
// adapter-cache/BiomeFsrAutomaticModesGuard.cs). See FsrStubs.cs header for
// the same rationale. See Plans/MUTATION-REGRESSION-MODULE.md Phase C1.
using UnityEngine;

namespace ImmersiveVRTools.Runtime.Common
{
    // GetOrCreateDispatcher() does `host.AddComponent<UnityMainThreadDispatcher>()`
    // -- must derive from the stub UnityEngine.Component (UnityStubs.cs) to
    // satisfy GameObject.AddComponent<T>()'s constraint.
    public sealed class UnityMainThreadDispatcher : Component
    {
    }
}

namespace ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition
{
    public sealed class ToggleProjectEditorPreferenceDefinition
    {
        private bool _persistedValue;

        public void SetEditorPersistedValue(bool value) => _persistedValue = value;

        public object GetEditorPersistedValueOrDefault() => _persistedValue;
    }
}
