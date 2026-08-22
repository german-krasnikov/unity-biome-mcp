using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AutoRefreshGuardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        int _setAutoRefreshVal;
        int _getAutoRefreshVal;

        [SetUp]
        public void SetUp()
        {
            AutoRefreshGuard.ResetForTest();
            _getAutoRefreshVal = 1;  // Unity default: auto-refresh ON
            _setAutoRefreshVal = 1;
            AutoRefreshGuard._getAutoRefresh = () => _getAutoRefreshVal;
            AutoRefreshGuard._setAutoRefresh = v => _setAutoRefreshVal = v;
            ProtectEditorPrefInt("kAutoRefresh");
            ProtectEditorPrefInt("kAutoRefreshMode");
            // Ensure HR package check always returns false in unit tests
            HotReloadDetector._cachedPackageInstalled = false;
            RegisterCleanup(AutoRefreshGuard.ResetForTest);
            RegisterCleanup(() => HotReloadDetector._cachedPackageInstalled = null);
        }

        [Test]
        public void Apply_WhenNotApplied_SetsIsAppliedTrue()
        {
            AutoRefreshGuard.Apply();
            Assert.IsTrue(AutoRefreshGuard.IsApplied);
        }

        [Test]
        public void Apply_WhenNotApplied_WritesZeroToAutoRefresh()
        {
            _getAutoRefreshVal = 1;
            AutoRefreshGuard.Apply();
            Assert.That(_setAutoRefreshVal, Is.EqualTo(0));
        }

        [Test]
        public void Apply_WhenAlreadyApplied_IsNoop()
        {
            AutoRefreshGuard.Apply();
            int setCalls = 0;
            AutoRefreshGuard._setAutoRefresh = _ => setCalls++;
            AutoRefreshGuard.Apply();
            Assert.That(setCalls, Is.EqualTo(0), "Second Apply must be no-op");
        }

        [Test]
        public void Restore_WhenApplied_RestoresOriginalValue()
        {
            _getAutoRefreshVal = 1;
            AutoRefreshGuard.Apply();
            // Now simulate kAutoRefresh is 0 (as Apply set it)
            _setAutoRefreshVal = -999;  // sentinel: should be overwritten by Restore
            AutoRefreshGuard.Restore();
            Assert.That(_setAutoRefreshVal, Is.EqualTo(1), "Restore must write original value");
        }

        [Test]
        public void Restore_WhenApplied_SetsIsAppliedFalse()
        {
            AutoRefreshGuard.Apply();
            AutoRefreshGuard.Restore();
            Assert.IsFalse(AutoRefreshGuard.IsApplied);
        }

        [Test]
        public void Restore_WhenNotApplied_IsNoop()
        {
            int setCalls = 0;
            AutoRefreshGuard._setAutoRefresh = _ => setCalls++;
            AutoRefreshGuard.Restore();
            Assert.That(setCalls, Is.EqualTo(0), "Restore on idle must be no-op");
        }
    }
}
