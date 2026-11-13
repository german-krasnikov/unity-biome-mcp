using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Roslyn;

namespace UnityMCP.Editor
{
    internal partial class MCPDebugUI
    {
        private readonly List<string> _evalHistory = new();
        private const int MaxEvalHistory = 10;

        private VisualElement BuildEvalBar()
        {
            var container = new VisualElement();
            container.AddToClassList("eval-section");

            var header = new Label("Eval");
            header.AddToClassList("section-header");
            container.Add(header);

            var bar = new VisualElement();
            bar.AddToClassList("eval-bar");

            var input = new TextField { value = "" };
            input.AddToClassList("eval-input");
            input.tooltip = "Enter C# expression (e.g. Application.targetFrameRate)";

            var submit = new Button(() => RunEval(input)) { text = "Run" };
            submit.AddToClassList("eval-submit");

            _evalResultLabel = new Label("–");
            _evalResultLabel.AddToClassList("eval-result");

            input.RegisterCallback<KeyDownEvent>(e => {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    RunEval(input);
            });

            bar.Add(input);
            bar.Add(submit);
            container.Add(bar);
            container.Add(_evalResultLabel);
            return container;
        }

        private void RunEval(TextField input)
        {
            var code = input.value?.Trim() ?? "";
            if (string.IsNullOrEmpty(code)) return;

            PushHistory(code);
            try
            {
                var result = CodeExecutor.Execute(code, "eval");
                if (_evalResultLabel != null)
                    _evalResultLabel.text = result ?? "null";
            }
            catch (Exception ex)
            {
                if (_evalResultLabel != null)
                    _evalResultLabel.text = "Error: " + ex.Message;
            }
        }

        private void PushHistory(string code)
        {
            _evalHistory.Remove(code);
            _evalHistory.Insert(0, code);
            if (_evalHistory.Count > MaxEvalHistory)
                _evalHistory.RemoveAt(_evalHistory.Count - 1);
        }
    }
}
