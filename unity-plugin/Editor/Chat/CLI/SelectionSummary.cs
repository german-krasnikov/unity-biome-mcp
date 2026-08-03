// Auto-include the active selection in a chat message.
// Pure static — no Unity Editor API beyond GetComponents (safe in EditMode tests).
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class SelectionSummary
    {
        private const int MaxComponents = 3;

        /// <summary>
        /// Returns "[Selection: /Path (Comp1, Comp2)]" or "" if go is null.
        /// Lists top MaxComponents non-Transform components; appends "..." if more exist.
        /// </summary>
        internal static string Summarize(GameObject go) => Summarize(go, "Selection");

        /// <summary>
        /// Returns "[{tag}: /Path (Comp1, Comp2)]" or "" if go is null.
        /// </summary>
        internal static string Summarize(GameObject go, string tag)
        {
            if (go == null || !go) return ""; // !go catches destroyed-but-non-null Unity refs

            var path = ComponentSerializer.GetPath(go);
            var comps = go.GetComponents<Component>();

            var sb = new StringBuilder("[");
            sb.Append(tag); sb.Append(": ");
            sb.Append(path);
            sb.Append(" #"); sb.Append(TransientObjectId.GetWireValue(go));

            // Collect non-Transform component names
            var names = new List<string>(comps.Length);
            foreach (var c in comps)
                if (c != null && !(c is Transform))
                    names.Add(c.GetType().Name);

            if (names.Count > 0)
            {
                sb.Append(" (");
                var shown = names.Count <= MaxComponents ? names.Count : MaxComponents;
                for (var i = 0; i < shown; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(names[i]);
                }
                if (names.Count > MaxComponents) sb.Append(", ...");
                sb.Append(")");
            }

            sb.Append("]");
            return sb.ToString();
        }

    }
}
