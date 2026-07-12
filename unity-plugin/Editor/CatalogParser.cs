using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Parses the Python-pushed catalog text into category→tools map.
    ///
    /// Wire format (one line per category):
    ///   CORE:get_hierarchy,batch,inspect
    ///   SCENE:delete_object,find_objects,set_active
    ///   SYSTEM:
    /// </summary>
    internal static class CatalogParser
    {
        public static Dictionary<string, string[]> Parse(string text)
        {
            var result = new Dictionary<string, string[]>();
            if (string.IsNullOrEmpty(text)) return result;

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var colon = trimmed.IndexOf(':');
                if (colon < 0) continue;

                var key = trimmed.Substring(0, colon).Trim();
                var right = trimmed.Substring(colon + 1).Trim();

                string[] tools;
                if (string.IsNullOrEmpty(right))
                {
                    tools = Array.Empty<string>();
                }
                else
                {
                    var parts = right.Split(',');
                    var list = new List<string>(parts.Length);
                    foreach (var p in parts)
                    {
                        var t = p.Trim();
                        if (!string.IsNullOrEmpty(t)) list.Add(t);
                    }
                    tools = list.ToArray();
                }

                if (!string.IsNullOrEmpty(key))
                    result[key] = tools;
            }
            return result;
        }
    }
}
