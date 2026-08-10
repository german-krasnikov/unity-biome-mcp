using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PluginUpdateMonitorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        const string LastVersionKey  = "UnityMCP.PluginUpdateMonitor.LastVersion";
        const string UpdatedFlagKey  = "UnityMCP.PluginUpdateMonitor.UpdatedThisSession";

        [SetUp]
        public void SetUpMonitorTest()
        {
            PluginUpdateMonitor._versionOverride = null;
            DeleteEditorPrefString(LastVersionKey);
            SessionState.EraseBool(UpdatedFlagKey);
        }

        [TearDown]
        public void TearDownMonitorTest()
        {
            PluginUpdateMonitor._versionOverride = null;
            SessionState.EraseBool(UpdatedFlagKey);
        }

        [Test]
        public void NoUpdateDetectedOnFirstRun()
        {
            PluginUpdateMonitor._versionOverride = "1.5.0";
            PluginUpdateMonitor.CheckVersionChange();
            Assert.IsFalse(SessionState.GetBool(UpdatedFlagKey, false));
        }

        [Test]
        public void UpdateDetectedWhenVersionChanges()
        {
            SetEditorPrefString(LastVersionKey, "1.4.0");
            PluginUpdateMonitor._versionOverride = "1.5.0";
            PluginUpdateMonitor.CheckVersionChange();
            Assert.IsTrue(SessionState.GetBool(UpdatedFlagKey, false));
        }

        [Test]
        public void NoUpdateDetectedWhenVersionSame()
        {
            SetEditorPrefString(LastVersionKey, "1.5.0");
            PluginUpdateMonitor._versionOverride = "1.5.0";
            PluginUpdateMonitor.CheckVersionChange();
            Assert.IsFalse(SessionState.GetBool(UpdatedFlagKey, false));
        }

        [Test]
        public void VersionStoredAfterCheck()
        {
            ProtectEditorPrefString(LastVersionKey);
            PluginUpdateMonitor._versionOverride = "1.6.0";
            PluginUpdateMonitor.CheckVersionChange();
            Assert.AreEqual("1.6.0", EditorPrefs.GetString(LastVersionKey, ""));
        }

        [Test]
        public void GetCurrentVersion_ReturnsNonEmpty()
        {
            // Just verify it doesn't throw and returns something parseable.
            var ver = PluginUpdateMonitor.GetCurrentVersion();
            Assert.IsNotNull(ver);
            Assert.IsNotEmpty(ver);
        }
    }
}
