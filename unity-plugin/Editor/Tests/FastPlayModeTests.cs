using System;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class FastPlayModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        bool _setEnabledVal;
        EnterPlayModeOptions _setOptionsVal;
        bool _getEnabledVal;
        EnterPlayModeOptions _getOptionsVal;

        [SetUp]
        public void SetUp()
        {
            FastPlayMode.ResetForTest();
            _setEnabledVal = false;
            _setOptionsVal = EnterPlayModeOptions.None;
            _getEnabledVal = false;
            _getOptionsVal = EnterPlayModeOptions.None;
            FastPlayMode._setEnabled = v => _setEnabledVal = v;
            FastPlayMode._setOptions = v => _setOptionsVal = v;
            FastPlayMode._getEnabled = () => _getEnabledVal;
            FastPlayMode._getOptions = () => _getOptionsVal;
            ProtectEditorPrefBool("UnityMCP_FastPlayMode");
            RegisterCleanup(FastPlayMode.ResetForTest);
        }

        [Test]
        public void Apply_WhenNotApplied_SetsIsAppliedTrue()
        {
            FastPlayMode.Apply();
            Assert.IsTrue(FastPlayMode.IsApplied);
        }

        [Test]
        public void Apply_WhenNotApplied_EnablesPlayModeOptions()
        {
            _getEnabledVal = false;
            _getOptionsVal = EnterPlayModeOptions.None;
            FastPlayMode.Apply();
            Assert.IsTrue(_setEnabledVal);
            Assert.IsTrue((_setOptionsVal & EnterPlayModeOptions.DisableDomainReload) != 0);
        }

        [Test]
        public void Apply_WhenAlreadyApplied_IsNoop()
        {
            FastPlayMode.Apply();
            int setCalls = 0;
            FastPlayMode._setEnabled = _ => setCalls++;
            FastPlayMode.Apply();
            Assert.That(setCalls, Is.EqualTo(0), "Second Apply must be no-op");
        }

        [Test]
        public void Restore_WhenApplied_SetsIsAppliedFalse()
        {
            FastPlayMode.Apply();
            FastPlayMode.Restore();
            Assert.IsFalse(FastPlayMode.IsApplied);
        }

        [Test]
        public void Restore_WhenNotApplied_IsNoop()
        {
            int setCalls = 0;
            FastPlayMode._setEnabled = _ => setCalls++;
            FastPlayMode.Restore();
            Assert.That(setCalls, Is.EqualTo(0), "Restore on idle must be no-op");
        }

        [Test]
        public void IsApplied_ReflectsSessionState()
        {
            Assert.IsFalse(FastPlayMode.IsApplied);
            FastPlayMode.Apply();
            Assert.IsTrue(FastPlayMode.IsApplied);
        }

        // ── Regression: WS-MCP-247 ───────────────────────────────────────────

        [Test]
        public void Apply_WhenEnablingOptionsInjectsUnityDefaults_DoesNotDisableSceneReload()
        {
            // Simulate Unity 6 side-effect: setEnabled(true) causes getOptions to return mask=3
            bool enabled = false;
            var options = EnterPlayModeOptions.None;
            FastPlayMode._getEnabled = () => enabled;
            FastPlayMode._getOptions = () => options;
            FastPlayMode._setEnabled = v =>
            {
                enabled = v;
                if (v) options = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            };
            FastPlayMode._setOptions = v => options = v;

            FastPlayMode.Apply();

            Assert.AreEqual(EnterPlayModeOptions.DisableDomainReload, options,
                "Only DisableDomainReload should be set — DisableSceneReload must NOT be injected");
        }

        [Test]
        public void Apply_WhenUserHadDisableSceneReloadEnabled_PreservesIt()
        {
            // User already had Play Mode Options ON with DisableSceneReload
            bool enabled = true;
            var options = EnterPlayModeOptions.DisableSceneReload;
            FastPlayMode._getEnabled = () => enabled;
            FastPlayMode._getOptions = () => options;
            FastPlayMode._setEnabled = v => enabled = v;
            FastPlayMode._setOptions = v => options = v;

            FastPlayMode.Apply();

            Assert.IsTrue(options.HasFlag(EnterPlayModeOptions.DisableDomainReload), "Must add DisableDomainReload");
            Assert.IsTrue(options.HasFlag(EnterPlayModeOptions.DisableSceneReload), "Must preserve user's DisableSceneReload");
        }

        [Test]
        public void Apply_WhenPlayModeOptionsDisabled_SavesNoneAsOriginal()
        {
            // Play Mode Options were OFF — original should be saved as None/false
            FastPlayMode._getEnabled = () => false;
            FastPlayMode._getOptions = () => EnterPlayModeOptions.None;
            FastPlayMode._setEnabled = _ => { };
            FastPlayMode._setOptions = _ => { };

            FastPlayMode.Apply();

            Assert.AreEqual(0, SessionState.GetInt("MCP_FPM_OrigOptions", -1),
                "Original options must be None (0) when Play Mode Options were disabled");
        }
    }
}
