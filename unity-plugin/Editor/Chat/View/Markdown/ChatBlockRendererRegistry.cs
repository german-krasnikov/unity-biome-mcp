// Ordered renderer registry: first CanRender winner takes the block.
// Optionally post-processes rendered labels to linkify scene/script refs.
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public sealed class ChatBlockRendererRegistry
    {
        private readonly List<IChatBlockRenderer> _renderers = new List<IChatBlockRenderer>();
        private ChatRefResolver  _resolver;
        private Action<string>   _addToContext;

        internal void Register(IChatBlockRenderer r) => _renderers.Add(r);

        /// <summary>
        /// Enables clickable scene/script references on all rendered rich-text labels.
        /// Call once after construction, before rendering any blocks.
        /// </summary>
        internal void SetLinkSupport(ChatRefResolver resolver, Action<string> addToContext)
        {
            _resolver     = resolver;
            _addToContext = addToContext;
        }

        /// <summary>
        /// Returns the first renderer that can handle the block, or a plain rich-text Label fallback.
        /// Never returns null.
        /// </summary>
        internal VisualElement RenderBlock(in MdBlock block)
        {
            VisualElement result = null;
            foreach (var r in _renderers)
            {
                if (r.CanRender(in block))
                { result = r.Render(in block); break; }
            }

            if (result == null)
            {
                // Fallback: join lines and render as rich-text paragraph.
                var text = block.Lines != null ? string.Join("\n", block.Lines) : "";
                var lbl  = ChatLabel.Selectable(MarkdownInline.ToRichText(text), richText: true);
                lbl.AddToClassList("md-para");
                result = lbl;
            }

            if (_resolver != null) PostProcessLinks(result);
            return result;
        }

        // Returns true when a label's text already contains project-specific link tags
        // (chip:, obj:, script:, asset:) that need ChatRefAction handlers.
        // Excludes url: links (emitted by MarkdownInline for [text](url) markdown links) —
        // those are external URLs and must NOT arm scene/script navigation handlers.
        internal static bool IsProjectLink(string text) =>
            text.Contains("<link=\"chip:")   ||
            text.Contains("<link=\"obj:")    ||
            text.Contains("<link=\"script:") ||
            text.Contains("<link=\"asset:");

        // Walks the VisualElement tree; linkifies every rich-text Label and installs click handlers.
        // Link handlers are deferred until GeometryChangedEvent to avoid the Unity 6.3 b7 ATG bug
        // (UUM-142829): ATGFindIntersectingLink throws "TextGenerationInfo pointer is null" when
        // link events fire before the ATG TextGenerationInfo buffer is populated after a .text mutation.
        private void PostProcessLinks(VisualElement root)
        {
            if (root is Label lbl && lbl.enableRichText && !string.IsNullOrEmpty(lbl.text))
            {
                // F20: scene-object resolver removed — bare object names in code spans are not
                // underline-linked; they reach BareNameNormalizer as pills instead (Path A).
                // B2: narrow to project links only — url: markdown links must NOT arm navigation.
                bool alreadyLinked = IsProjectLink(lbl.text);
                var linkified = ChatLinkify.Apply(lbl.text, null, _resolver.ResolveScript, ResolveAssetPath);
                linkified = ChatLinkify.ApplyPlainPaths(linkified, ResolveAssetPath);
                if (linkified != lbl.text || alreadyLinked)
                {
                    bool textMutated = linkified != lbl.text;
                    if (textMutated) { lbl.text = linkified; lbl.MarkDirtyRepaint(); }
                    // Unity bug UUM-142829: ATGFindIntersectingLink throws null-ref when link events
                    // fire before ATG buffer repopulates AFTER a .text mutation. Only disable
                    // selection when we mutated the text — labels with only pre-existing markdown
                    // links (no mutation) don't have the post-mutation timing window.
                    // Revisit when Unity patches ATG/isSelectable interaction.
                    if (textMutated) lbl.selection.isSelectable = false;
                    var add = _addToContext;
                    // Arm handlers only after first layout+ATG generation pass.
                    void Arm(GeometryChangedEvent _)
                    {
                        lbl.UnregisterCallback<GeometryChangedEvent>(Arm);
                        ChatRefAction.Install(lbl, add);
                    }
                    lbl.RegisterCallback<GeometryChangedEvent>(Arm);
                }
            }
            foreach (var child in root.Children())
                PostProcessLinks(child);
        }

        // Path-based asset resolver: only linkifies spans whose content is an existing asset path
        // (must start with "Assets/"). Bare names like "Player" never match — no false positives.
        private static string ResolveAssetPath(string name)
        {
            if (name == null || !name.StartsWith("Assets/")) return null;
            var obj = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(name);
            return obj != null ? name : null;
        }
    }
}
