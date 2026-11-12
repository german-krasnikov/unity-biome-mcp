// Scans CLI session files for the session picker popup.
// HomeDir/ProjectDir seams allow NUnit testing without real filesystem.
using System;
using System.Collections.Generic;
using System.IO;

namespace UnityMCP.Editor.Chat
{
    internal struct SessionEntry
    {
        internal string      Id;    // session UUID/filename-stem
        internal string      Title; // ai-title from JSONL, or "Untitled"
        internal string      Date;  // mtime formatted as "yyyy-MM-dd HH:mm"
        internal BackendKind Kind;
    }

    internal static class SessionScanner
    {
        // Test seams — replace in tests, reset after.
        internal static Func<string> HomeDir = () =>
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Unity project root (parent of Assets/). Override in tests.
        internal static Func<string> ProjectDir = () =>
            Path.GetDirectoryName(UnityEngine.Application.dataPath);

        /// <summary>
        /// Returns sessions for the given backend, sorted newest-first, capped at maxCount.
        /// </summary>
        internal static SessionEntry[] Scan(BackendKind kind, int maxCount = 20)
        {
            var dir = GetSessionDir(kind);
            if (dir == null || !Directory.Exists(dir)) return Array.Empty<SessionEntry>();

            var entries = new List<SessionEntry>();

            switch (BackendTable.Get(kind)?.SessionFormat)
            {
                case SessionFormat.Jsonl: ScanJsonlDir(dir, kind, entries); break;
                case SessionFormat.Codex: ScanCodexDir(dir, kind, entries); break;
                case SessionFormat.Agy:   ScanAgyDir(dir, kind, entries); break;
            }

            entries.Sort((a, b) => string.CompareOrdinal(b.Date, a.Date));

            if (entries.Count > maxCount)
                entries.RemoveRange(maxCount, entries.Count - maxCount);

            return entries.ToArray();
        }

        /// <summary>Returns the session directory for the given backend, or null if unsupported.</summary>
        internal static string GetSessionDir(BackendKind kind)
        {
            var row = BackendTable.Get(kind);
            if (row == null || row.SessionDir.Length == 0) return null; // OpenCode uses SQLite — not supported

            var parts = new string[row.SessionDir.Length + 1];
            parts[0] = HomeDir();
            for (int i = 0; i < row.SessionDir.Length; i++)
                parts[i + 1] = row.SessionDir[i] == "{cwd}" ? EncodeCwd(ProjectDir()) : row.SessionDir[i];
            return Path.Combine(parts);
        }

        // Claude CWD encoding: replace each '/' with '-'.
        // /Users/german/Work → -Users-german-Work (leading '/' becomes leading '-').
        internal static string EncodeCwd(string path)
        {
            if (string.IsNullOrEmpty(path)) return "-";
            var normalized = path.Replace('\\', '/');
            return normalized.Replace("/", "-");
        }

        // ── Private scanners ────────────────────────────────────────────────────

        private static void ScanJsonlDir(string dir, BackendKind kind, List<SessionEntry> out_)
        {
            string[] files;
            try { files = Directory.GetFiles(dir, "*.jsonl"); }
            catch { return; }

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var id   = Path.GetFileNameWithoutExtension(file);
                out_.Add(new SessionEntry
                {
                    Id    = id,
                    Title = ReadJsonlTitle(file),
                    Date  = FormatDate(info.LastWriteTime),
                    Kind  = kind,
                });
            }
        }

        private static void ScanCodexDir(string dir, BackendKind kind, List<SessionEntry> out_)
        {
            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch { return; }

            foreach (var sub in subdirs)
            {
                var info = new DirectoryInfo(sub);
                out_.Add(new SessionEntry
                {
                    Id    = info.Name,
                    Title = "Untitled",
                    Date  = FormatDate(info.LastWriteTime),
                    Kind  = kind,
                });
            }
        }

        // agy stores sessions as SQLite .db files; title is not accessible without parsing proto blobs.
        private static void ScanAgyDir(string dir, BackendKind kind, List<SessionEntry> out_)
        {
            string[] files;
            try { files = Directory.GetFiles(dir, "*.db"); }
            catch { return; }

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var id   = Path.GetFileNameWithoutExtension(file);
                out_.Add(new SessionEntry
                {
                    Id    = id,
                    Title = "Untitled",
                    Date  = FormatDate(info.LastWriteTime),
                    Kind  = kind,
                });
            }
        }

        /// <summary>Read first 20 lines of a JSONL file looking for ai-title.</summary>
        internal static string ReadJsonlTitle(string path)
        {
            try
            {
                using var reader = new StreamReader(path);
                int lineCount = 0;
                string line;
                while ((line = reader.ReadLine()) != null && lineCount < 20)
                {
                    lineCount++;
                    var title = ExtractAiTitle(line);
                    if (title != null) return title;
                }
            }
            catch { /* ignore — return Untitled */ }
            return "Untitled";
        }

        // Look for "type":"summary" / "subtype":"ai-title" patterns
        internal static string ExtractAiTitle(string jsonLine)
        {
            if (string.IsNullOrEmpty(jsonLine)) return null;

            // Pattern 1: {"type":"summary","subtype":"ai-title","value":"..."}
            var subtype = JsonHelper.ExtractString(jsonLine, "subtype");
            if (subtype == "ai-title")
            {
                var value = JsonHelper.ExtractString(jsonLine, "value");
                if (!string.IsNullOrEmpty(value)) return value;
            }

            // Pattern 2: {"type":"ai-title","value":"..."}
            var type = JsonHelper.ExtractString(jsonLine, "type");
            if (type == "ai-title")
            {
                var value = JsonHelper.ExtractString(jsonLine, "value");
                if (!string.IsNullOrEmpty(value)) return value;
            }

            return null;
        }

        private static string FormatDate(DateTime dt) =>
            dt.ToString("yyyy-MM-dd HH:mm");
    }
}
