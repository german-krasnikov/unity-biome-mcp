using System;
using System.IO;
using UnityEditor;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor
{
    /// <summary>Real ISourcePatchBytesPort — raw file I/O + AssetDatabase import,
    /// mirroring AssetDatabaseHelper.WriteText's write+import pairing (§6 P0-70).</summary>
    internal sealed class UnitySourcePatchBytesPort : ISourcePatchBytesPort
    {
        public byte[] Read(string assetPath) => File.ReadAllBytes(Path.GetFullPath(assetPath));

        public void Write(string assetPath, byte[] content)
        {
            File.WriteAllBytes(Path.GetFullPath(assetPath), content);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
        }
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
