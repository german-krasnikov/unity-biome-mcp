using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor.Roslyn
{
    /// <summary>Plain-text formatter for Roslyn diagnostics. Output matches server/tests/fixtures/roslyn_responses.txt.</summary>
    internal static class RoslynFormat
    {
        public static string FormatDiagnostics(IEnumerable diagnostics, long elapsedMs)
        {
            var errors = new List<string>();
            foreach (var d in diagnostics)
            {
                var severity = d.GetType().GetProperty("Severity")?.GetValue(d)?.ToString();
                if (severity == "Error") errors.Add(d.ToString());
            }

            if (errors.Count == 0)
                return $"OK preflight ({elapsedMs}ms)";

            var sb = new StringBuilder("ERR preflight\n");
            foreach (var e in errors) sb.AppendLine(e);
            return sb.ToString().TrimEnd();
        }
    }
}
