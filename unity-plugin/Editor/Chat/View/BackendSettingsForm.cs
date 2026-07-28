// UIToolkit forms for per-backend settings. Pure UI wiring — no persistence logic.
// P4: BuildChipDisplayForm replaces hardcoded BuildChipConfigForm — registry-driven.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public static class BackendSettingsForm
    {
        private static readonly List<string> _depthOptions =
            new List<string> { "path", "summary", "full", "none" };

        private static readonly (string name, string label)[] _allowedTypeRows =
        {
            ("GameObject",     "GameObject (Prefabs)"),
            ("Material",       "Material"),
            ("Texture",        "Texture (Texture2D, Cubemap...)"),
            ("AnimationClip",  "Animation Clip"),
            ("MonoScript",     "Script (MonoScript)"),
            ("Mesh",           "Mesh (.fbx, .obj)"),
            ("AudioClip",      "Audio Clip"),
            ("ScriptableObject", "ScriptableObject"),
        };

        /// <summary>Registry-driven chip display form: one row per registered kind.</summary>
        internal static void BuildChipDisplayForm(
            VisualElement parent,
            ChipConfig config,
            Action onSave)
        {
            // Allowed asset types section
            var header = new Label("Allowed Chip Types");
            header.AddToClassList("settings-section-header");
            parent.Add(header);

            foreach (var (name, lbl) in _allowedTypeRows)
            {
                var toggle = new Toggle(lbl) { value = ChatChipPolicy.IsTypeEnabled(name) };
                toggle.AddToClassList("chat-form-field");
                var capturedName = name;
                toggle.RegisterValueChangedCallback(evt =>
                    EditorPrefs.SetBool(ChatChipPolicy.PrefKey(capturedName), evt.newValue));
                parent.Add(toggle);
            }

            var separator = new VisualElement();
            separator.AddToClassList("chat-separator");
            parent.Add(separator);

            foreach (var kindKey in ChipKindRegistry.AllKeys)
            {
                var provider = ChipKindRegistry.ForKey(kindKey);
                if (provider == null) continue;

                var row = new VisualElement();
                row.AddToClassList("chip-config-row");

                var label = new Label(kindKey);
                label.AddToClassList("chip-config-label");
                row.Add(label);

                var currentDepth = config.DepthFor(kindKey);
                var depthField = new DropdownField(_depthOptions,
                    System.Math.Max(0, _depthOptions.IndexOf(currentDepth)));
                depthField.AddToClassList("chip-config-depth");
                var capturedKey = kindKey;
                depthField.RegisterValueChangedCallback(e =>
                {
                    config.SetDepthOverride(capturedKey, e.newValue);
                    onSave();
                });
                row.Add(depthField);

                var currentColor = config.ResolveColor(kindKey);
                ChipPillFactory.TryParseHex(currentColor, out var col);
                var colorField = new ColorField { value = col, showAlpha = false };
                colorField.AddToClassList("chip-config-color");
                colorField.RegisterValueChangedCallback(e =>
                {
                    config.SetColorOverride(capturedKey,
                        "#" + ColorUtility.ToHtmlStringRGB(e.newValue));
                    onSave();
                });
                row.Add(colorField);

                var resetBtn = BiomeUI.QuietButton("Reset", () =>
                {
                    config.SetDepthOverride(capturedKey, provider.DefaultDepth); // explicit default wins over legacy
                    config.SetColorOverride(capturedKey, null);                  // null → provider color
                    depthField.value = provider.DefaultDepth;
                    ChipPillFactory.TryParseHex(provider.HexColor, out var defaultCol);
                    colorField.value = defaultCol;
                    onSave();
                });
                resetBtn.AddToClassList("chip-config-reset");
                row.Add(resetBtn);

                parent.Add(row);
            }
        }

        internal static void BuildClaudeForm(
            VisualElement parent,
            ClaudeBackendConfig config,
            Action onSave)
        {
            var modelField = new TextField("Model") { value = config.Model };
            modelField.AddToClassList("chat-form-field");
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var permField = new DropdownField(
                "Permission Mode",
                new List<string> { "plan", "acceptEdits" },
                config.PermissionMode == "acceptEdits" ? 1 : 0);
            permField.AddToClassList("chat-form-field");
            permField.RegisterValueChangedCallback(e => { config.PermissionMode = e.newValue; onSave(); });
            parent.Add(permField);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.AddToClassList("chat-form-field");
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }

        /// <summary>
        /// Shared: "Auto: path" hint + optional install hint + binary path EditorPrefs field.
        /// Used by Antigravity, Kimi, and OpenCode forms.
        /// </summary>
        private static void BuildBinarySection(
            VisualElement parent,
            string binaryName,
            string prefKey,
            string installHint)
        {
            var autoPath = ChatBinaryResolver.Resolve(binaryName);
            var hint = new Label($"Auto: {autoPath ?? "not found"}");
            hint.AddToClassList("chat-hint");
            hint.AddToClassList(autoPath != null ? "chat-hint--success" : "chat-hint--error");
            parent.Add(hint);

            if (autoPath == null && !string.IsNullOrEmpty(installHint))
            {
                var install = new Label(installHint);
                install.AddToClassList("chat-warning");
                parent.Add(install);
            }

            var pathField = new TextField("Binary Path")
                { value = EditorPrefs.GetString(prefKey, "") };
            pathField.AddToClassList("chat-form-field");
            pathField.RegisterValueChangedCallback(e =>
            {
                if (string.IsNullOrEmpty(e.newValue))
                    EditorPrefs.DeleteKey(prefKey);
                else
                    EditorPrefs.SetString(prefKey, e.newValue);
            });
            parent.Add(pathField);
        }

        internal static void BuildAntigravityForm(
            VisualElement parent,
            AntigravityBackendConfig config,
            Action onSave)
        {
            BuildBinarySection(parent, "agy",
                ChatBinaryResolver.AgyPrefKey,
                "Install: https://github.com/google/antigravity-cli");

            var modelField = new TextField("Model") { value = config.Model };
            modelField.AddToClassList("chat-form-field");
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var approvalField = new DropdownField(
                "Approval Mode",
                new List<string> { "default", "yolo" },
                config.ApprovalMode == "yolo" ? 1 : 0);
            approvalField.AddToClassList("chat-form-field");
            approvalField.RegisterValueChangedCallback(e => { config.ApprovalMode = e.newValue == "default" ? "" : e.newValue; onSave(); });
            parent.Add(approvalField);

            var sandboxToggle = new Toggle("Sandbox") { value = config.Sandbox };
            sandboxToggle.AddToClassList("chat-form-field");
            sandboxToggle.RegisterValueChangedCallback(e => { config.Sandbox = e.newValue; onSave(); });
            parent.Add(sandboxToggle);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.AddToClassList("chat-form-field");
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }

        internal static void BuildKimiForm(
            VisualElement parent,
            KimiBackendConfig config,
            Action onSave)
        {
            BuildBinarySection(parent, "kimi",
                ChatBinaryResolver.KimiPrefKey,
                "Install: curl -fsSL https://code.kimi.com/kimi-code/install.sh | bash");

            var modelField = new TextField("Model") { value = config.Model };
            modelField.AddToClassList("chat-form-field");
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var approvalField = new DropdownField(
                "Approval Mode",
                new List<string> { "default", "yolo", "plan" },
                config.ApprovalMode == "yolo" ? 1 : config.ApprovalMode == "plan" ? 2 : 0);
            approvalField.AddToClassList("chat-form-field");
            approvalField.RegisterValueChangedCallback(e =>
            {
                config.ApprovalMode = e.newValue == "default" ? "" : e.newValue;
                onSave();
            });
            parent.Add(approvalField);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.AddToClassList("chat-form-field");
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }

        internal static void BuildOpenCodeForm(
            VisualElement parent,
            OpenCodeBackendConfig config,
            Action onSave)
        {
            BuildBinarySection(parent, "opencode",
                ChatBinaryResolver.OpenCodePrefKey,
                "Install: curl -fsSL https://opencode.sh | bash");

            var fmtHint = new Label("Model: provider/modelId  e.g. anthropic/claude-sonnet-4");
            fmtHint.AddToClassList("chat-hint");
            parent.Add(fmtHint);

            var modelField = new TextField("Model") { value = config.Model };
            modelField.AddToClassList("chat-form-field");
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var skipToggle = new Toggle("Skip Permissions") { value = config.SkipPermissions };
            skipToggle.AddToClassList("chat-form-field");
            skipToggle.RegisterValueChangedCallback(e => { config.SkipPermissions = e.newValue; onSave(); });
            parent.Add(skipToggle);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.AddToClassList("chat-form-field");
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }

        internal static void BuildCodexForm(
            VisualElement parent,
            CodexBackendConfig config,
            Action onSave)
        {
            // Binary path override (R1 — escape hatch when where.exe/which can't find codex)
            var autoCodexPath = ChatBinaryResolver.Resolve("codex");
            var codexPathHint = new Label($"Auto: {autoCodexPath ?? "not found"}");
            codexPathHint.AddToClassList("chat-hint");
            codexPathHint.AddToClassList(autoCodexPath != null ? "chat-hint--success" : "chat-hint--error");
            parent.Add(codexPathHint);

            var codexPathField = new TextField("Binary Path")
                { value = EditorPrefs.GetString(ChatBinaryResolver.CodexPrefKey, "") };
            codexPathField.AddToClassList("chat-form-field");
            codexPathField.RegisterValueChangedCallback(e =>
            {
                if (string.IsNullOrEmpty(e.newValue))
                    EditorPrefs.DeleteKey(ChatBinaryResolver.CodexPrefKey);
                else
                    EditorPrefs.SetString(ChatBinaryResolver.CodexPrefKey, e.newValue);
            });
            parent.Add(codexPathField);

            var modelField = new TextField("Model") { value = config.Model };
            modelField.AddToClassList("chat-form-field");
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var permField = new DropdownField(
                "Permission Mode",
                new List<string> { "danger-full-access" },
                0);
            permField.AddToClassList("chat-form-field");
            permField.RegisterValueChangedCallback(e => { config.PermissionMode = e.newValue; onSave(); });
            parent.Add(permField);

            var timeoutField = new IntegerField("Startup Timeout (s)") { value = config.StartupTimeoutSec };
            timeoutField.AddToClassList("chat-form-field");
            timeoutField.RegisterValueChangedCallback(e =>
            {
                int value = Mathf.Clamp(e.newValue, 1, 120);
                timeoutField.SetValueWithoutNotify(value);
                config.StartupTimeoutSec = value;
                onSave();
            });
            parent.Add(timeoutField);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.AddToClassList("chat-form-field");
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }
    }
}
