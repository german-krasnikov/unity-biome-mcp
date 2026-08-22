using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Real integration tests: no seam injection — proves Mutation Mode writes real
    /// EditorPrefs and EditorSettings, not just in-memory state.
    /// Cleanup via ProtectEditorPref* ensures originals are restored after each test.
    /// </summary>
    [TestFixture]
    public class MutationModeRealTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            ProtectEditorPrefInt("kAutoRefresh");
            ProtectEditorPrefInt("kAutoRefreshMode");
            ProtectEditorPrefBool("UnityMCP_HotReloadMode");
            ProtectEditorPrefBool("UnityMCP_FastPlayMode");
            // Prevent stale package-installed cache from skipping AutoRefreshGuard.Apply()
            HotReloadDetector._cachedPackageInstalled = false;
            // Restore seams to real implementations — prior mock tests may have left no-ops in place.
            AutoRefreshGuard.RestoreDefaultSeams();
            FastPlayMode.RestoreDefaultSeams();
            // Clean slate before each test
            AutoRefreshGuard.ResetForTest();
            FastPlayMode.ResetForTest();
            MCPSettings.SetMutationMode(false);
            // Register get_status command for status-assertion tests
            CommandRegistry.Clear();
            CommandRouter.RegisterMetaCommands();
            RegisterCleanup(() =>
            {
                MCPSettings.SetMutationMode(false);
                // Use ResetForTest (clears SessionState only) — ProtectEditorPrefInt handles EditorPrefs restore
                AutoRefreshGuard.ResetForTest();
                FastPlayMode.ResetForTest();
                HotReloadDetector._cachedPackageInstalled = null;
                CommandRegistry.Clear();
                CommandRegistry.InitDefaults();
            });
        }

        [Test]
        public void Enable_ReallyDisablesAutoRefresh()
        {
            Assert.AreEqual(1, EditorPrefs.GetInt("kAutoRefresh", 1), "Pre-condition: kAutoRefresh should be 1");

            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");

            Assert.AreEqual(0, EditorPrefs.GetInt("kAutoRefresh", 1), "kAutoRefresh must be 0 after Enable");
        }

        [Test]
        public void Enable_ReallyEnablesFastPlayMode()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");

            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled, "enterPlayModeOptionsEnabled must be true");
            Assert.IsTrue(
                EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload),
                "DisableDomainReload must be set");
        }

        [Test]
        public void Disable_RestoresAutoRefresh()
        {
            var before = EditorPrefs.GetInt("kAutoRefresh", 1);

            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");

            Assert.AreEqual(before, EditorPrefs.GetInt("kAutoRefresh", 1), "kAutoRefresh must be restored");
        }

        [Test]
        public void Disable_RestoresFastPlayMode()
        {
            var wasFPM = EditorSettings.enterPlayModeOptionsEnabled;

            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");

            Assert.AreEqual(wasFPM, EditorSettings.enterPlayModeOptionsEnabled, "enterPlayModeOptions must be restored");
        }

        [Test]
        public void ToggleThreeTimes_NoStateCorruption()
        {
            var origKAR = EditorPrefs.GetInt("kAutoRefresh", 1);
            var origFPM = EditorSettings.enterPlayModeOptionsEnabled;

            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            Assert.AreEqual(0, EditorPrefs.GetInt("kAutoRefresh", 1));
            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled);

            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");
            Assert.AreEqual(origKAR, EditorPrefs.GetInt("kAutoRefresh", 1));
            Assert.AreEqual(origFPM, EditorSettings.enterPlayModeOptionsEnabled);

            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            Assert.AreEqual(0, EditorPrefs.GetInt("kAutoRefresh", 1));

            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");
            Assert.AreEqual(origKAR, EditorPrefs.GetInt("kAutoRefresh", 1));
        }

        [Test]
        public void GetStatus_ReportsRealAutoRefreshState()
        {
            var statusOff = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("auto_refresh=true", statusOff);
            StringAssert.Contains("mutation_mode=false", statusOff);

            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");

            var statusOn = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("auto_refresh=false", statusOn);
            StringAssert.Contains("mutation_mode=true", statusOn);
            StringAssert.Contains("fast_play_mode=true", statusOn);
        }

        [Test]
        public void GetStatus_DomainReloadCount_IsPositive()
        {
            var status = CommandRegistry.Execute("get_status", "{}");
            var match = System.Text.RegularExpressions.Regex.Match(status, @"domain_reload_count=(\d+)");
            Assert.IsTrue(match.Success, "get_status must contain domain_reload_count=<n>");
            int count = int.Parse(match.Groups[1].Value);
            Assert.Greater(count, 0, "domain_reload_count must be positive (at least 1 reload happened to run this test)");
        }
    }

    [TestFixture]
    public class MutationModeCrashRecoveryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            ProtectEditorPrefBool("UnityMCP_HotReloadMode"); // actual key (backward-compat string in MCPSettings)
            ProtectEditorPrefBool("UnityMCP_FastPlayMode");
            ProtectEditorPrefInt("kAutoRefresh");
            ProtectEditorPrefInt("kAutoRefreshMode");
            AutoRefreshGuard.ResetForTest();
            FastPlayMode.ResetForTest();
            RegisterCleanup(AutoRefreshGuard.ResetForTest);
            RegisterCleanup(FastPlayMode.ResetForTest);
            RegisterCleanup(() => MCPSettings.SetMutationMode(false));
        }

        [Test]
        public void RecoverIfNeeded_WhenMMOnButSessionStateLost_RestoresEverything()
        {
            // Simulate crash state: EditorPref says MM=ON but SessionState cleared (guards not applied)
            MCPSettings.SetMutationMode(true);
            SetEditorPrefInt("kAutoRefresh", 0);
            SetEditorPrefInt("kAutoRefreshMode", 2);
            // SessionState is clean (AutoRefreshGuard.IsApplied=false) — simulates crash

            bool recovered = MutationModeCrashRecovery.RecoverIfNeeded();

            Assert.IsTrue(recovered, "Should detect crash state and recover");
            Assert.IsFalse(MCPSettings.GetMutationMode(), "MM must be OFF after recovery");
            Assert.AreEqual(1, EditorPrefs.GetInt("kAutoRefresh", -1), "kAutoRefresh must be restored to 1");
            Assert.AreEqual(0, EditorPrefs.GetInt("kAutoRefreshMode", -1), "kAutoRefreshMode must be 0 (Enabled)");
        }

        [Test]
        public void RecoverIfNeeded_WhenMMOff_DoesNothing()
        {
            MCPSettings.SetMutationMode(false);
            bool recovered = MutationModeCrashRecovery.RecoverIfNeeded();
            Assert.IsFalse(recovered);
        }

        [Test]
        public void RecoverIfNeeded_WhenMMOnAndGuardApplied_DoesNothing()
        {
            // Normal state: MM=ON and guard properly applied (not a crash)
            MCPSettings.SetMutationMode(true);
            AutoRefreshGuard._getAutoRefresh = () => 1;
            AutoRefreshGuard._setAutoRefresh = _ => { };
            AutoRefreshGuard.Apply();

            bool recovered = MutationModeCrashRecovery.RecoverIfNeeded();
            Assert.IsFalse(recovered, "Should not recover when guards are properly applied");
            Assert.IsTrue(MCPSettings.GetMutationMode(), "MM should stay ON");
        }
    }
}
