using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace UnityMCP.Editor.TestRuns
{
    /// <summary>
    /// Immutable categories/assemblies/tests selection threaded from wire args
    /// (CommandRouter.AsyncRunTests) through TestRunService.Start into the
    /// durable request and run records.
    /// </summary>
    internal sealed class TestRunSelection
    {
        internal static readonly TestRunSelection Empty = new TestRunSelection(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        internal string[] Categories { get; }
        internal string[] Assemblies { get; }
        internal string[] Tests { get; }

        internal TestRunSelection(string[] categories, string[] assemblies, string[] tests)
        {
            Categories = categories ?? Array.Empty<string>();
            Assemblies = assemblies ?? Array.Empty<string>();
            Tests = tests ?? Array.Empty<string>();
        }

        /// <summary>
        /// THE single canonical selection hash. SHA-256 hex of
        /// "mode|filter|group|categories|assemblies|tests" where each array is
        /// sorted ordinal and newline-joined before the pipe-join. Python (A23)
        /// must reproduce this exact layout byte-for-byte to validate against it.
        /// </summary>
        internal static string ComputeSha256(
            string mode, string filter, string group, TestRunSelection selection)
        {
            var s = selection ?? Empty;
            var canonical = string.Join("|", new[]
            {
                mode ?? "",
                filter ?? "",
                group ?? "",
                Canonicalize(s.Categories),
                Canonicalize(s.Assemblies),
                Canonicalize(s.Tests),
            });
            using var algorithm = SHA256.Create();
            var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string Canonicalize(string[] values) =>
            string.Join("\n", values.OrderBy(v => v, StringComparer.Ordinal));
    }
}
