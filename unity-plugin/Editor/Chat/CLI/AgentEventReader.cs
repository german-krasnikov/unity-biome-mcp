// T23: Convert JSONL history lines → List<TranscriptEntry> for display restore.
using System.Collections.Generic;
using System.Text;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat.CLI
{
    internal static class AgentEventReader
    {
        /// <summary>Convert JSONL event lines into TranscriptEntry list for RestoreFromReload.</summary>
        internal static List<TranscriptEntry> ReadEntries(string[] eventLines)
        {
            var result       = new List<TranscriptEntry>();
            var assistantBuf = new StringBuilder();
            TranscriptEntry? lastTool = null;

            foreach (var line in eventLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string kind;
                try { kind = JsonHelper.ExtractString(line, "kind"); }
                catch { continue; }
                if (string.IsNullOrEmpty(kind)) continue;

                var payloadJson = JsonHelper.ExtractObject(line, "payload");

                switch (kind)
                {
                    case "turn_started":
                    {
                        var text = JsonHelper.ExtractString(payloadJson, "text") ?? "";
                        result.Add(new TranscriptEntry
                        {
                            EntryKind = TranscriptEntry.Kind.User,
                            Text      = text,
                        });
                        assistantBuf.Clear();
                        break;
                    }
                    case "assistant_delta":
                    {
                        var text = JsonHelper.ExtractString(payloadJson, "text") ?? "";
                        assistantBuf.Append(text);
                        break;
                    }
                    case "turn_completed":
                    {
                        result.Add(new TranscriptEntry
                        {
                            EntryKind = TranscriptEntry.Kind.Assistant,
                            Text      = assistantBuf.ToString(),
                        });
                        assistantBuf.Clear();
                        break;
                    }
                    case "tool_call_started":
                    {
                        var name  = JsonHelper.ExtractString(payloadJson, "name") ?? "";
                        var id    = JsonHelper.ExtractString(payloadJson, "id") ?? "";
                        var entry = new TranscriptEntry
                        {
                            EntryKind  = TranscriptEntry.Kind.Tool,
                            Text       = name,
                            LlmPayload = id,
                        };
                        result.Add(entry);
                        lastTool = entry;
                        break;
                    }
                    case "tool_call_completed":
                    {
                        if (lastTool.HasValue)
                        {
                            var idx = result.Count - 1;
                            // Walk back to find the last Tool entry
                            for (int i = result.Count - 1; i >= 0; i--)
                            {
                                if (result[i].EntryKind == TranscriptEntry.Kind.Tool)
                                { idx = i; break; }
                            }
                            var e = result[idx];
                            e.ChipsData = "1";
                            result[idx] = e;
                        }
                        break;
                    }
                    case "tool_call_failed":
                    {
                        if (lastTool.HasValue)
                        {
                            var idx = result.Count - 1;
                            for (int i = result.Count - 1; i >= 0; i--)
                            {
                                if (result[i].EntryKind == TranscriptEntry.Kind.Tool)
                                { idx = i; break; }
                            }
                            var e = result[idx];
                            e.ChipsData = "0";
                            result[idx] = e;
                        }
                        break;
                    }
                }
            }
            return result;
        }
    }
}
