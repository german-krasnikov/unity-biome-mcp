// BuildHelper: BuildPipeline.BuildPlayer wrapper.
// Must run on main thread; dispatches via MainThreadDispatcher.Enqueue.
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class BuildHelper
    {
        internal static void Execute(string action, string target, string scenes,
                                     string path, bool dev,
                                     TaskCompletionSource<string> inner)
        {
            if (action != "build")
            {
                inner.TrySetResult($"err:invalid action '{action}'");
                return;
            }

            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    var buildTarget = ParseTarget(
                        target ?? EditorUserBuildSettings.activeBuildTarget.ToString());
                    var sceneList = ParseScenes(scenes);
                    var opts = new BuildPlayerOptions
                    {
                        scenes           = sceneList,
                        locationPathName = path ?? $"Builds/{buildTarget}",
                        target           = buildTarget,
                        options          = dev ? BuildOptions.Development : BuildOptions.None,
                    };
                    var report = BuildPipeline.BuildPlayer(opts);
                    inner.TrySetResult(FormatBuildResult(report, buildTarget.ToString(), sceneList.Length));
                }
                catch (Exception ex)
                {
                    inner.TrySetResult($"err:{ex.Message}");
                }
            });
        }

        internal static BuildTarget ParseTarget(string target)
        {
            if (!Enum.TryParse<BuildTarget>(target, ignoreCase: true, out var result))
                throw new InvalidOperationException(
                    $"Unknown build target '{target}'. Valid: StandaloneWindows64, StandaloneOSX, Android, iOS, WebGL");
            return result;
        }

        internal static string[] ParseScenes(string scenes)
        {
            if (string.IsNullOrEmpty(scenes)) return Array.Empty<string>();
            return scenes.Split(',')
                         .Select(s => s.Trim())
                         .Where(s => s.Length > 0)
                         .ToArray();
        }

        internal static string FormatBuildResult(BuildReport report, string target, int sceneCount)
        {
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                return $"ok\n" +
                       $"target:{target}\n" +
                       $"path:{summary.outputPath}\n" +
                       $"scenes:{sceneCount}\n" +
                       $"size:{summary.totalSize}\n" +
                       $"warnings:{summary.totalWarnings}\n" +
                       $"time:{summary.totalTime.TotalSeconds:F1}s";
            }
            if (summary.result == BuildResult.Cancelled)
                return $"err:build cancelled\ntarget:{target}";
            return $"err:{summary.totalErrors} errors\ntarget:{target}";
        }
    }
}
