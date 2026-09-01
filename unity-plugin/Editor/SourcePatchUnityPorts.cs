using System;
using System.IO;
using UnityEditor;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor
{
    /// <summary>Real ISourcePatchBytesPort — raw file I/O only, deliberately
    /// import-free (§6 P0-70 fix). AssetDatabase.ImportAsset on a .cs path
    /// synchronously flips EditorApplication.isCompiling and requests a real
    /// Unity script compilation, which is exactly the self-inflicted "zero
    /// compile" violation that made a genuinely successful FSR apply read as
    /// Uncertain/Recovery in P0-80 Cycle A live testing (the provider itself
    /// returned applied=True; SyncHelperCompileEvidencePort correctly, if
    /// unfairly, detected this port's own ImportAsset call as the violation).
    /// §3.2 requires a "raw full-file source update": Unity must observe an
    /// ON-path write only through the OFF-path sync later, never immediately
    /// via import. The legacy/OFF route (AssetDatabaseHelper.WriteText) is a
    /// separate implementation and keeps its own ImportAsset call unchanged.</summary>
    internal sealed class UnitySourcePatchBytesPort : ISourcePatchBytesPort
    {
        public byte[] Read(string assetPath) => File.ReadAllBytes(Path.GetFullPath(assetPath));

        public void Write(string assetPath, byte[] content) =>
            File.WriteAllBytes(Path.GetFullPath(assetPath), content);
    }

    /// <summary>Real IAutoRefreshLeasePort — DisallowAutoRefresh + LockReloadAssemblies,
    /// released exactly once via try/finally regardless of how Dispose is called
    /// (§ coordinator clarification 2: "releases owned holds once").</summary>
    internal sealed class UnityAutoRefreshLeasePort : IAutoRefreshLeasePort
    {
        public IDisposable AcquireLease()
        {
            AssetDatabase.DisallowAutoRefresh();
            EditorApplication.LockReloadAssemblies();
            return new Lease();
        }

        private sealed class Lease : IDisposable
        {
            private bool _released;

            public void Dispose()
            {
                if (_released) return;
                _released = true;
                try
                {
                    EditorApplication.UnlockReloadAssemblies();
                }
                finally
                {
                    AssetDatabase.AllowAutoRefresh();
                }
            }
        }
    }

    /// <summary>Real ICompileEvidencePort — proves the ON-path opposite of OFF's
    /// evidence: zero compile/reload occurred. Captures the domain stamp once at
    /// construction (armed exactly when transitioning Off -&gt; OnReady) and
    /// compares on every call — a changed stamp or an active compile means the
    /// apply did NOT stay compile-free.</summary>
    internal sealed class SyncHelperCompileEvidencePort : ICompileEvidencePort
    {
        private readonly string _stampAtArm = SyncHelper.CurrentDomainStamp;

        public bool ConfirmApplied(SourcePatchRequest request) =>
            !UnityEditor.EditorApplication.isCompiling
            && SyncHelper.CurrentDomainStamp == _stampAtArm;
    }
}
