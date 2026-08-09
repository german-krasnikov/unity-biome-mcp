using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnityMCP.CI
{
    public static class CiBuildSmoke
    {
        public static void Build()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var output = GetArgument("-ciBuildOutput") ?? DefaultOutput(target);
            var scenes = SelectedScenes(GetArgument("-ciBuildScene"));

            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Build smoke failed: {summary.result}, errors={summary.totalErrors}, target={target}");
            }

            if (!File.Exists(summary.outputPath) && !Directory.Exists(summary.outputPath))
            {
                throw new FileNotFoundException($"Build output missing: {summary.outputPath}");
            }
        }

        private static string[] SelectedScenes(string explicitScene)
        {
            if (!string.IsNullOrWhiteSpace(explicitScene))
            {
                if (!File.Exists(explicitScene))
                    throw new FileNotFoundException($"Build scene missing: {explicitScene}");
                return new[] { explicitScene };
            }

            return EnabledScenes();
        }

        private static string[] EnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();

            return scenes.Length > 0 ? scenes : new[] { "Assets/Scenes/SampleScene.unity" };
        }

        private static string DefaultOutput(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => "artifacts/player-smoke/UnityMCP.exe",
                BuildTarget.StandaloneOSX => "artifacts/player-smoke/UnityMCP.app",
                _ => "artifacts/player-smoke/UnityMCP",
            };
        }

        private static string GetArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return null;
        }
    }
}
