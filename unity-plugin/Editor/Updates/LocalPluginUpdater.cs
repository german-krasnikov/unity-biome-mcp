using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class LocalPluginUpdater
    {
        internal interface IProcessRunner
        {
            int Run(string exe, string args, string workingDir);
        }

        class DefaultRunner : IProcessRunner
        {
            public int Run(string exe, string args, string workingDir)
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = workingDir,
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit();
                return p?.ExitCode ?? -1;
            }
        }

        static readonly IProcessRunner _default = new DefaultRunner();

        /// <summary>Run git pull on background thread; fires callbacks on result.</summary>
        internal static void UpdateAsync(
            string repoRoot,
            IProcessRunner runner = null,
            Action<string> onProgress = null,
            Action<bool> onComplete = null)
        {
            runner ??= _default;

            if (string.IsNullOrEmpty(repoRoot))
            {
                Debug.LogWarning($"{BiomeLabel.Tag} No repo root found — update manually.");
                onComplete?.Invoke(false);
                return;
            }

            onProgress?.Invoke("Running git pull --tags --autostash …");

            // --autostash: stash dirty WD automatically, pull, pop — safe for local dev installs.
            const string GitArgs = "pull --tags --autostash";

            // Production: offload blocking WaitForExit to background thread, marshal back via delayCall.
            if (runner is DefaultRunner)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    var code = runner.Run("git", GitArgs, repoRoot);
                    EditorApplication.delayCall += () =>
                    {
                        if (code == 0)
                        {
                            onProgress?.Invoke("Refreshing Unity assets …");
                            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                            onComplete?.Invoke(true);
                        }
                        else
                        {
                            Debug.LogError($"{BiomeLabel.Tag} git pull failed (exit {code}).\nRun manually:\n  cd \"{repoRoot}\"\n  git stash && git pull --tags && git stash pop");
                            onComplete?.Invoke(false);
                        }
                    };
                });
                return;
            }

            // Tests inject synchronous FakeRunner — run inline so asserts fire immediately.
            var exitCode = runner.Run("git", GitArgs, repoRoot);
            if (exitCode == 0)
            {
                onProgress?.Invoke("Refreshing Unity assets …");
                EditorApplication.delayCall += () => AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogError($"{BiomeLabel.Tag} git pull failed (exit {exitCode}).\nRun manually:\n  cd \"{repoRoot}\"\n  git stash && git pull --tags && git stash pop");
                onComplete?.Invoke(false);
            }
        }
    }
}
