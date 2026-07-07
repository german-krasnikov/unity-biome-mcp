using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class PlaytestFileHelper
    {
        internal static string EnsurePlaytestsFolder()
        {
            var dir = Path.GetFullPath(Application.dataPath + "/../Playtests");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        internal static void Save(string dsl, ref string lastFilePath)
        {
            var folder = string.IsNullOrEmpty(lastFilePath)
                ? EnsurePlaytestsFolder()
                : Path.GetDirectoryName(lastFilePath);
            var path = EditorUtility.SaveFilePanel("Save Playtest", folder, "test", "playtest");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, dsl);
            lastFilePath = path;
        }

        internal static List<VisualStep> Load(ref string lastFilePath)
        {
            var folder = string.IsNullOrEmpty(lastFilePath)
                ? EnsurePlaytestsFolder()
                : Path.GetDirectoryName(lastFilePath);
            var path = EditorUtility.OpenFilePanel("Load Playtest", folder, "playtest");
            if (string.IsNullOrEmpty(path)) return null;
            lastFilePath = path;
            var steps = new List<VisualStep>();
            foreach (var p in PlaytestParser.Parse(File.ReadAllText(path)))
                steps.Add(PlaytestDslExporter.FromParsed(p));
            return steps;
        }
    }
}
