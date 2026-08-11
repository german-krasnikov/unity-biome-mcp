using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Chip kind for a component on a GameObject.
    /// Key = "component". Path = "goPath|CompType".
    /// CanHandle = false — created programmatically.
    /// </summary>
    internal sealed class ComponentChipProvider : IChipKindProvider
    {
        public string   Key              => ChipKindKeys.Component;
        public int      Priority         => 125;
        public string   IconName         => "d_Transform Icon";
        public string   HexColor         => "#38bdf8";
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
            if (parts == null || parts.Length < 2) return bracket + "\n(invalid component path)";

            var goPath   = parts[0];
            var compType = parts[1];

            var go = FindObject(goPath);
            if (go == null) return bracket + "\n(object not found)";

            var comp = go.GetComponent(compType);
            if (comp == null) return bracket + "\n(component not found)";

            using var so = new SerializedObject(comp);
            var prop = so.GetIterator();
            var sb   = new StringBuilder(bracket);
            int count = 0;
            int limit = ctx.Depth == "full" ? int.MaxValue : 10;

            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.name == "m_Script") continue;
                    if (count >= limit) { sb.Append("\n..."); break; }
                    sb.Append($"\n{prop.name}={ChipPropertyFormatter.Format(prop)}");
                    count++;
                }
                while (prop.NextVisible(false));
            }

            return sb.ToString();
        }

        // Test seam: replace to avoid scene queries in unit tests.
        internal static System.Func<string, GameObject> FindObjectOverride;

        private static GameObject FindObject(string path)
            => FindObjectOverride != null ? FindObjectOverride(path) : ComponentSerializer.FindObject(path);

        public void Navigate(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return;
            var parts = reference.Split('|');
            var go = FindObject(parts[0]);
            if (go == null) return;
            EditorGUIUtility.PingObject(go);
            Selection.activeGameObject = go;
        }

        public void Ping(string reference) => Navigate(reference);

        public void AppendContextMenuItems(DropdownMenu menu, string reference) { }
    }
}
