using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    // S6: pull "Assets/Path.cs:42" out of a Unity stack trace's first in-project frame.
    // Frames outside Assets/ (Packages/, Library/) are skipped since they're not user code.
    internal static class ConsoleStackParser
    {
        private static readonly Regex FileLocationPattern = new Regex(@"\(at\s+(Assets/[^:]+):(\d+)\)");

        internal static string ExtractFileLocation(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return null;
            var match = FileLocationPattern.Match(stackTrace);
            return match.Success ? $"{match.Groups[1].Value}:{match.Groups[2].Value}" : null;
        }
    }
}
