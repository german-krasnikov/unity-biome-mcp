using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal sealed class PlaytestStepElement : VisualElement
    {
        readonly DropdownField _typeField;
        readonly TextField    _descField;
        readonly TextField    _pathField;
        readonly Vector3Field _posField;
        readonly FloatField   _delayField;
        readonly TextField     _queryWu, _valWu;
        readonly DropdownField _opWu;
        readonly FloatField    _timeout;
        readonly Toggle        _abortToggle;
        readonly TextField     _queryAs, _valAs;
        readonly DropdownField _opAs;
        readonly TextField     _message;
        readonly TextField     _invPath, _invComp, _invMethod, _invArgs;
        readonly TextField     _queryMon;
        readonly TextField     _clickPath;
        readonly FloatField    _clickDelay;
        readonly TextField     _capLabel, _capQuery;
        readonly TextField     _acLabel;
        readonly DropdownField _acOp;
        readonly TextField     _nearPath, _nearTarget;
        readonly FloatField    _nearThreshold;
        readonly TextField     _accIgnore;

        readonly Dictionary<StepType, VisualElement> _panels = new();
        VisualStep _step;
        Action _onChanged;
        Action<VisualStep> _onDuplicate;
        Action<VisualStep> _onDelete;
        bool _bound;

        public PlaytestStepElement()
        {
            AddToClassList("step-row");
            this.AddManipulator(new ContextualMenuManipulator(evt => {
                evt.menu.AppendAction("Duplicate", _ => _onDuplicate?.Invoke(_step));
                evt.menu.AppendAction("Delete",    _ => _onDelete?.Invoke(_step));
            }));
            var typeChoices = new List<string>();
            foreach (var type in PlaytestDslExporter.SelectableTypes)
                typeChoices.Add(type.ToString());
            _typeField = new DropdownField(typeChoices, typeChoices.IndexOf(StepType.Wait.ToString()));
            _typeField.name = "step-type-field";
            _typeField.AddToClassList("step-type-field");
            _typeField.RegisterValueChangedCallback(e => {
                if (!_bound || _step == null) return;
                if (!Enum.TryParse(e.newValue, out StepType selected)
                    || !PlaytestDslExporter.IsSupportedType(selected)) return;
                _step.type = selected;
                SetTypeClass(_step.type); ShowPanel(_step.type); UpdateValidationState(); _onChanged?.Invoke();
            });
            _descField = Tf("step-desc-field");
            _descField.RegisterValueChangedCallback(e => Mut(() => _step.description = e.newValue));
            var hdr = new VisualElement(); hdr.AddToClassList("step-header");
            hdr.Add(_typeField); hdr.Add(_descField); Add(hdr);

            // Move/Teleport
            var mvp = Panel();
            _pathField = Tf("step-path-field");
            _pathField.RegisterValueChangedCallback(e => Mut(() => _step.path = e.newValue));
            _pathField.RegisterCallback<MouseDownEvent>(e => { if (e.clickCount == 2) Ping(_step?.path); });
            PlaytestDropHelper.AttachDnD(_pathField, (comp, go, path) => {
                if (!_bound || _step == null) return;
                var pos = comp != null ? comp.transform.position : (go != null ? go.transform.position : Vector3.zero);
                var menu = new GenericDropdownMenu();
                menu.AddItem($"Set path + position ({FormatPos(pos)})", false, () => {
                    _step.path = path; _step.position = pos;
                    _pathField.SetValueWithoutNotify(path); _posField.SetValueWithoutNotify(pos); _onChanged?.Invoke();
                });
                menu.AddItem("Set path only", false, () => {
                    _step.path = path; _pathField.SetValueWithoutNotify(path); _onChanged?.Invoke();
                });
                menu.AddItem($"Set position only ({FormatPos(pos)})", false, () => {
                    _step.position = pos; _posField.SetValueWithoutNotify(pos); _onChanged?.Invoke();
                });
                menu.DropDown(_pathField.worldBound, _pathField, false);
            });
            var eye = new Button(OnEyedropper) { text = "⊙", tooltip = "Fill from Selection" };
            eye.AddToClassList("step-eyedropper");
            _posField = new Vector3Field(); _posField.AddToClassList("step-grow-field");
            _posField.RegisterValueChangedCallback(e => Mut(() => _step.position = e.newValue));
            mvp.Add(_pathField); mvp.Add(eye); mvp.Add(_posField);
            AddPanel(mvp, StepType.Move, StepType.Teleport);

            // Wait/TimeScale
            var wtp = Panel();
            _delayField = new FloatField("sec"); _delayField.AddToClassList("step-grow-field");
            _delayField.RegisterValueChangedCallback(e => Mut(() => _step.delay = e.newValue));
            wtp.Add(_delayField); AddPanel(wtp, StepType.Wait, StepType.TimeScale);

            // WaitUntil
            var wup = Panel();
            _queryWu = Tf("step-query-field"); _queryWu.RegisterValueChangedCallback(e => Mut(() => _step.query = e.newValue));
            DnDQuery(_queryWu, StepType.WaitUntil);
            _opWu  = OpDropdown(); _opWu.RegisterValueChangedCallback(e => Mut(() => _step.op = e.newValue));
            _valWu = Tf("step-grow-field"); _valWu.RegisterValueChangedCallback(e => Mut(() => _step.value = e.newValue));
            _timeout = new FloatField(); _timeout.AddToClassList("step-small-field");
            _timeout.RegisterValueChangedCallback(e => Mut(() => _step.timeout = e.newValue));
            _abortToggle = new Toggle("Abort");
            _abortToggle.RegisterValueChangedCallback(e => Mut(() => _step.abortOnFail = e.newValue));
            wup.Add(_queryWu); wup.Add(_opWu); wup.Add(_valWu); wup.Add(_timeout); wup.Add(_abortToggle);
            AddPanel(wup, StepType.WaitUntil);

            // Assert
            var asp = Panel();
            _queryAs = Tf("step-query-field"); _queryAs.RegisterValueChangedCallback(e => Mut(() => _step.query = e.newValue));
            DnDQuery(_queryAs, StepType.Assert);
            _opAs  = OpDropdown(); _opAs.RegisterValueChangedCallback(e => Mut(() => _step.op = e.newValue));
            _valAs = Tf("step-grow-field"); _valAs.RegisterValueChangedCallback(e => Mut(() => _step.value = e.newValue));
            asp.Add(_queryAs); asp.Add(_opAs); asp.Add(_valAs);
            AddPanel(asp, StepType.Assert, StepType.Invariant);

            // Section/Log
            var sgp = Panel();
            _message = Tf("step-grow-field"); _message.RegisterValueChangedCallback(e => Mut(() => _step.message = e.newValue));
            sgp.Add(_message); AddPanel(sgp, StepType.Section, StepType.Log);

            // Invoke
            var ivp = Panel();
            _invPath = Tf("step-path-field"); _invPath.RegisterValueChangedCallback(e => Mut(() => _step.path = e.newValue));
            PlaytestDropHelper.AttachDnD(_invPath, (comp, go, path) => {
                if (!_bound || _step == null) return;
                _step.path = path; _invPath.SetValueWithoutNotify(path);
                var ctx = _step.type;
                Action syncInv = () => {
                    _invComp.SetValueWithoutNotify(_step.component ?? "");
                    _invMethod.SetValueWithoutNotify(_step.method ?? "");
                    _invArgs.SetValueWithoutNotify(_step.args ?? "");
                    _onChanged?.Invoke();
                };
                if (comp != null) { _step.component = comp.GetType().Name; _invComp.SetValueWithoutNotify(_step.component); PlaytestDropHelper.ShowFieldPicker(comp, _step, ctx, syncInv, _invPath); }
                else if (go != null) PlaytestDropHelper.ShowComponentPicker(go, _step, ctx, syncInv, _invPath);
                _onChanged?.Invoke();
            });
            _invComp   = Tf("step-small-field"); _invComp.RegisterValueChangedCallback(e => Mut(() => _step.component = e.newValue));
            _invMethod = Tf("step-small-field"); _invMethod.RegisterValueChangedCallback(e => Mut(() => _step.method = e.newValue));
            _invArgs   = Tf("step-grow-field");  _invArgs.RegisterValueChangedCallback(e => Mut(() => _step.args = e.newValue));
            ivp.Add(_invPath); ivp.Add(_invComp); ivp.Add(_invMethod); ivp.Add(_invArgs);
            AddPanel(ivp, StepType.Invoke, StepType.Set);

            // Monitor
            var mnp = Panel();
            _queryMon = Tf("step-grow-field"); _queryMon.RegisterValueChangedCallback(e => Mut(() => _step.query = e.newValue));
            DnDQuery(_queryMon, StepType.Monitor);
            mnp.Add(_queryMon); AddPanel(mnp, StepType.Monitor);

            // Click
            var clkp = Panel();
            _clickPath = Tf("step-path-field"); _clickPath.RegisterValueChangedCallback(e => Mut(() => _step.path = e.newValue));
            PlaytestDropHelper.AttachDnD(_clickPath, (comp, go, path) => {
                if (!_bound || _step == null) return;
                _step.path = path; _clickPath.SetValueWithoutNotify(path); _onChanged?.Invoke();
            });
            _clickDelay = new FloatField("wait sec"); _clickDelay.AddToClassList("step-small-field");
            _clickDelay.RegisterValueChangedCallback(e => Mut(() => _step.delay = e.newValue));
            clkp.Add(_clickPath); clkp.Add(_clickDelay);
            AddPanel(clkp, StepType.Click);

            // Capture
            var capp = Panel();
            _capLabel = Tf("step-grow-field"); _capLabel.RegisterValueChangedCallback(e => Mut(() => _step.message = e.newValue));
            _capQuery = Tf("step-query-field"); _capQuery.RegisterValueChangedCallback(e => Mut(() => _step.query = e.newValue));
            DnDQuery(_capQuery, StepType.Capture);
            capp.Add(_capLabel); capp.Add(_capQuery);
            AddPanel(capp, StepType.Capture);

            // AssertCaptured
            var acpp = Panel();
            _acLabel = Tf("step-grow-field"); _acLabel.RegisterValueChangedCallback(e => Mut(() => _step.message = e.newValue));
            _acOp = new DropdownField(_modeChoices, 0); _acOp.AddToClassList("step-op-field");
            _acOp.RegisterValueChangedCallback(e => Mut(() => _step.op = e.newValue));
            acpp.Add(_acLabel); acpp.Add(_acOp);
            AddPanel(acpp, StepType.AssertCaptured);

            // AssertNear
            var nrp = Panel();
            _nearPath = Tf("step-path-field"); _nearPath.RegisterValueChangedCallback(e => Mut(() => _step.path = e.newValue));
            PlaytestDropHelper.AttachDnD(_nearPath, (comp, go, path) => { if (!_bound || _step == null) return; _step.path = path; _nearPath.SetValueWithoutNotify(path); _onChanged?.Invoke(); });
            _nearTarget = Tf("step-path-field"); _nearTarget.RegisterValueChangedCallback(e => Mut(() => _step.value = e.newValue));
            PlaytestDropHelper.AttachDnD(_nearTarget, (comp, go, path) => { if (!_bound || _step == null) return; _step.value = path; _nearTarget.SetValueWithoutNotify(path); _onChanged?.Invoke(); });
            _nearThreshold = new FloatField("dist"); _nearThreshold.AddToClassList("step-small-field"); // reuses step.delay as distance threshold
            _nearThreshold.RegisterValueChangedCallback(e => Mut(() => _step.delay = e.newValue));
            nrp.Add(_nearPath); nrp.Add(_nearTarget); nrp.Add(_nearThreshold);
            AddPanel(nrp, StepType.AssertNear);

            // AssertConsoleClean
            var accp = Panel();
            _accIgnore = Tf("step-grow-field"); _accIgnore.RegisterValueChangedCallback(e => Mut(() => _step.message = e.newValue));
            accp.Add(_accIgnore);
            AddPanel(accp, StepType.AssertConsoleClean);

            foreach (var p in _panels.Values) p.style.display = DisplayStyle.None;
        }

        public void Bind(VisualStep step, Action onChanged, Action<VisualStep> onDuplicate = null, Action<VisualStep> onDelete = null)
        {
            if (step == null) { Unbind(); return; }
            _step = step; _onChanged = onChanged; _onDuplicate = onDuplicate; _onDelete = onDelete; _bound = true;
            _typeField.SetValueWithoutNotify(step.type.ToString());
            _descField.SetValueWithoutNotify(step.description ?? "");
            SetTypeClass(step.type); ShowPanel(step.type);
            _pathField.SetValueWithoutNotify(step.path ?? ""); _posField.SetValueWithoutNotify(step.position);
            _delayField.label = step.type == StepType.TimeScale ? "×" : "sec";
            _delayField.SetValueWithoutNotify(step.delay);
            _queryWu.SetValueWithoutNotify(step.query ?? ""); SetDropdown(_opWu, step.op ?? "==");
            _valWu.SetValueWithoutNotify(step.value ?? "");   _timeout.SetValueWithoutNotify(step.timeout);
            _abortToggle.SetValueWithoutNotify(step.abortOnFail);
            _queryAs.SetValueWithoutNotify(step.query ?? ""); SetDropdown(_opAs, step.op ?? "=="); _valAs.SetValueWithoutNotify(step.value ?? "");
            _message.SetValueWithoutNotify(step.message ?? "");
            _invPath.SetValueWithoutNotify(step.path ?? ""); _invComp.SetValueWithoutNotify(step.component ?? "");
            _invMethod.label = step.type == StepType.Set ? "field" : "method";
            _invMethod.SetValueWithoutNotify(step.method ?? ""); _invArgs.SetValueWithoutNotify(step.args ?? "");
            _queryMon.SetValueWithoutNotify(step.query ?? "");
            _clickPath.SetValueWithoutNotify(step.path ?? ""); _clickDelay.SetValueWithoutNotify(step.delay);
            _capLabel.SetValueWithoutNotify(step.message ?? ""); _capQuery.SetValueWithoutNotify(step.query ?? "");
            _acLabel.SetValueWithoutNotify(step.message ?? ""); SetDropdown(_acOp, step.op ?? "DELTA");
            _nearPath.SetValueWithoutNotify(step.path ?? ""); _nearTarget.SetValueWithoutNotify(step.value ?? ""); _nearThreshold.SetValueWithoutNotify(step.delay);
            _accIgnore.SetValueWithoutNotify(step.message ?? "");
            UpdateValidationState();
        }

        public void Unbind() { _bound = false; _step = null; _onChanged = null; _onDuplicate = null; _onDelete = null; }

        void UpdateValidationState()
        {
            if (_step == null) return;
            var err = PlaytestStepValidator.GetValidationError(_step);
            EnableInClassList("step-error", err != null);
            tooltip = err ?? "";
        }

        void ShowPanel(StepType t)
        {
            foreach (var p in _panels.Values) p.style.display = DisplayStyle.None;
            if (_panels.TryGetValue(t, out var panel)) panel.style.display = DisplayStyle.Flex;
        }

        void SetTypeClass(StepType t)
        {
            EnableInClassList("step-type--assert",    t is StepType.Assert or StepType.AssertConsoleClean or StepType.Invariant);
            EnableInClassList("step-type--waituntil", t == StepType.WaitUntil);
            EnableInClassList("step-type--wait",      t == StepType.Wait);
            EnableInClassList("step-type--move",      t is StepType.Move or StepType.Teleport);
            EnableInClassList("step-type--invoke",    t is StepType.Invoke or StepType.Set);
            EnableInClassList("step-type--section",   t == StepType.Section);
            EnableInClassList("step-type--log",       t == StepType.Log);
            EnableInClassList("step-type--timescale", t == StepType.TimeScale);
            EnableInClassList("step-type--monitor",       t == StepType.Monitor);
            EnableInClassList("step-type--click",         t == StepType.Click);
            EnableInClassList("step-type--capture",       t is StepType.Capture or StepType.AssertCaptured);
            EnableInClassList("step-type--assertnear",    t == StepType.AssertNear);
        }

        void OnEyedropper()
        {
            if (!_bound || _step == null) return;
            var sel = Selection.activeGameObject; if (sel == null) return;
            _step.path = ComponentSerializer.GetPath(sel); _step.position = sel.transform.position;
            _pathField.SetValueWithoutNotify(_step.path); _posField.SetValueWithoutNotify(_step.position); _onChanged?.Invoke();
        }

        void Mut(Action a) { if (!_bound || _step == null) return; a(); UpdateValidationState(); _onChanged?.Invoke(); }
        void DnDQuery(TextField f, StepType ctx) => PlaytestDropHelper.AttachDnD(f, (comp, go, path) => {
            if (!_bound || _step == null) return;
            if (comp == null && go == null) return;
            Action onDone = () => { f.SetValueWithoutNotify(_step.query); _onChanged?.Invoke(); };
            if (comp != null) PlaytestDropHelper.ShowFieldPicker(comp, _step, ctx, onDone, f);
            else              PlaytestDropHelper.ShowComponentPicker(go, _step, ctx, onDone, f);
        });
        void AddPanel(VisualElement p, params StepType[] types) { Add(p); foreach (var t in types) _panels[t] = p; }
        static VisualElement Panel() { var p = new VisualElement(); p.AddToClassList("step-panel"); return p; }
        static TextField Tf(string cls) { var f = new TextField { isDelayed = true }; f.AddToClassList(cls); return f; }
        static void Ping(string path) { if (string.IsNullOrEmpty(path)) return; var go = ComponentSerializer.FindObject(path); if (go != null) EditorGUIUtility.PingObject(go); }
        static readonly List<string> _opChoices   = new List<string> { "==", "!=", ">", ">=", "<", "<=" };
        static readonly List<string> _modeChoices = new List<string> { "DELTA", "RATIO", "INCREASED", "DECREASED", "UNCHANGED", "CHANGED" };
        static DropdownField OpDropdown() { var dd = new DropdownField(_opChoices, 0); dd.AddToClassList("step-op-field"); return dd; }
        static void SetDropdown(DropdownField dd, string val) => dd.SetValueWithoutNotify(dd.choices.Contains(val) ? val : dd.choices[0]);
        static string FormatPos(Vector3 v) => $"{v.x:G},{v.y:G},{v.z:G}";
    }
}
