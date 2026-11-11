using System;
using System.Linq;
using UnityMCP.Playtest.Core.SharedTests;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The notation of the matcher tests: what a match bound, and where a failure was furthest, as short strings that a case can state on one line.
    internal static class MatchShow
    {
        /// <summary>"name=raw@column" for each slot, by name, then "+WORD" for each flag, by word, all joined by "; ". Nothing bound is "". No match (null) is "no match".</summary>
        public static string Bound(SlotBindings bindings) => bindings == null
            ? "no match"
            : string.Join("; ", bindings.Slots.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value.Raw + "@" + p.Value.Col)
                .Concat(bindings.Flags.OrderBy(f => f, StringComparer.Ordinal).Select(f => "+" + f)));

        /// <summary>What the reader read, as the reader in JavaScript prints a use: "PT_X 1-2: " the bindings of the head (as Bound), then " ITEMS " and the bindings of each item, joined by " || ". Lines are 1-based.</summary>
        public static string Use(LineUse use) =>
            use.Form.Construct + " " + (use.Index + 1) + "-" + (use.EndIndex + 1) + ": " + Bound(use.Head) + (use.Items.Count > 0 ? " ITEMS " + string.Join(" || ", use.Items.Select(Bound)) : "");

        /// <summary>What the reader read of a script, each use as Use, joined by " | ".</summary>
        public static string Uses(ReadResult result) => string.Join(" | ", result.Uses.Select(Use));

        /// <summary>What the reader said of a script, each diagnostic as ReaderHarness.Say, joined by " | ".</summary>
        public static string Said(ReadResult result) => string.Join(" | ", result.Diagnostics.Select(ReaderHarness.Say));

        /// <summary>"at N", the index of the token where the furthest failure was, then " slot name" when a slot was due or refused there and " word W" when a word was due.</summary>
        public static string Failed(MatchTrace trace) =>
            "at " + trace.At + (trace.Slot != null ? " slot " + trace.Slot.Text : "") + (trace.Lit != null ? " word " + trace.Lit : "");
    }
}
