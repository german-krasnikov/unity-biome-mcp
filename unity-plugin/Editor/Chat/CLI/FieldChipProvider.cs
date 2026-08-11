using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Chip kind for a single serialized field of a component or ScriptableObject asset.
    /// Key = "field". Path = "goPath|CompType|fieldName" or "Assets/file.asset|SOType|fieldName".
    /// CanHandle = false — created programmatically from Inspector context menu.
    /// Registered via ChipKindRegistry.EnsureBuiltIns().
    /// </summary>
    internal sealed class FieldChipProvider : IChipKindProvider
    {
        public string   Key              => ChipKindKeys.Field;
        public int      Priority         => 130;
        public string   IconName         => "d_FilterByLabel";
        public string   HexColor         => "#f59e0b";
        public string   DefaultDepth     => "summary";
        public string[] BarePathExtensions => System.Array.Empty<string>();

        public bool     CanHandle(Object obj, string assetPath) => false;
        public ChipData Create(Object obj, string assetPath)    => default;

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            if (ctx.Depth == "none") return "";

            var bracket = $"[{Key}:{chip.Path}]";
            if (ctx.Depth == "path") return bracket;

            var parts = chip.Path?.Split('|');
            if (parts == null || parts.Length < 3) return bracket + "\n(invalid field path)";

            var goPath    = parts[0];
            var compType  = parts[1];
            var fieldName = parts[2];

            // Asset paths (ScriptableObject) — "Assets/..." or "Packages/..."
            if (goPath.StartsWith("Assets/") || goPath.StartsWith("Packages/"))
            {
                var so = FindSO(goPath);
                if (so == null) return bracket + $"\n{fieldName}=(asset not found)";
                using var soObj = new SerializedObject(so);
                var soProp = soObj.FindProperty(fieldName);
                if (soProp == null) return bracket + $"\n{fieldName}=(not found)";
                return bracket + $"\n{fieldName}={ChipPropertyFormatter.Format(soProp)}";
            }

            // Scene paths — "/" prefix or "SceneName:/" multi-scene
            var go = FindObject(goPath);
            if (go == null) return bracket + $"\n{fieldName}=(object not found)";

            var comp = go.GetComponent(compType);
            if (comp == null) return bracket + $"\n{fieldName}=(component not found)";

            using var soComp = new SerializedObject(comp);
            var prop = soComp.FindProperty(fieldName);
            if (prop == null) return bracket + $"\n{fieldName}=(not found)";

            return bracket + $"\n{fieldName}={ChipPropertyFormatter.Format(prop)}";
        }

        // Test seams — replace with mocks to avoid scene/asset queries in unit tests.
        internal static System.Func<string, GameObject> FindObjectOverride;
#if UNITY_INCLUDE_TESTS
        internal static System.Func<string, ScriptableObject> FindSOOverride;
#endif

        private static GameObject FindObject(string path)
            => FindObjectOverride != null ? FindObjectOverride(path) : ComponentSerializer.FindObject(path);

        private static ScriptableObject FindSO(string path)
        {
#if UNITY_INCLUDE_TESTS
            if (FindSOOverride != null) return FindSOOverride(path);
#endif
            return AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        }

        public void Navigate(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return;
            var parts = reference.Split('|');
            var go    = FindObject(parts[0]);
            if (go == null) return;
            EditorGUIUtility.PingObject(go);
            Selection.activeGameObject = go;
        }

        public void Ping(string reference) => Navigate(reference);

        public void AppendContextMenuItems(DropdownMenu menu, string reference) { }
    }
}
