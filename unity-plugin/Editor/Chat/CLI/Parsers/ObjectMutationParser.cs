// Parser for object mutation tool argsJson.
// Pure C# — no UnityEngine deps (noEngineReferences: true).
// Supports: set_property, set_property_delta, set_active, create_object,
//   delete_object, manage_component, set_parent, rename_object,
//   wire_event, batch, apply_scene_change.
using System.Text;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal enum MutationKind
    {
        Unknown, SetProperty, CreateObject, DeleteObject, RenameObject, ManageComponent
    }

    internal struct MutationArgs
    {
        public MutationKind Kind;
        public string Path, Property, Value, Name, OldName, NewName;
        public bool IsValid;
    }

    internal static class ObjectMutationParser
    {
        internal static MutationArgs Parse(string toolName, string argsJson)
        {
            var kind = ToolToKind(toolName);

            if (string.IsNullOrEmpty(argsJson))
                return new MutationArgs { Kind = kind };

            var trimmed = argsJson.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] != '{')
                return new MutationArgs { Kind = kind };

            return new MutationArgs
            {
                Kind     = kind,
                Path     = ReadStringField(argsJson, "path"),
                Property = ReadStringField(argsJson, "prop"),
                Value    = ReadRawField(argsJson, "value"),
                Name     = ReadStringField(argsJson, "name"),
                OldName  = ReadStringField(argsJson, "old_name"),
                NewName  = ReadStringField(argsJson, "new_name"),
                IsValid  = true,
            };
        }

        // ── toolName → MutationKind ──────────────────────────────────────────
        private static MutationKind ToolToKind(string toolName)
        {
            switch (toolName)
            {
                case "set_property":
                case "set_property_delta":
                case "set_active":
                    return MutationKind.SetProperty;
                case "create_object":
                    return MutationKind.CreateObject;
                case "delete_object":
                    return MutationKind.DeleteObject;
                case "rename_object":
                    return MutationKind.RenameObject;
                case "manage_component":
                    return MutationKind.ManageComponent;
                default:
                    return MutationKind.Unknown;
            }
        }

        // ── String field reader (returns decoded string or null) ─────────────
        private static string ReadStringField(string json, string key)
        {
            var needle = "\"" + key + "\":";
            int idx = json.IndexOf(needle, System.StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += needle.Length;
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length || json[idx] != '"') return null;
            idx++;
            var sb = new StringBuilder();
            while (idx < json.Length)
            {
                char c = json[idx++];
                if (c == '\\' && idx < json.Length)
                {
                    char esc = json[idx++];
                    switch (esc)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(esc);  break;
                    }
                }
                else if (c == '"') break;
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ── Raw field reader — handles strings, numbers, booleans, arrays ────
        // For strings: returns decoded value (without quotes).
        // For arrays/objects/primitives: returns raw JSON token.
        private static string ReadRawField(string json, string key)
        {
            var needle = "\"" + key + "\":";
            int idx = json.IndexOf(needle, System.StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += needle.Length;
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length) return null;

            char first = json[idx];

            // String value — delegate to string reader for proper escape handling
            if (first == '"') return ReadStringField(json, key);

            // Array or object — scan to balanced close
            if (first == '[' || first == '{')
            {
                char open  = first;
                char close = (open == '[') ? ']' : '}';
                int depth = 0, start = idx;
                while (idx < json.Length)
                {
                    char c = json[idx];
                    if (c == open)  depth++;
                    else if (c == close) { depth--; if (depth == 0) { idx++; break; } }
                    else if (c == '"') // skip string content
                    {
                        idx++;
                        while (idx < json.Length)
                        {
                            char sc = json[idx++];
                            if (sc == '\\') idx++;
                            else if (sc == '"') break;
                        }
                        continue;
                    }
                    idx++;
                }
                return json.Substring(start, idx - start);
            }

            // Primitive (number, true, false, null) — scan to , or }
            {
                int start = idx;
                while (idx < json.Length && json[idx] != ',' && json[idx] != '}') idx++;
                return json.Substring(start, idx - start).Trim();
            }
        }
    }
}
