using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class HotReloadDetectorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => HotReloadDetector._overrideForTest = null);
        }

        [Test]
        public void IsActive_WhenOverrideReturnsTrue_ReturnsTrue()
        {
            HotReloadDetector._overrideForTest = () => true;
            Assert.IsTrue(HotReloadDetector.IsActive());
        }

        [Test]
        public void IsActive_WhenOverrideReturnsFalse_ReturnsFalse()
        {
            HotReloadDetector._overrideForTest = () => false;
            Assert.IsFalse(HotReloadDetector.IsActive());
        }

        [Test]
        public void IsAutoRefreshDisabled_WhenKAutoRefreshIs0_ReturnsTrue()
        {
            SetEditorPrefInt("kAutoRefresh", 0);
            Assert.IsTrue(HotReloadDetector.IsAutoRefreshDisabled());
        }

        [Test]
        public void IsAutoRefreshDisabled_WhenKAutoRefreshIs1_ReturnsFalse()
        {
            SetEditorPrefInt("kAutoRefresh", 1);
            Assert.IsFalse(HotReloadDetector.IsAutoRefreshDisabled());
        }

        [Test]
        public void GetHotReloadMode_DefaultsToFalse()
        {
            DeleteEditorPrefBool("UnityMCP_HotReloadMode");
            Assert.IsFalse(MCPSettings.GetHotReloadMode());
        }

        [Test]
        public void SetHotReloadMode_Persists()
        {
            ProtectEditorPrefBool("UnityMCP_HotReloadMode");
            MCPSettings.SetHotReloadMode(true);
            Assert.IsTrue(MCPSettings.GetHotReloadMode());
        }

        [Test]
        public void IsPackageInstalled_NeverThrows()
        {
            // Reset cache so the assembly scan actually runs.
            HotReloadDetector._cachedPackageInstalled = null;
            RegisterCleanup(() => HotReloadDetector._cachedPackageInstalled = null);
            // Dynamic/emit assemblies can throw on GetName(); this must not propagate.
            Assert.DoesNotThrow(() => HotReloadDetector.IsPackageInstalled());
        }
    }
}
