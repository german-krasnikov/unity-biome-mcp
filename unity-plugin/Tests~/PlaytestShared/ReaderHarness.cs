using System.Collections.Generic;

namespace UnityMCP.Playtest.Core.SharedTests
{
    /// <summary>What the reader made of a script: the commands it read, and what it said about the lines it could not.</summary>
    internal sealed class ReadResult
    {
        public readonly List<LineUse> Uses = new List<LineUse>();
        public readonly List<PlaytestDiagnostic> Diagnostics = new List<PlaytestDiagnostic>();
    }

    /// <summary>
    /// Drives the reader the way a host will: from the first line, Read after Read until the lines are used up. The lines of a script are numbered from 1, as the examples numbers them.
    /// A diagnostic is shown as the examples write it, `PT_SYNTAX@1:23` (code, line, column), or with its text.
    /// </summary>
    internal static class ReaderHarness
    {
        public static ReadResult Read(string script) => Read(script.Split('\n'));

        public static ReadResult Read(string[] lines)
        {
            var numbers = new int[lines.Length];
            for (var k = 0; k < numbers.Length; k++) numbers[k] = k + 1;
            return Read(lines, numbers);
        }

        public static ReadResult Read(string[] lines, int[] lineNos)
        {
            var result = new ReadResult();
            for (var i = 0; i < lines.Length;)
            {
                var before = i;
                var use = PlaytestReader.Read(lines, lineNos, ref i, result.Diagnostics);
                if (i <= before) throw new System.InvalidOperationException("Read did not move past line " + before + ": a host that loops on it would not end.");
                if (use != null) result.Uses.Add(use);
            }
            return result;
        }

        /// <summary>"PT_SYNTAX@1:23".</summary>
        public static string Place(PlaytestDiagnostic d) => d.Code + "@" + d.Line + ":" + d.Col;

        /// <summary>"PT_SYNTAX@1:23 ASSERT: the line ends early".</summary>
        public static string Say(PlaytestDiagnostic d) => Place(d) + " " + d.Text;
    }
}
