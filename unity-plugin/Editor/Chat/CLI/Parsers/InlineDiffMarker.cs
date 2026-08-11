// Inline diff highlighter: finds changed region in a line pair and marks it.
// Markers \x01/\x02 are inserted BEFORE SyntaxHighlighter runs so they survive
// <color=...> wrapping. FinalizeMarkers() converts them to <u></u> afterwards.
// Pure C#, no UnityEngine deps.
namespace UnityMCP.Editor.Chat.Parsers
{
    internal static class InlineDiffMarker
    {
        private const char Open  = '\x01';
        private const char Close = '\x02';

        /// <summary>
        /// Find the longest common prefix and suffix of two lines.
        /// Returns (prefix, suffix) character counts.
        /// The region [prefix .. len-suffix) is the changed part.
        /// </summary>
        internal static (int prefix, int suffix) FindChangeBounds(string oldLine, string newLine)
        {
            int minLen = oldLine.Length < newLine.Length ? oldLine.Length : newLine.Length;

            int prefix = 0;
            while (prefix < minLen && oldLine[prefix] == newLine[prefix])
                prefix++;

            int suffix = 0;
            int oldTail = oldLine.Length - 1;
            int newTail = newLine.Length - 1;
            // Don't let suffix overlap into the prefix region
            while (suffix < minLen - prefix &&
                   oldLine[oldTail - suffix] == newLine[newTail - suffix])
                suffix++;

            return (prefix, suffix);
        }

        /// <summary>
        /// Wrap the changed region of <paramref name="rawLine"/> with \x01/\x02 markers.
        /// </summary>
        internal static string InsertMarkers(string rawLine, int prefix, int suffix)
        {
            int end = rawLine.Length - suffix;
            return rawLine.Substring(0, prefix)
                 + Open
                 + rawLine.Substring(prefix, end - prefix)
                 + Close
                 + rawLine.Substring(end);
        }

        /// <summary>
        /// Replace \x01 → &lt;u&gt; and \x02 → &lt;/u&gt;.
        /// Call AFTER SyntaxHighlighter has run — markers survive color-tag wrapping.
        /// </summary>
        internal static string FinalizeMarkers(string highlighted)
            => highlighted.Replace("\x01", "<u>").Replace("\x02", "</u>");
    }
}
