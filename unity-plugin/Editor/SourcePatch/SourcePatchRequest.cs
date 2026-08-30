using System;

namespace UnityMCP.Editor.SourcePatch
{
    /// <summary>
    /// Immutable request to replace the full contents of one existing
    /// project-owned .cs file, carrying the exact "before" bytes the caller
    /// believes are currently on disk (for the coordinator's CAS check) and
    /// the exact "after" bytes to write. See Plans/HotReload/V2/
    /// FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §3.1/§6 P0-40.
    ///
    /// Scope note: this DTO validates only structural invariants (non-null,
    /// path shape). Project-relative-path/"under Assets/" validation belongs
    /// to the host (P0-50), which knows the project root; this neutral
    /// module does not.
    /// </summary>
    public sealed class SourcePatchRequest
    {
        // Backing fields are never handed out directly: each getter clones,
        // so a caller (e.g. the future provider adapter) mutating a returned
        // array can never corrupt the bytes the coordinator uses for its own
        // CAS/rollback comparisons.
        private readonly byte[] _expectedBeforeContent;
        private readonly byte[] _newContent;

        public string AssetPath { get; }
        public byte[] ExpectedBeforeContent => (byte[])_expectedBeforeContent.Clone();
        public byte[] NewContent => (byte[])_newContent.Clone();

        private SourcePatchRequest(string assetPath, byte[] expectedBeforeContent, byte[] newContent)
        {
            AssetPath = assetPath;
            _expectedBeforeContent = expectedBeforeContent;
            _newContent = newContent;
        }

        public static bool TryCreate(
            string assetPath,
            byte[] expectedBeforeContent,
            byte[] newContent,
            out SourcePatchRequest request)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            if (!assetPath.EndsWith(".cs", StringComparison.Ordinal)) return false;
            if (expectedBeforeContent == null) return false;
            if (newContent == null) return false;

            request = new SourcePatchRequest(
                assetPath,
                (byte[])expectedBeforeContent.Clone(),
                (byte[])newContent.Clone());
            return true;
        }
    }
}
