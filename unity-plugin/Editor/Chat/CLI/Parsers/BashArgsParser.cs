// T2.3a: Parser for Bash tool argsJson.
// Extracts command (required) and description (optional, human-readable label).
// Pure C# — no UnityEngine deps (lives in noEngineReferences Parsers assembly).
namespace UnityMCP.Editor.Chat.Parsers
{
    internal struct BashArgs
    {
        public string Command;      // the shell command string
        public string Description;  // optional human-readable label; null when absent
        public bool   IsValid;      // false when command key is absent
    }

    internal static class BashArgsParser
    {
        public static BashArgs Parse(string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson))
                return new BashArgs { IsValid = false };

            var command = JsonFieldReader.ReadString(argsJson, "command");
            if (command == null)
                return new BashArgs { IsValid = false };

            return new BashArgs
            {
                Command     = command,
                Description = JsonFieldReader.ReadString(argsJson, "description"),
                IsValid     = true,
            };
        }
    }
}
