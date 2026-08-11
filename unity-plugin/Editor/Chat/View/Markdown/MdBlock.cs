// Block-level model for parsed Markdown. Pure, NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public enum MdBlockKind
    {
        Paragraph,
        Heading,
        CodeFence,
        Mermaid,
        BulletList,
        OrderedList,
        BlockQuote,
        HorizontalRule,
        Table,
        Image,
    }

    /// <summary>Immutable parsed Markdown block.</summary>
    public readonly struct MdBlock
    {
        public MdBlockKind    Kind      { get; }
        public int            Level     { get; }   // Heading: 1-6; OrderedList: start index
        public string         Lang      { get; }   // CodeFence: language token
        public List<string>   Lines     { get; }   // text lines (para, heading, bullets, quote, code body)
        public List<string[]> TableRows { get; }   // Table: each row is a cell array
        public string         Src       { get; }   // Image: path/url
        public string         Alt       { get; }   // Image: alt text
        public List<int>      Depths    { get; }   // BulletList: per-item indent depth (null for non-list blocks)

        private MdBlock(MdBlockKind kind, int level = 0, string lang = null,
            List<string> lines = null, List<string[]> tableRows = null,
            string src = null, string alt = null, List<int> depths = null)
        {
            Kind      = kind;
            Level     = level;
            Lang      = lang;
            Lines     = lines;
            TableRows = tableRows;
            Src       = src;
            Alt       = alt;
            Depths    = depths;
        }

        public static MdBlock Para(List<string> lines) =>
            new MdBlock(MdBlockKind.Paragraph, lines: lines);

        public static MdBlock Heading(int level, string text) =>
            new MdBlock(MdBlockKind.Heading, level: level,
                lines: new List<string> { text });

        public static MdBlock Code(string lang, List<string> body) =>
            new MdBlock(MdBlockKind.CodeFence, lang: lang, lines: body);

        public static MdBlock Mermaid(List<string> body) =>
            new MdBlock(MdBlockKind.Mermaid, lines: body);

        public static MdBlock Bullets(List<string> items) =>
            new MdBlock(MdBlockKind.BulletList, lines: items);

        public static MdBlock Bullets(List<string> items, List<int> depths) =>
            new MdBlock(MdBlockKind.BulletList, lines: items, depths: depths);

        public static MdBlock Ordered(int start, List<string> items) =>
            new MdBlock(MdBlockKind.OrderedList, level: start, lines: items);

        public static MdBlock Quote(List<string> lines) =>
            new MdBlock(MdBlockKind.BlockQuote, lines: lines);

        public static MdBlock Rule() =>
            new MdBlock(MdBlockKind.HorizontalRule);

        public static MdBlock Table(List<string[]> rows) =>
            new MdBlock(MdBlockKind.Table, tableRows: rows);

        public static MdBlock Image(string src, string alt) =>
            new MdBlock(MdBlockKind.Image, src: src, alt: alt);
    }
}
