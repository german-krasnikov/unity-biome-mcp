using System.Globalization;
using System.Text;

namespace UnityMCP.Editor
{
    /// <summary>
    /// One entry in a playtest's structured step ledger (B15/B16). A superset of the Player
    /// runtime's simpler {raw, passed, message} StepResult (PlayerPlaytestReceipts.cs:55-81) —
    /// kept richer here for Wave D's eventual Editor/Player convergence.
    /// </summary>
    internal readonly struct PlaytestStepReceipt
    {
        internal readonly int Index;
        internal readonly string Type;
        internal readonly double Ms;
        internal readonly string SourceFile;
        internal readonly int SourceLine;
        internal readonly bool RawPassed;
        internal readonly bool ExpectedFail;
        internal readonly bool ConsoleErrored;

        internal PlaytestStepReceipt(int index, string type, double ms, string sourceFile, int sourceLine,
            bool rawPassed, bool expectedFail, bool consoleErrored = false)
        {
            Index = index;
            Type = type;
            Ms = ms;
            SourceFile = sourceFile;
            SourceLine = sourceLine;
            RawPassed = rawPassed;
            ExpectedFail = expectedFail;
            ConsoleErrored = consoleErrored;
        }

        /// <summary>
        /// Effective pass/fail after expected-fail inversion. F7: a genuine console error is a
        /// structurally separate failure channel (C07) and must never be absorbed by EXPECT_FAIL
        /// inverting the step's own raw outcome — ConsoleErrored always forces Ok false.
        /// </summary>
        internal bool Ok => RawPassed != ExpectedFail && !ConsoleErrored;

        internal string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"index\":").Append(Index).Append(',');
            sb.Append("\"type\":\"").Append(EscapeJsonString(Type)).Append("\",");
            sb.Append("\"ok\":").Append(Ok ? "true" : "false").Append(',');
            sb.Append("\"ms\":").Append(Ms.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"source_file\":")
              .Append(SourceFile == null ? "null" : "\"" + EscapeJsonString(SourceFile) + "\"").Append(',');
            sb.Append("\"source_line\":").Append(SourceLine).Append(',');
            sb.Append("\"raw_passed\":").Append(RawPassed ? "true" : "false").Append(',');
            sb.Append("\"expected_fail\":").Append(ExpectedFail ? "true" : "false").Append(',');
            sb.Append("\"console_errored\":").Append(ConsoleErrored ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        internal static string EscapeJsonString(string value) => (value ?? "")
            .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
