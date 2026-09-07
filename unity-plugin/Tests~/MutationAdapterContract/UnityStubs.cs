// Minimal Unity-namespace stubs for the SourcePatch seam compile contract.
// The three seam files (ISourcePatchProvider.cs, SourcePatchRequest.cs,
// SourcePatchProviderSlot.cs) reference no Unity API themselves; these
// stubs exist for the adapter files layered on top of this seam project in
// unity-plugin/Tests~/MutationAdapterContract/AdapterContract.Tests.csproj
// (Phase C1-b), so this seam project need not be touched again to add them.
using System;

namespace UnityEditor
{
    public sealed class InitializeOnLoadAttribute : Attribute { }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static void LogWarning(object value) { }
    }

    // GameObject/Component/HideFlags/Application below exist only for
    // BiomeFsrSourcePatchProvider.GetOrCreateDispatcher (adapter-cache) to
    // compile -- AddComponent<T>() is a same-assembly stub, not a real
    // Unity object model; it is exercised as a compile-contract surface,
    // not a runtime one.
    public class Component { }

    public enum HideFlags
    {
        None,
        HideAndDontSave,
    }

    public class GameObject
    {
        public string Name { get; }
        public HideFlags hideFlags { get; set; }

        public GameObject(string name)
        {
            Name = name;
        }

        public T AddComponent<T>() where T : Component, new() => new T();
    }

    public static class Application
    {
        public static string dataPath = "/tmp/adapter-contract-stub-project/Assets";
    }
}
