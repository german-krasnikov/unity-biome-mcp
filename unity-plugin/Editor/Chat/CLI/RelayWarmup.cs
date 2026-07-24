// Silent background uvx pre-warm (ARCH-coldstart-ux.md, component 1). First-run of
// `uvx --from git+URL unity-biome-mcp-relay` takes 10-45s because uvx clones the git repo and
// builds the wheel from scratch. Firing a throwaway `--version` probe in the background
// during domain reload/startup populates ~/.cache/uv so the real Chat spawn later hits the
// warm wheel cache instead (~1-2s).
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class RelayWarmup
    {
        private const string WarmKey = "MCPRelay_UvxWarmed_v1";

#if UNITY_INCLUDE_TESTS
        // Seams — mirror ChatBinaryResolver.WhichOverride / InstallSourceDetector.SetSourceForTest.
        internal static Func<bool>                       SkipForTests;            // true → ShouldWarm() short-circuits false
        internal static Action                           OnWarmStarted;           // spy: fires right before Task.Run
        internal static Func<(string cmd, string[] argv)> CommandResolverOverride; // replaces RelayCommandResolver.Resolve
        internal static Func<bool>                        WarmKeyGetterOverride;   // replaces EditorPrefs.GetBool(WarmKey)
        internal static Action<bool>                       WarmKeySetterOverride;   // replaces EditorPrefs.SetBool(WarmKey)

        internal static void ResetForTests()
        {
            SkipForTests             = null;
            OnWarmStarted             = null;
            CommandResolverOverride   = null;
            WarmKeyGetterOverride     = null;
            WarmKeySetterOverride     = null;
        }
#endif

        static RelayWarmup()
        {
            // NOT the ctor body directly — PackageInfo (needed by RelayCommandResolver via
            // InstallSourceDetector) isn't reliably ready until after domain reload settles.
            EditorApplication.delayCall += TryWarm;
        }

        /// <summary>
        /// True if a warmup probe should run: not a Local (dev checkout) install, and not
        /// already warmed for this plugin version. EditorPrefs (not SessionState) is used for
        /// the warmed flag so it survives full Unity restarts, not just domain reloads.
        /// </summary>
        internal static bool ShouldWarm()
        {
#if UNITY_INCLUDE_TESTS
            if (SkipForTests != null && SkipForTests()) return false;
#endif
            if (InstallSourceDetector.Detect() == InstallSourceDetector.Source.Local) return false;
            if (GetWarmKey()) return false;
            return true;
        }

        // internal (not private) so tests can drive it directly instead of depending on a live
        // EditorApplication.delayCall tick.
        internal static void TryWarm()
        {
            if (!ShouldWarm()) return;

            // MAIN THREAD ONLY — CommandResolver touches InstallSourceDetector/PackageInfo/EditorPrefs.
            var (cmd, argv) = ResolveCommand();
            if (string.IsNullOrEmpty(cmd)) return; // uvx not found — real spawn reports this error later

            var warmArgv = BuildWarmupArgv(argv);

#if UNITY_INCLUDE_TESTS
            OnWarmStarted?.Invoke();
#endif
            Task.Run(() => RunWarmup(cmd, warmArgv));
        }

        /// <summary>
        /// Pure — swaps the relay's normal invocation for a throwaway version probe so the
        /// warmup never opens a TCP port or writes SessionState. E.g.
        /// ["--from", url, "unity-biome-mcp-relay"] → [..., "--version"].
        /// </summary>
        internal static string[] BuildWarmupArgv(string[] argv)
        {
            var len = argv?.Length ?? 0;
            var result = new string[len + 1];
            for (var i = 0; i < len; i++) result[i] = argv[i];
            result[len] = "--version";
            return result;
        }

        // Runs on the ThreadPool — Process.Start/WaitForExit are plain .NET, not Editor APIs.
        // Must not touch EditorPrefs/SessionState/PackageInfo/Debug directly (see RelaySpawner's
        // ExecuteSpawn for the same contract); the success write is marshalled back below.
        private static void RunWarmup(string cmd, string[] argv)
        {
            try
            {
                var psi = new ProcessStartInfo(cmd)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                foreach (var a in argv) psi.ArgumentList.Add(a);

                using var p = Process.Start(psi);
                if (p == null) return;
                if (!p.WaitForExit(90_000))
                {
                    try { p.Kill(); } catch { /* already gone */ }
                    return;
                }
                if (p.ExitCode != 0) return; // failed — leave WarmKey unset, real spawn does the cold start normally

                MainThreadDispatcher.Enqueue(() => SetWarmKey(true));
            }
            catch
            {
                // Best-effort background warmup only. Any failure here just means the real
                // spawn later pays the full cold-start cost — never surface this to the user.
            }
        }

        private static (string cmd, string[] argv) ResolveCommand()
        {
#if UNITY_INCLUDE_TESTS
            if (CommandResolverOverride != null) return CommandResolverOverride();
#endif
            return RelayCommandResolver.Resolve();
        }

        private static bool GetWarmKey()
        {
#if UNITY_INCLUDE_TESTS
            if (WarmKeyGetterOverride != null) return WarmKeyGetterOverride();
#endif
            return EditorPrefs.GetBool(WarmKey, false);
        }

        private static void SetWarmKey(bool value)
        {
#if UNITY_INCLUDE_TESTS
            if (WarmKeySetterOverride != null) { WarmKeySetterOverride(value); return; }
#endif
            EditorPrefs.SetBool(WarmKey, value);
        }
    }
}
