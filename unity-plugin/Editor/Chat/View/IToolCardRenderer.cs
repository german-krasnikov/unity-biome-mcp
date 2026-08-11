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
    /// </summary>
    public interface IToolCardRenderer
    {
        void OnStart(VisualElement chip, ToolCallRecord rec);
        void OnUpdate(VisualElement chip, ToolCallRecord rec);
    }
}
