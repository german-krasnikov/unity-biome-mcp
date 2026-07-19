using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class FileOutputHelper
    {
        public const int TEXT_THRESHOLD = 81920; // 80KB

        private static string _outputDir;
        private static string _screenshotsDir;

        public static string OutputDir
        {
            get
            {
                if (_outputDir == null)
                {
                    _outputDir = Path.Combine(Application.dataPath, "..", "Temp", "MCP");
                    Directory.CreateDirectory(_outputDir);
                }
                return _outputDir;
            }
        }

        public static string ScreenshotsDir
        {
            get
            {
                if (_screenshotsDir == null)
                {
                    _screenshotsDir = Path.Combine(Application.dataPath, "..", "ScreenShots");
                    Directory.CreateDirectory(_screenshotsDir);
                }
                return _screenshotsDir;
            }
        }

        public static string WriteText(string content, string prefix = "output")
        {
            Cleanup();
            var path = Path.Combine(OutputDir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, content);
            return path;
        }

        public static string WritePng(byte[] pngData, string prefix = "screenshot", string outputPath = null)
        {
            if (!string.IsNullOrEmpty(outputPath))
            {
                var fullPath = Path.GetFullPath(outputPath);
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                    + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"outputPath must be within project: {outputPath}");
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(fullPath, pngData);
                return fullPath;
            }
            CleanupScreenshots();
            var path = Path.Combine(ScreenshotsDir, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{prefix}.png");
            File.WriteAllBytes(path, pngData);
            return path;
        }

        public static void CleanupScreenshots(int keepCount = 20)
        {
            if (!Directory.Exists(ScreenshotsDir)) return;
            var files = Directory.GetFiles(ScreenshotsDir, "*.png")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToArray();
            for (int i = keepCount; i < files.Length; i++)
                try { File.Delete(files[i]); } catch { }
        }

        public static void Cleanup()
        {
            if (!Directory.Exists(OutputDir)) return;
            var cutoff = DateTime.Now.AddHours(-1);
            foreach (var file in Directory.GetFiles(OutputDir))
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    try { File.Delete(file); } catch { /* ignore */ }
                }
            }
        }
    }
}
