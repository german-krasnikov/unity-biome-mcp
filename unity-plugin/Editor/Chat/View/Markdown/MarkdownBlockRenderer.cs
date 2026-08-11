// Renders all core Markdown block kinds. Lists and tables delegate to partials.
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed partial class MarkdownBlockRenderer : IChatBlockRenderer
    {
        public bool CanRender(in MdBlock block) =>
            block.Kind == MdBlockKind.Paragraph    ||
            block.Kind == MdBlockKind.Heading      ||
            block.Kind == MdBlockKind.CodeFence    ||
            block.Kind == MdBlockKind.BlockQuote   ||
            block.Kind == MdBlockKind.HorizontalRule ||
            block.Kind == MdBlockKind.BulletList   ||
            block.Kind == MdBlockKind.OrderedList  ||
            block.Kind == MdBlockKind.Table;

        public VisualElement Render(in MdBlock block)
        {
            switch (block.Kind)
            {
                case MdBlockKind.Paragraph:      return RenderParagraph(in block);
                case MdBlockKind.Heading:        return RenderHeading(in block);
                case MdBlockKind.CodeFence:      return RenderCode(in block);
                case MdBlockKind.BlockQuote:     return RenderQuote(in block);
                case MdBlockKind.HorizontalRule: return RenderRule();
                case MdBlockKind.BulletList:
                case MdBlockKind.OrderedList:    return RenderList(in block);
                case MdBlockKind.Table:          return RenderTable(in block);
                default:                         return RenderParagraph(in block);
            }
        }

        private static VisualElement RenderParagraph(in MdBlock b)
        {
            var text = b.Lines != null ? string.Join("\n", b.Lines) : "";
            return MixedParagraphRenderer.InlineElement(text, "md-para");
        }

        private static VisualElement RenderHeading(in MdBlock b)
        {
            var text  = b.Lines != null && b.Lines.Count > 0 ? b.Lines[0] : "";
            var level = Mathf.Clamp(b.Level, 1, 6);
            var lbl   = ChatLabel.Selectable(MarkdownInline.ToRichText(text), richText: true);
            lbl.AddToClassList($"md-h{level}");
            return lbl;
        }

        private static VisualElement RenderCode(in MdBlock b)
        {
            var box  = new VisualElement(); box.AddToClassList("md-codebox");

            if (!string.IsNullOrEmpty(b.Lang))
            {
                var lang = new Label(b.Lang); lang.AddToClassList("md-code-lang");
                box.Add(lang);
            }

            var body = b.Lines != null ? string.Join("\n", b.Lines) : "";
            // Raw code — no rich-text, so <> show literally. Selectable so users can copy it.
            var code = ChatLabel.Selectable(body, richText: false);
            code.AddToClassList("md-code");
            box.Add(code);
            // T-7c-A Item 4: one-click copy button for the code block contents.
            var copyBtn = new Button(() => GUIUtility.systemCopyBuffer = body) { text = "⎘" };
            copyBtn.AddToClassList("md-code-copy");
            box.Add(copyBtn);
            return box;
        }

        private static VisualElement RenderQuote(in MdBlock b)
        {
            var text = b.Lines != null ? string.Join("\n", b.Lines) : "";
            var ve   = new VisualElement(); ve.AddToClassList("md-quote");
            var lbl  = ChatLabel.Selectable(MarkdownInline.ToRichText(text), richText: true);
            ve.Add(lbl);
            return ve;
        }

        private static VisualElement RenderRule()
        {
            var ve = new VisualElement(); ve.AddToClassList("md-hr");
            return ve;
        }
    }
}
