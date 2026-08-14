// TDD: GlobalConfigSync reads/writes JSON config matching Python's GlobalConfig format.
// Tests use ConfigPathOverride seam to avoid writing to real ~/.unity-biome-mcp/.
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GlobalConfigSyncTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempDir;

        [SetUp]
        public void SetupTempDir()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GlobalConfigSyncTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            GlobalConfigSync.ConfigPathOverride = Path.Combine(_tempDir, "global-config.json");
            RegisterCleanup(() =>
            {
                GlobalConfigSync.ConfigPathOverride = null;
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            });
        }

        [Test]
        public void SaveToDisk_WritesValidJson()
        {
            SetEditorPrefBool(PrefKeys.IdleAutoSuspend, true);
            SetEditorPrefInt(PrefKeys.IdleTimeoutMin, 30);
            SetEditorPrefBool(PrefKeys.TerminateOrphan, true);
            SetEditorPrefInt(PrefKeys.OrphanGraceMin, 2);

            GlobalConfigSync.SaveToDisk();

            var path = GlobalConfigSync.ConfigPath;
            Assert.IsTrue(File.Exists(path), "Config file should exist after save");
            var json = File.ReadAllText(path);
            StringAssert.Contains("\"idle_timeout_min\": 30", json);
            StringAssert.Contains("\"idle_auto_suspend\": true", json);
            StringAssert.Contains("\"bridge_terminate_orphan\": true", json);
            StringAssert.Contains("\"bridge_orphan_grace_min\": 2", json);
        }

        [Test]
        public void LoadFromDisk_PopulatesEditorPrefs()
        {
            ProtectEditorPrefBool(PrefKeys.IdleAutoSuspend);
            ProtectEditorPrefInt(PrefKeys.IdleTimeoutMin);
            ProtectEditorPrefBool(PrefKeys.TerminateOrphan);
            ProtectEditorPrefInt(PrefKeys.OrphanGraceMin);

            var json = "{\"idle_timeout_min\":45,\"idle_auto_suspend\":false," +
                       "\"bridge_terminate_orphan\":false,\"bridge_orphan_grace_min\":5}";
            File.WriteAllText(GlobalConfigSync.ConfigPath, json);

            GlobalConfigSync.LoadFromDisk();

            Assert.AreEqual(45, EditorPrefs.GetInt(PrefKeys.IdleTimeoutMin, 30));
            Assert.IsFalse(EditorPrefs.GetBool(PrefKeys.IdleAutoSuspend, true));
            Assert.IsFalse(EditorPrefs.GetBool(PrefKeys.TerminateOrphan, true));
            Assert.AreEqual(5, EditorPrefs.GetInt(PrefKeys.OrphanGraceMin, 2));
        }

        [Test]
        public void SaveToDisk_IsAtomic_NoTmpLeft()
        {
            SetEditorPrefBool(PrefKeys.IdleAutoSuspend, true);
            SetEditorPrefInt(PrefKeys.IdleTimeoutMin, 30);

            GlobalConfigSync.SaveToDisk();

            var tmpPath = GlobalConfigSync.ConfigPath + ".tmp";
            Assert.IsFalse(File.Exists(tmpPath), ".tmp file must not remain after atomic save");
            Assert.IsTrue(File.Exists(GlobalConfigSync.ConfigPath), "Config file must exist after atomic save");
        }

        [Test]
        public void EffectiveBool_FileValueFalse_PrefSetToFalse()
        {
            ProtectEditorPrefBool(PrefKeys.IdleAutoSuspend);

            var json = "{\"idle_auto_suspend\":false,\"idle_timeout_min\":30," +
                       "\"bridge_terminate_orphan\":true,\"bridge_orphan_grace_min\":2}";
            File.WriteAllText(GlobalConfigSync.ConfigPath, json);

            GlobalConfigSync.LoadFromDisk();

            Assert.IsFalse(EditorPrefs.GetBool(PrefKeys.IdleAutoSuspend, true),
                "File value false must override hardcoded default true");
        }

        [Test]
        public void EffectiveBool_ConfigWins_OverDefault()
        {
            SetEditorPrefBool(PrefKeys.TerminateOrphan, true);
            SetEditorPrefInt(PrefKeys.OrphanGraceMin, 2);
            GlobalConfigSync.SaveToDisk();

            // Change the pref in memory then reload — file value should win
            SetEditorPrefBool(PrefKeys.TerminateOrphan, false);
            GlobalConfigSync.LoadFromDisk();

            Assert.IsTrue(EditorPrefs.GetBool(PrefKeys.TerminateOrphan, false),
                "Config file value must override in-memory pref after LoadFromDisk");
        }

        [Test]
        public void EffectiveInt_Default_WhenNoFile()
        {
            ProtectEditorPrefInt(PrefKeys.IdleTimeoutMin);
            ProtectEditorPrefInt(PrefKeys.OrphanGraceMin);

            // Ensure no config file exists
            if (File.Exists(GlobalConfigSync.ConfigPath))
                File.Delete(GlobalConfigSync.ConfigPath);

            // Should not throw and should write defaults
            GlobalConfigSync.LoadFromDisk();

            Assert.AreEqual(30, EditorPrefs.GetInt(PrefKeys.IdleTimeoutMin, 0));
            Assert.AreEqual(2, EditorPrefs.GetInt(PrefKeys.OrphanGraceMin, 0));
        }
    }
}
