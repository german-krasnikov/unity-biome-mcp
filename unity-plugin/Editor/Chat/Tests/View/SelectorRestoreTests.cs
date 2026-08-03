// TDD — Issue 28: backend selector must survive DisplayName renames/reload without a silent
// fallback to Claude. Tests RestoreSelectedBackendFromPrefs() directly (internal, called from
// OnEnable() before CreateBackend()).
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SelectorRestoreTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string PrefKey = "MCPChat.SelectedBackend";

        private static readonly FieldInfo s_kind     = typeof(MCPChatWindow).GetField("_selectedKind",  BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_agent    = typeof(MCPChatWindow).GetField("_selectedAgent", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_backends = typeof(MCPChatWindow).GetField("_backends",      BindingFlags.NonPublic | BindingFlags.Instance);

        [SetUp]
        public void SetUp() => DeleteEditorPrefString(PrefKey);

        [Test]
        public void Restore_KnownBackendByKind_SetsCorrectSelectedKind()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                SetEditorPrefString(PrefKey, BackendKind.Codex.ToString());

                w.RestoreSelectedBackendFromPrefs();

                Assert.AreEqual(BackendKind.Codex, (BackendKind)s_kind.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void Restore_UnknownSavedName_LogsWarning_FallsBackGracefully()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                SetEditorPrefString(PrefKey, "Ghost");
                LogAssert.Expect(LogType.Warning, new Regex("Ghost"));

                Assert.DoesNotThrow(() => w.RestoreSelectedBackendFromPrefs());

                // Unresolved save must not corrupt state — stays at the field's default.
                Assert.AreEqual(BackendKind.Claude, (BackendKind)s_kind.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void Restore_RenamedCustomAgent_StableIdSurvives()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Simulate a custom project agent whose DisplayName changed (e.g. frontmatter
                // `name:` edited) while its stable AgentName/Kind did not — the class of bug the
                // old "Codex (Session)" -> "Codex" one-off migration shim was papering over.
                var specs = new List<BackendSpec>
                {
                    new BackendSpec("Claude", null, true, BackendKind.Claude),
                    new BackendSpec("My Custom Agent (renamed)", "my-custom-agent", true, BackendKind.Claude),
                };
                s_backends.SetValue(w, specs);
                SetEditorPrefString(PrefKey, "my-custom-agent"); // stable id, not the renamed DisplayName

                w.RestoreSelectedBackendFromPrefs();

                Assert.AreEqual(BackendKind.Claude, (BackendKind)s_kind.GetValue(w));
                Assert.AreEqual("my-custom-agent", (string)s_agent.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void Restore_EmptySaved_NoOp_NoWarning()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                Assert.DoesNotThrow(() => w.RestoreSelectedBackendFromPrefs());
                Assert.AreEqual(BackendKind.Claude, (BackendKind)s_kind.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void Restore_LegacyDisplayNameFormat_StillResolves()
        {
            // Backward compat: prefs written by pre-Issue-28 plugin versions store DisplayName.
            // For built-ins that's identical to the stable id ("Codex" == BackendKind.Codex.ToString()),
            // exercised here via a synthetic custom spec whose old-format saved value only matches
            // DisplayName (not the new stable AgentName key).
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var specs = new List<BackendSpec>
                {
                    new BackendSpec("Claude", null, true, BackendKind.Claude),
                    new BackendSpec("LegacyAgent", "legacy-agent", true, BackendKind.Claude),
                };
                s_backends.SetValue(w, specs);
                SetEditorPrefString(PrefKey, "LegacyAgent"); // old format: DisplayName

                w.RestoreSelectedBackendFromPrefs();

                Assert.AreEqual("legacy-agent", (string)s_agent.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }
    }
}
