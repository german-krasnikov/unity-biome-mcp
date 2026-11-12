// Generated from dsl/constructs/playtest.construct
// source-sha256=4488af3986367387fcc4ee2da4c52b1cc5059494957837c1c6437cd9289a6006
// Edit the source, then run biome generate.

// The reader of a SURFACE: the table of its forms (Keywords, Forms) and the front that reads a line by it. C# 9, no reference but the base library.
#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using Form = UnityMCP.Playtest.Core.PlaytestGrammar.Form;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;

namespace UnityMCP.Playtest.Core
{
    // The grammar of a SURFACE as data: its forms, for a reader that matches a line against them. A form is a line, or a block (a head, lines, END),
    // made of words, slots and optional groups; the reader tokenizes as the SURFACE says (see the constants, which are in the other part of this class).
    public static partial class PlaytestGrammar
    {
        /// <summary>What a slot takes: the type of a &lt;name:type&gt; of the construct.</summary>
        public enum SlotType
        {
            /// <summary>The rest of the line as written; a Lazy one ends before the first word that starts what follows it.</summary>
            Text,
            /// <summary>One token: digits with an optional sign and fraction.</summary>
            Number,
            /// <summary>One token: digits.</summary>
            UInt,
            /// <summary>One token that the regular expression Arg matches whole.</summary>
            Match,
            /// <summary>One token that is one of the words of Arg, which are separated by |.</summary>
            Enum
        }

        public enum ElementKind { Word, Slot, Group }

        /// <summary>A word of the line, a slot for a value, or an optional group of elements.</summary>
        public sealed class Element
        {
            public readonly ElementKind Kind;
            /// <summary>A word: the word. A slot: its name.</summary>
            public readonly string Text;
            /// <summary>A slot: what it takes.</summary>
            public readonly SlotType Type;
            /// <summary>A slot of Match or Enum: the expression, or the words.</summary>
            public readonly string Arg;
            /// <summary>A slot: the code of the diagnostic when its value is of the wrong type; null: CodePrefix_SYNTAX.</summary>
            public readonly string Code;
            /// <summary>A slot of Text: it ends before the first word that starts what follows it.</summary>
            public readonly bool Lazy;
            /// <summary>A group: its elements, in order.</summary>
            public readonly IReadOnlyList<Element> Items;

            public Element(ElementKind kind, string text, SlotType type = SlotType.Text, string arg = null, string code = null, bool lazy = false, IReadOnlyList<Element> items = null)
            {
                Kind = kind;
                Text = text;
                Type = type;
                Arg = arg;
                Code = code;
                Lazy = lazy;
                Items = items;
            }
        }

        /// <summary>The line that a construct reads, or the head of the block that it reads.</summary>
        public sealed class Form
        {
            /// <summary>The name of the construct, as the SURFACE line lists it.</summary>
            public readonly string Construct;
            /// <summary>Its first word: WAIT.</summary>
            public readonly string Verb;
            /// <summary>The line, or the head of the block.</summary>
            public readonly IReadOnlyList<Element> Elements;
            /// <summary>A block: the forms of the lines between the head and END; empty for a line.</summary>
            public readonly IReadOnlyList<IReadOnlyList<Element>> Items;
            /// <summary>A block (a head, lines, END) or a line.</summary>
            public readonly bool IsBlock;

            public Form(string construct, IReadOnlyList<Element> elements, IReadOnlyList<IReadOnlyList<Element>> items, bool isBlock)
            {
                Construct = construct;
                Verb = elements[0].Text;
                Elements = elements;
                Items = items;
                IsBlock = isBlock;
            }
        }

        // The forms of the table are written with these: W a word, S a slot, G an optional group, F a line, B a block.
        private static Element W(string word) => new Element(ElementKind.Word, word);

        private static Element S(string name, SlotType type, string arg = null, string code = null, bool lazy = false) =>
            new Element(ElementKind.Slot, name, type, arg, code, lazy);

        private static Element G(params Element[] items) => new Element(ElementKind.Group, null, items: items);

        private static Form F(string construct, params Element[] elements) => new Form(construct, elements, new Element[0][], false);

        private static Form B(string construct, Element[] head, params Element[][] items) => new Form(construct, head, items, true);
    }

    public static partial class PlaytestGrammar
    {
        public const string Surface = "playtest";
        public const string CodePrefix = "PT";
        public const bool Ordered = true;
        public const bool TokensWords = true;
        public const bool CommentLine = true;
        public const bool CaseFold = false;

        /// <summary>The first word of each form, once, in ordinal order.</summary>
        public static readonly IReadOnlyList<string> Keywords = new[]
        {
            "ABORT_ON_FAIL", "ASSERT", "ASSERT_BATCH", "ASSERT_CAPTURED", "ASSERT_CHANGED", "ASSERT_CONSERVED",
            "ASSERT_CONSOLE_CLEAN", "ASSERT_CTA", "ASSERT_FRAMES_DIFFER", "ASSERT_FRAMES_STATIC", "ASSERT_MAX", "ASSERT_MIN",
            "ASSERT_NEAR", "ASSERT_ONE_ACTIVE", "CAPTURE", "CAPTURE_FRAMES", "CAPTURE_MAX", "CAPTURE_MIN",
            "CLICK", "COMPLETE_PURCHASE", "DESC", "EXPECT_FAIL", "FILL", "FOCUS",
            "INCLUDE", "INVARIANT", "INVOKE", "INVOKE_REPEAT", "LOG", "MCP",
            "MONITOR", "MOVE", "MOVE_PATH", "PATH_PREFIX", "SECTION", "SET",
            "SETUP", "SETUP_END", "SET_ACTIVE", "SET_DEFAULT_TIMEOUT", "SIMULATE", "SNAPSHOT",
            "SWEEP_PATH", "TAP", "TEARDOWN", "TEARDOWN_END", "TELEPORT", "TIMESCALE",
            "TRACE_FLOW", "VAL", "VAR", "WAIT", "WAIT_CAPTURED", "WAIT_STABLE",
            "WAIT_UNTIL",
        };

        /// <summary>The forms in the order of the SURFACE line: when Ordered, the first form that reads a line wins.</summary>
        public static readonly IReadOnlyList<Form> Forms = new[]
        {
            F("PT_INCLUDE", W("INCLUDE"), S("file", SlotType.Text)),
            F("PT_PATH_PREFIX", W("PATH_PREFIX"), S("prefix", SlotType.Text)),
            F("PT_VAL", W("VAL"), S("name", SlotType.Match, "\\$?[A-Za-z_][A-Za-z0-9_]*"), S("value", SlotType.Text)),
            F("PT_VAR", W("VAR"), S("name", SlotType.Match, "\\$?[A-Za-z_][A-Za-z0-9_]*"), S("query", SlotType.Match, "@\\S*\\|\\S*\\|\\S*")),
            F("PT_SETUP", W("SETUP")),
            F("PT_SETUP_END", W("SETUP_END")),
            F("PT_TEARDOWN", W("TEARDOWN")),
            F("PT_TEARDOWN_END", W("TEARDOWN_END")),
            F("PT_SECTION", W("SECTION"), G(S("name", SlotType.Text))),
            F("PT_LOG", W("LOG"), G(S("message", SlotType.Text))),
            F("PT_WAIT", W("WAIT"), S("seconds", SlotType.Number, code: "PT_NUMBER")),
            F("PT_TIMESCALE", W("TIMESCALE"), S("scale", SlotType.Number, code: "PT_NUMBER")),
            F("PT_SET_DEFAULT_TIMEOUT", W("SET_DEFAULT_TIMEOUT"), S("seconds", SlotType.Number, code: "PT_NUMBER")),
            F("PT_INVOKE", W("INVOKE"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("component", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("method", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(S("args", SlotType.Text))),
            F("PT_SET", W("SET"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("component", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("field", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("value", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")),
            F("PT_SET_ACTIVE", W("SET_ACTIVE"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("active", SlotType.Enum, "true|false|True|False|TRUE|FALSE|1|0")),
            F("PT_SNAPSHOT", W("SNAPSHOT"), G(S("queries", SlotType.Text))),
            F("PT_CLICK", W("CLICK"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(W("WAIT"), S("wait", SlotType.Number, code: "PT_NUMBER"))),
            F("PT_FILL", W("FILL"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(S("value", SlotType.Text))),
            F("PT_FOCUS", W("FOCUS"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")),
            F("PT_ASSERT_CONSOLE_CLEAN", W("ASSERT_CONSOLE_CLEAN"), G(W("IGNORE"), S("patterns", SlotType.Text))),
            F("PT_ASSERT", W("ASSERT"), S("query", SlotType.Text, lazy: true), S("op", SlotType.Match, "[<>]=?|==|!=|contains"), S("value", SlotType.Text, lazy: true), G(W("TIMEOUT"), S("timeout", SlotType.Number, code: "PT_NUMBER")), G(W("AS"), S("label", SlotType.Text, lazy: true), G(W("TIMEOUT"), S("timeout2", SlotType.Number, code: "PT_NUMBER")))),
            F("PT_ASSERT_FLAG", W("ASSERT"), S("flag", SlotType.Match, "!?(?:\\$\\S*|[/#]\\S*|\\(\\S+\\))")),
            F("PT_WAIT_UNTIL", W("WAIT_UNTIL"), S("q1", SlotType.Text, lazy: true), S("op1", SlotType.Match, "[<>]=?|==|!=|contains"), S("v1", SlotType.Text, lazy: true), G(W("AND"), S("q2", SlotType.Text, lazy: true), S("op2", SlotType.Match, "[<>]=?|==|!=|contains"), S("v2", SlotType.Text, lazy: true)), G(W("AND"), S("q3", SlotType.Text, lazy: true), S("op3", SlotType.Match, "[<>]=?|==|!=|contains"), S("v3", SlotType.Text, lazy: true)), G(W("OR"), S("q4", SlotType.Text, lazy: true), S("op4", SlotType.Match, "[<>]=?|==|!=|contains"), S("v4", SlotType.Text, lazy: true)), G(W("OR"), S("q5", SlotType.Text, lazy: true), S("op5", SlotType.Match, "[<>]=?|==|!=|contains"), S("v5", SlotType.Text, lazy: true)), G(W("TIMEOUT"), S("timeout", SlotType.Number, code: "PT_NUMBER")), G(W("ABORT"))),
            F("PT_WAIT_UNTIL_FLAG", W("WAIT_UNTIL"), S("flag", SlotType.Match, "!?\\$\\S*")),
            F("PT_INVARIANT", W("INVARIANT"), S("query", SlotType.Text, lazy: true), S("op", SlotType.Match, "[<>]=?|==|!=|contains"), S("value", SlotType.Text)),
            F("PT_MCP_INTO", W("MCP"), S("command", SlotType.Match, "\\S+"), G(S("args", SlotType.Text, lazy: true)), W("INTO"), S("var", SlotType.Match, "\\$[A-Za-z_]\\w*")),
            F("PT_MCP", W("MCP"), S("command", SlotType.Match, "\\S+"), G(S("args", SlotType.Text))),
            F("PT_MOVE", W("MOVE"), G(S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")), W("TO"), S("to", SlotType.Match, "@\\S+|\\$\\S+|\"[^\"]*\"|\\(?[-+0-9.eE]+,[-+0-9.eE]+,[-+0-9.eE]+\\)?"), G(S("offset", SlotType.Text))),
            F("PT_TELEPORT", W("TELEPORT"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("to", SlotType.Match, "@\\S+|\\$\\S+|\"[^\"]*\"|\\(?[-+0-9.eE]+,[-+0-9.eE]+,[-+0-9.eE]+\\)?"), G(S("offset", SlotType.Text))),
            F("PT_TAP", W("TAP"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(W("WAIT"), S("wait", SlotType.Number, code: "PT_NUMBER"))),
            F("PT_ASSERT_NEAR", W("ASSERT_NEAR"), S("path1", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("path2", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("threshold", SlotType.Number, code: "PT_NUMBER")),
            F("PT_ASSERT_CTA", W("ASSERT_CTA"), G(S("state", SlotType.Enum, "VISIBLE|CLICKABLE"))),
            F("PT_ASSERT_ONE_ACTIVE", W("ASSERT_ONE_ACTIVE"), S("path1", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("path2", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(S("more", SlotType.Text))),
            F("PT_DESC", W("DESC"), G(S("label", SlotType.Text))),
            F("PT_ABORT_ON_FAIL", W("ABORT_ON_FAIL")),
            F("PT_EXPECT_FAIL", W("EXPECT_FAIL")),
            F("PT_MONITOR", W("MONITOR"), G(S("query", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"))),
            F("PT_CAPTURE", W("CAPTURE"), S("label", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("query", SlotType.Text)),
            F("PT_CAPTURE_MIN", W("CAPTURE_MIN"), S("name", SlotType.Match, "\\$?[A-Za-z_][A-Za-z0-9_]*"), S("query", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(W("OVER"), S("over", SlotType.Number, code: "PT_NUMBER"))),
            F("PT_CAPTURE_MAX", W("CAPTURE_MAX"), S("name", SlotType.Match, "\\$?[A-Za-z_][A-Za-z0-9_]*"), S("query", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(W("OVER"), S("over", SlotType.Number, code: "PT_NUMBER"))),
            F("PT_ASSERT_CHANGED", W("ASSERT_CHANGED"), S("label", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")),
            F("PT_ASSERT_CAPTURED", W("ASSERT_CAPTURED"), S("label", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("mode", SlotType.Enum, "INCREASED|DECREASED|UNCHANGED|INCREASED_BY|DECREASED_BY"), G(S("subop", SlotType.Match, "[<>]=?|==|!=|contains"), S("value", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"))),
            F("PT_ASSERT_MIN", W("ASSERT_MIN"), S("name", SlotType.Match, "\\$?[A-Za-z_][A-Za-z0-9_]*"), S("op", SlotType.Match, "[<>]=?|==|!=|contains"), S("value", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")),
            F("PT_ASSERT_MAX", W("ASSERT_MAX"), S("name", SlotType.Match, "\\$?[A-Za-z_][A-Za-z0-9_]*"), S("op", SlotType.Match, "[<>]=?|==|!=|contains"), S("value", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")),
            F("PT_ASSERT_FRAMES_DIFFER", W("ASSERT_FRAMES_DIFFER"), S("label", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")),
            F("PT_ASSERT_FRAMES_STATIC", W("ASSERT_FRAMES_STATIC"), S("label", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")),
            F("PT_CAPTURE_FRAMES", W("CAPTURE_FRAMES"), S("count", SlotType.Match, "[2-9]|[1-9][0-9]+", code: "PT_COUNT"), W("INTERVAL"), S("interval", SlotType.Number, code: "PT_NUMBER"), G(W("CAMERA"), S("camera", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")), G(W("MODE"), S("mode", SlotType.Enum, "strip|list|Strip|List|STRIP|LIST")), G(W("LABEL"), S("label", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"))),
            F("PT_WAIT_CAPTURED", W("WAIT_CAPTURED"), S("label", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("mode", SlotType.Enum, "INCREASED|DECREASED|UNCHANGED|INCREASED_BY|DECREASED_BY"), G(S("subop", SlotType.Match, "[<>]=?|==|!=|contains"), S("value", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")), G(W("TIMEOUT"), S("timeout", SlotType.Number, code: "PT_NUMBER")), G(W("OVER"), S("over", SlotType.Number, code: "PT_NUMBER"), G(W("TIMEOUT"), S("timeout2", SlotType.Number, code: "PT_NUMBER")))),
            F("PT_WAIT_STABLE", W("WAIT_STABLE"), S("query", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), W("DELTA"), S("delta", SlotType.Number, code: "PT_NUMBER"), W("OVER"), S("over", SlotType.Number, code: "PT_NUMBER"), G(W("TIMEOUT"), S("timeout", SlotType.Number, code: "PT_NUMBER"))),
            F("PT_ASSERT_CONSERVED", W("ASSERT_CONSERVED"), G(W("SUM")), S("terms", SlotType.Text, lazy: true), G(W("=="), S("expected", SlotType.Match, "CONSTANT|-?\\d+(?:\\.\\d+)?")), W("OVER"), S("over", SlotType.Number, code: "PT_NUMBER")),
            F("PT_SIMULATE", W("SIMULATE"), S("name", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(W("DURATION"), S("duration", SlotType.Number, code: "PT_NUMBER")), G(W("TIMESCALE"), S("timescale", SlotType.Number, code: "PT_NUMBER")), G(W("TARGET"), S("target", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+")), G(W("FREQUENCY"), S("frequency", SlotType.Number, code: "PT_NUMBER"))),
            F("PT_TRACE_FLOW", W("TRACE_FLOW"), W("FROM"), S("from", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), W("TO"), S("to", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), W("FIELD"), S("field", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(W("TIMEOUT"), S("timeout", SlotType.Number, code: "PT_NUMBER"))),
            F("PT_MOVE_PATH", W("MOVE_PATH"), S("path", SlotType.Text, lazy: true), G(W("TIMEOUT"), S("timeout", SlotType.Number, code: "PT_NUMBER"))),
            B("PT_ASSERT_BATCH", new[] { W("ASSERT_BATCH") }, new[] { W("ASSERT"), S("query", SlotType.Text, lazy: true), S("op", SlotType.Match, "[<>]=?|==|!=|contains"), S("value", SlotType.Text, lazy: true) }),
            F("PT_SWEEP_PATH", W("SWEEP_PATH"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), W("DWELL"), S("dwell", SlotType.Number, code: "PT_NUMBER")),
            F("PT_COMPLETE_PURCHASE", W("COMPLETE_PURCHASE"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(W("EXPECT"))),
            F("PT_INVOKE_REPEAT", W("INVOKE_REPEAT"), S("count", SlotType.UInt, code: "PT_COUNT"), S("path", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("component", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), S("method", SlotType.Match, "\"[^\"]*\"|(?!.*\\[[^\\]]*$)\\S+"), G(S("args", SlotType.Text))),
        };
    }

    /// <summary>
    /// How the word of a form is compared with the word of a line, as sameWord and foldKey of the reader of the SURFACE in JavaScript (casefold.ts) do. A word is the same word when it is equal as written.
    /// When the SURFACE says CASEFOLD (PlaytestGrammar.CaseFold) it is also the same when the two are of one length and at every place equal or two ASCII letters that differ only in case, and nothing else folds:
    /// no culture and no ToUpperInvariant, so the Kelvin sign U+212A, the long s U+017F, the dotless i U+0131 and the dotted I U+0130 are no K, S or I here, as in JavaScript, and every host that reads the SURFACE
    /// decides alike. Without CaseFold the result is the one of == and nothing is allocated.
    /// </summary>
    internal static class PatternWords
    {
        // A field and not the constant: a branch on a constant leaves code that is never reached (CS0162), and a warning is an error in the project that builds the reader.
        private static readonly bool Folding = PlaytestGrammar.CaseFold;

        /// <summary>Whether the token of a line is the word of a form.</summary>
        public static bool Same(string word, string token) => word == token || (Folding && token != null && word.Length == token.Length && FoldedAt(word, 0, token));

        /// <summary>Whether the characters of `list` from `start` on, as many as `raw` has, are the word `raw`: the words of an enum are cut out of their list without a copy, so the caller has seen to the length.</summary>
        public static bool SameAt(string list, int start, string raw) => string.CompareOrdinal(list, start, raw, 0, raw.Length) == 0 || (Folding && FoldedAt(list, start, raw));

        /// <summary>The key of a word: under CaseFold its ASCII letters in upper case, so that words that are one word have one key (the word itself, not a copy, when there is nothing to change); else the word.</summary>
        public static string Key(string word)
        {
            if (!Folding) return word;
            char[] upper = null;
            for (var i = 0; i < word.Length; i++)
            {
                if (word[i] < 'a' || word[i] > 'z') continue;
                if (upper == null) upper = word.ToCharArray();
                upper[i] = (char)(word[i] - 32);
            }
            return upper == null ? word : new string(upper);
        }

        // At every place equal, or two ASCII letters: the other case of a letter differs in the bit 32 and in nothing else. `@` and the grave accent, `[` and `{` differ in it too and are no letters, so the letter is asked for.
        private static bool FoldedAt(string text, int start, string raw)
        {
            for (var i = 0; i < raw.Length; i++)
            {
                int x = text[start + i], y = raw[i];
                if (x != y && !(((x >= 'A' && x <= 'Z') || (x >= 'a' && x <= 'z')) && (x ^ y) == 32)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// A blank-separated word of a line. Text is as written: a text in quotes keeps its quotes. Col is its 0-based column in the physical line,
    /// the indent included, in UTF-16 code units (as the reader of the SURFACE in JavaScript counts).
    /// </summary>
    internal readonly struct PatternToken
    {
        public readonly string Text;
        public readonly int Col;

        public PatternToken(string text, int col)
        {
            Text = text;
            Col = col;
        }
    }

    /// <summary>
    /// How a line is cut for the table of PlaytestGrammar when TokensWords is set: into what the JavaScript expression /"[^"]*"|\S+/g finds, which is how the
    /// reader of the SURFACE cuts it. A quote opens a text, blanks included, only where a token starts and only if another quote follows; the text ends at that
    /// quote, with a blank after it or not. Any other quote is part of its word, and [..] is not protected (a path with a blank in [..] is the business of its slot).
    /// </summary>
    internal static class PatternTokens
    {
        /// <summary>The tokens of the line, in order, in linear time. Never throws: no line, an empty one and one of blanks have no token.</summary>
        public static List<PatternToken> Split(string line)
        {
            var tokens = new List<PatternToken>();
            if (line == null) return tokens;
            for (var i = 0; i < line.Length;)
            {
                if (IsBlank(line[i]))
                {
                    i++;
                    continue;
                }
                var start = i;
                // A quote that finds no closing quote after it means that no quote is left in the line, so this search fails once at most.
                var close = line[i] == '"' ? line.IndexOf('"', i + 1) : -1;
                if (close >= 0)
                    i = close + 1;
                else
                    while (i < line.Length && !IsBlank(line[i])) i++;
                tokens.Add(new PatternToken(line.Substring(start, i - start), start));
            }
            return tokens;
        }

        // \s of JavaScript, not char.IsWhiteSpace: that one reads U+0085 as a blank and U+FEFF (the byte order mark) as none.
        private static bool IsBlank(char c) =>
            (c >= '\t' && c <= '\r') || c == ' ' || c == 0x00A0 || c == 0x1680 || (c >= 0x2000 && c <= 0x200A)
            || c == 0x2028 || c == 0x2029 || c == 0x202F || c == 0x205F || c == 0x3000 || c == 0xFEFF;
    }

    /// <summary>
    /// What a slot of a form takes as a value, as the reader of the SURFACE in JavaScript decides it (types.ts, checkParam), and what a message calls the slot.
    /// The value is a token of PatternTokens.Split: a blank is none of its characters unless it is in quotes, so JavaScript and .NET cannot disagree about one.
    /// </summary>
    internal static class SlotCheck
    {
        // A token that the expression of a slot takes a long time over (a long run of [ against the expression of a path) must not hold the editor up.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

        // One Regex per expression of the table, made when a line first asks for it. The parser may run on any thread.
        private static readonly ConcurrentDictionary<string, Regex> Expressions = new ConcurrentDictionary<string, Regex>();

        /// <summary>
        /// Whether the slot takes the token. Text takes any string; Number is -?[0-9]+(\.[0-9]+)?, UInt is [0-9]+ (digits of ASCII only); Match is a regular
        /// expression that must read the whole token, and a match that runs out of time is not a match; Enum is one of the words of Arg, as written (PatternWords.SameAt: in any ASCII case when the SURFACE says CASEFOLD).
        /// No value (null) is taken by none. A value never throws; a slot type that this class does not know does, so that a type added to the table is not accepted in silence.
        /// </summary>
        public static bool Accepts(Element slot, string raw) => slot.Type switch
        {
            SlotType.Text => raw != null,
            SlotType.Number => raw != null && IsNumber(raw),
            SlotType.UInt => raw != null && IsUInt(raw),
            SlotType.Enum => raw != null && IsWordOf(slot.Arg, raw),
            SlotType.Match => raw != null && Reads(slot.Arg, raw),
            _ => throw Unknown(slot.Type),
        };

        /// <summary>What a message calls the value of the slot, as the reader in JavaScript does: "a number", "one of A|B", "like [expression]", "A or B" for a Match of only words.</summary>
        public static string Describe(Element slot) => slot.Type switch
        {
            SlotType.Text => "a text",
            SlotType.Number => "a number",
            SlotType.UInt => "a uint",
            SlotType.Enum => "one of " + slot.Arg,
            SlotType.Match => OnlyWords(slot.Arg) ? slot.Arg.Replace("|", " or ") : "like " + slot.Arg,
            _ => throw Unknown(slot.Type),
        };

        private static NotSupportedException Unknown(SlotType type) =>
            new NotSupportedException("The reader does not know the slot type " + type + ": teach SlotCheck about it before the table uses it.");

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        // The end of the run of digits that starts at `from`.
        private static int SkipDigits(string s, int from)
        {
            var i = from;
            while (i < s.Length && IsDigit(s[i])) i++;
            return i;
        }

        private static bool IsUInt(string raw) => raw.Length > 0 && SkipDigits(raw, 0) == raw.Length;

        // -?[0-9]+(\.[0-9]+)?
        private static bool IsNumber(string raw)
        {
            var whole = raw.Length > 0 && raw[0] == '-' ? 1 : 0;
            var point = SkipDigits(raw, whole);
            if (point == whole) return false;
            if (point == raw.Length) return true;
            var end = raw[point] == '.' ? SkipDigits(raw, point + 1) : -1;
            return end > point + 1 && end == raw.Length;
        }

        // The token is one of the words of the list, which are separated by |. Without a copy: this runs for every token of every line.
        private static bool IsWordOf(string list, string raw)
        {
            for (var start = 0; start <= list.Length;)
            {
                var end = list.IndexOf('|', start);
                if (end < 0) end = list.Length;
                if (end - start == raw.Length && PatternWords.SameAt(list, start, raw)) return true;
                start = end + 1;
            }
            return false;
        }

        // ECMAScript mode, so that \d, \s and \w read as they do in JavaScript. $ of .NET also matches before a final line feed, JavaScript's does not,
        // so the match must be as long as the token.
        private static bool Reads(string expression, string raw)
        {
            var regex = Expressions.GetOrAdd(expression, e => new Regex("^(?:" + e + ")$", RegexOptions.ECMAScript, MatchTimeout));
            try
            {
                var match = regex.Match(raw);
                return match.Success && match.Length == raw.Length;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        // A Match whose expression is only words (FROM|ON) is called by them, as matchWords of types.ts does.
        private static bool OnlyWords(string expression)
        {
            foreach (var word in expression.Split('|'))
            {
                if (word.Length == 0 || IsDigit(word[0])) return false;
                foreach (var c in word)
                    if (!(IsDigit(c) || c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))) return false;
            }
            return true;
        }
    }

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

        // Whether the token ends the text: the first word of a group before `need`, or what must come (ahead[need]), a word or a slot that takes the token. A word is compared by PatternWords.Same.
        private static bool Starts(IReadOnlyList<Element> ahead, int need, string token)
        {
            for (var n = 0; n < need; n++)
            {
                var items = ahead[n].Items;
                if (items.Count > 0 && items[0].Kind == ElementKind.Word && PatternWords.Same(items[0].Text, token)) return true;
            }
            if (need == ahead.Count) return false;
            var due = ahead[need];
            return due.Kind == ElementKind.Word ? PatternWords.Same(due.Text, token) : SlotCheck.Accepts(due, token);
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

        /// <summary>Notes a failure if it is further than the ones before it; of two at one token the first stays.</summary>
        public void Note(int at, Element slot, string lit)
        {
            if (at <= At) return;
            At = at;
            Slot = slot;
            Lit = lit;
        }
    }

    /// <summary>
    /// Matches the tokens of a line against the elements of a form as matchPattern of the reader of the SURFACE in JavaScript (pattern.ts) does with the options that READ uses. A word is
    /// a token as written (PatternWords.Same: in any ASCII case when the SURFACE says CASEFOLD). A slot is one token that SlotCheck accepts, but a Text, which is the rest of the line or, with a word after it in its own list, the tokens before the first
    /// token that is that word (at least one). An optional group is read with what follows it and left out when that fails, unless its first element is a word that is the token in hand:
    /// then it must match whole. A group of only words that matched is a flag. Every token must be used. The end of a Lazy text with no word after it is PatternLazy; for a text in a group
    /// that includes what follows the group, which the matcher carries along as `after`.
    /// </summary>
    internal static class PatternMatcher
    {
        static PatternMatcher()
        {
            // The one reading of the SURFACE line that the front implements: forms in order, tokens as words, a comment by line. Another one in the table means lines read as they are not meant.
            var implemented = PlaytestGrammar.Ordered && PlaytestGrammar.TokensWords && PlaytestGrammar.CommentLine;
            if (!implemented)
                throw new NotSupportedException("The SURFACE of the Playtest grammar is not the one the reader implements: Ordered, TokensWords and CommentLine must all be true.");
        }

        /// <summary>The bindings if the form reads the line, else null. `toks` are the tokens of `line` (PatternTokens.Split); `trace`, if given, receives the furthest failure and the work.</summary>
        public static SlotBindings Match(IReadOnlyList<Element> els, IReadOnlyList<PatternToken> toks, string line, MatchTrace trace)
        {
            var run = new Run(toks, line, trace);
            var bound = run.Go(els, 0, 0, Nothing, (ti, acc) => ti == toks.Count ? acc : run.Fail(ti), NoElements);
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

        private static readonly Element[] NoElements = new Element[0];

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

            /// <summary>Notes a failure in the trace, if there is one, and says "no match".</summary>
            public Bound Fail(int at, Element slot = null, string lit = null)
            {
                trace?.Note(at, slot, lit);
                return null;
            }

            /// <summary>Matches list[li..] from token ti on, then `rest` (what comes after the list); the bindings of the whole, or null. `after`: the elements that follow the group that holds the list.</summary>
            public Bound Go(IReadOnlyList<Element> list, int li, int ti, Bound acc, Func<int, Bound, Bound> rest, IReadOnlyList<Element> after)
            {
                if (trace != null) trace.Work++;
                if (li == list.Count) return rest(ti, acc);
                var el = list[li];
                switch (el.Kind)
                {
                    case ElementKind.Word:
                        return ti < toks.Count && PatternWords.Same(el.Text, toks[ti].Text) ? Go(list, li + 1, ti + 1, acc, rest, after) : Fail(ti, null, el.Text);
                    case ElementKind.Slot:
                        return Slot(list, li, ti, acc, rest, after);
                    case ElementKind.Group:
                        return Group(list, li, ti, acc, rest, after);
                    default:
                        throw new NotSupportedException("The reader does not know the element kind " + el.Kind + ".");
                }
            }

            private Bound Slot(IReadOnlyList<Element> list, int li, int ti, Bound acc, Func<int, Bound, Bound> rest, IReadOnlyList<Element> after)
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
                        while (k < toks.Count && !PatternWords.Same(stop, toks[k].Text)) k++;
                        if (trace != null) trace.Work += k - ti;
                        if (k == ti) return Fail(ti, el);
                        if (k == toks.Count) return Fail(toks.Count, null, stop);
                        end = k - 1;
                    }
                    else if (el.Lazy)
                    {
                        end = PatternLazy.LazyEnd(PatternLazy.Ahead(list, li + 1, after), toks, ti, trace);
                        if (end < 0) return null;
                    }
                }
                var first = toks[ti];
                var last = toks[end];
                var raw = el.Type == SlotType.Text ? line.Substring(first.Col, last.Col + last.Text.Length - first.Col) : first.Text;
                if (!SlotCheck.Accepts(el, raw)) return Fail(ti, el);
                return Go(list, li + 1, end + 1, new Bound(el.Text, new SlotValue(raw, first.Col), false, acc), rest, after);
            }

            private Bound Group(IReadOnlyList<Element> list, int li, int ti, Bound acc, Func<int, Bound, Bound> rest, IReadOnlyList<Element> after)
            {
                var items = list[li].Items;
                var head = items.Count > 0 ? items[0] : null;
                var flag = OnlyWords(items) ? head.Text : null;
                // READ commits: a group whose first word is the token in hand must match whole; it is not left out so that the token is read some other way.
                var committed = head != null && head.Kind == ElementKind.Word && ti < toks.Count && PatternWords.Same(head.Text, toks[ti].Text);
                // A LAZY text in the group ends before what follows the group, too.
                var inside = PatternLazy.HoldsLazy(list[li]) ? PatternLazy.Ahead(list, li + 1, after) : after;
                var taken = Go(items, 0, ti, acc, (t2, a2) => Go(list, li + 1, t2, flag == null ? a2 : new Bound(flag, default, true, a2), rest, after), inside);
                return taken ?? (committed ? null : Go(list, li + 1, ti, acc, rest, after));
            }
        }
    }

    /// <summary>
    /// What the reader says about a line that no form reads, as the reader of the SURFACE in JavaScript does (uses.ts, `take`). Code: CodePrefix_UNKNOWN_KEYWORD for a first word that no form starts with;
    /// else the code of the slot whose value was of the wrong type, if the slot has one, else CodePrefix_SYNTAX. Line is the number the caller gave the line (its source's, 1-based), Col the 1-based column of
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
    /// Reads a command of a script by the table PlaytestGrammar, as scanDeclarationLines of the reader of the SURFACE in JavaScript (uses.ts) does with the options that READ uses. The forms that
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

        // The ways to read a line, by its first word (its key: PatternWords.Key), made once.
        private static readonly Dictionary<string, Way[]> ByVerb = Index();

        private static Dictionary<string, Way[]> Index()
        {
            var lists = new Dictionary<string, List<Way>>(StringComparer.Ordinal);
            foreach (var form in PlaytestGrammar.Forms)
            {
                if (form.Elements[0].Kind != ElementKind.Word)
                    throw new NotSupportedException("The form " + form.Construct + " does not start with a word: the reader finds the forms of a line by its first word.");
                var key = PatternWords.Key(form.Verb);
                if (!lists.TryGetValue(key, out var list)) lists.Add(key, list = new List<Way>());
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
            var ways = ByVerb.TryGetValue(PatternWords.Key(toks[0].Text), out var found) ? found : NoWays;
            var form = Take(ways, toks, line, at, lineNos[at], diags, out var head);
            if (form == null)
            {
                if (AllBlocks(ways)) i = AfterEnd(lines, at + 1, toks[0].Col);
                return null;
            }
            return form.IsBlock ? ReadBlock(form, head, lines, lineNos, at, toks, ref i, diags) : new LineUse(form, head, NoItems, at, at, toks[0].Col, TextOf(line, toks));
        }

        // The lines after the head up to an END that is not indented deeper than the head (one that is deeper is a line of the block); every line there is one of the block, to the last if no END comes.
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
                if (pattern.Count == 0 || pattern[0].Kind != ElementKind.Word || PatternWords.Same(pattern[0].Text, word)) ways.Add(new Way(block, pattern));
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

        private static bool IsEnd(IReadOnlyList<PatternToken> toks, int headIndent) => toks.Count == 1 && PatternWords.Same("END", toks[0].Text) && toks[0].Col <= headIndent;

        private static string TextOf(string line, IReadOnlyList<PatternToken> toks)
        {
            var last = toks[toks.Count - 1];
            return line.Substring(toks[0].Col, last.Col + last.Text.Length - toks[0].Col);
        }
    }
}
