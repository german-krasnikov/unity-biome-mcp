// Smart Command EditorWindow — NL text → DSL via heuristic + optional LLM.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal enum LineStatus { Valid, Invalid, UnparsedIntent }

    internal sealed class NlCommandWindow : EditorWindow
    {
        List<VisualStep> _steps;
        ListView         _list;
        Action           _onChanged;
        SamplingConfig   _cfg;
        string           _dsl;
        bool             _isParsing;

        TextField     _input;
        Toggle        _useAiToggle;
        Button        _parseBtn;
        Button        _okBtn;
        VisualElement _previewContainer;

        internal static void Show(List<VisualStep> steps, ListView list, Action onChanged)
        {
            var w = GetWindow<NlCommandWindow>(true, "Smart Command Input");
            if (w._isParsing) { w.Focus(); return; }
            w.minSize = w.maxSize = new Vector2(400, 360);
            w._steps     = steps;
            w._list      = list;
            w._onChanged = onChanged;
            w._cfg       = LlmConfigStore.Load().Config.NlComposer;
            w.Focus();
        }

        void CreateGUI()
        {
            var ss = MCPEditorUtils.LoadStyleSheet("PlaytestComposer.uss");
            if (ss != null) rootVisualElement.styleSheets.Add(ss);
            rootVisualElement.AddToClassList("nl-window-root");

            BuildInputSection(rootVisualElement);
            BuildParseRow(rootVisualElement);
            BuildPreviewSection(rootVisualElement);
            BuildButtonRow(rootVisualElement);
        }

        void BuildInputSection(VisualElement root)
        {
            _input = new TextField { multiline = true };
            _input.AddToClassList("nl-window-input");
            _input.tooltip = "Describe steps (Russian or English). Drop GameObjects to insert paths.";
            PlaytestDropHelper.AttachDnD(_input, (_, go, path) =>
            {
                var menu = new GenericDropdownMenu();
                menu.AddItem("Insert reference [/path]", false,
                    () => _input.value += $" [{path}]");
                if (go != null)
                {
                    var coords = FormatVec(go.transform.position);
                    menu.AddItem($"Insert coordinates ({coords})", false,
                        () => _input.value += $" {coords}");
                    menu.AddItem($"MOVE {path} TO {coords}", false,
                        () => _input.value += $"\nMOVE {path} TO {coords}");
                    menu.AddItem($"TELEPORT {path} {coords}", false,
                        () => _input.value += $"\nTELEPORT {path} {coords}");
                }
                menu.DropDown(_input.worldBound, _input, false);
            });
            root.Add(_input);
        }

        void BuildParseRow(VisualElement root)
        {
            var row = new VisualElement();
            row.AddToClassList("nl-window-parse-row");

            _parseBtn = new Button(() => OnParseAsync()) { text = "Parse ▶" };
            _parseBtn.AddToClassList("composer-toolbar-btn");
            row.Add(_parseBtn);

            _useAiToggle = new Toggle("Use AI") { value = true };
            row.Add(_useAiToggle);
            root.Add(row);
        }

        void BuildPreviewSection(VisualElement root)
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("nl-window-preview");
            _previewContainer = new VisualElement();
            scroll.Add(_previewContainer);
            root.Add(scroll);
        }

        void BuildButtonRow(VisualElement root)
        {
            var row = new VisualElement();
            row.AddToClassList("nl-window-btn-row");

            var cancelBtn = new Button(OnCancel) { text = "Cancel" };
            cancelBtn.AddToClassList("composer-toolbar-btn");
            row.Add(cancelBtn);

            _okBtn = new Button(OnOk) { text = "OK ✓" };
            _okBtn.AddToClassList("composer-toolbar-btn");
            _okBtn.SetEnabled(false);
            row.Add(_okBtn);
            root.Add(row);
        }

        async void OnParseAsync()
        {
            if (_isParsing) return;
            var text = _input?.value ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;

            _isParsing = true;
            _parseBtn?.SetEnabled(false);
            _okBtn?.SetEnabled(false);

            try
            {
                bool useAi = _useAiToggle?.value ?? false;

                if (useAi)
                {
                    SetPreviewStatus("Querying AI…");
                    try
                    {
                        var llm = await NlComposerBridge.ParseAsync(text, _cfg);
                        if (this == null) return;
                        if (llm != null) { _dsl = llm; RefreshPreview(_dsl); }
                        else { _dsl = NlStepParser.ConvertToDsl(text); RefreshPreview(_dsl); }
                    }
                    catch (Exception e)
                    {
                        if (this == null) return;
                        _dsl = NlStepParser.ConvertToDsl(text);
                        RefreshPreview(_dsl);
                        SetPreviewStatus($"AI error: {e.Message} — heuristic fallback used");
                    }
                }
                else
                {
                    _dsl = NlStepParser.ConvertToDsl(text);
                    RefreshPreview(_dsl);
                }
            }
            finally
            {
                if (this != null)
                {
                    _isParsing = false;
                    _parseBtn?.SetEnabled(true);
                    _okBtn?.SetEnabled(HasValidLines(_dsl));
                }
            }
        }

        void RefreshPreview(string dsl)
        {
            _previewContainer?.Clear();
            if (string.IsNullOrWhiteSpace(dsl)) return;
            foreach (var line in dsl.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var label = new Label(line);
                bool valid = !line.TrimStart().StartsWith("LOG # UNPARSED");
                try { if (valid) PlaytestParser.Parse(line); }
                catch (Exception) { valid = false; }
                label.AddToClassList(valid ? "nl-preview-line--valid" : "nl-preview-line--invalid");
                _previewContainer?.Add(label);
            }
        }

        void SetPreviewStatus(string msg)
        {
            _previewContainer?.Clear();
            _previewContainer?.Add(new Label(msg));
        }

        bool HasUnparsed(string dsl) => dsl != null && dsl.Contains("LOG # UNPARSED:");

        bool HasValidLines(string dsl)
        {
            if (string.IsNullOrWhiteSpace(dsl)) return false;
            foreach (var line in dsl.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.TrimStart().StartsWith("LOG # UNPARSED")) return true;
            }
            return false;
        }

        void OnOk()
        {
            if (string.IsNullOrWhiteSpace(_dsl)) return;
            var added = DslToSteps(_dsl);
            foreach (var step in added) _steps?.Add(step);
            _list?.Rebuild();
            _onChanged?.Invoke();
            Close();
        }

        void OnCancel() => Close();

        // ── Pure static helpers (testable without EditorWindow instance) ───

        internal static List<VisualStep> DslToSteps(string dsl)
        {
            var result = new List<VisualStep>();
            if (string.IsNullOrWhiteSpace(dsl)) return result;
            foreach (var line in dsl.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                try
                {
                    var parsed = PlaytestParser.Parse(line);
                    foreach (var p in parsed) result.Add(PlaytestDslExporter.FromParsed(p));
                }
                catch (Exception) { /* skip invalid lines */ }
            }
            return result;
        }

        internal static LineStatus GetLineStatus(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                return LineStatus.Valid;
            if (line.TrimStart().StartsWith("LOG # UNPARSED"))
                return LineStatus.UnparsedIntent;
            try { PlaytestParser.Parse(line); return LineStatus.Valid; }
            catch { return LineStatus.Invalid; }
        }

        static string FormatVec(Vector3 v) =>
            $"{v.x.ToString("G", System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString("G", System.Globalization.CultureInfo.InvariantCulture)},{v.z.ToString("G", System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
