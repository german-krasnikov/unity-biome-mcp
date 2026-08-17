// TDD: ConnectionPolicyUI builds config controls and wires pref persistence.
// UIElements value-change callbacks require a real panel — those tests use ShowUtility().
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConnectionPolicyUITests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempDir;

        [SetUp]
        public void SetupContext()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ConnPolicyUITests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            GlobalConfigSync.ConfigPathOverride = Path.Combine(_tempDir, "global-config.json");

            RegisterCleanup(() =>
            {
                GlobalConfigSync.ConfigPathOverride = null;
                ConnectionPolicyUI.SaveAction = GlobalConfigSync.SaveToDisk;
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            });
        }

        private EditorWindow CreateAttachedWindow()
        {
            var win = CreateOwnedEditorWindow<EditorWindow>();
            win.ShowUtility();
            return win;
        }

        [Test]
        public void Build_CreatesAllFiveControls()
        {
            var root = ConnectionPolicyUI.Build();

            int toggleCount = 0, fieldCount = 0;
            root.Query<Toggle>().ForEach(_ => toggleCount++);
            root.Query<IntegerField>().ForEach(_ => fieldCount++);

            Assert.AreEqual(3, toggleCount, "Expected 3 Toggles");
            Assert.AreEqual(2, fieldCount, "Expected 2 IntegerFields");
        }

        [Test]
        public void IdleTimeoutField_DisabledWhenAutoSuspendOff()
        {
            SetEditorPrefBool(PrefKeys.IdleAutoSuspend, false);

            var root = ConnectionPolicyUI.Build();

            IntegerField timeoutField = null;
            root.Query<IntegerField>().ForEach(f =>
            {
                if (f.label.Contains("timeout") || f.label.Contains("Idle"))
                    timeoutField = f;
            });

            Assert.IsNotNull(timeoutField, "Idle timeout field not found");
            Assert.IsFalse(timeoutField.enabledSelf, "Idle timeout must be disabled when auto-suspend is off");
        }

        [Test]
        public void OrphanGraceField_DisabledWhenTerminateOrphanOff()
        {
            SetEditorPrefBool(PrefKeys.TerminateOrphan, false);

            var root = ConnectionPolicyUI.Build();

            IntegerField graceField = null;
            root.Query<IntegerField>().ForEach(f =>
            {
                if (f.label.Contains("grace") || f.label.Contains("Orphan"))
                    graceField = f;
            });

            Assert.IsNotNull(graceField, "Orphan grace field not found");
            Assert.IsFalse(graceField.enabledSelf, "Grace field must be disabled when terminate orphan is off");
        }

        [Test, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
        public void ToggleChange_CallsSaveToDisk()
        {
            ProtectEditorPrefBool(PrefKeys.IdleAutoSuspend);

            var saveCount = 0;
            ConnectionPolicyUI.SaveAction = () => saveCount++;

            var win = CreateAttachedWindow();
            var root = ConnectionPolicyUI.Build();
            win.rootVisualElement.Add(root);  // attach to panel so callbacks fire

            Toggle suspendToggle = null;
            root.Query<Toggle>().ForEach(t =>
            {
                if (t.label.Contains("suspend") || t.label.Contains("idle") || t.label.Contains("Auto"))
                    suspendToggle = t;
            });

            Assert.IsNotNull(suspendToggle, "Auto-suspend toggle not found");
            suspendToggle.value = !suspendToggle.value;  // triggers RegisterValueChangedCallback

            Assert.GreaterOrEqual(saveCount, 1, "SaveToDisk must be called on toggle change");
        }

        [Test, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
        public void IntField_ClampsToRange()
        {
            SetEditorPrefBool(PrefKeys.IdleAutoSuspend, true);
            SetEditorPrefInt(PrefKeys.IdleTimeoutMin, 30);

            var win = CreateAttachedWindow();
            var root = ConnectionPolicyUI.Build();
            win.rootVisualElement.Add(root);  // attach to panel

            IntegerField timeoutField = null;
            root.Query<IntegerField>().ForEach(f =>
            {
                if (f.label.Contains("timeout") || f.label.Contains("Idle"))
                    timeoutField = f;
            });

            Assert.IsNotNull(timeoutField);

            timeoutField.value = 1;  // below min of 5 — callback clamps to 5

            var stored = EditorPrefs.GetInt(PrefKeys.IdleTimeoutMin, 30);
            Assert.GreaterOrEqual(stored, 5, "Idle timeout must be clamped to minimum 5");
        }
    }
}
