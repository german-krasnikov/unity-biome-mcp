// T23: Scan history dir and load JSONL for one conversation.
using System;
using System.Collections.Generic;
using System.IO;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat.CLI
{
    internal struct BiomeConversationMeta
    {
        internal string Id;
        internal string Title;
        internal string Date;
        internal string BackendKind;
        internal string SessionId;
        internal int    TurnCount;
    }

    internal static class BiomeConversationStore
    {
        // Test seam — override in tests.
        internal static Func<string> HistoryDir = ProjectFingerprint.BiomeHistoryDir;

        private const int MaxEventsPerConversation = 200;

        /// <summary>Scan history dir, return metas newest-first, capped at maxCount.</summary>
        internal static BiomeConversationMeta[] Scan(int maxCount = 30)
        {
            var dir = HistoryDir();
            if (!Directory.Exists(dir)) return Array.Empty<BiomeConversationMeta>();

            string[] files;
            try { files = Directory.GetFiles(dir, "*.meta.json"); }
            catch { return Array.Empty<BiomeConversationMeta>(); }

            var list = new List<BiomeConversationMeta>();
            foreach (var file in files)
            {
                var meta = ParseMeta(file);
                if (meta.Id != null) list.Add(meta);
            }

            list.Sort((a, b) => string.CompareOrdinal(b.Date, a.Date));
            if (list.Count > maxCount) list.RemoveRange(maxCount, list.Count - maxCount);
            return list.ToArray();
        }

        /// <summary>Load JSONL lines for one conversation (capped at 200 events). Empty on missing.</summary>
        internal static string[] LoadEventLines(string convId)
        {
            var dir  = HistoryDir();
            var path = Path.Combine(dir, $"{convId}.jsonl");
            if (!File.Exists(path)) return Array.Empty<string>();
            try
            {
                var lines = new List<string>();
                using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
                string line;
                while ((line = reader.ReadLine()) != null && lines.Count < MaxEventsPerConversation)
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
                return lines.ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        private static BiomeConversationMeta ParseMeta(string path)
        {
            try
            {
                var json    = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var id      = JsonHelper.ExtractString(json, "id");
                if (string.IsNullOrEmpty(id)) return default;
                var info    = new FileInfo(path);
                return new BiomeConversationMeta
                {
                    Id          = id,
                    Title       = JsonHelper.ExtractString(json, "title") ?? "",
                    Date        = FormatDate(info.LastWriteTime),
                    BackendKind = JsonHelper.ExtractString(json, "backend") ?? "",
                    SessionId   = JsonHelper.ExtractString(json, "session_id") ?? "",
                    TurnCount   = JsonHelper.ExtractInt(json, "turn_count"),
                };
            }
            catch { return default; }
        }

        private static string FormatDate(DateTime dt) => dt.ToString("yyyy-MM-dd HH:mm");
    }
}
