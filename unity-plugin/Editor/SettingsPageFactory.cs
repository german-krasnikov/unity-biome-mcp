using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class SettingsPageFactory
    {
        internal static VisualElement BuildToolsPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.AddToClassList("biome-page");
            page.Add(BackHeader("Tools", onBack));
            page.Add(ToolsHeaderAnim.Build(page));
            var scroll = new ScrollView();
            scroll.AddToClassList("tool-scroll");
            MCPSettingsUI.BuildToolsSection(scroll);
            page.Add(scroll);
            return page;
        }

        internal static VisualElement BuildPermissionsPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.AddToClassList("biome-page");
            page.Add(BackHeader("Permissions", onBack));
            page.Add(PermissionsHeaderAnim.Build(page));
            var info = new Label($"Controls which {BiomeLabel.DisplayName} tools the in-Unity Chat agent may call. " +
                                 "Tool toggles are in the Settings hub.");
            info.AddToClassList("info-label");
            page.Add(info);
            page.Add(MCPSettingsPermUI.BuildContent(new PermissionConfig()));
            return page;
        }

        internal static VisualElement BuildChatPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.AddToClassList("biome-page");
            page.Add(BackHeader("Chat Settings", onBack));
            page.Add(ChatHeaderAnim.Build(page));
            var scroll = new ScrollView();
            scroll.AddToClassList("biome-page-scroll");
            ChatSettingsHook.InvokeConnection(scroll);
            if (scroll.childCount == 0)
                ChatSettingsHook.InvokeConnectionViaReflection(scroll);
            page.Add(scroll);
            return page;
        }

        internal static VisualElement BuildPluginsPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.AddToClassList("biome-page");
            page.Add(BackHeader("Plugins", onBack));
            page.Add(EcosystemHeaderAnim.BuildPlugins());

            var plugins = PluginRegistry.All;
            if (plugins.Count == 0)
            {
                page.Add(BiomeUI.StatusLabel("No plugins registered."));
                return page;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("biome-page-scroll");
            page.Add(scroll);

            bool anyWithUI = false;
            foreach (var plugin in plugins)
            {
                if (!plugin.HasSettingsUI) continue;
                anyWithUI = true;
                var p = plugin;
                var detail = new VisualElement();
                detail.AddToClassList("plugin-settings-card");
                detail.style.display = DisplayStyle.None;
                bool built = false;
                bool expanded = false;

                var card = HubCardButton.Build("🧩", p.Name, p.Description,
                    () =>
                    {
                        if (!built)
                        {
                            BuildPluginSettings(p, detail);
                            built = true;
                        }
                        expanded = !expanded;
                        detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                    });
                scroll.Add(card);
                scroll.Add(detail);
            }

            if (!anyWithUI)
            {
                scroll.Add(BiomeUI.StatusLabel("No plugins have settings UI."));
                return page;
            }

            return page;
        }

        private static void BuildPluginSettings(IMCPPlugin plugin, VisualElement detail)
        {
            if (!string.IsNullOrEmpty(plugin.Description))
            {
                var description = new Label(plugin.Description);
                description.AddToClassList("plugin-settings-description");
                detail.Add(description);
            }

            VisualElement ui = null;
            try { ui = plugin.BuildSettingsUI(); }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{BiomeLabel.Tag} Plugin '{plugin.Name}' BuildSettingsUI failed: {e.Message}");
                var status = BiomeUI.StatusLabel();
                BiomeUI.SetStatus(status, $"Could not load {plugin.Name} settings.", "error");
                detail.Add(status);
            }
            if (ui != null)
                detail.Add(ui);
        }

        internal static VisualElement BuildUpdatesPage(Action onBack) =>
            UpdatesPage.Build(onBack);

        internal static VisualElement BuildVersionPickerPage(Action onBack) =>
            VersionPickerPage.Build(onBack);

        internal static VisualElement BuildSamplingPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.AddToClassList("biome-page");
            page.Add(BackHeader("LLM Sampling", onBack));
            page.Add(SamplingHeaderAnim.Build(page));
            var store = LlmConfigStore.Load();
            page.Add(BuildSamplingForm(store));
            return page;
        }

        internal static VisualElement BackHeader(string title, Action onBack)
        {
            var header = new VisualElement();
            header.AddToClassList("nav-back-header");
            var btn = BiomeUI.QuietButton("← Back", onBack, "Return to the previous settings page");
            btn.AddToClassList("nav-back-btn");
            var lbl = new Label(title);
            lbl.AddToClassList("nav-back-title");
            header.Add(btn);
            header.Add(lbl);
            return header;
        }

        // --- Apply-all preset definitions ---
        private static readonly (string Label, string Backend, string Model)[] _applyAllPresets =
        {
            ("Claude Fast",   "claude", "haiku"),
            ("Gemini Flash",  "gemini", "gemini-2.5-flash"),
            ("Codex",         "codex",  "codex-mini-latest"),
        };

        private static VisualElement BuildSamplingForm(LlmConfigStore store)
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("biome-page-scroll");
            var cfg = store.Config;

            // --- Apply-All preset buttons (replaces old Claude/Codex tabs) ---
            var presetRow = new VisualElement();
            presetRow.AddToClassList("preset-row");

            // Collect all per-tool controls for Apply-All to reference
            var toolControls = new Dictionary<string, (DropdownField backend, DropdownField model)>();

            var rows = new (string label, string key, SamplingConfig config, string toolDep)[]
            {
                ("Visual Verify",       "visual_verify",       cfg.VisualVerify,       "screenshot"),
                ("Screenshot Describe", "screenshot_describe", cfg.ScreenshotDescribe, "screenshot"),
                ("Visual Diff",         "visual_diff",         cfg.VisualDiff,         "screenshot"),
                ("Summarize",           "summarize",           cfg.Summarize,          null),
                ("Do Intent",           "do_intent",           cfg.DoIntent,           null),
                ("Distiller",           "distiller",           cfg.Distiller,          null),
                ("UI Intent",           "ui_intent",           cfg.UiIntent,           null),
                ("VFX Intent",          "vfx_intent",          cfg.VfxIntent,          null),
                ("Animator Intent",     "animator_intent",     cfg.AnimatorIntent,     null),
                ("NL Composer",         "nl_composer",         cfg.NlComposer,         null),
            };

            foreach (var (label, key, config, toolDep) in rows)
            {
                var (backendDd, modelDd) = AddSamplingCard(scroll, label, config, store, toolDep);
                toolControls[key] = (backendDd, modelDd);
            }

            foreach (var (label, backend, model) in _applyAllPresets)
            {
                var b = backend; var m = model; // capture
                var btn = BiomeUI.SecondaryButton(label, () =>
                {
                    foreach (var kv in toolControls)
                    {
                        kv.Value.backend.value = b;
                        kv.Value.model.value   = m;
                    }
                });
                btn.AddToClassList("preset-btn");
                presetRow.Add(btn);
            }

            scroll.Insert(0, presetRow);
            return scroll;
        }

        private static (DropdownField backendDd, DropdownField modelDd) AddSamplingCard(
            VisualElement parent, string label, SamplingConfig cfg, LlmConfigStore store,
            string toolDep = null)
        {
            string HeaderText() =>
                $"{label} · {(string.IsNullOrEmpty(cfg.Backend) ? "claude" : cfg.Backend)} / " +
                $"{(string.IsNullOrEmpty(cfg.Model) ? "default" : cfg.Model)}";

            var foldout = new Foldout { text = HeaderText(), value = false };
            foldout.AddToClassList("sampling-card");

            if (toolDep != null && !MCPSettings.IsToolEnabled(toolDep))
            {
                foldout.SetEnabled(false);
                foldout.tooltip = $"Enable the '{toolDep}' tool to configure this sampler.";
            }

            var body = new VisualElement();
            body.AddToClassList("sampling-card-body");

            // Backend dropdown
            var currentBackend = string.IsNullOrEmpty(cfg.Backend) ? "claude" : cfg.Backend;
            var backendDd = new DropdownField("Backend",
                new List<string>(SamplingPresets.KnownBackends), 0);
            backendDd.value = currentBackend;
            backendDd.AddToClassList("sampling-backend-dd");

            // Model dropdown — seeded from current backend
            var currentModels = GetModels(currentBackend);
            var currentModel  = ResolveModel(cfg.Model, currentModels);
            var modelDd = new DropdownField("Model", new List<string>(currentModels), 0);
            modelDd.value = currentModel;
            modelDd.AddToClassList("sampling-model-dd");

            // Backend change → rebuild model choices, flash model dd
            backendDd.RegisterValueChangedCallback(e =>
            {
                cfg.Backend = e.newValue;
                var models = GetModels(e.newValue);
                modelDd.choices = new List<string>(models);
                if (System.Array.IndexOf(models, modelDd.value) < 0)
                    modelDd.value = models[0];
                FlashElement(modelDd, "model-changed-flash");
                foldout.text = HeaderText();
                store.Save();
            });

            modelDd.RegisterValueChangedCallback(e =>
            {
                cfg.Model = e.newValue;
                foldout.text = HeaderText();
                store.Save();
            });

            // Row 1: Backend + Model
            var row1 = new VisualElement();
            row1.AddToClassList("sampling-inline-row");
            row1.Add(backendDd);
            row1.Add(modelDd);
            body.Add(row1);

            // Row 2: Max Turns + Timeout
            var row2 = new VisualElement();
            row2.AddToClassList("sampling-inline-row");

            var maxTurns = new IntegerField("Max Turns") { value = cfg.MaxTurns };
            maxTurns.AddToClassList("sampling-int-field");
            maxTurns.RegisterValueChangedCallback(e =>
            {
                int value = UnityEngine.Mathf.Clamp(e.newValue, 1, 100);
                maxTurns.SetValueWithoutNotify(value);
                cfg.MaxTurns = value;
                store.Save();
            });
            row2.Add(maxTurns);

            var timeout = new FloatField("Timeout") { value = cfg.Timeout };
            timeout.AddToClassList("sampling-float-field");
            timeout.RegisterValueChangedCallback(e =>
            {
                float value = UnityEngine.Mathf.Clamp(e.newValue, 1f, 600f);
                timeout.SetValueWithoutNotify(value);
                cfg.Timeout = value;
                store.Save();
            });
            row2.Add(timeout);

            body.Add(row2);
            foldout.Add(body);
            parent.Add(foldout);

            return (backendDd, modelDd);
        }

        private static string[] GetModels(string backend)
        {
            if (SamplingPresets.ModelsByBackend.TryGetValue(backend, out var list)) return list;
            return SamplingPresets.ModelsByBackend["claude"];
        }

        private static string ResolveModel(string current, string[] models)
        {
            if (!string.IsNullOrEmpty(current) && System.Array.IndexOf(models, current) >= 0)
                return current;
            return models[0];
        }

        private static void FlashElement(VisualElement el, string cls)
        {
            el.AddToClassList(cls);
            el.schedule.Execute(() => el.RemoveFromClassList(cls)).StartingIn(200);
        }
    }
}
