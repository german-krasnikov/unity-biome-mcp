using System;
using System.Collections.Generic;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;

namespace UnityMCP.Playtest.Core
{
    /// <summary>
    /// The end of a LAZY text, as lazyEnd of the reader of the SURFACE in JavaScript (pattern-lazy.ts) finds it. A text takes the rest of the line unless a word follows it in its own list; a LAZY one
    /// that no word follows instead ends before the first token that starts what follows it: the first word of each optional group right after it, then what must come, a word or a slot.
    /// Inside a group, what follows the group counts too (`after` in the matcher), or `[AS &lt;label LAZY&gt; [TIMEOUT &lt;n&gt;]]` would not stop at TIMEOUT.
    /// </summary>
    internal static class PatternLazy
    {
        /// <summary>
        /// The index of the last token of the LAZY text that starts at token `ti`, or -1 when the line ends early. `ahead` is what follows the text, in order. The text is at least its first
        /// token, and that one is never what ends it (`ASSERT /x|C|f == TIMEOUT` has the value TIMEOUT). It ends before the first later token that starts what follows: the first word of an
        /// optional group of `ahead` that comes before the first element that is no group, or that element itself, a word or a slot that takes the token. If no token does, the text ends with
        /// the line when nothing must come (`ahead` is only groups), and else the line ends early: -1, and `trace`, when given, notes the word or the slot that is due there.
        /// </summary>
        public static int LazyEnd(IReadOnlyList<Element> ahead, IReadOnlyList<PatternToken> toks, int ti, MatchTrace trace)
        {
            // The element that must come: the first of `ahead` that is no group (ahead.Count: none).
            var need = 0;
            while (need < ahead.Count && ahead[need].Kind == ElementKind.Group) need++;
            if (need < ahead.Count && ahead[need].Kind != ElementKind.Word && ahead[need].Kind != ElementKind.Slot)
                throw new NotSupportedException("The reader does not know the element kind " + ahead[need].Kind + ".");
            var k = ti + 1;
            while (k < toks.Count && !Starts(ahead, need, toks[k].Text)) k++;
            if (trace != null) trace.Work += need + 1 + (k - ti - 1);
            if (k < toks.Count || need == ahead.Count) return k - 1;
            var due = ahead[need];
            trace?.Note(k, due.Kind == ElementKind.Slot ? due : null, due.Kind == ElementKind.Word ? due.Text : null);
            return -1;
        }

        // Whether the token ends the text: the first word of a group before `need`, or what must come (ahead[need]), a word or a slot that takes the token.
        private static bool Starts(IReadOnlyList<Element> ahead, int need, string token)
        {
            for (var n = 0; n < need; n++)
            {
                var items = ahead[n].Items;
                if (items.Count > 0 && items[0].Kind == ElementKind.Word && items[0].Text == token) return true;
            }
            if (need == ahead.Count) return false;
            var due = ahead[need];
            return due.Kind == ElementKind.Word ? due.Text == token : SlotCheck.Accepts(due, token);
        }

        /// <summary>What follows the element before `from` in `list`: the rest of the list, then `after`, what follows the group that holds the list. The same list as `after` itself when no element of `list` is left.</summary>
        public static IReadOnlyList<Element> Ahead(IReadOnlyList<Element> list, int from, IReadOnlyList<Element> after)
        {
            if (from >= list.Count) return after;
            var all = new Element[list.Count - from + after.Count];
            for (var n = from; n < list.Count; n++) all[n - from] = list[n];
            for (var n = 0; n < after.Count; n++) all[list.Count - from + n] = after[n];
            return all;
        }

        /// <summary>
        /// Whether the group holds a LAZY text, directly or in a group in it: what follows such a group follows that text too. The table marks Lazy on slots only, so a group is asked,
        /// where pattern.ts sets `lazy` on the group itself when a slot in it is lazy.
        /// </summary>
        public static bool HoldsLazy(Element group)
        {
            foreach (var item in group.Items)
                if (item.Kind == ElementKind.Slot ? item.Lazy : item.Kind == ElementKind.Group && HoldsLazy(item)) return true;
            return false;
        }
    }
}
