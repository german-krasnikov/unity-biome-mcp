// Parser for object mutation tool argsJson.
// Pure C# — no UnityEngine deps (noEngineReferences: true).
// Supports: set_property, set_property_delta, set_active, create_object,
//   delete_object, manage_component, set_parent, rename_object,
//   wire_event, batch, apply_scene_change.

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

        private static string ReadStringField(string json, string key) =>
            JsonFieldReader.ReadString(json, key);

        private static string ReadRawField(string json, string key) =>
            JsonFieldReader.ReadRaw(json, key);
    }
}
