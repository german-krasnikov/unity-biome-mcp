using System;
using System.Collections.Generic;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;

namespace UnityMCP.Playtest.Core
{
    /// <summary>The text that a slot read, as written, and the 0-based column of its first character in the physical line.</summary>
    internal readonly struct SlotValue
    {
        public readonly string Raw;
        public readonly int Col;

        public SlotValue(string raw, int col) { Raw = raw; Col = col; }
    }

    /// <summary>
    /// What a form read from a line: the value of each slot that it reached, by the name of the slot, and the flags: a group of only words that matched is the flag
    /// named by its first word (SUM, EXPECT, ABORT).
    /// </summary>
    internal sealed class SlotBindings
    {
        public readonly Dictionary<string, SlotValue> Slots = new Dictionary<string, SlotValue>();
        public readonly HashSet<string> Flags = new HashSet<string>();
    }

    /// <summary>
    /// Where a failed match went furthest, to tell what is wrong with a line that no form reads. At: the index of the token (the number of tokens when the line ended early; -1 before
    /// any failure). Slot: the slot that was due there or refused the token. Lit: the word that was due. Of two failures at one token the first stays. Work: the elements visited plus
    /// the tokens scanned for a word, a count that a test can hold to linear without a clock.
    /// </summary>
    internal sealed class MatchTrace
    {
        public int At = -1;
        public Element Slot;
        public string Lit;
        public long Work;
    }

    /// <summary>
    /// Matches the tokens of a line against the elements of a form as matchPattern of the reader of the SURFACE in JavaScript (pattern.ts) does with the options that READ uses. A word is
    /// a token as written. A slot is one token that SlotCheck accepts, but a Text, which is the rest of the line or, with a word after it in its own list, the tokens before the first
    /// token that is that word (at least one). An optional group is read with what follows it and left out when that fails, unless its first element is a word that is the token in hand:
    /// then it must match whole. A group of only words that matched is a flag. Every token must be used. The end of a Lazy text with no word after it is PatternLazy (step 7.5 H1.1);
    /// until that is written, such a slot throws NotSupportedException.
    /// </summary>
    internal static class PatternMatcher
    {
        static PatternMatcher()
        {
            // The one reading of the SURFACE line that the front implements: forms in order, tokens as words, a comment by line. Another one in the table means lines read as they are not meant.
            var implemented = PlaytestGrammar.Ordered && PlaytestGrammar.TokensWords && PlaytestGrammar.CommentLine;
            if (!implemented)
                throw new NotSupportedException("The SURFACE of the playtest grammar is not the one the reader implements: Ordered, TokensWords and CommentLine must all be true.");
        }

        /// <summary>The bindings if the form reads the line, else null. `toks` are the tokens of `line` (PatternTokens.Split); `trace`, if given, receives the furthest failure and the work.</summary>
        public static SlotBindings Match(IReadOnlyList<Element> els, IReadOnlyList<PatternToken> toks, string line, MatchTrace trace)
        {
            var run = new Run(toks, line, trace);
            var bound = run.Go(els, 0, 0, Nothing, (ti, acc) => ti == toks.Count ? acc : run.Fail(ti));
            return bound == null ? null : Collect(bound);
        }

        // The bindings so far, newest first: a branch that fails drops its own and shares the rest, so nothing is copied or taken back.
        private sealed class Bound
        {
            public readonly string Name;
            public readonly SlotValue Value;
            public readonly bool IsFlag;
            public readonly Bound Prev;

            public Bound(string name, SlotValue value, bool isFlag, Bound prev) { Name = name; Value = value; IsFlag = isFlag; Prev = prev; }
        }

        // No binding yet. null is "no match", so "nothing bound" needs a value of its own.
        private static readonly Bound Nothing = new Bound(null, default, false, null);

        private static SlotBindings Collect(Bound bound)
        {
            var result = new SlotBindings();
            for (var b = bound; b != Nothing; b = b.Prev)
            {
                if (b.IsFlag)
                    result.Flags.Add(b.Name);
                else if (!result.Slots.ContainsKey(b.Name))
                    result.Slots.Add(b.Name, b.Value);   // newest first, so the last value of a name stays
            }
            return result;
        }

        private static bool OnlyWords(IReadOnlyList<Element> items)
        {
            foreach (var item in items)
                if (item.Kind != ElementKind.Word) return false;
            return items.Count > 0;
        }

        // One match: the tokens and the line it works on, and the trace it reports to. A branch is a call that returns the bindings with its own added, or null.
        private sealed class Run
        {
            private readonly IReadOnlyList<PatternToken> toks;
            private readonly string line;
            private readonly MatchTrace trace;

            public Run(IReadOnlyList<PatternToken> toks, string line, MatchTrace trace) { this.toks = toks; this.line = line; this.trace = trace; }

            /// <summary>Notes a failure if it is further than the ones before it, and says "no match".</summary>
            public Bound Fail(int at, Element slot = null, string lit = null)
            {
                if (trace != null && at > trace.At)
                {
                    trace.At = at;
                    trace.Slot = slot;
                    trace.Lit = lit;
                }
                return null;
            }

            /// <summary>Matches list[li..] from token ti on, then `rest` (what comes after the list); the bindings of the whole, or null.</summary>
            public Bound Go(IReadOnlyList<Element> list, int li, int ti, Bound acc, Func<int, Bound, Bound> rest)
            {
                if (trace != null) trace.Work++;
                if (li == list.Count) return rest(ti, acc);
                var el = list[li];
                switch (el.Kind)
                {
                    case ElementKind.Word:
                        return ti < toks.Count && toks[ti].Text == el.Text ? Go(list, li + 1, ti + 1, acc, rest) : Fail(ti, null, el.Text);
                    case ElementKind.Slot:
                        return Slot(list, li, ti, acc, rest);
                    case ElementKind.Group:
                        return Group(list, li, ti, acc, rest);
                    default:
                        throw new NotSupportedException("The reader does not know the element kind " + el.Kind + ".");
                }
            }

            private Bound Slot(IReadOnlyList<Element> list, int li, int ti, Bound acc, Func<int, Bound, Bound> rest)
            {
                var el = list[li];
                if (ti >= toks.Count) return Fail(ti, el);
                var end = ti;
                if (el.Type == SlotType.Text)
                {
                    end = toks.Count - 1;
                    // A word after the text in its own list (not past a group) ends it: before the first token that is that word, and the text is at least one token.
                    var stop = li + 1 < list.Count && list[li + 1].Kind == ElementKind.Word ? list[li + 1].Text : null;
                    if (stop != null)
                    {
                        var k = ti;
                        while (k < toks.Count && toks[k].Text != stop) k++;
                        if (trace != null) trace.Work += k - ti;
                        if (k == ti) return Fail(ti, el);
                        if (k == toks.Count) return Fail(toks.Count, null, stop);
                        end = k - 1;
                    }
                    else if (el.Lazy)
                        throw new NotSupportedException("The end of the LAZY text " + el.Text + " is PatternLazy (step 7.5 H1.1), which is not written yet.");
                }
                var first = toks[ti];
                var last = toks[end];
                var raw = el.Type == SlotType.Text ? line.Substring(first.Col, last.Col + last.Text.Length - first.Col) : first.Text;
                if (!SlotCheck.Accepts(el, raw)) return Fail(ti, el);
                return Go(list, li + 1, end + 1, new Bound(el.Text, new SlotValue(raw, first.Col), false, acc), rest);
            }

            private Bound Group(IReadOnlyList<Element> list, int li, int ti, Bound acc, Func<int, Bound, Bound> rest)
            {
                var items = list[li].Items;
                var head = items.Count > 0 ? items[0] : null;
                var flag = OnlyWords(items) ? head.Text : null;
                // READ commits: a group whose first word is the token in hand must match whole; it is not left out so that the token is read some other way.
                var committed = head != null && head.Kind == ElementKind.Word && ti < toks.Count && toks[ti].Text == head.Text;
                var taken = Go(items, 0, ti, acc, (t2, a2) => Go(list, li + 1, t2, flag == null ? a2 : new Bound(flag, default, true, a2), rest));
                return taken ?? (committed ? null : Go(list, li + 1, ti, acc, rest));
            }
        }
    }
}
