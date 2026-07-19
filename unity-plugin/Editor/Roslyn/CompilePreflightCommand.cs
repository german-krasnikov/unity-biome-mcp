using System;
using System.Diagnostics;

namespace UnityMCP.Editor
{
    /// <summary>compile_preflight handler — dry-compile C# source via Roslyn without writing files.</summary>
    internal static class CompilePreflightCommand
    {
        public static string Execute(string argsJson)
        {
            var filePath   = JsonHelper.ExtractString(argsJson, "file_path") ?? "";
            var newContent = JsonHelper.ExtractString(argsJson, "new_content") ?? "";
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
                var hints = Roslyn.UnityPreflightHints.Analyze(filePath, newContent);
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
