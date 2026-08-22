using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Verifies ReloadGuard.EnsureScriptCompilationDuringPlay sets
    /// ScriptCompilationDuringPlay=1 when HR is active.
    /// The base class (UnityMcpTestBase) already provides IsolatedReloadGuardOps —
    /// no additional BeginTestIsolation nesting required.
    /// </summary>
    [TestFixture]
    public class ReloadGuardHotReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string ScriptCompKey = "ScriptCompilationDuringPlay";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => HotReloadDetector._overrideForTest = null);
            ProtectEditorPrefInt(ScriptCompKey);
            // Always clean up any lock the test may acquire via OnTurnStarted.
            RegisterCleanup(() => ReloadGuard.ResetForTest());
        }

        [Test]
        public void EnsureScriptCompilationDuringPlay_WhenHrActive_AndValueIs0_SetsTo1()
        {
            HotReloadDetector._overrideForTest = () => true;
            SetEditorPrefInt(ScriptCompKey, 0);

            ReloadGuard.OnTurnStarted();

            Assert.AreEqual(1, EditorPrefs.GetInt(ScriptCompKey, 0));
        }

        [Test]
        public void EnsureScriptCompilationDuringPlay_WhenHrActive_AndValueAlready1_DoesNotChange()
        {
            HotReloadDetector._overrideForTest = () => true;
            SetEditorPrefInt(ScriptCompKey, 1);

            ReloadGuard.OnTurnStarted();

            Assert.AreEqual(1, EditorPrefs.GetInt(ScriptCompKey, 0));
        }

        [Test]
        public void EnsureScriptCompilationDuringPlay_WhenHrInactive_DoesNothing()
        {
            HotReloadDetector._overrideForTest = () => false;
            SetEditorPrefInt(ScriptCompKey, 0);

            ReloadGuard.OnTurnStarted();

            Assert.AreEqual(0, EditorPrefs.GetInt(ScriptCompKey, 99));
        }

        [Test]
        public void ForceUnlock_WhenHrChangedPref_RestoresOriginalValue()
        {
            HotReloadDetector._overrideForTest = () => true;
            SetEditorPrefInt(ScriptCompKey, 0);

            ReloadGuard.OnTurnStarted();
            Assert.AreEqual(1, EditorPrefs.GetInt(ScriptCompKey, 99), "pref must be 1 after OnTurnStarted with HR active");

            ReloadGuard.ForceUnlock();
            Assert.AreEqual(0, EditorPrefs.GetInt(ScriptCompKey, 99), "pref must be restored to 0 after ForceUnlock");
        }
    }
}
