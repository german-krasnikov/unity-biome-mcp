using System;
using System.Collections.Generic;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using Form = UnityMCP.Playtest.Core.PlaytestGrammar.Form;

namespace UnityMCP.Playtest.Core
{
    /// <summary>
    /// A command that the reader read: a line, or a block (a head, lines, END). Form is the form of the table that read it and Head what its slots hold (for a block, those of the head); Items is what each
    /// line of a block that was read holds, in order (empty for a line). Index is the index of the line (of the head) in the lines that were read, EndIndex the index of its last line, the END of a block or,
    /// if it has none, the last line there is. Col is the 0-based column of its first token, in the physical line, and Text the line as written without the blanks around it (the head, for a block).
    /// </summary>
    internal sealed class LineUse
    {
        public readonly Form Form;
        public readonly SlotBindings Head;
        public readonly IReadOnlyList<SlotBindings> Items;
        public readonly int Index;
        public readonly int EndIndex;
        public readonly int Col;
        public readonly string Text;

        public LineUse(Form form, SlotBindings head, IReadOnlyList<SlotBindings> items, int index, int endIndex, int col, string text)
        {
            Form = form;
            Head = head;
            Items = items;
            Index = index;
            EndIndex = endIndex;
            Col = col;
            Text = text;
        }
    }

    /// <summary>
    /// Reads a command of a .playtest script by the table PlaytestGrammar, as scanDeclarationLines of the reader of the SURFACE in JavaScript (uses.ts) does with the options that READ uses. The forms that
    /// can read a line are those whose first word is the first token of the line, as written, in the order of the table, and the first that reads it wins. If none does, the one whose reading got
    /// furthest says what is wrong (PlaytestDiagnostic), the first of two that got equally far. A block is a head, lines that are read by the lines of the block, and an END that is not indented deeper than
    /// the head; a head that is not read skips the block to its END. Blank lines and comments (the first token starts with #) are no commands. The reader knows the table and nothing else: what a command
    /// means is the business of whoever builds from it.
    /// </summary>
    internal static class PlaytestReader
    {
        // A way to read a line: the elements to match, and the form they belong to (an item of a block belongs to the block, for the construct and the name that a diagnostic gives).
        internal readonly struct Way
        {
            public readonly Form Form;
            public readonly IReadOnlyList<Element> Elements;

            public Way(Form form, IReadOnlyList<Element> elements)
            {
                Form = form;
                Elements = elements;
            }
        }

        private static readonly Way[] NoWays = new Way[0];
        private static readonly IReadOnlyList<SlotBindings> NoItems = new SlotBindings[0];

        // The ways to read a line, by its first word, made once.
        private static readonly Dictionary<string, Way[]> ByVerb = Index();

        private static Dictionary<string, Way[]> Index()
        {
            var lists = new Dictionary<string, List<Way>>(StringComparer.Ordinal);
            foreach (var form in PlaytestGrammar.Forms)
            {
                if (form.Elements[0].Kind != ElementKind.Word)
                    throw new NotSupportedException("The form " + form.Construct + " does not start with a word: the reader finds the forms of a line by its first word.");
                if (!lists.TryGetValue(form.Verb, out var list)) lists.Add(form.Verb, list = new List<Way>());
                list.Add(new Way(form, form.Elements));
            }
            var index = new Dictionary<string, Way[]>(StringComparer.Ordinal);
            foreach (var pair in lists) index.Add(pair.Key, pair.Value.ToArray());
            return index;
        }

        /// <summary>
        /// Reads the command that starts at lines[i] and moves i past it (past the END of a block, or to the end of the lines if a block has none). The command, or null when the line is none (a blank line or
        /// a comment, with nothing said) or no form reads it, in which case a diagnostic is added to `diags`; a block whose head is read is returned with the lines of it that are read, and a diagnostic
        /// for each of its lines that is not (and one at its head if it has no END, before those). `lineNos[k]` is the number of lines[k] in its source, which is not k + 1 for the lines an INCLUDE or a
        /// CALL brought. Does not throw for any text of a line.
        /// </summary>
        public static LineUse Read(string[] lines, int[] lineNos, ref int i, List<PlaytestDiagnostic> diags)
        {
            var at = i;
            var line = lines[at];
            var toks = PatternTokens.Split(line);
            i = at + 1;
            if (IsBlank(toks)) return null;
            var ways = ByVerb.TryGetValue(toks[0].Text, out var found) ? found : NoWays;
            var form = Take(ways, toks, line, at, lineNos[at], diags, out var head);
            if (form == null)
            {
                if (AllBlocks(ways)) i = AfterEnd(lines, at + 1, toks[0].Col);
                return null;
            }
            return form.IsBlock ? ReadBlock(form, head, lines, lineNos, at, toks, ref i, diags) : new LineUse(form, head, NoItems, at, at, toks[0].Col, TextOf(line, toks));
        }

        // The lines after the head up to an END that is not indented deeper than the head (INTENDED I11: one that is deeper is a line of the block); every line there is one of the block, to the last if no END comes.
        private static LineUse ReadBlock(Form block, SlotBindings head, string[] lines, int[] lineNos, int at, IReadOnlyList<PatternToken> headToks, ref int i, List<PlaytestDiagnostic> diags)
        {
            var mark = diags.Count;
            var items = new List<SlotBindings>();
            var j = at + 1;
            for (; j < lines.Length; j++)
            {
                var toks = PatternTokens.Split(lines[j]);
                if (IsBlank(toks)) continue;
                if (IsEnd(toks, headToks[0].Col)) break;
                if (Take(Starting(block, toks[0].Text), toks, lines[j], j, lineNos[j], diags, out var item) != null) items.Add(item);
            }
            var closed = j < lines.Length;
            // The head is before every line of the block, so what is said about it goes before what is said about them.
            if (!closed) diags.Insert(mark, PlaytestDiagnostic.MissingEnd(block, headToks[0], lineNos[at], at));
            var end = closed ? j : lines.Length - 1;
            i = end + 1;
            return new LineUse(block, head, items, at, end, headToks[0].Col, TextOf(lines[at], headToks));
        }

        // The first of the ways that reads the line, with what its slots hold (the form it belongs to is returned: null if none reads it), or a diagnostic: no way at all is an unknown first word; else the
        // way that got furthest (strictly: of two at one token the first) says what is wrong. Internal so that a test can give it ways of its own.
        internal static Form Take(Way[] ways, IReadOnlyList<PatternToken> toks, string line, int index, int lineNo, List<PlaytestDiagnostic> diags, out SlotBindings bound)
        {
            foreach (var way in ways)
            {
                bound = PatternMatcher.Match(way.Elements, toks, line, null);
                if (bound != null) return way.Form;
            }
            bound = null;
            if (ways.Length == 0)
            {
                diags.Add(PlaytestDiagnostic.UnknownKeyword(toks[0], lineNo, index));
                return null;
            }
            MatchTrace furthest = null;
            Form owner = null;
            foreach (var way in ways)
            {
                var trace = new MatchTrace();
                PatternMatcher.Match(way.Elements, toks, line, trace);
                if (furthest == null || trace.At > furthest.At)
                {
                    furthest = trace;
                    owner = way.Form;
                }
            }
            diags.Add(PlaytestDiagnostic.Refused(owner, furthest, toks, lineNo, index));
            return null;
        }

        // The lines of a block are read by the patterns of its items that their first word can start: one that starts with that word, or with no word.
        private static Way[] Starting(Form block, string word)
        {
            var ways = new List<Way>();
            foreach (var pattern in block.Items)
                if (pattern.Count == 0 || pattern[0].Kind != ElementKind.Word || pattern[0].Text == word) ways.Add(new Way(block, pattern));
            return ways.ToArray();
        }

        private static bool AllBlocks(Way[] ways)
        {
            foreach (var way in ways)
                if (!way.Form.IsBlock) return false;
            return ways.Length > 0;
        }

        // The index after the first END from `from` on (to the end of the lines if there is none), for a block whose head is not read.
        private static int AfterEnd(string[] lines, int from, int indent)
        {
            for (var j = from; j < lines.Length; j++)
                if (IsEnd(PatternTokens.Split(lines[j]), indent)) return j + 1;
            return lines.Length;
        }

        private static bool IsBlank(IReadOnlyList<PatternToken> toks) => toks.Count == 0 || toks[0].Text[0] == '#';

        private static bool IsEnd(IReadOnlyList<PatternToken> toks, int headIndent) => toks.Count == 1 && toks[0].Text == "END" && toks[0].Col <= headIndent;

        private static string TextOf(string line, IReadOnlyList<PatternToken> toks)
        {
            var last = toks[toks.Count - 1];
            return line.Substring(toks[0].Col, last.Col + last.Text.Length - toks[0].Col);
        }
    }
}
