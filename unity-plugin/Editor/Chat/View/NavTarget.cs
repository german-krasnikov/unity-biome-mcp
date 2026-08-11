// Pure value struct for navigation targets.
// KindKey maps to ChipKindRegistry keys (e.g. "script", "hierarchy").
// Line is separate from Reference — never embedded in the reference string.
namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Identifies a navigation destination: what kind of thing to open and where.
    /// IsEmpty when KindKey is null/empty or Reference is null.
    /// </summary>
    public readonly struct NavTarget
    {
        /// <summary>ChipKindRegistry key, e.g. "script", "hierarchy", "component".</summary>
        public string KindKey   { get; }

        /// <summary>Asset path, hierarchy path, or other kind-specific reference.</summary>
        public string Reference { get; }

        /// <summary>Source line number. 0 = not specified. Used by FileLineNavigator.</summary>
        public int    Line      { get; }

        public bool IsEmpty => string.IsNullOrEmpty(KindKey) || Reference == null;

        public NavTarget(string kindKey, string reference, int line = 0)
        {
            KindKey   = kindKey;
            Reference = reference;
            Line      = line;
        }
    }
}
