using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class ChangeWatcher
    {
        static readonly List<string> _changes = new();
        const int MaxChanges = 50;

        static ChangeWatcher()
        {
            EditorApplication.hierarchyChanged += () => RecordChange("HIERARCHY_CHANGED");
            Undo.undoRedoPerformed += () => RecordChange("UNDO_REDO");
            EditorApplication.playModeStateChanged += state => RecordChange($"PLAY_MODE:{state}");
            EditorSceneManager.sceneOpened += (scene, _) => RecordChange($"SCENE_OPENED:{scene.name}");
            EditorSceneManager.sceneSaved += scene => RecordChange($"SCENE_SAVED:{scene.name}");
            Selection.selectionChanged += () =>
            {
                var sel = Selection.activeGameObject;
                if (sel != null) RecordChange($"SELECTED:{sel.name}");
            };
        }

        static void RecordChange(string change)
        {
            _changes.Add($"{DateTime.Now:HH:mm:ss} {change}");
            if (_changes.Count > MaxChanges)
                _changes.RemoveAt(0);
        }

        // Called inline from CommandRouter.Process() after each mutating command,
        // since deferred events (hierarchyChanged etc.) don't fire synchronously.
        public static void RecordMutation(string mutation) => RecordChange(mutation);

        public static string GetChanges(bool clear = true)
        {
            if (_changes.Count == 0) return "NO_CHANGES";
            var result = string.Join("\n", _changes);
            if (clear) _changes.Clear();
            return result;
        }
    }
}
