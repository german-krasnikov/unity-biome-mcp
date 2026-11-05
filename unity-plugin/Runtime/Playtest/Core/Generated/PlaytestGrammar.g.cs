// Generated from dsl/constructs/playtest.construct
// source-sha256=4488af3986367387fcc4ee2da4c52b1cc5059494957837c1c6437cd9289a6006
// Edit the source, then run biome generate.

// The grammar of a .playtest line as data: the forms of the PT_* constructs of dsl/constructs/playtest.construct, for a reader that matches a line against them (step 7.5).
// A form is a line, or a block (a head, lines, END), made of words, slots and optional groups; the reader tokenizes as the SURFACE says (see the constants).
// C# 9 and no engine reference: the assembly UnityMCP.Playtest.Core has noEngineReferences.

using System.Collections.Generic;

namespace UnityMCP.Playtest.Core
{
    public static class PlaytestGrammar
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
            /// <summary>The name of the construct: PT_WAIT.</summary>
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

        public const string Surface = "playtest";
        public const string CodePrefix = "PT";
        public const bool Ordered = true;
        public const bool TokensWords = true;
        public const bool CommentLine = true;

        /// <summary>The first word of each form, once, in ordinal order. Upper case: the parser compares without case.</summary>
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

        // The forms above are written with these: W a word, S a slot, G an optional group, F a line, B a block.
        private static Element W(string word) => new Element(ElementKind.Word, word);

        private static Element S(string name, SlotType type, string arg = null, string code = null, bool lazy = false) =>
            new Element(ElementKind.Slot, name, type, arg, code, lazy);

        private static Element G(params Element[] items) => new Element(ElementKind.Group, null, items: items);

        private static Form F(string construct, params Element[] elements) => new Form(construct, elements, new Element[0][], false);

        private static Form B(string construct, Element[] head, params Element[][] items) => new Form(construct, head, items, true);
    }
}
