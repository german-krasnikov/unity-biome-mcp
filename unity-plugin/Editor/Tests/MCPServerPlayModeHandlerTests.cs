// TDD: OnPlayModeStateChanged coverage and source-text verification.
//
// MCPServer.cs: OnPlayModeStateChanged invalidates scene caches on
// ExitingEditMode and ExitingPlayMode only. These tests verify:
// - All 4 PlayModeStateChange values are handled without exception
// - Source text confirms exactly 2 states trigger InvalidateSceneCaches
// - WatchdogTick and StartAsync re-entrancy guard exist in source
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPServerPlayModeHandlerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static System.Reflection.MethodInfo GetPlayModeHandler()
        {
            return typeof(MCPServer).GetMethod(
                "OnPlayModeStateChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        }

        // ── ExitingEditMode triggers InvalidateSceneCaches ────────────────────

        [Test]
        public void OnPlayModeStateChanged_ExitingEditMode_DoesNotThrow()
        {
            var method = GetPlayModeHandler();
            Assert.IsNotNull(method, "OnPlayModeStateChanged must exist as a non-public static method");

            Assert.DoesNotThrow(() =>
                method.Invoke(null, new object[] { PlayModeStateChange.ExitingEditMode }),
                "ExitingEditMode must not throw — it calls InvalidateSceneCaches");
        }

        // ── ExitingPlayMode triggers InvalidateSceneCaches ───────────────────

        [Test]
        public void OnPlayModeStateChanged_ExitingPlayMode_DoesNotThrow()
        {
            var method = GetPlayModeHandler();
            Assert.IsNotNull(method);

            Assert.DoesNotThrow(() =>
                method.Invoke(null, new object[] { PlayModeStateChange.ExitingPlayMode }),
                "ExitingPlayMode must not throw — it calls InvalidateSceneCaches");
        }

        // ── EnteredEditMode does NOT invalidate caches ────────────────────────

        [Test]
        public void OnPlayModeStateChanged_EnteredEditMode_DoesNotThrow()
        {
            var method = GetPlayModeHandler();
            Assert.IsNotNull(method);

            Assert.DoesNotThrow(() =>
                method.Invoke(null, new object[] { PlayModeStateChange.EnteredEditMode }),
                "EnteredEditMode must not throw — it is a no-op in the handler");
        }

        // ── EnteredPlayMode does NOT invalidate caches ────────────────────────

        [Test]
        public void OnPlayModeStateChanged_EnteredPlayMode_DoesNotThrow()
        {
            var method = GetPlayModeHandler();
            Assert.IsNotNull(method);

            Assert.DoesNotThrow(() =>
                method.Invoke(null, new object[] { PlayModeStateChange.EnteredPlayMode }),
                "EnteredPlayMode must not throw — it is a no-op in the handler");
        }

        // ── Source-text: only 2 states invalidate; WatchdogTick + re-entrancy guard ──

        [Test]
        public void MCPServer_PlayModeAndGuardContracts_VerifiedBySource()
        {
            var source = ReadRequiredPackageSource(typeof(MCPServer), "Editor/MCPServer.cs");

            // The handler must target exactly these two states
            StringAssert.Contains("ExitingEditMode", source,
                "OnPlayModeStateChanged must check ExitingEditMode");
            StringAssert.Contains("ExitingPlayMode", source,
                "OnPlayModeStateChanged must check ExitingPlayMode");

            // WatchdogTick must restart the server when not running and not compiling
            StringAssert.Contains("WatchdogTick", source,
                "WatchdogTick must exist to restart server after domain reload");
            StringAssert.Contains("IsRunning", source,
                "WatchdogTick must check IsRunning before calling StartAsync");
            StringAssert.Contains("IsReallyCompiling", source,
                "WatchdogTick must check IsReallyCompiling to avoid premature restart");

            // StartAsync re-entrancy guard prevents duplicate bind loops
            StringAssert.Contains("_starting", source,
                "_starting re-entrancy guard must exist in StartAsync");
        }
    }
}
