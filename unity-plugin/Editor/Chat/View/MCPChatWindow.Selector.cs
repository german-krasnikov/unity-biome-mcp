// Agent / backend selector dropdown — partial of MCPChatWindow.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private string             _selectedAgent; // null = default Claude
        private List<BackendSpec>  _backends;
        private DropdownField      _agentDropdown;
        private const string       DropdownPrefKey = "MCPChat.SelectedBackend";

        // Model selector
        private string        _selectedModel = "";
        private DropdownField _modelDropdown;
        private TextField     _customModelField;
        private const string  CustomModelSentinel = ModelPresetDefaults.CustomSentinel;

        // Backward-compat property for existing tests — returns default presets (no config I/O)
        internal static Dictionary<BackendKind, (string label, string modelId)[]> ModelPresetsPerKind
            => ModelPresetDefaults.All;

        // Backward-compat alias for existing tests
        internal static (string label, string modelId)[] ModelPresets
            => ModelPresetDefaults.All[BackendKind.Claude];

        private static string ModelPrefKeyFor(BackendKind kind)
            => $"MCPChat.SelectedModel.{kind}";

        private static (string label, string modelId)[] PresetsForKind(BackendKind kind)
            => BackendConfigStore.Load().GetPresetsForKind(kind);

        private void RefreshBackends()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var home        = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            _backends       = BackendRegistry.Discover(AgentSearchPath.Resolve(projectRoot, home));
        }

        private VisualElement BuildAgentSelector()
        {
            if (_backends == null) RefreshBackends();

            var choices = new List<string>();
            foreach (var b in _backends)
                if (b.AgentName == null) choices.Add(b.DisplayName);

            // Issue 28: _selectedKind/_selectedAgent are already restored by
            // RestoreSelectedBackendFromPrefs() (called from OnEnable() before CreateBackend()) —
            // just reflect that state as the dropdown's initial value. No second mutation / backend
            // recreation happens here anymore (removes the old "P0-3" double-create).
            var current      = _backends.Find(b => b.Kind == _selectedKind && b.AgentName == _selectedAgent);
            var initialIndex = choices.IndexOf(current.DisplayName);
            if (initialIndex < 0)
            {
                // Saved backend is an agent (excluded from dropdown) or no longer exists.
                // Sync internal state to the first visible choice so UI and state agree.
                initialIndex = 0;
                var firstVisible = _backends.Find(b => b.AgentName == null && b.Enabled);
                if (firstVisible.DisplayName != null)
                {
                    _selectedKind  = firstVisible.Kind;
                    _selectedAgent = null;
                    EditorPrefs.SetString(DropdownPrefKey, StableIdFor(firstVisible));
                }
            }

            _agentDropdown = new DropdownField(choices, initialIndex)
            {
                tooltip = "Backend / Agent"
            };
            _agentDropdown.AddToClassList("agent-selector");

            _agentDropdown.RegisterValueChangedCallback(evt =>
            {
                var chosenName = evt.newValue;
                var spec       = _backends.Find(b => b.DisplayName == chosenName);

                if (!spec.Enabled)
                {
                    // Revert to previous selection — placeholder, do nothing
                    _agentDropdown.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                _selectedKind  = spec.Kind;
                _selectedAgent = spec.AgentName;
                _selectedModel = ""; // reset before CreateBackend so stale model doesn't leak to new backend
                EditorPrefs.SetString(DropdownPrefKey, StableIdFor(spec)); // Issue 28: persist stable id, not DisplayName
                _backend?.Stop();
                ResetTokenCounters();
                CreateBackend();
                RebuildModelDropdown();
            });

            return _agentDropdown;
        }

        // Issue 28: stable identifier for EditorPrefs persistence — survives DisplayName renames.
        // Built-in backends key by BackendKind (the enum name never changes); custom project-level
        // agents (Kind == Claude with a non-null AgentName) key by AgentName, which stays fixed even
        // if the rendered DisplayName changes.
        private static string StableIdFor(BackendSpec spec)
            => spec.AgentName == null ? spec.Kind.ToString() : spec.AgentName;

        // Issue 28: restore _selectedKind/_selectedAgent from EditorPrefs. Must run BEFORE
        // CreateBackend() (called from OnEnable()) so the very first backend spawned after a
        // domain reload / window reopen is already the correct CLI — not a default-Claude
        // backend that gets silently recreated later.
        internal void RestoreSelectedBackendFromPrefs()
        {
            if (_backends == null) RefreshBackends();

            var saved = EditorPrefs.GetString(DropdownPrefKey, "");
            if (saved == "Codex (Session)") saved = "Codex"; // legacy F28 rename shim, kept for old installs
            if (string.IsNullOrEmpty(saved)) return;

            var spec = FindBackendByStableId(saved);
            if (spec != null)
            {
                _selectedKind  = spec.Value.Kind;
                _selectedAgent = spec.Value.AgentName;
                return;
            }

            // Issue 28: previously a silent no-op fallback to whatever _selectedKind already was
            // (BackendKind.Claude by default) — now explicit, so a rename/removal is diagnosable.
            Debug.LogWarning($"{BiomeLabel.Tag} Saved backend '{saved}' not found — falling back to {_selectedKind}.");
        }

        private BackendSpec? FindBackendByStableId(string saved)
        {
            foreach (var b in _backends)
                // DisplayName equality is a backward-compat fallback for prefs written by older
                // plugin versions (pre-Issue-28), which stored the free-form DisplayName.
                if (b.Enabled && (StableIdFor(b) == saved || b.DisplayName == saved))
                    return b;
            return null;
        }

        private VisualElement BuildModelSelector()
        {
            var presets = PresetsForKind(_selectedKind);
            var labels  = new List<string>();
            foreach (var p in presets) labels.Add(p.label);

            _modelDropdown = new DropdownField(labels, 0) { tooltip = "Model" };
            _modelDropdown.AddToClassList("agent-selector");

            _customModelField = new TextField { tooltip = "Custom model ID", value = "" };
            _customModelField.AddToClassList("agent-selector");
            _customModelField.style.display  = DisplayStyle.None;
            _customModelField.style.minWidth = 120;

            // Restore saved selection
            var saved = EditorPrefs.GetString(ModelPrefKeyFor(_selectedKind), "");
            var idx   = System.Array.FindIndex(presets, p => p.label == saved);
            if (idx >= 0)
            {
                _modelDropdown.SetValueWithoutNotify(presets[idx].label);
                if (presets[idx].modelId == CustomModelSentinel)
                {
                    var customVal = EditorPrefs.GetString(ModelPrefKeyFor(_selectedKind) + ".custom", "");
                    _customModelField.value          = customVal;
                    _customModelField.style.display  = DisplayStyle.Flex;
                    _selectedModel                   = customVal;
                }
                else
                {
                    _selectedModel = presets[idx].modelId;
                }
            }

            _modelDropdown.RegisterValueChangedCallback(evt =>
            {
                var presetArr = PresetsForKind(_selectedKind);
                var p = System.Array.Find(presetArr, x => x.label == evt.newValue);
                EditorPrefs.SetString(ModelPrefKeyFor(_selectedKind), evt.newValue);

                if (p.modelId == CustomModelSentinel)
                {
                    _customModelField.style.display = DisplayStyle.Flex;
                    _selectedModel = _customModelField.value;
                    return; // don't restart backend until user types a value
                }

                _customModelField.style.display = DisplayStyle.None;
                _selectedModel = p.modelId;
                _backend?.Stop();
                ResetTokenCounters();
                CreateBackend();
            });

            _customModelField.RegisterCallback<FocusOutEvent>(_ => ApplyCustomModel());
            _customModelField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    ApplyCustomModel();
            });

            _modelDropdown.SetEnabled(presets.Length > 1);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.Add(_modelDropdown);
            container.Add(_customModelField);
            return container;
        }

        private void ApplyCustomModel()
        {
            var val = _customModelField?.value ?? "";
            EditorPrefs.SetString(ModelPrefKeyFor(_selectedKind) + ".custom", val);
            _selectedModel = val;
            _backend?.Stop();
            ResetTokenCounters();
            CreateBackend();
        }

        private void RebuildModelDropdown()
        {
            if (_modelDropdown == null) return;
            var presets = PresetsForKind(_selectedKind);
            var labels  = new List<string>();
            foreach (var p in presets) labels.Add(p.label);

            _modelDropdown.choices = labels;

            var saved = EditorPrefs.GetString(ModelPrefKeyFor(_selectedKind), "");
            var idx   = System.Array.FindIndex(presets, p => p.label == saved);
            if (idx < 0) idx = 0;

            _modelDropdown.SetValueWithoutNotify(presets[idx].label);
            _modelDropdown.SetEnabled(presets.Length > 1);

            if (presets[idx].modelId == CustomModelSentinel && _customModelField != null)
            {
                var customVal = EditorPrefs.GetString(ModelPrefKeyFor(_selectedKind) + ".custom", "");
                _customModelField.value         = customVal;
                _customModelField.style.display = DisplayStyle.Flex;
                _selectedModel                  = customVal;
            }
            else
            {
                _selectedModel = presets[idx].modelId;
                if (_customModelField != null) _customModelField.style.display = DisplayStyle.None;
            }
        }
    }
}
