// Generated from dsl/data/backends.data-contract, dsl/data/backends.core-data
// source-sha256=84c14f60890a5a5593076dd9e795e49cce9a6f1b093bf6abe88af12c4fd7ce37
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Chat
{
    public enum SessionFormat
    {
        Jsonl,
        Codex,
        Agy,
        None,
    }

    public static class SessionFormatValues
    {
        public const string Jsonl = "jsonl";
        public const string Codex = "codex";
        public const string Agy = "agy";
        public const string None = "none";
        public static readonly string[] Names = { Jsonl, Codex, Agy, None };
    }

    public enum BackendKind
    {
        Claude,
        Codex,
        Antigravity,
        Kimi,
        OpenCode,
    }

    public static class BackendKindValues
    {
        public const string Claude = "Claude";
        public const string Codex = "Codex";
        public const string Antigravity = "Antigravity";
        public const string Kimi = "Kimi";
        public const string OpenCode = "OpenCode";
        public static readonly string[] Names = { Claude, Codex, Antigravity, Kimi, OpenCode };
    }

    public sealed class Backend
    {
        public readonly BackendKind Kind;
        public readonly string Id;
        public readonly string Display;
        public readonly string Binary;
        public readonly string PrefKey;
        public readonly string Resume;
        public readonly bool ResumeCd;
        public readonly string[] SessionDir;
        public readonly SessionFormat SessionFormat;
        public readonly int ContextFallback;
        public Backend(
            BackendKind kind,
            string id,
            string display,
            string binary,
            string prefKey,
            string resume,
            bool resumeCd,
            string[] sessionDir,
            SessionFormat sessionFormat,
            int contextFallback)
        {
            this.Kind = kind;
            this.Id = id;
            this.Display = display;
            this.Binary = binary;
            this.PrefKey = prefKey;
            this.Resume = resume;
            this.ResumeCd = resumeCd;
            this.SessionDir = sessionDir;
            this.SessionFormat = sessionFormat;
            this.ContextFallback = contextFallback;
        }
    }

    public static class BackendRows
    {
        public static readonly Backend[] All =
        {
            new Backend(
                kind: BackendKind.Claude,
                id: "claude",
                display: "Claude",
                binary: "claude",
                prefKey: "UnityMCP_Chat_ClaudePath",
                resume: "claude --resume {id}",
                resumeCd: true,
                sessionDir: new string[] { ".claude", "projects", "{cwd}" },
                sessionFormat: SessionFormat.Jsonl,
                contextFallback: 200000
            ),
            new Backend(
                kind: BackendKind.Codex,
                id: "codex",
                display: "Codex",
                binary: "codex",
                prefKey: "UnityMCP_Chat_Path_codex",
                resume: "codex resume {id}",
                resumeCd: false,
                sessionDir: new string[] { ".codex", "sessions" },
                sessionFormat: SessionFormat.Codex,
                contextFallback: 1000000
            ),
            new Backend(
                kind: BackendKind.Antigravity,
                id: "antigravity",
                display: "Antigravity",
                binary: "agy",
                prefKey: "UnityMCP_Chat_Path_agy",
                resume: "agy --conversation {id}",
                resumeCd: false,
                sessionDir: new string[] { ".gemini", "antigravity-cli", "conversations" },
                sessionFormat: SessionFormat.Agy,
                contextFallback: 1000000
            ),
            new Backend(
                kind: BackendKind.Kimi,
                id: "kimi",
                display: "Kimi",
                binary: "kimi",
                prefKey: "UnityMCP_Chat_Path_kimi",
                resume: "kimi -S {id}",
                resumeCd: false,
                sessionDir: new string[] { ".kimi-code", "sessions" },
                sessionFormat: SessionFormat.Jsonl,
                contextFallback: 128000
            ),
            new Backend(
                kind: BackendKind.OpenCode,
                id: "opencode",
                display: "OpenCode",
                binary: "opencode",
                prefKey: "UnityMCP_Chat_Path_opencode",
                resume: "opencode -s {id}",
                resumeCd: false,
                sessionDir: new string[] { },
                sessionFormat: SessionFormat.None,
                contextFallback: 0
            ),
        };
    }
}
