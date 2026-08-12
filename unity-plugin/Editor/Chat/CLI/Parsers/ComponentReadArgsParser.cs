// T2.4: Parser for get_component / inspect / get_components_list argsJson.
// Pure C# — noEngineReferences assembly; no UnityEngine deps.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal enum ReadToolKind { Unknown, GetComponent, Inspect, GetComponentsList }

    internal struct ComponentReadArgs
    {
        public ReadToolKind Kind;
        public bool IsValid;
        // get_component
        public string Path;           // single hierarchy path  e.g. "/Hero/Body"
        public string ComponentType;  // component type name    e.g. "Rigidbody"
        // inspect
        public string[] Paths;        // one or more paths (null when not applicable)
        public string[] Components;   // component filter; null = all components
        // get_components_list
        public string ObjectId;       // "$3E8" (hex) or "#123" (legacy decimal)
    }

    internal static class ComponentReadArgsParser
    {
        internal static ComponentReadArgs Parse(string toolName, string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson))
                return new ComponentReadArgs { Kind = ReadToolKind.Unknown, IsValid = false };

            switch (toolName)
            {
                case "get_component":      return ParseGetComponent(argsJson);
                case "inspect":            return ParseInspect(argsJson);
                case "get_components_list": return ParseGetComponentsList(argsJson);
                default:
                    return new ComponentReadArgs { Kind = ReadToolKind.Unknown, IsValid = false };
            }
        }

        private static ComponentReadArgs ParseGetComponent(string argsJson)
        {
            var path = JsonFieldReader.ReadString(argsJson, "path");
            var type = JsonFieldReader.ReadString(argsJson, "type");
            return new ComponentReadArgs
            {
                Kind          = ReadToolKind.GetComponent,
                IsValid       = !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(type),
                Path          = path,
                ComponentType = type,
            };
        }

        private static ComponentReadArgs ParseInspect(string argsJson)
        {
            var pathsStr = JsonFieldReader.ReadString(argsJson, "paths");
            if (string.IsNullOrEmpty(pathsStr))
                return new ComponentReadArgs { Kind = ReadToolKind.Inspect, IsValid = false };

            var paths = SplitTrimmed(pathsStr);
            if (paths.Length == 0)
                return new ComponentReadArgs { Kind = ReadToolKind.Inspect, IsValid = false };

            // "components" takes priority over "type" alias — mirrors ExecInspect behaviour
            var compStr = JsonFieldReader.ReadString(argsJson, "components")
                       ?? JsonFieldReader.ReadString(argsJson, "type");
            var components = string.IsNullOrEmpty(compStr) ? null : SplitTrimmed(compStr);

            return new ComponentReadArgs
            {
                Kind       = ReadToolKind.Inspect,
                IsValid    = true,
                Paths      = paths,
                Components = (components != null && components.Length > 0) ? components : null,
            };
        }

        private static ComponentReadArgs ParseGetComponentsList(string argsJson)
        {
            var id = JsonFieldReader.ReadString(argsJson, "id");
            return new ComponentReadArgs
            {
                Kind     = ReadToolKind.GetComponentsList,
                IsValid  = !string.IsNullOrEmpty(id),
                ObjectId = id,
            };
        }

        private static string[] SplitTrimmed(string s)
        {
            var parts = s.Split(',');
            var result = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0) result.Add(t);
            }
            return result.ToArray();
        }
    }
}
