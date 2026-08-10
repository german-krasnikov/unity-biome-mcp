using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatSettingsSection
    {
        /// <summary>Builds connection settings content — called by ChatConnectionSection via OnBuildConnection.</summary>
        internal static void BuildContent(VisualElement parent)
        {
            parent.AddToClassList("chat-settings");

            // F22: Auto-scroll toggle — at the top, outside any foldout.
            var autoScrollToggle = new Toggle("Auto-scroll") { value = EditorPrefs.GetBool(PrefKeys.ChatAutoScroll, true) };
            autoScrollToggle.AddToClassList("chat-form-field");
            autoScrollToggle.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefKeys.ChatAutoScroll, evt.newValue));
            parent.Add(autoScrollToggle);

            // General settings
            var store = BackendConfigStore.Load();
            var timeoutField = new IntegerField("Inactivity Timeout (s)") { value = store.InactivityTimeoutSec };
            timeoutField.AddToClassList("chat-form-field");
            timeoutField.tooltip = "Kill turn after this many seconds of silence (30–600)";
            timeoutField.RegisterValueChangedCallback(e =>
            {
                int value = Mathf.Clamp(e.newValue, 30, 600);
                timeoutField.SetValueWithoutNotify(value);
                store.InactivityTimeoutSec = value;
                store.Save();
            });
            parent.Add(timeoutField);

            // Per-backend settings — Claude foldout is expanded by default (contains primary connection info)
            var claudeFoldout = new Foldout { text = "Claude Settings", value = true };
            claudeFoldout.AddToClassList("chat-settings-foldout");

            // Binary path (auto + override) — inside Claude foldout
            var autoPath = ChatBinaryResolver.Resolve();
            var pathHint = new Label($"Auto: {autoPath ?? "not found"}");
            pathHint.AddToClassList("chat-hint");
            pathHint.AddToClassList(autoPath != null ? "chat-hint--success" : "chat-hint--error");
            claudeFoldout.Add(pathHint);

            var pathField = new TextField("Override Path")
                { value = EditorPrefs.GetString(ChatBinaryResolver.PrefKey, "") };
            pathField.AddToClassList("chat-form-field");
            pathField.RegisterValueChangedCallback(e =>
            {
                if (string.IsNullOrEmpty(e.newValue))
                    EditorPrefs.DeleteKey(ChatBinaryResolver.PrefKey);
                else
                    EditorPrefs.SetString(ChatBinaryResolver.PrefKey, e.newValue);
                ChatBinaryResolver.Resolve(forceRefresh: true);
            });
            claudeFoldout.Add(pathField);

            // Auth status probe
            var authLabel = new Label("Auth: checking...");
            authLabel.AddToClassList("chat-hint");
            authLabel.AddToClassList("chat-hint--warning");
            claudeFoldout.Add(authLabel);
            ProbeAuthAsync(authLabel);

            // ANTHROPIC_API_KEY warning
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
            {
                var warn = new Label("Warning: ANTHROPIC_API_KEY is set — it will be stripped from the chat process to use subscription auth.");
                warn.AddToClassList("chat-warning");
                claudeFoldout.Add(warn);
            }

            BackendSettingsForm.BuildClaudeForm(claudeFoldout, store.Claude, () => store.Save());
            parent.Add(claudeFoldout);

            var codexFoldout = new Foldout { text = "Codex Settings", value = false };
            codexFoldout.AddToClassList("chat-settings-foldout");
            BackendSettingsForm.BuildCodexForm(codexFoldout, store.Codex, () => store.Save());
            parent.Add(codexFoldout);

            var antigravityFoldout = new Foldout { text = "Antigravity Settings", value = false };
            antigravityFoldout.AddToClassList("chat-settings-foldout");
            BackendSettingsForm.BuildAntigravityForm(antigravityFoldout, store.Antigravity, () => store.Save());
            parent.Add(antigravityFoldout);

            var kimiFoldout = new Foldout { text = "Kimi Settings", value = false };
            kimiFoldout.AddToClassList("chat-settings-foldout");
            BackendSettingsForm.BuildKimiForm(kimiFoldout, store.Kimi, () => store.Save());
            parent.Add(kimiFoldout);

            var openCodeFoldout = new Foldout { text = "OpenCode Settings", value = false };
            openCodeFoldout.AddToClassList("chat-settings-foldout");
            BackendSettingsForm.BuildOpenCodeForm(openCodeFoldout, store.OpenCode, () => store.Save());
            parent.Add(openCodeFoldout);

            // Context Chips — per-kind depth + color overrides
            var chipFoldout = new Foldout { text = "Context Chips", value = false };
            chipFoldout.AddToClassList("chat-settings-foldout");
            BackendSettingsForm.BuildChipDisplayForm(chipFoldout, store.Chips, () =>
            {
                store.Save();
                foreach (var w in Resources.FindObjectsOfTypeAll<MCPChatWindow>())
                {
                    w.RefreshColorResolver();
                    w.RefreshChipDisplay();
                }
            });
            parent.Add(chipFoldout);

            var mentionFoldout = new Foldout { text = "@ Mention", value = false };
            mentionFoldout.AddToClassList("chat-settings-foldout");
            BuildMentionForm(mentionFoldout, store.Mention, () => store.Save());
            parent.Add(mentionFoldout);

            // Plugin settings — each provider gets its own foldout, collapsed by default.
            foreach (var p in SettingsProviderRegistry.All)
            {
                var foldout = new Foldout { text = p.DisplayName, value = false };
                foldout.AddToClassList("chat-settings-foldout");
                try { p.BuildUI(foldout); }
                catch (System.Exception e) { Debug.LogException(e); continue; }
                parent.Add(foldout);
            }
        }

        private static void BuildMentionForm(VisualElement parent, MentionConfig cfg, System.Action onSave)
        {
            var rowsField = new IntegerField("Max Results") { value = cfg.MaxPopupRows };
            rowsField.AddToClassList("chat-form-field");
            rowsField.tooltip = "Number of items shown in the @ dropdown (3–20)";
            rowsField.RegisterValueChangedCallback(e =>
            {
                cfg.MaxPopupRows = Mathf.Clamp(e.newValue, 3, 20);
                rowsField.SetValueWithoutNotify(cfg.MaxPopupRows);
                onSave();
            });
            parent.Add(rowsField);

            var sortNames = new System.Collections.Generic.List<string>
                { "By Relevance", "By Name", "By Type", "By Recency" };
            var sortField = new DropdownField("Sort Order", sortNames, (int)cfg.SortOrder);
            sortField.AddToClassList("chat-form-field");
            sortField.RegisterValueChangedCallback(e =>
            {
                cfg.SortOrder = (MentionSortOrder)sortField.index;
                onSave();
            });
            parent.Add(sortField);
        }

        private static void ProbeAuthAsync(Label label)
        {
            var binary = ChatBinaryResolver.Resolve();
            if (binary == null)
            {
                BiomeUI.SetExclusiveClass(
                    label,
                    "chat-hint--error",
                    "chat-hint--success",
                    "chat-hint--warning",
                    "chat-hint--error");
                label.text = "Auth: binary not found";
                return;
            }

            // Build PSI on main thread (Unity APIs like SystemInfo require it)
            ProcessStartInfo psi;
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                psi = new ProcessStartInfo(binary, "auth status")
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = new UTF8Encoding(false),  // cp1251 safety
                    StandardErrorEncoding  = new UTF8Encoding(false),
                };
            }
            else
            {
                psi = LoginShellCommand.Create("\"$1\" auth status", binary);
                psi.RedirectStandardError  = true;
                psi.StandardErrorEncoding  = new UTF8Encoding(false);  // cp1251 safety
            }

            Process activeProcess = null;
            int cancelled = 0;
            label.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                System.Threading.Interlocked.Exchange(ref cancelled, 1);
                try
                {
                    if (activeProcess != null && !activeProcess.HasExited)
                        activeProcess.Kill();
                }
                catch { }
            });

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = false;
                try
                {
                    using var p = Process.Start(psi);
                    activeProcess = p;
                    if (p != null)
                    {
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        int waited = 0;
                        while (!p.WaitForExit(100) && waited < 2000)
                        {
                            waited += 100;
                            if (System.Threading.Volatile.Read(ref cancelled) != 0)
                            {
                                try { p.Kill(); } catch { }
                                return;
                            }
                        }
                        if (!p.HasExited) { try { p.Kill(); } catch { } }
                    }
                    ok = p != null && p.HasExited && p.ExitCode == 0;
                }
                catch { }
                finally { activeProcess = null; }

                if (System.Threading.Volatile.Read(ref cancelled) != 0)
                    return;
                EditorApplication.delayCall += () =>
                {
                    if (label?.panel == null) return;
                    label.text = ok ? "Auth: logged in" : "Auth: not logged in";
                    BiomeUI.SetExclusiveClass(
                        label,
                        ok ? "chat-hint--success" : "chat-hint--error",
                        "chat-hint--success",
                        "chat-hint--warning",
                        "chat-hint--error");
                    EditorPrefs.SetString(PrefKeys.ChatAuthStatus, ok ? "ok" : "fail");
                };
            });
        }
    }
}
