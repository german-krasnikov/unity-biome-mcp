// Immutable descriptor for a backend / agent choice shown in the selector dropdown.
namespace UnityMCP.Editor.Chat
{
    internal readonly struct BackendSpec
    {
        internal readonly string      DisplayName; // shown in UI
        internal readonly string      AgentName;   // passed to --agent (null = default Claude)
        internal readonly bool        Enabled;     // false = placeholder
        internal readonly BackendKind Kind;

        internal BackendSpec(string displayName, string agentName, bool enabled,
            BackendKind kind = BackendKind.Claude)
        {
            DisplayName = displayName;
            AgentName   = agentName;
            Enabled     = enabled;
            Kind        = kind;
        }
    }
}
