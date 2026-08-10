using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class UpdateDispatcher
    {
        internal static void DoUpdate(System.Action<bool> onComplete = null)
        {
            var ver = UpdateChecker.AvailableVersion;

            void OnDone(bool ok)
            {
                if (ok) UpdateChecker.ClearCache();
                // Server pin (.mcp.json @vX) re-syncs itself: the UPM update triggers a domain
                // reload, and ProjectConfigWriter (Wizard assembly) rewrites the config for the
                // new PackageInfo.version on that reload. No cross-assembly call needed here.
                onComplete?.Invoke(ok);
            }

            if (InstallSourceDetector.Detect() == InstallSourceDetector.Source.Local)
            {
                var root = InstallSourceDetector.LocalRepoRoot();
                if (root == null)
                {
                    Debug.LogWarning($"{BiomeLabel.Tag} Local install but repo root not found. Pull manually.");
                    OnDone(false);
                    return;
                }
                LocalPluginUpdater.UpdateAsync(root, onProgress: m => Debug.Log($"{BiomeLabel.Tag} " + m), onComplete: OnDone);
            }
            else
            {
                UpmPluginUpdater.Update(ver, OnDone);
            }
        }
    }
}
