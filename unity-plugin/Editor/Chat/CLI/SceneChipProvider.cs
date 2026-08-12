// Chip provider for Unity scene assets (.unity files).
// Navigate: pings and offers to open the scene (dialog suppressed in tests via seam).
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat
{
    internal sealed class SceneChipProvider : AssetChipProviderBase
    {
#if UNITY_INCLUDE_TESTS
        /// <summary>Test seam: override to suppress or fake the Open Scene? dialog.</summary>
        internal static System.Func<string, bool> DisplayDialogOverride;
#endif

        public override string Key      => ChipKindKeys.Scene;
        public override int    Priority => 200;
        public override string IconName => "d_SceneAsset Icon";
        public override string HexColor => "#c084fc";

        public override bool CanHandle(Object obj, string assetPath)
            => obj != null && !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".unity");

        // chip.Path = scene name ("MyScene"), not the full asset path.
        public override ChipData Create(Object obj, string assetPath)
        {
            // obj null: direct-call path only — CanHandle requires non-null for registry calls
            var name = obj != null ? obj.name : Path.GetFileNameWithoutExtension(assetPath);
            return new ChipData(Key, name, name, 0);
        }

        public override void Navigate(string reference)
        {
            if (UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Cannot open scene in Play Mode: {reference}");
                return;
            }
            var obj = LoadScene(reference);
            if (obj == null)
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Scene not found: {reference}");
                return;
            }
            // Ping first so user can see it in the Project window.
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;

            // Offer to open the scene if it is not currently loaded.
            var scenePath = AssetDatabase.GetAssetPath(obj);
            var isLoaded  = false;
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (s.isLoaded && string.Equals(s.path, scenePath,
                        System.StringComparison.Ordinal))
                { isLoaded = true; break; }
            }
            if (!isLoaded && !string.IsNullOrEmpty(scenePath))
            {
                bool open;
#if UNITY_INCLUDE_TESTS
                // Suppress the modal dialog in tests unless the test explicitly sets the override.
                open = DisplayDialogOverride != null && DisplayDialogOverride(reference);
#else
                open = EditorUtility.DisplayDialog("Open Scene?", $"Load '{reference}' in the Editor?", "Open", "Cancel");
#endif
                if (open)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        scenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Additive);
                }
            }
        }

        public override void Ping(string reference)
        {
            var obj = LoadScene(reference);
            if (obj != null) { EditorGUIUtility.PingObject(obj); Selection.activeObject = obj; }
        }

        // Full path or .unity extension → direct load; name-only → exact-name lookup.
        private static Object LoadScene(string reference)
        {
            if (reference.Contains("/") || reference.EndsWith(".unity"))
                return AssetDatabase.LoadAssetAtPath<Object>(reference);
            var path = FindScenePathByExactName(reference);
            return path != null ? AssetDatabase.LoadAssetAtPath<Object>(path) : null;
        }

        // FindAssets uses substring search; filter by exact filename to avoid "Level" matching "Level1".
        internal static string FindScenePathByExactName(string sceneName)
        {
            var guids = AssetDatabase.FindAssets("t:Scene " + sceneName);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                    return path;
            }
            return null;
        }
    }
}
