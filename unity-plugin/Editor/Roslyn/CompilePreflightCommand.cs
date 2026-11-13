using System;
using System.Diagnostics;

namespace UnityMCP.Editor.Roslyn
{
    /// <summary>compile_preflight handler — dry-compile C# source via Roslyn without writing files.</summary>
    internal static class CompilePreflightCommand
    {
        public static string Execute(string filePath, string newContent)
        {
            filePath ??= "";
            newContent ??= "";
            if (string.IsNullOrWhiteSpace(filePath))
                return "err: file_path is required";
            if (string.IsNullOrWhiteSpace(newContent))
                return "err: new_content is required";

            var sw = Stopwatch.StartNew();
            try
            {
                var diagnostics = RoslynWorkspace.GetDiagnostics(filePath, newContent);
                sw.Stop();
                var result = RoslynFormat.FormatDiagnostics(diagnostics, sw.ElapsedMilliseconds);
                var hints = UnityPreflightHints.Analyze(filePath, newContent);
                if (!string.IsNullOrEmpty(hints))
                    result += "\n" + hints;
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                var msg = ex.InnerException?.Message ?? ex.Message;
                if (msg.StartsWith("[ROSLYN UNAVAILABLE", StringComparison.Ordinal))
                    return msg;
                return $"[ROSLYN UNAVAILABLE: {msg}]";
            }
        }
    }
}
