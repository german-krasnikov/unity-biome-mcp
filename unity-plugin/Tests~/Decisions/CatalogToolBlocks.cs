using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Decisions.Tests
{
    // Shared catalog parsing for the TEMPLATE csargs oracle (ToolArgsAccessorTests) and its PascalCase
    // collision scan (ToolArgsPascalCaseTests): one TOOL/PYTOOL/INTERNAL block of dsl/tools.biome, with
    // every non-LOCAL ARG's source key (the WIRE rename when present, else the catalog ARG name).
    //
    // ToPascalCase is an INDEPENDENT re-implementation of the toolchain's case-transform filter
    // (biome-dsl commit e9d718f4, packages/kit/src/names.ts toPascalCase, wired into TEMPLATE refs by
    // packages/core/src/render.ts): split on a run of non-alphanumeric characters, drop the parts that
    // split produces empty (a leading/trailing/doubled separator), upper-case the first character of
    // every remaining part, lower-case nothing else, join with no separator. Nothing else -- it does not
    // special-case digits, and a part that starts with a digit (e.g. "3d_mode" -> "3dMode") is passed
    // through with that digit unchanged, which is why the structural tests below also check that every
    // generated identifier starts with a letter.
    internal static class CatalogToolBlocks
    {
        private static readonly Regex HeadRe = new Regex(@"^(?:TOOL|PYTOOL|INTERNAL) ([a-z][a-z0-9_]*)\b", RegexOptions.Multiline);
        private static readonly Regex ArgRe = new Regex(@"^  ARG ([A-Za-z][A-Za-z0-9_]*) (?:str|ostr|int|oint|float|ofloat|bool|obool|Any)\b(.*)$", RegexOptions.Multiline);
        private static readonly Regex SplitRe = new Regex("[^a-zA-Z0-9]+");

        internal readonly struct ArgKey
        {
            internal readonly string SourceKey;
            internal readonly bool IsWire;

            internal ArgKey(string sourceKey, bool isWire)
            {
                SourceKey = sourceKey;
                IsWire = isWire;
            }
        }

        internal readonly struct ToolBlock
        {
            internal readonly string Name;
            internal readonly List<ArgKey> Args;

            internal ToolBlock(string name, List<ArgKey> args)
            {
                Name = name;
                Args = args;
            }
        }

        // split on [^a-zA-Z0-9]+, drop empty parts, upper-case the first letter of each part, join.
        internal static string ToPascalCase(string s) =>
            string.Concat(SplitRe.Split(s).Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));

        internal static List<ToolBlock> Parse(string source)
        {
            var heads = HeadRe.Matches(source).Cast<Match>().ToList();
            var blocks = new List<ToolBlock>();
            for (var i = 0; i < heads.Count; i++)
            {
                var name = heads[i].Groups[1].Value;
                var blockStart = heads[i].Index;
                var blockEnd = i + 1 < heads.Count ? heads[i + 1].Index : source.Length;
                var block = source.Substring(blockStart, blockEnd - blockStart);
                var args = ArgRe.Matches(block).Cast<Match>()
                    .Where(m => !Regex.IsMatch(m.Groups[2].Value, @"(?:^|\s)LOCAL(?:\s|$)"))
                    .Select(m =>
                    {
                        var wire = Regex.Match(m.Groups[2].Value, "WIRE \"([^\"]+)\"");
                        return wire.Success ? new ArgKey(wire.Groups[1].Value, true) : new ArgKey(m.Groups[1].Value, false);
                    })
                    .ToList();
                blocks.Add(new ToolBlock(name, args));
            }
            return blocks;
        }
    }
}
