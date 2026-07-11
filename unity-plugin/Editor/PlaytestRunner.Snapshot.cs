using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    internal static partial class PlaytestRunner
    {
        /// <summary>
        /// Build a compact data snapshot for the failing step.
        /// Extracts $sigil names from RawLine, reads their current values, appends recent console errors.
        /// Called only when snapshotOnFailure=true. Unresolvable sigils are silently skipped.
        /// </summary>
        internal static string BuildFailureSnapshot(PlaytestStep step, PlaytestConfig config)
        {
            var sb = new StringBuilder("snapshot:");

            foreach (Match m in PlaytestParser.SigilRegex.Matches(step.RawLine ?? ""))
            {
                var name = m.Groups[1].Value;
                try
                {
                    var (p, c, f) = PlaytestParser.ResolveQuery("$" + name, config);
                    sb.Append($"\n  ${name}={ReadValue(p, c, f)}");
                }
                catch { /* skip unresolvable sigils */ }
            }

            var errs = ConsoleCapture.GetLogs(3, "error");
            if (!string.IsNullOrEmpty(errs))
                sb.Append($"\n  console_errors: {errs.Replace("\n", " | ")}");

            return sb.ToString();
        }
    }
}
