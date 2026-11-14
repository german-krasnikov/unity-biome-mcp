// Parser for object mutation tool argsJson.
// Pure C# — no UnityEngine deps (noEngineReferences: true; UnityMCP.Editor.Chat.Parsers
// references nothing, so it cannot see the generated ToolUi.CardOf in UnityMCP.Editor).
// The caller (MutationDiffCard, in UnityMCP.Editor.Chat.View — which does reference
// UnityMCP.Editor) resolves the tool name to a MutationKind via ToolUi.CardOf before
// calling Parse; this parser only extracts fields from argsJson.

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
        internal static MutationArgs Parse(MutationKind kind, string argsJson)
        {
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

        private static string ReadStringField(string json, string key) =>
            JsonFieldReader.ReadString(json, key);

        private static string ReadRawField(string json, string key) =>
            JsonFieldReader.ReadRaw(json, key);
    }
}
