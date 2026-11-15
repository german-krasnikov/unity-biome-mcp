using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Central source of truth for multi-scene state.
    /// Usage: var ctx = SceneContext.Current; if (ctx.IsMulti) ...
    /// </summary>
    public sealed class SceneContext
    {
        public static SceneContext Current => new SceneContext(HierarchySerializer.GetAllLoadedSceneRoots());

        // Keep for callers that already call InvalidateCache() — now a no-op.
        internal static void InvalidateCache() { }

        public readonly List<(string name, GameObject[] roots)> Scenes;
        public bool IsMulti => Scenes.Count > 1;

        private SceneContext(List<(string, GameObject[])> scenes) => Scenes = scenes;

        /// <summary>"Scene:/path" when multi-scene, "/path" when single.</summary>
        public string QualifyPath(GameObject go, string localPath)
        {
            if (IsMulti && go.scene.IsValid())
                return go.scene.name + ":/" + localPath;
            return "/" + localPath;
        }

        /// <summary>Filter scenes by name. Returns all when sceneName is null/empty.</summary>
        public List<(string name, GameObject[] roots)> FilterByScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return Scenes;
            return Scenes.FindAll(s => s.name.Equals(sceneName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
