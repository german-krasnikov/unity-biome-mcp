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
                        {
                            // min-width: 0 allows flex-shrink to reduce the label below its
                            // natural single-line width so long text wraps inside the bubble.
                            var lbl = ChatLabel.Selectable(MarkdownInline.ToRichText(stripped), richText: true);
                            lbl.style.minWidth = 0;
                            container.Add(lbl);
                        }
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
            bool endsDouble   = text.TrimEnd().EndsWith("**") && text.Trim().Length >= 4;
            // Guard: no orphan bold markers — preserve whitespace (e.g. spaces between chips).
            if (!startsDouble && !endsDouble) return text;
            var t = text.Trim();
            if (startsDouble && !endsDouble)
            {
                // Only strip if the leading ** has NO matching close inside the fragment.
                // "**Деревья** — " has a close ** inside → NOT an orphan, return unchanged.
                var after = t.Length > 2 ? t.Substring(2) : "";
                if (!after.Contains("**")) return after.TrimStart();
            }
            if (endsDouble && !startsDouble)
            {
                // Only strip if the trailing ** has NO matching open inside the fragment.
                var before = t.Length > 2 ? t.Substring(0, t.Length - 2) : "";
                if (!before.Contains("**")) return before.TrimEnd();
            }
            return text; // complete pair inside fragment — pass through unchanged
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

            // Wrap pill + panel in a column container so panel sits below pill.
            // marginRight on wrapper (not pill) so the outer flex-row sees the spacing.
            // Pill's own marginRight (set by ChipPillFactory.Build for input-field use) is
            // irrelevant inside a column container — reset it here to avoid double-margin.
            var wrapper = new VisualElement();
            wrapper.AddToClassList("chip-pill-wrapper");
            wrapper.style.flexDirection = FlexDirection.Column;
            wrapper.style.marginRight = 2f;
            pill.style.marginRight = 0;
            wrapper.Add(pill);
            wrapper.Add(previewPanel);
            return wrapper;
        }

    }
}
