namespace UnityMCP.Editor
{
    // Connection-layer participant in Unity's domain-reload lifecycle.
    // A sink never subscribes to AssemblyReloadEvents directly — it registers
    // with ReloadLifecycleCoordinator, which owns the one subscription pair.
    internal interface IReloadLifecycleSink
    {
        // Lower runs first; ties keep registration order. Override only when
        // this sink's effect must happen before/after another sink's.
        int Priority => 0;

        // Runs on AssemblyReloadEvents.beforeAssemblyReload.
        void OnBeforeReload() { }

        // Runs on AssemblyReloadEvents.afterAssemblyReload.
        void OnAfterReload() { }
    }
}
