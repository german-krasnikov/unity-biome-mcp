using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal class PlaytestComposerWindow : EditorWindow
    {
        [MenuItem("🧬MCP/Playtest Composer #&p")]
        internal static void Open() => GetWindow<PlaytestComposerWindow>("Playtest Composer");

        [SerializeField] List<VisualStep> _steps = new();
        [SerializeField] float _globalTimeout = 60f;
        [SerializeField] bool  _globalAbort;
        [SerializeField] string _lastFilePath = "";

        Label    _resultsLabel;
        Label    _dslLabel;
        ListView _list;
        Button   _runBtn;
        bool     _dslDirty;

        void OnEnable()
        {
            var state = ComposerStateStore.Load();
            if (_steps.Count == 0)
            {
                _steps = state.steps ?? new List<VisualStep>();
                _globalTimeout = state.globalTimeout > 0 ? state.globalTimeout : 60f;
                _globalAbort = state.globalAbort;
                _lastFilePath = state.lastFilePath ?? "";
            }
        }

        void OnDisable()
        {
            ComposerStateStore.Save(new ComposerState
            {
                steps = _steps, globalTimeout = _globalTimeout,
                globalAbort = _globalAbort, lastFilePath = _lastFilePath,
            });
        }

        void CreateGUI()
        {
            var ss = MCPEditorUtils.LoadStyleSheet("PlaytestComposer.uss");
            if (ss != null) rootVisualElement.styleSheets.Add(ss);
            rootVisualElement.AddToClassList("composer-root");

            rootVisualElement.Add(BuildToolbar());

            _list = new ListView(_steps);
            _list.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _list.makeItem  = () => new PlaytestStepElement();
            _list.bindItem  = (el, i) => ((PlaytestStepElement)el).Bind(_steps[i], MarkDslDirty, DuplicateStep, DeleteStep);
            _list.unbindItem  = (el, _) => ((PlaytestStepElement)el).Unbind();
            _list.reorderable = true;
            _list.reorderMode = ListViewReorderMode.Animated;
            _list.showAddRemoveFooter = true;
            _list.itemsAdded   += indices => { foreach (var i in indices) _steps[i] = new VisualStep { type = StepType.Wait, delay = 1f }; MarkDslDirty(); };
            _list.itemsRemoved += _ => MarkDslDirty();
            _list.itemIndexChanged += (_, _) => MarkDslDirty();
            _list.style.flexGrow = 1;
            rootVisualElement.Add(_list);
            PlaytestDropHelper.AttachMultiDnD(_list, drops => {
                foreach (var (_, go, _) in drops)
                    if (go != null) PlaytestSmartDrop.ShowActionMenu(go,
                        step => { _steps.Add(step); _list.Rebuild(); MarkDslDirty(); },
                        () => { _list.Rebuild(); MarkDslDirty(); }, _list);
            });

            var resHdr = new Label("Results:");
            resHdr.AddToClassList("composer-section-label");
            rootVisualElement.Add(resHdr);
            _resultsLabel = new Label();
            _resultsLabel.AddToClassList("composer-results");
            rootVisualElement.Add(_resultsLabel);

            var dslHdr = new Label("DSL Preview:");
            dslHdr.AddToClassList("composer-section-label");
            rootVisualElement.Add(dslHdr);
            _dslLabel = new Label();
            _dslLabel.AddToClassList("composer-dsl-preview");
            rootVisualElement.Add(_dslLabel);

            rootVisualElement.schedule.Execute(FlushDsl).Every(400);
        }

        VisualElement BuildToolbar()
        {
            var tb = new VisualElement();
            tb.AddToClassList("composer-toolbar");

            _runBtn = new Button(RunPlaytest) { text = "▶ Run" };
            _runBtn.AddToClassList("composer-toolbar-btn");
            tb.Add(_runBtn);
            Btn(tb, "Save",        SaveFile);
            Btn(tb, "Load",        LoadFile);
            Btn(tb, "Copy DSL",    () => GUIUtility.systemCopyBuffer = BuildDsl());
            Btn(tb, "Copy for AI", () => GUIUtility.systemCopyBuffer = BuildDslFenced());
            Btn(tb, "＋ Smart Command", () => NlCommandWindow.Show(_steps, _list, MarkDslDirty));

            var toRow = new VisualElement();
            toRow.AddToClassList("composer-to-row");
            toRow.Add(new Label("TO:"));
            var toField = new FloatField { value = _globalTimeout };
            toField.RegisterValueChangedCallback(e => _globalTimeout = e.newValue);
            toRow.Add(toField);
            var abortToggle = new Toggle("Abort") { value = _globalAbort };
            abortToggle.RegisterValueChangedCallback(e => _globalAbort = e.newValue);
            toRow.Add(abortToggle);
            tb.Add(toRow);
            return tb;
        }

        static void Btn(VisualElement parent, string label, System.Action click)
        {
            var btn = new Button(click) { text = label };
            btn.AddToClassList("composer-toolbar-btn");
            parent.Add(btn);
        }

        void DuplicateStep(VisualStep step)
        {
            var idx = _steps.IndexOf(step);
            if (idx < 0) return;
            _steps.Insert(idx + 1, step.Clone());
            _list.Rebuild();
            MarkDslDirty();
        }

        void DeleteStep(VisualStep step)
        {
            if (!_steps.Remove(step)) return;
            _list.Rebuild();
            MarkDslDirty();
        }

        void MarkDslDirty() { _dslDirty = true; RefreshRunButton(); }

        void RefreshRunButton()
        {
            if (_runBtn == null) return;
            _runBtn.SetEnabled(_steps.Count > 0 && PlaytestStepValidator.IsScriptValid(_steps));
        }

        void FlushDsl()
        {
            if (!_dslDirty || _dslLabel == null) return;
            _dslDirty = false;
            _dslLabel.text = BuildDsl();
        }

        string BuildDsl() => PlaytestDslExporter.Export(_steps, _globalAbort);

        string BuildDslFenced()
        {
            var header = $"timeout={_globalTimeout} abort_on_fail={_globalAbort.ToString().ToLower()}";
            return $"```playtest {header}\n{BuildDsl()}\n```";
        }

        void RunPlaytest()
        {
            if (_resultsLabel != null) _resultsLabel.text = "Running…";
            var tcs = new TaskCompletionSource<string>();
            var ctx = System.Threading.SynchronizationContext.Current;
            tcs.Task.ContinueWith(t => {
                var r = t.Result;
                ctx.Post(_ => { if (_resultsLabel != null) _resultsLabel.text = r; }, null);
            }, TaskScheduler.Default);
            PlaytestRunner.Run(BuildDsl(), Mathf.Max(0.1f, _globalTimeout), tcs, _globalAbort);
        }

        void SaveFile() => PlaytestFileHelper.Save(BuildDsl(), ref _lastFilePath);

        void LoadFile()
        {
            var loaded = PlaytestFileHelper.Load(ref _lastFilePath);
            if (loaded == null) return;
            _steps.Clear();
            _steps.AddRange(loaded);
            _list?.Rebuild();
            MarkDslDirty();
        }
    }
}
