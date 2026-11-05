using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityMCP.Playtest.Core.SharedTests
{
    /// <summary>One EXAMPLE of a dsl/constructs/*.examples file: the lines of a script (SOURCE) and what the reader must say about them (EXPECT; none: every line is read).</summary>
    internal sealed class ExampleBlock
    {
        public readonly string FileName;
        public readonly string Title;
        public readonly List<string> Source = new List<string>();
        public readonly List<string> Expect = new List<string>();

        public ExampleBlock(string fileName, string title)
        {
            FileName = fileName;
            Title = title;
        }
    }

    /// <summary>
    /// The playtest examples of the repository (dsl/constructs/playtest*.examples): the requirement examples that the reader in JavaScript and this front are both held to.
    /// A line that is no EXAMPLE, SOURCE or EXPECT stops the read, so that a change of the format cannot make a test pass by reading nothing.
    /// </summary>
    internal static class ExamplesFile
    {
        public static List<ExampleBlock> ReadAll()
        {
            var dir = Path.Combine(RepoRoot.Find(), "dsl", "constructs");
            var files = Directory.GetFiles(dir, "playtest*.examples");
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length == 0) throw new InvalidOperationException("No playtest*.examples in " + dir + ": a test over them would pass without checking anything.");
            var blocks = new List<ExampleBlock>();
            foreach (var path in files)
            {
                ExampleBlock block = null;
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("EXAMPLE ", StringComparison.Ordinal))
                        blocks.Add(block = new ExampleBlock(Path.GetFileName(path), line.Substring(8)));
                    else if (block != null && line.StartsWith("  SOURCE ", StringComparison.Ordinal))
                        block.Source.Add(line.Substring(9));
                    else if (block != null && line.StartsWith("  EXPECT ", StringComparison.Ordinal))
                        block.Expect.Add(line.Substring(9));
                    else if (line.Trim().Length > 0)
                        throw new InvalidOperationException(Path.GetFileName(path) + ": not an EXAMPLE, SOURCE or EXPECT line: " + line);
                }
            }
            return blocks;
        }
    }
}
