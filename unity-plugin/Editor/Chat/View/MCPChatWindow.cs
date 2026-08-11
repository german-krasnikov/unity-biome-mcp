using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow : EditorWindow
    {
        private IChatBackend   _backend;
        internal ChatTranscript _transcript;
        private bool           _agentMode;
        private BackendKind    _selectedKind = BackendKind.Claude;
        private PermissionConfig _permConfig = new PermissionConfig();
        private int                _inputTokens, _outputTokens;
        private ContextProgressBar _contextBar;
        private readonly TurnUndoTracker  _undoTracker       = new TurnUndoTracker();
        private readonly SessionAllowlist  _sessionAllowlist  = new SessionAllowlist();
        private readonly SentTextCache _sentTextCache = new SentTextCache();
        // task#10: caches the full-path LLM payload (paths + [kind:path] block) sent this turn,
        // so an in-flight domain reload re-sends the SAME payload, not the short-name display text.
        private readonly SentTextCache _sentLlmCache = new SentTextCache();
        private readonly List<ChatEvent>       _evBuf    = new List<ChatEvent>(16);
        private readonly List<ToolCallRecord>  _toolBuf  = new List<ToolCallRecord>(8);
        internal readonly CompileAutoFix       _autoFix  = new CompileAutoFix();
        internal bool _turnEditedCode;
        internal bool _turnHasToolCalls;
        internal bool _needsRefresh;
        private bool  _transcriptRestored;
        // D6: bounded retry counter for TryResumePendingTurn compile-clean gate.
        // Resets to 0 on success; gives up after MaxResumeRetries+1 calls with !IsCompileClean.
        internal int  _resumeRetryCount;
        internal const int MaxResumeRetries = 30;
        private bool _resumeDelayScheduled;
        private TextField          _input;
        private Label              _tokenReadout;
        // Tier 2 (chat-relay-upm-fix.md): shown while RelaySpawnState.RequestSpawn() cold-starts
        // uvx (first run after a UPM install can take ~30s). Updated in DrainAndRender.
        private Label              _relayStatusLabel;
        private Button             _askBtn, _agentBtn;
        private VisualElement      _inputArea;
        private ScrollView         _scroll;
        private InputHeightCalc    _heightCalc = new InputHeightCalc();

        [MenuItem("🧬MCP/Chat", priority = 0)]
        public static void ShowWindow()
        {
            var w = GetWindow<MCPChatWindow>($"{BiomeLabel.DisplayName} Chat");
            w.minSize = new Vector2(320, 400);
        }

        public static bool IsChatBackendRunning()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<MCPChatWindow>())
                if (w._backend?.IsRunning ?? false) return true;
            return false;
        }

        // M1: name of the last tool call in the current turn (for timeout hint).
        internal string _lastToolName;

        // P0-2: DRY helper — reset all per-turn flags (3 sites in Drain + CancelTurn + NewSession).
        private void ResetTurnFlags()
        {
            _turnEditedCode = _turnHasToolCalls = _needsRefresh = false;
            _lastEventTime  = 0;
            _lastToolName   = null;
        }

        internal void ResetTokenCounters()
        {
            _inputTokens = _outputTokens = 0;
            if (_tokenReadout != null) _tokenReadout.text = "";
            _contextBar?.Reset();
        }

        internal void RefreshColorResolver()
        {
            ChipPillFactory.ColorResolver = BackendConfigStore.Load().Chips.ResolveColor;
        }

        private Label _copyFlashLabel;

        private void OnEnable()
        {
            RefreshColorResolver();
            ChipPillFactory.AddToContextAction = chip => _chipField?.AddChip(chip);
            RegionTool.SceneRegionTool.OnRegionCommitted = (id, label) =>
            { if (EditorPrefs.GetBool(PrefKeys.RegionAutoAdd, true)) ChipPillFactory.AddToContextAction?.Invoke(new ChipData(ChipKindKeys.Region, id, label, 0)); };
            RegionTool.SceneAnnotationTool.OnAnnotationCommitted = (id, label) =>
            { if (EditorPrefs.GetBool(PrefKeys.RegionAutoAdd, true)) ChipPillFactory.AddToContextAction?.Invoke(new ChipData(ChipKindKeys.Region, id, label, 0)); };
            ScreenshotToolbarButton.OnScreenshotCaptured = path =>
                ProcessExternalPath(path, InsertInlineChip);
            Annotation.AnnotationEditorWindow.OnAnnotationReady = (path, displayName) =>
                ChipPillFactory.AddToContextAction?.Invoke(
                    new ChipData(ChipKindKeys.AnnotatedScreenshot, path, displayName, 0));
            CopyFlash.ShowAction = ShowCopyFlash;
            RestoreSelectedBackendFromPrefs(); // Issue 28: restore _selectedKind/_selectedAgent BEFORE CreateBackend()
            CreateBackend();
            ResetTokenCounters();
            RelaySpawner.OnAfterReloadResume += TryResumePendingTurn;
            AssemblyReloadEvents.beforeAssemblyReload += SaveStateBeforeReload;
            EditorApplication.hierarchyChanged += RefreshResolver;
            _autoFix.Subscribe();
            _autoFix.OnErrorsDetected += InjectCompileErrors;
            _undoTracker.Invalidate();
            CommandRouter.OnAskUser += OnMcpAskUser;
            ChatMcpConfigWriter.CleanupStaleConfigs();
        }

        private void ShowCopyFlash()
        {
            if (_copyFlashLabel == null) return;
            _copyFlashLabel.RemoveFromClassList("copy-flash--hidden");
            _copyFlashLabel.schedule.Execute(() =>
                _copyFlashLabel?.AddToClassList("copy-flash--hidden")).ExecuteLater(1500);
        }

        internal void RefreshChipDisplay()
        {
            _chipField?.RebuildFromModel();
        }

        private void OnDisable()
        {
            _flowMotion?.SetActive(false);
            // P0-A: persist transcript so window close/reopen restores history (not just domain reload)
            SessionState.SetString(PrefKeys.ChatTranscript, _transcript?.SerializeForReload() ?? "");
            CommandRouter.OnAskUser -= OnMcpAskUser;
            EditorApplication.hierarchyChanged -= RefreshResolver;
            AssemblyReloadEvents.beforeAssemblyReload -= SaveStateBeforeReload;
            RelaySpawner.OnAfterReloadResume -= TryResumePendingTurn;
            CancelPendingTurnResume();
            _autoFix.OnErrorsDetected -= InjectCompileErrors;
            _autoFix.Unsubscribe();
            ChipPillFactory.AddToContextAction = null;
            RegionTool.SceneRegionTool.OnRegionCommitted = null;
            RegionTool.SceneAnnotationTool.OnAnnotationCommitted = null;
            ScreenshotToolbarButton.OnScreenshotCaptured = null;
            Annotation.AnnotationEditorWindow.OnAnnotationReady = null;
            CopyFlash.ShowAction = null;
            ReloadGuard.OnTurnFinished();
            if (_activity.Phase != ActivityPhase.Idle)
                _undoTracker.OnTurnFailed();
            _backend?.Stop();
            ChatMcpConfigWriter.DeleteOwnConfig();
            _backend = null;
            _assetMentionIndex?.Dispose();
            _assetMentionIndex = null;
        }

        private ChatRefResolver _resolver;

        private void RefreshResolver()
        {
            _resolver?.Refresh();
        }

        static void TryAddStyleSheet(VisualElement root, string name)
        {
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                $"Packages/com.unity-biome-mcp.editor/Editor/Chat/View/{name}");
            if (ss != null) root.styleSheets.Add(ss);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            TryAddStyleSheet(root, "Chat.Tokens.uss");  // semantic --chat-* vars loaded first
            TryAddStyleSheet(root, "MCPChatWindow.uss"); // existing layout styles
            root.AddToClassList("chat-root");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("chat-root--light");
            MarkdownInlineFormatter.IsDarkTheme = EditorGUIUtility.isProSkin;
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.AddToClassList("chat-scroll");
            var inner = new VisualElement();
            _scroll.Add(inner);
            _resolver = new ChatRefResolver();
            _resolver.Refresh();
            var registry = ChatBlockRendererFactory.CreateDefault(_resolver, AddRefToContext);
            _transcript = new ChatTranscript(inner, registry);
            _transcript.SceneObjects = () => _resolver?.Objects;
            // F21: restore transcript that was saved before domain reload
            var savedTranscript = SessionState.GetString(PrefKeys.ChatTranscript, "");
            if (!string.IsNullOrEmpty(savedTranscript))
            {
                _transcript.RestoreFromReload(savedTranscript);
                SessionState.EraseString(PrefKeys.ChatTranscript);
            }
            _transcriptRestored = !string.IsNullOrEmpty(savedTranscript); // P0-1: guard duplicate bubble
            root.Add(_scroll);
            _inputArea = BuildInputArea();
            ResetInputAreaHeight();
            root.Add(BuildResizeHandle(_inputArea));
            root.Add(_inputArea);
            SetupAutoHeight();
            SetupSlash();
            SetupMention();
            root.schedule.Execute(DrainAndRender).Every(33);
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);
            // F20: Esc cancels a running turn. Guard: Idle → no-op (slash popup handles its own Esc).
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape && _activity.Phase != ActivityPhase.Idle)
                {
                    CancelTurn();
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);

            _copyFlashLabel = new Label("✓ Copied!");
            _copyFlashLabel.AddToClassList("copy-flash");
            _copyFlashLabel.AddToClassList("copy-flash--hidden");
            root.Add(_copyFlashLabel);

            TryResumePendingTurn();
        }

        private VisualElement BuildInputArea()
        {
            var area = new VisualElement(); area.AddToClassList("input-area");
            area.Add(BuildFlowBar());

            _chipField = new InlineChipField();
            while (ChipPillFactory.PendingChips.Count > 0)
                _chipField.AddChip(ChipPillFactory.PendingChips.Dequeue());
            _chipField.AddToClassList("chat-input");
            _input = _chipField.TextField;
            area.Add(_chipField);
            WireChipInput();
            WireClipboardPaste();

            EnterKeySend.Attach(_input, OnSend);
            area.Add(BuildFooterBar());
            return area;
        }

        private void SetMode(bool agentMode)
        {
            if (_agentMode == agentMode) return;
            _agentMode = agentMode;
            // Pure UI state flip — no process kill/restart.
            // Agent mode = PermissionPrompt events are auto-approved in EventHandlers.
            _askBtn?.EnableInClassList("mode-toggle-btn--active",   !agentMode);
            _agentBtn?.EnableInClassList("mode-toggle-btn--active", agentMode);
            (_backend as RelayBackend)?.SetMode(agentMode ? "agent" : "ask");
        }

        private void CreateBackend()
        {
            SessionState.EraseString(PrefKeys.ChatBackendSessionId);
            CreateBackendWithSession(null);
        }

        private void CreateBackendWithSession(string resumeSessionId, BackendConfigStore store = null)
        {
            if (BackendFactoryForTest != null)
            {
                _backend = BackendFactoryForTest(resumeSessionId);
                return;
            }
            var backendId  = BackendProviderRegistry.KindToId(_selectedKind);
            var sysPrompt  = ChipSystemPrompt.ForBackend(_selectedKind);
            _backend = new RelayBackend(backendId, _agentMode ? "agent" : "ask",
                                        _selectedModel, MCPServer.ServerChatPort, resumeSessionId,
                                        sysPrompt);
        }

        internal static Func<string, IChatBackend> BackendFactoryForTest;

        internal static BackendConfigStore ApplySelectedModel(
            BackendConfigStore src, BackendKind kind, string selectedModel)
            => src.WithModel(kind, selectedModel);

        internal void CancelTurn()
        {
            if (_activity.Phase == ActivityPhase.Idle) return;
            _transcript?.FinalizeAssistant();
            ReloadGuard.OnTurnFinished();
            ResetTurnFlags(); // P0-2: clear stale per-turn flags on cancel
            _undoTracker.OnTurnFailed();
            _activity.Fail();
            OnActivityChanged();
            _backend?.Stop();
            _backend = null;
            CreateBackend();
        }

        private void ResetInputAreaHeight()
        {
            _inputArea.style.height    = InputHeightCalc.CompactH;
            _inputArea.style.minHeight = StyleKeyword.Null;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }

    }
}
