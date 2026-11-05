using System;
using System.Collections.Generic;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using Form = UnityMCP.Playtest.Core.PlaytestGrammar.Form;

namespace UnityMCP.Playtest.Core
{
    /// <summary>
    /// What the reader says about a line that no form reads, as the reader of the SURFACE in JavaScript does (uses.ts, `take`). Code: PT_UNKNOWN_KEYWORD for a first word that no form starts with;
    /// else the code of the slot whose value was of the wrong type, if the slot has one, else PT_SYNTAX. Line is the number the caller gave the line (its source's, 1-based), Col the 1-based column of
    /// the token the failure is at in the physical line, indent included, Index the index of the line in the lines that were read. Construct is the form whose reading went furthest (null for an unknown first
    /// word). Text is for a person; the code and the place are what the examples hold.
    /// </summary>
    internal readonly struct PlaytestDiagnostic
    {
        public readonly string Code;
        public readonly int Line;
        public readonly int Col;
        public readonly int Index;
        public readonly string Construct;
        public readonly string Text;

        public PlaytestDiagnostic(string code, int line, int col, int index, string construct, string text)
        {
            Code = code;
            Line = line;
            Col = col;
            Index = index;
            Construct = construct;
            Text = text;
        }

        /// <summary>No form starts with the first word of the line: `unknown keyword wait`.</summary>
        public static PlaytestDiagnostic UnknownKeyword(PatternToken token, int line, int index) =>
            new PlaytestDiagnostic(PlaytestGrammar.CodePrefix + "_UNKNOWN_KEYWORD", line, token.Col + 1, index, null, "unknown keyword " + token.Text);

        /// <summary>A block that has no END, at its head.</summary>
        public static PlaytestDiagnostic MissingEnd(Form block, PatternToken head, int line, int index) =>
            new PlaytestDiagnostic(PlaytestGrammar.CodePrefix + "_SYNTAX", line, head.Col + 1, index, block.Construct, Kw(block) + " block is missing END");

        /// <summary>
        /// The form whose reading of the line went furthest, `trace` being that failure: the line ends early (the last token), a token is not what the slot due takes (that token), or a token is
        /// one too many (that token). A value of the wrong type has the code of its slot, unless it is written in quotes: a text in quotes is no word for the grammar, so `WAIT "abc"` is a
        /// syntax error and not a bad number, as the hand parser said it.
        /// </summary>
        public static PlaytestDiagnostic Refused(Form form, MatchTrace trace, IReadOnlyList<PatternToken> toks, int line, int index)
        {
            var token = toks[Math.Min(trace.At, toks.Count - 1)];
            var early = trace.At >= toks.Count;
            var slot = trace.Slot;
            var kw = Kw(form);
            var code = slot != null && !early && !IsQuoted(token.Text) && slot.Code != null ? slot.Code : PlaytestGrammar.CodePrefix + "_SYNTAX";
            var text = early ? kw + ": the line ends early"
                : slot != null ? kw + ": \"" + token.Text + "\" is not " + SlotCheck.Describe(slot) + " (" + slot.Text + ")"
                : kw + ": unexpected \"" + token.Text + "\"";
            return new PlaytestDiagnostic(code, line, token.Col + 1, index, form.Construct, text);
        }

        /// <summary>How a message names a form: its leading words (`TRACE_FLOW FROM`), or its construct when it starts with no word.</summary>
        public static string Kw(Form form)
        {
            var words = new List<string>();
            foreach (var element in form.Elements)
            {
                if (element.Kind != ElementKind.Word) break;
                words.Add(element.Text);
            }
            return words.Count > 0 ? string.Join(" ", words) : form.Construct;
        }

        // A text in quotes. The tokenizer ends such a token at the second quote, so a token that starts and ends with a quote has no other (the JavaScript's /^"[^"]*"$/); one quote alone is no text.
        private static bool IsQuoted(string token) => token.Length >= 2 && token[0] == '"' && token[token.Length - 1] == '"';
    }
}
