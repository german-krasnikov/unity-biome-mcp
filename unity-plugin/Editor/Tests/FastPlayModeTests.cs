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
    }
}
