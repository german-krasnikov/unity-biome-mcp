// Pure path extractor for screenshot tool results.
// No UnityEngine deps — lives in noEngineReferences Parsers assembly.
// Pattern priority (first match wins):
//   1. "Data saved to: /abs/path.png"
//   2. "Baseline saved: /abs/path.png"
//   3. "[img:/abs/path.png]"
using System;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal static class ScreenshotResultParser
    {
        /// <summary>Extract absolute image path from any screenshot tool result. Returns null on no match.</summary>
        internal static string ExtractPath(string resultText)
        {
            if (string.IsNullOrEmpty(resultText)) return null;

            var path = ExtractAfterMarker(resultText, "Data saved to: ");
            if (path != null) return path;

            path = ExtractAfterMarker(resultText, "Baseline saved: ");
            if (path != null) return path;

            return ExtractImgMarker(resultText);
        }

        private static string ExtractAfterMarker(string text, string marker)
        {
            var i = text.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return null;
            var start = i + marker.Length;
            var end   = text.IndexOf('\n', start);
            var raw   = end < 0 ? text.Substring(start) : text.Substring(start, end - start);
            return raw.Trim();
        }

        private static string ExtractImgMarker(string text)
        {
            const string open = "[img:";
            var i = text.IndexOf(open, StringComparison.Ordinal);
            if (i < 0) return null;
            var start = i + open.Length;
            var end   = text.IndexOf(']', start);
            if (end <= start) return null;
            return text.Substring(start, end - start).Trim();
        }
    }
}
