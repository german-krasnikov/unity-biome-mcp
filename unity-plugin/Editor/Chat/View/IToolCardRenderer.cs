// Public interface for per-tool chip renderers.
// Implement and call ToolCardRendererRegistry.Register() from [InitializeOnLoad].
// CONSTRAINT: OnStart/OnUpdate are always called on the main thread — no await.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Specialized renderer for a named tool's chip.
    /// Register via ToolCardRendererRegistry with [InitializeOnLoad].
    /// OnStart: chip just created, rec.ArgsJson is null.
    /// OnUpdate: called up to twice per tool call (ArgsComplete, then Result). Must be idempotent.
    ///
    /// API STABILITY: This interface is frozen for external implementors (UPM package boundary).
    /// New lifecycle stages will NOT be added as methods here — doing so would be a breaking
    /// change for every third-party renderer. If a new stage is needed, introduce a separate
    /// optional interface (e.g. IToolCardRendererV2) and have the registry check for it via
    /// a cast, or add a coordinator class that renderers can opt into via composition.
    /// </summary>
    public interface IToolCardRenderer
    {
        void OnStart(VisualElement chip, ToolCallRecord rec);
        void OnUpdate(VisualElement chip, ToolCallRecord rec);
    }
}
