// Renders a paragraph containing [kind:ref] tags, ⟦kind:ref⟧ fences and bare paths as a flex-wrap container.
// Text runs -> Labels via MarkdownInline.ToRichText.
// Tag/BarePath runs -> ChipPillFactory pills (response mode: no remove button, click-to-navigate).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public static class MixedParagraphRenderer
    {
        /// <summary>
        /// Seam: overrides the preview context used by Render/InlineElement.
        /// Tests can inject a fake context to avoid AssetDatabase/SceneObjectFinder calls.
        /// </summary>
        internal static IPreviewContext ContextOverride;

#if UNITY_INCLUDE_TESTS
        /// <summary>Preserves the exact injected context instance for a test scope.</summary>
        internal static IDisposable PreserveStateForTests()
            => new TestIsolationScope(ContextOverride);

        private sealed class TestIsolationScope : IDisposable
        {
            private IPreviewContext _context;
            private bool _disposed;

            internal TestIsolationScope(IPreviewContext context) => _context = context;

            public void Dispose()
            {
                if (_disposed) return;

                ContextOverride = _context;
                _context = null;
                _disposed = true;
            }
        }
#endif

        /// <summary>
        /// Render a paragraph with mixed text+tag+bare-path content.
        /// Returns a flex-row/wrap VisualElement container marked with md-para--mixed;
        /// caller adds the contextual class (md-para / md-list-content).
        /// </summary>
        internal static VisualElement Render(string rawText, IPreviewContext context = null)
            => Render(ResponseTagTokenizer.Tokenize(rawText), context);

        internal static VisualElement Render(IReadOnlyList<TagToken> tokens, IPreviewContext context = null)
        {
            var container = new VisualElement();
            container.AddToClassList("md-para--mixed");
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap      = Wrap.Wrap;
            container.style.alignItems    = Align.Center;

            foreach (var token in tokens)
            {
                if (token.Kind == TokenKind.Text)
                {
                    var lines = token.Raw.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i > 0)
                        {
                            var br = new VisualElement();
                            br.style.flexBasis = new StyleLength(Length.Percent(100));
                            br.style.height = 0;
                            container.Add(br);
                        }
                        var stripped = StripOrphanBold(lines[i]);
                        if (!string.IsNullOrEmpty(stripped))
                            container.Add(ChatLabel.Selectable(MarkdownInline.ToRichText(stripped), richText: true));
                    }
                }
                else
                {
                    container.Add(BuildPill(token.KindKey, token.Ref, context));
                }
            }
            return container;
        }

        /// <summary>
        /// Returns either a mixed-pill container or a plain selectable label, then adds cssClass.
        /// Tokenizes once — reuses tokens for both the hasTags check and the Render call.
        /// </summary>
        internal static VisualElement InlineElement(string text, string cssClass, IPreviewContext context = null)
        {
            var tokens = ResponseTagTokenizer.Tokenize(text);
            bool hasTags = false;
            foreach (var t in tokens)
                if (t.Kind != TokenKind.Text) { hasTags = true; break; }

            VisualElement ve = hasTags
                ? Render(tokens, context)
                : ChatLabel.Selectable(MarkdownInline.ToRichText(text), richText: true);
            ve.AddToClassList(cssClass);
            return ve;
        }

        // ── private ───────────────────────────────────────────────────────────

        /// <summary>Strip orphan leading/trailing ** from text segments adjacent to pills.</summary>
        internal static string StripOrphanBold(string text)
        {
            bool startsDouble = text.TrimStart().StartsWith("**");
            bool endsDouble   = text.TrimEnd().EndsWith("**") && text.TrimEnd().Length >= 4;
            // Guard: no orphan bold markers — preserve whitespace (e.g. spaces between chips).
            if (!startsDouble && !endsDouble) return text;
            var t = text.Trim();
            if (startsDouble && !endsDouble) t = t.Substring(2).TrimStart();
            if (endsDouble   && !startsDouble) t = t.Substring(0, t.Length - 2).TrimEnd();
            return t;
        }

        private static VisualElement BuildPill(string kindKey, string rawRef, IPreviewContext context)
        {
            context ??= ContextOverride ?? PreviewLifetimeScope.Current;

            var chip = RefParser.Parse(kindKey, rawRef);
            var pill = ChipPillFactory.Build(chip.KindKey, chip.DisplayName);
            pill.tooltip = rawRef; // full ref for "reveal"

            var existenceService = context?.ExistenceService;
            if (existenceService != null)
                StaleStateDecorator.Attach(pill, chip.KindKey, chip.Path, existenceService);

            Action navigateAction = () =>
            {
                var provider = ChipKindRegistry.ForKey(kindKey);
                provider?.Navigate(rawRef);
            };
            Action pingAction = () =>
            {
                var provider = ChipKindRegistry.ForKey(kindKey);
                provider?.Ping(rawRef);
            };

            var previewPanel = new ChipInlinePreviewPanel(kindKey, rawRef,
                navigateFallback: navigateAction,
                pingAction: pingAction,
                context: context);

            ChipClickRouter.Register(pill, previewPanel, navigateAction);
            ChipPillFactory.AttachContextMenu(pill, chip,
                onPreview: () => previewPanel.Toggle(),
                onNavigate: navigateAction);

            // Wrap pill + panel in a column container so panel sits below pill
            var wrapper = new VisualElement();
            wrapper.AddToClassList("chip-pill-wrapper");
            wrapper.style.flexDirection = FlexDirection.Column;
            wrapper.Add(pill);
            wrapper.Add(previewPanel);
            return wrapper;
        }

    }
}
