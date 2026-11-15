using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The forms that the tests of the matcher write by hand, one rule to a form: the elements as short calls (W a word, S a slot, G an optional group), and how a form reads a line in the
    // notation of MatchShow. The comment on a form in a test gives the same form as the pattern text of the reader in JavaScript (`X [WAIT <n:number>] <rest:text>`), which the case was run through.
    internal static class SyntheticForms
    {
        public static Element W(string word) => new Element(ElementKind.Word, word);

        public static Element S(string name, SlotType type = SlotType.Text, string arg = null) => new Element(ElementKind.Slot, name, type, arg);

        /// <summary>A slot of one token: <name:match(\S+)>.</summary>
        public static Element One(string name) => S(name, SlotType.Match, "\\S+");

        /// <summary>A text that ends before the first token that starts what follows it: <name:text LAZY>.</summary>
        public static Element Lazy(string name) => new Element(ElementKind.Slot, name, SlotType.Text, lazy: true);

        public static Element G(params Element[] items) => new Element(ElementKind.Group, null, items: items);

        public static SlotBindings Read(Element[] form, string line, MatchTrace trace = null) => PatternMatcher.Match(form, PatternTokens.Split(line), line, trace);

        /// <summary>What the form bound from the line, or "no match" (MatchShow.Bound).</summary>
        public static string Bound(Element[] form, string line) => MatchShow.Bound(Read(form, line));

        /// <summary>Where the furthest failure was (MatchShow.Failed), "matched" when the form reads the line.</summary>
        public static string Why(Element[] form, string line)
        {
            var trace = new MatchTrace();
            return Read(form, line, trace) == null ? MatchShow.Failed(trace) : "matched";
        }

        /// <summary>What the form bound (MatchShow.Bound), or "no match, " and where the furthest failure was (MatchShow.Failed).</summary>
        public static string Fate(Element[] form, string line)
        {
            var trace = new MatchTrace();
            var bound = Read(form, line, trace);
            return bound == null ? "no match, " + MatchShow.Failed(trace) : MatchShow.Bound(bound);
        }
    }
}
