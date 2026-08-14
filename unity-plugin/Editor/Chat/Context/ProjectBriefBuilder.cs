using System;
using System.Security.Cryptography;
using System.Text;

namespace UnityMCP.Editor.Chat.Context
{
    /// <summary>
    /// Assembles a deterministic plain-text project brief on the Unity main thread.
    /// Section order: compile → console → hierarchy. Empty sections omitted.
    /// Truncation marked with …(truncated) at line boundary.
    /// NOTE: output is NOT byte-identical to the Python brief_build tool because
    /// each reads data via different paths. Never cross-compare section hashes.
    /// </summary>
    internal static class ProjectBriefBuilder
    {
        internal const int DefaultBudgetChars = 8000;  // ≈ 2000 tokens at 4 chars/tok

        private const int CompileMaxChars = 800;
        private const int ConsoleMaxChars = 1200;

#if UNITY_INCLUDE_TESTS
        internal static Func<string> CompileProviderOverride;
        internal static Func<string> ConsoleProviderOverride;
        internal static Func<string> SceneProviderOverride;
#endif

        internal static string Build(int budgetChars = DefaultBudgetChars)
        {
            var compileRaw = GetCompileContent();
            var consoleRaw = GetConsoleContent();
            var hierarchyRaw = GetHierarchyContent();

            var sb = new StringBuilder();
            var remaining = budgetChars;

            // [Compile] — critical, max CompileMaxChars
            if (!string.IsNullOrEmpty(compileRaw))
            {
                var section = NormalizeCompile(compileRaw);
                var truncated = Truncate(section, Math.Min(CompileMaxChars, remaining));
                sb.AppendLine("[Compile]");
                sb.AppendLine(truncated);
                remaining -= truncated.Length;
            }

            // [Console] — critical, max ConsoleMaxChars
            if (!string.IsNullOrEmpty(consoleRaw) && remaining > 0)
            {
                var truncated = Truncate(consoleRaw, Math.Min(ConsoleMaxChars, remaining));
                sb.AppendLine("[Console]");
                sb.AppendLine(truncated);
                remaining -= truncated.Length;
            }

            // [Hierarchy] — medium, remaining budget
            if (!string.IsNullOrEmpty(hierarchyRaw) && remaining > 0)
            {
                var truncated = Truncate(hierarchyRaw, remaining);
                sb.AppendLine("[Hierarchy]");
                sb.Append(truncated);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Returns a 12-char lowercase hex hash of sectionContent (sha256[:12]).</summary>
        internal static string SectionHash(string sectionContent)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sectionContent ?? ""));
            var hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            return hex.Substring(0, 12);
        }

        private static string GetCompileContent()
        {
#if UNITY_INCLUDE_TESTS
            if (CompileProviderOverride != null) return CompileProviderOverride();
#endif
            return CompileErrorCapture.GetErrors();
        }

        private static string GetConsoleContent()
        {
#if UNITY_INCLUDE_TESTS
            if (ConsoleProviderOverride != null) return ConsoleProviderOverride();
#endif
            return ConsoleCapture.GetLogs(10, "error,warning");
        }

        private static string GetHierarchyContent()
        {
#if UNITY_INCLUDE_TESTS
            if (SceneProviderOverride != null) return SceneProviderOverride();
#endif
            return HierarchySerializer.SerializeSummary();
        }

        private static string NormalizeCompile(string raw) =>
            raw.Contains("No compilation errors") ? "clean" : raw;

        private static string Truncate(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
                return text ?? "";
            var cut = text.Substring(0, maxChars);
            var lastNl = cut.LastIndexOf('\n');
            if (lastNl > 0) cut = cut.Substring(0, lastNl);
            return cut + "\n…(truncated)";
        }
    }
}
