using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor
{
    [Serializable]
    internal class ComposerState
    {
        public List<VisualStep> steps = new();
        public float globalTimeout = 60f;
        public bool globalAbort;
        public string lastFilePath = "";
    }

    internal static class ComposerStateStore
    {
        static string StorePath => Application.dataPath + "/../Library/PlaytestComposerState.json";

        internal static string _testOverride;

        static string Path => _testOverride ?? StorePath;

        public static void Save(ComposerState state)
        {
            try { File.WriteAllText(Path, JsonUtility.ToJson(state, true)); }
            catch (Exception e) { Debug.LogWarning($"ComposerStateStore.Save failed: {e.Message}"); }
        }

        public static ComposerState Load()
        {
            try
            {
                if (!File.Exists(Path)) return new ComposerState();
                return JsonUtility.FromJson<ComposerState>(File.ReadAllText(Path)) ?? new ComposerState();
            }
            catch (Exception e) { Debug.LogWarning($"ComposerStateStore.Load failed: {e.Message}"); return new ComposerState(); }
        }
    }
}
