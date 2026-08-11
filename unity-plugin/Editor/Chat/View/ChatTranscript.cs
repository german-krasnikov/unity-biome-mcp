// Transcript rendering. F13: AppendUserBubble(UserMessage) + AtMentionNormalizer wired.
// F21: reload-survival via _entries + SerializeForReload/RestoreFromReload.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public sealed class ChatTranscript
    {

        private readonly VisualElement             _container;
        private readonly ChatBlockRendererRegistry _registry;
        private readonly StringBuilder             _assistantRaw = new StringBuilder();
        private readonly ToolChipGrouper           _grouper;
        private readonly List<TranscriptEntry>     _entries = new List<TranscriptEntry>();
        private VisualElement _assistantBubble, _assistantRow, _liveTail;
        private string        _liveTailSrc;
        private int           _committed, _msgCount;
        private bool          _dirty, _restoring;
        private IReadOnlyList<ChipData> _lastTurnChips;
        internal Func<IReadOnlyDictionary<string, string>> SceneObjects;
        private const int MaxMessages = 200;

        public ChatTranscript(VisualElement container, ChatBlockRendererRegistry registry)
        {
            _container = container;
            _registry  = registry;
            _grouper   = new ToolChipGrouper(Append, () => _msgCount--);
        }

        /// <summary>F13 Bug 2: set chips for @mention normalization in response.</summary>
        internal void SetLastTurnChips(IReadOnlyList<ChipData> chips) => _lastTurnChips = chips;

        /// <summary>F13: render an interleaved UserMessage (fixes Bug 1 double display).</summary>
        internal void AppendUserBubble(UserMessage msg, string llmPayload = null, string imagePath = null)
        {
            FinalizeAssistant();
            var display = ChipTextInterleaver.ToDisplayText(msg);
            var ud      = new UserBubbleData(display,
                string.IsNullOrEmpty(llmPayload) ? display : llmPayload);
            var bubble  = MakeBubble(ud);
            var row    = Row("msg-user");
            var wrap   = new VisualElement(); wrap.AddToClassList("msg-user-content");
            wrap.style.flexDirection = FlexDirection.Row;
            wrap.style.flexWrap = Wrap.Wrap; wrap.style.alignItems = Align.Center;
            bool any = false;
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip)
                {
                    var pill = ChipPillFactory.Build(seg.Chip);
                    ChipPillFactory.AttachReadOnlyBehavior(pill, seg.Chip);
                    wrap.Add(pill);
                    any = true;
                }
                else if (!string.IsNullOrWhiteSpace(seg.Text))
                {
                    var lbl = ChatLabel.Selectable(seg.Text);
                    lbl.AddToClassList("msg-text");
                    wrap.Add(lbl);
                    any = true;
                }
            }
            if (any) bubble.Add(wrap);
            AppendImage(bubble, imagePath);
            row.Add(bubble); Append(row);
            if (!_restoring)
            {
                _entries.Add(new TranscriptEntry {
                    EntryKind  = TranscriptEntry.Kind.User,
                    Text       = display,
                    ChipsData  = TranscriptSerializer.SerializeChips(msg.Chips),
                    LlmPayload = string.IsNullOrEmpty(llmPayload) ? null : llmPayload,
                    ImagePath  = imagePath,
                });
                if (_entries.Count > MaxMessages) _entries.RemoveAt(0);
            }
        }

        /// <summary>BUG 4 fix: multi-image overload — appends all images in imagePaths list.</summary>
        internal void AppendUserBubble(string text,
            IReadOnlyList<ChipData> chips, IReadOnlyList<string> imagePaths,
            string llmPayload = null)
        {
            FinalizeAssistant();
            object userData = llmPayload != null
                ? (object)new UserBubbleData(text ?? "", llmPayload)
                : (text ?? "");
            var bubble    = MakeBubble(userData);
            var row       = Row("msg-user");
            bool hasChips = chips != null && chips.Count > 0;
            if (hasChips)
            {
                var strip = new VisualElement(); strip.AddToClassList("user-chip-strip");
                foreach (var c in chips)
                {
                    var p = ChipPillFactory.Build(c);
                    ChipPillFactory.AttachReadOnlyBehavior(p, c);
                    strip.Add(p);
                }
                bubble.Add(strip);
            }
            var dt = hasChips ? UserTextCleaner.Strip(text) : text;
            if (!string.IsNullOrEmpty(dt))
                bubble.Add(MixedParagraphRenderer.InlineElement(dt, "msg-text"));
            if (imagePaths != null)
                foreach (var ip in imagePaths) AppendImage(bubble, ip);
            row.Add(bubble); Append(row);
            if (!_restoring)
            {
                _entries.Add(new TranscriptEntry {
                    EntryKind  = TranscriptEntry.Kind.User,
                    Text       = text ?? "",
                    ChipsData  = TranscriptSerializer.SerializeChips(chips),
                    LlmPayload = llmPayload,
                    ImagePath  = imagePaths != null && imagePaths.Count > 0 ? imagePaths[0] : null, // only first image survives reload
                });
                if (_entries.Count > MaxMessages) _entries.RemoveAt(0);
            }
        }

        /// <summary>Legacy overload — kept for TryResumePendingTurn and existing tests.
        /// Forwards to the multi-image overload.</summary>
        internal void AppendUserBubble(string text,
            IReadOnlyList<ChipData> chips = null, string imagePath = null,
            string llmPayload = null)
            => AppendUserBubble(text, chips,
                string.IsNullOrEmpty(imagePath)
                    ? (IReadOnlyList<string>)System.Array.Empty<string>()
                    : new[] { imagePath },
                llmPayload);

        internal void AppendOrExtendAssistant(string token)
        { if (_assistantBubble == null) BeginAssistant(); _assistantRaw.Append(token); _dirty = true; }

        internal void FlushStreaming()
        { if (!_dirty || _assistantBubble == null) return; _dirty = false; RenderProgressive(final: false); }

        internal void FinalizeAssistant() { _grouper.Close(); FreezeAssistantBubble(); }

        internal void AppendToolChip(string toolName, bool ok, string toolId = null)
        {
            FreezeAssistantBubble();
            var chip = BuildChip(toolName, ok, toolId);
            ToolCardRendererRegistry.Resolve(toolName)
                ?.OnStart(chip, new ToolCallRecord(toolName, toolId, null));
            _grouper.Add(chip, isError: !ok);
            if (!_restoring)
            {
                _entries.Add(new TranscriptEntry {
                    EntryKind  = TranscriptEntry.Kind.Tool,
                    Text       = toolName,
                    ChipsData  = ok ? "1" : "0",
                    LlmPayload = toolId,
                });
                if (_entries.Count > MaxMessages) _entries.RemoveAt(0);
            }
        }

        private const string CopyAttachedClass = "copy-attached";
        internal void UpdateToolDetail(string toolId, ToolCallRecord rec)
        {
            if (string.IsNullOrEmpty(toolId)) return;
            var chip = FindChipById(toolId);
            if (chip == null) return;
            chip.userData = rec;
            if (!chip.ClassListContains(CopyAttachedClass))
            { CopyableText.Attach(chip); chip.AddToClassList(CopyAttachedClass); }
            var renderer = ToolCardRendererRegistry.Resolve(rec.Name);
            if (renderer != null) renderer.OnUpdate(chip, rec);
            else ToolDetailBuilder.AttachOrUpdate(chip, rec);
        }

        private void FreezeAssistantBubble()
        {
            if (_assistantBubble == null) return;
            // F13 Bug 2: normalize @Name → [kind:ref] before final render.
            if (_lastTurnChips != null && _lastTurnChips.Count > 0)
            {
                var raw        = _assistantRaw.ToString();
                var normalized = AtMentionNormalizer.Normalize(raw, _lastTurnChips);
                normalized     = BareNameNormalizer.Normalize(normalized, _lastTurnChips);
                if (normalized != raw)
                {
                    _assistantRaw.Clear(); _assistantRaw.Append(normalized);
                    // Re-render all blocks since normalization changed text
                    _assistantBubble.Clear();
                    _committed = 0;
                }
            }
            // Scene object normalization: convert bare names even when no chips were sent.
            // Kill-switch: EditorPrefs.GetBool(PrefKeys.DisableSceneNameNorm, false) disables this pass.
            if (!UnityEditor.EditorPrefs.GetBool(PrefKeys.DisableSceneNameNorm, false))
            {
                var sceneMap = SceneObjects?.Invoke();
                if (sceneMap != null && sceneMap.Count > 0)
                {
                    var raw        = _assistantRaw.ToString();
                    var sceneChips = new List<ChipData>();
                    foreach (var kvp in sceneMap)
                        if (kvp.Key.Length > 1)
                            sceneChips.Add(new ChipData(ChipKindKeys.Hierarchy, kvp.Value, kvp.Key, 0));
                    var normalized = BareNameNormalizer.Normalize(raw, sceneChips);
                    if (normalized != raw)
                    {
                        _assistantRaw.Clear(); _assistantRaw.Append(normalized);
                        _assistantBubble.Clear();
                        _committed = 0;
                    }
                }
            }
            RenderProgressive(final: true);
            _assistantBubble.userData = _assistantRaw.ToString();
            CopyableText.Attach(_assistantBubble);
            if (!_restoring)
            {
                _entries.Add(new TranscriptEntry {
                    EntryKind = TranscriptEntry.Kind.Assistant,
                    Text      = _assistantRaw.ToString(),
                });
                if (_entries.Count > MaxMessages) _entries.RemoveAt(0);
            }
            _assistantBubble = null; _assistantRow = null; _liveTail = null; _liveTailSrc = null;
            _committed = 0; _assistantRaw.Clear(); _dirty = false;
        }

        private static VisualElement BuildChip(string toolName, bool ok, string toolId = null)
        {
            var chip = new VisualElement(); chip.AddToClassList("tool-chip");
            if (!ok) chip.AddToClassList("tool-chip--error");
            var verb = ToolVerbMap.Humanize(toolName);
            var lbl  = new Label(ok ? $"⚙ {verb}" : $"✕ {verb}"); lbl.AddToClassList("tool-chip-label");
            chip.Add(lbl); chip.userData = toolId; return chip;
        }

        private void BeginAssistant()
        {
            _grouper.Close(); _assistantRaw.Clear();
            _committed = 0; _liveTail = null; _liveTailSrc = null;
            _assistantRow    = Row(null);
            _assistantBubble = new VisualElement();
            _assistantBubble.AddToClassList("msg-bubble");
            _assistantBubble.AddToClassList("msg-bubble--assistant");
            _assistantBubble.AddToClassList("md-bubble");
            _assistantRow.Add(_assistantBubble); Append(_assistantRow);
        }

        private void RenderProgressive(bool final)
        {
            var blocks   = MarkdownParser.Parse(_assistantRaw.ToString());
            bool hasTail = !final && blocks.Count > 0;
            var  tail    = hasTail ? blocks[blocks.Count - 1] : default(MdBlock);
            bool reuse   = _liveTail != null && hasTail && tail.Kind == MdBlockKind.Image
                           && _liveTailSrc != null && _liveTailSrc == tail.Src;
            if (_liveTail != null && !reuse)
            { _liveTail.RemoveFromHierarchy(); _liveTail = null; _liveTailSrc = null; }
            int commitUpTo = final ? blocks.Count : blocks.Count - 1;
            for (int idx = _committed; idx < commitUpTo; idx++)
            { var blk = blocks[idx]; _assistantBubble.Add(_registry.RenderBlock(in blk)); }
            if (commitUpTo > _committed) _committed = commitUpTo;
            if (hasTail && !reuse)
            {
                _liveTail = _registry.RenderBlock(in tail);
                _liveTailSrc = tail.Kind == MdBlockKind.Image ? tail.Src : null;
                _assistantBubble.Add(_liveTail);
            }
        }

        private VisualElement FindChipById(string toolId)
            => FindDescendant(_container, ve =>
                (ve.userData is string s && s == toolId) ||
                (ve.userData is ToolCallRecord r && r.Id == toolId));

        private static VisualElement FindDescendant(VisualElement root, Func<VisualElement, bool> pred)
        {
            foreach (var child in root.Children())
            {
                if (pred(child)) return child;
                var found = FindDescendant(child, pred);
                if (found != null) return found;
            }
            return null;
        }

        internal void Clear() { FinalizeAssistant(); _container.Clear(); _msgCount = 0; _entries.Clear(); }

        // F21: reload-survival serialization — cap to MaxMessages to match _container eviction
        internal string SerializeForReload()
        {
            var toSerialize = _entries.Count > MaxMessages
                ? _entries.GetRange(_entries.Count - MaxMessages, MaxMessages)
                : _entries;
            return TranscriptSerializer.Serialize(toSerialize);
        }

        internal void RestoreFromReload(string data)
        {
            var entries = TranscriptSerializer.Deserialize(data);
            if (entries.Count == 0) return;
            _restoring = true;
            _entries.Clear(); // idempotent: prevent doubling on second call
            try
            {
                foreach (var e in entries)
                {
                    switch (e.EntryKind)
                    {
                        case TranscriptEntry.Kind.User:
                            AppendUserBubble(e.Text, TranscriptSerializer.DeserializeChips(e.ChipsData),
                                imagePath: e.ImagePath, llmPayload: e.LlmPayload);
                            break;
                        case TranscriptEntry.Kind.Assistant:
                            AppendOrExtendAssistant(e.Text);
                            FinalizeAssistant();
                            break;
                        case TranscriptEntry.Kind.Tool:
                            AppendToolChip(e.Text, e.ChipsData == "1", e.LlmPayload);
                            break;
                    }
                    _entries.Add(e); // rebuild _entries from saved data
                }
            }
            finally { _restoring = false; }
        }

        private static VisualElement Row(string cls)
        { var r = new VisualElement(); r.AddToClassList("msg-row"); if (cls != null) r.AddToClassList(cls); return r; }

        internal void Append(VisualElement el)
        {
            _container.Add(el); _msgCount++;
            if (_msgCount > MaxMessages) { _container.RemoveAt(0); _msgCount--; }
        }

        private static VisualElement MakeBubble(object userData)
        {
            var b = new VisualElement();
            b.AddToClassList("msg-bubble"); b.AddToClassList("msg-bubble--user");
            b.userData = userData; CopyableText.Attach(b); return b;
        }

        private void AppendImage(VisualElement bubble, string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            { var img = MdBlock.Image(imagePath, ""); bubble.Add(_registry.RenderBlock(in img)); }
        }
    }
}
